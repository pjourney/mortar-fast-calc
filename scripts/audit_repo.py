"""Offline, redacted checks for repository privacy and common credential patterns.

Uses the standard library. This is a scoped check, not a proof of security.
Personal comparison values come from the local environment and Git configuration;
they are never printed or written into reports.
"""
import argparse
import io
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, stderr=subprocess.DEVNULL)


def personal_values():
    values = []
    for key in ("user.name", "user.email"):
        try:
            values.append(git("config", "--global", "--get", key).decode().strip())
        except subprocess.CalledProcessError:
            pass
    values.extend(os.environ.get(key, "") for key in ("USERNAME", "COMPUTERNAME"))
    return [value.lower() for value in values if len(value) >= 5]


RULES = {
    "github_access_token": re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})\b"),
    "private_key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----"),
    "aws_access_key": re.compile(r"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b"),
    "authorization_header": re.compile(r"Authorization\s*:\s*(?:Basic|Bearer)\s+[A-Za-z0-9+/=_-]{24,}", re.I),
    "absolute_user_directory": re.compile(r"(?:[A-Za-z]:[\\/]+Users[\\/]+[^\\/\s<>]+|/(?:home|Users)/[A-Za-z0-9_.-]+)", re.I),
}
EMAIL = re.compile(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")
SAFE_EMAIL_DOMAINS = (".invalid", "@example.com", "@users.noreply.github.com")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--history", action="store_true")
    parser.add_argument("--staged", action="store_true")
    args = parser.parse_args()
    needles = personal_values()
    findings = []
    scanned = 0

    def flag(location, category):
        item = {"location": location, "category": category}
        if item not in findings:
            findings.append(item)

    def inspect(data, location, depth=0):
        nonlocal scanned
        scanned += 1
        # Compressed bytes can accidentally resemble addresses. Inspect ZIP names
        # and decompressed entries instead of treating compressed payloads as text.
        archive_payload = data.startswith(b"PK\x03\x04")
        variants = [] if archive_payload else [data.decode("utf-8", "ignore"), data.decode("utf-16-le", "ignore"), data[1:].decode("utf-16-le", "ignore")]
        for text in variants:
            lowered = text.lower()
            if any(value in lowered for value in needles):
                flag(location, "local_personal_identifier")
            for name, pattern in RULES.items():
                if pattern.search(text):
                    flag(location, name)
            for address in EMAIL.findall(text):
                if not address.lower().endswith(SAFE_EMAIL_DOMAINS):
                    flag(location, "email_requires_review")
        if archive_payload:
            if depth >= 3:
                flag(location, "archive_depth_limit")
                return
            with zipfile.ZipFile(io.BytesIO(data)) as archive:
                if sum(item.file_size for item in archive.infolist()) > 32 * 1024 * 1024:
                    flag(location, "archive_size_limit")
                    return
                for item in archive.infolist():
                    if item.is_dir():
                        continue
                    path = item.filename.replace("\\", "/")
                    if path.startswith("/") or ".." in path.split("/") or ":" in path:
                        flag(location, "unsafe_archive_path")
                    if any(part.lower() in (".git", ".env", "session.xml", "test-session.xml") for part in path.split("/")):
                        flag(location + "!" + path, "private_file_in_archive")
                    inspect(archive.read(item), location + "!" + path, depth + 1)
        if data.startswith(b"\x89PNG\r\n\x1a\n"):
            offset = 8
            while offset + 12 <= len(data):
                length = struct.unpack_from(">I", data, offset)[0]
                kind = data[offset+4:offset+8]
                if kind in (b"tEXt", b"zTXt", b"iTXt", b"eXIf"):
                    flag(location, "image_metadata_requires_review")
                offset += length + 12

    files = git("ls-files", "-z").decode().split("\0")
    for name in filter(None, files):
        data = git("show", ":" + name) if args.staged else (ROOT / name).read_bytes()
        inspect(data, name)
    if args.history:
        objects = git("rev-list", "--objects", "--all").decode().splitlines()
        for line in objects:
            oid, _, name = line.partition(" ")
            kind = git("cat-file", "-t", oid).strip()
            if kind in (b"commit", b"tag", b"blob"):
                inspect(git("cat-file", "-p", oid), "history/" + (name or kind.decode()))
    print(json.dumps({"objects_scanned": scanned, "findings": findings, "status": "clean" if not findings else "review_required"}, indent=2))
    return bool(findings)


if __name__ == "__main__":
    sys.exit(main())
