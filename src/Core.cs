using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace WardogsFastCalc {
    public struct Coordinate {
        public double X, Y;
        public Coordinate(double x, double y) { X = x; Y = y; }
    }
    public sealed class Solution {
        public double Distance, Range, Bearing;
        public double? Mil;
        public bool Overridden;
        public string Direction;
        public bool InRange { get { return Range >= 132 && Range <= 684; } }
        public string Callout { get { return String.Format(CultureInfo.InvariantCulture,
            "WARDOGS L81 | {0:000.0}° {1} | RNG {2:0} m | {3}", Math.Round(Bearing,1)%360, Direction, Range,
            InRange ? "~" + Mil.Value.ToString("0", CultureInfo.InvariantCulture) + " MIL (community estimate)" : "OUT OF RANGE"); } }
    }
    public static class Calculator {
        const string Number = @"[+-]?(?:\d+(?:\.\d+)?|\.\d+)";
        static readonly List<double[]> Table = LoadTable();
        static List<double[]> LoadTable() {
            var rows = new List<double[]>();
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("l81.csv"))) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    var v = line.Split(',');
                    rows.Add(new double[] { Double.Parse(v[0], CultureInfo.InvariantCulture), Double.Parse(v[1], CultureInfo.InvariantCulture) });
                }
            }
            return rows;
        }
        public static bool TryCoordinate(string text, out Coordinate value) {
            value = new Coordinate();
            if (String.IsNullOrWhiteSpace(text) || text.Length > 512) return false;
            text = text.Trim();
            // Explicit labels allow either order and decimal commas, without mistaking chat timestamps for coordinates.
            var x = Regex.Matches(text, @"(?i)(?<![a-z])x\s*[:=]?\s*([+-]?(?:\d+(?:[.,]\d+)?|[.,]\d+))");
            var y = Regex.Matches(text, @"(?i)(?<![a-z])y\s*[:=]?\s*([+-]?(?:\d+(?:[.,]\d+)?|[.,]\d+))");
            double a, b;
            if (x.Count == 1 && y.Count == 1) {
                if (!TryNumber(x[0].Groups[1].Value.Replace(',', '.'), out a) || !TryNumber(y[0].Groups[1].Value.Replace(',', '.'), out b)) return false;
                string remainder = text.Remove(Math.Max(x[0].Index, y[0].Index), x[0].Index > y[0].Index ? x[0].Length : y[0].Length);
                remainder = remainder.Remove(Math.Min(x[0].Index, y[0].Index), x[0].Index < y[0].Index ? x[0].Length : y[0].Length);
                if (!Regex.IsMatch(remainder, @"^[\s,;/()\[\]]*$")) return false;
            } else {
                var match = Regex.Match(text, @"^\s*[\[(]?\s*(" + Number + @")\s*[,;/\s]\s*(" + Number + @")\s*[\])]?\s*$");
                if (!match.Success || !TryNumber(match.Groups[1].Value, out a) || !TryNumber(match.Groups[2].Value, out b)) return false;
            }
            // All three currently calibrated maps lie within this envelope. These are game grid values, not GPS.
            if (a < 0 || a > 164 || b < 0 || b > 164) return false;
            value = new Coordinate(a, b); return true;
        }
        public static bool TryNumber(string text, out double value) {
            return Double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
                && !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
        public static double? EstimateMil(double range) {
            if (Double.IsNaN(range) || range < 132 || range > 684) return null;
            for (int i = 1; i < Table.Count; i++) if (range <= Table[i][0]) {
                var a = Table[i-1]; var b = Table[i];
                return a[1] + (range-a[0]) / (b[0]-a[0]) * (b[1]-a[1]);
            }
            return null;
        }
        public static Solution Solve(Coordinate origin, Coordinate target, double? distanceOverride) {
            double dx = target.X-origin.X, dy = target.Y-origin.Y;
            double distance = Math.Sqrt(dx*dx+dy*dy)*100;
            if (distance < 0.000001) throw new ArgumentException("Your position and target are the same. Enter a different target.");
            if (distanceOverride.HasValue && (Double.IsNaN(distanceOverride.Value) || Double.IsInfinity(distanceOverride.Value) || distanceOverride <= 0 || distanceOverride > 25000))
                throw new ArgumentException("Distance must be greater than 0 and at most 25000 meters.");
            double bearing = (Math.Atan2(dx, dy)*180/Math.PI+360)%360;
            string[] points = {"N","NNE","NE","ENE","E","ESE","SE","SSE","S","SSW","SW","WSW","W","WNW","NW","NNW"};
            double range = distanceOverride ?? distance;
            return new Solution { Distance=distance, Range=range, Bearing=bearing, Mil=EstimateMil(range), Overridden=distanceOverride.HasValue, Direction=points[(int)Math.Floor((bearing+11.25)/22.5)%16] };
        }
    }
}
