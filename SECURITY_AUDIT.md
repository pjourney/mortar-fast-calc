# Security and privacy audit

Reviewed September 20, 2026. Scope: all tracked files and reachable Git objects; author/committer metadata; the executable in both distribution locations; ZIP entries; PNG metadata and rendered screenshots; build and packaging scripts; local session parsing; and external execution/network surfaces in the application.

## Findings and remediation

| Priority | Finding | Action |
| --- | --- | --- |
| High privacy impact | The initial commit inherited a personal author/committer identity from global Git configuration. | Replace the single-commit history with a generic project identity and configure this checkout to use it for future commits. Values are deliberately omitted from this report. |
| Medium packaging risk | Recursive ZIP creation included a generated test-session XML file. Inspection confirmed synthetic test inputs, not player session data. | Use an explicit package file list. Test sessions now use a unique temporary directory and are removed after every test window has closed. |
| Low local hardening issue | Saved-session XML relied on default DTD handling and accepted unbounded per-field values from a locally modified file. | Explicitly prohibit DTDs, disable the resolver, bound document bytes/characters and fields, verify the document root, and omit invalid history entries. |
| Preventive privacy control | Ignore rules did not cover common credential-file names. | Ignore environment files, private keys, credential JSON, private audit material, and logs; add a redacted offline repository scanner. |

## Verification

- 202 compiled application assertions pass, including six security regressions for DTDs, external entities, excessive document/field sizes, unexpected document roots, and malformed history.
- Scan UTF-8 and UTF-16 content, Git metadata, decompressed archive entries, common credential patterns, machine/user identifiers available in the local environment, and PNG metadata chunks. The scanner reports categories and locations without printing the matched values.
- Rendered screenshots use the documented synthetic coordinate example and synthetic test setups. They do not capture the user's desktop, account, or game session.
- The application has no network client, credential storage, dynamic code compilation, process launching, or game-memory integration. Clipboard access is write-only and explicit. XAML is loaded only from embedded, build-time resources.
- The Windows manifest requests normal user privileges, without elevation or UIAccess. Dependencies are Windows/.NET Framework components; there are no downloaded application packages or update executables.
- The released ZIP uses a reviewed file list and excludes `.git`, session XML, credentials, and temporary files. Its executable is byte-for-byte identical to the tested distribution executable.

## Checks to repeat

```powershell
python scripts/audit_repo.py --history
.\build.ps1
$test = Start-Process .\dist\WardogsFastCalc.exe -ArgumentList '--test tests\test-results.txt' -PassThru -Wait
if ($test.ExitCode -ne 0) { throw 'Tests failed' }
.\package.ps1
python scripts/audit_repo.py
```

The scanner is a scoped heuristic, not a replacement for a secret-scanning service or a penetration test. Personal comparison values come from the environment and global Git identity on the machine where it runs. On another machine, it may not know the original publisher's identifiers. Review unexpected email findings rather than treating all third-party attribution as personal data to delete.

## Limits and remaining actions

Rewriting `main` removes the old identity from normal branch history and new clones. It cannot erase pre-existing clones, forks, or GitHub's retained objects/cached commit pages. [GitHub documents the remaining cleanup process](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository). A successful clean scan of reachable history is not a claim that host-retained objects have been purged.

The public repository account name and repository URL remain visible by design. This audit does not anonymize the owner's GitHub profile or third-party license attribution.

No access token was found in repository content. A credential supplied outside Git is outside this repository scan; rotate any credential disclosed in chat after maintenance is finished. The executable is unsigned, and operating-system/.NET servicing and hardware/driver vulnerabilities are outside this source audit.
