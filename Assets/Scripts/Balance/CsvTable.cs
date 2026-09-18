using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RogueShooter.Balance
{
    /// <summary>
    /// Minimal CSV reader for LOCK tables (quoted fields + [bracket] values).
    /// </summary>
    public sealed class CsvTable
    {
        public string[] Headers { get; private set; }
        public List<string[]> Rows { get; private set; }

        CsvTable(string[] headers, List<string[]> rows)
        {
            Headers = headers;
            Rows = rows;
        }

        public static CsvTable Parse(string text)
        {
            if (text != null && text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(text))
            {
                string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                for (int i = 0; i < raw.Length; i++)
                {
                    string line = raw[i].Trim();
                    if (line.Length > 0)
                        lines.Add(line);
                }
            }

            if (lines.Count == 0)
                return new CsvTable(Array.Empty<string>(), new List<string[]>());

            string[] headers = Split(lines[0]).ToArray();
            var rows = new List<string[]>();
            for (int i = 1; i < lines.Count; i++)
                rows.Add(Align(Split(lines[i]), headers.Length));
            return new CsvTable(headers, rows);
        }

        public int IndexOf(string header)
        {
            for (int i = 0; i < Headers.Length; i++)
            {
                if (string.Equals(Headers[i], header, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public string Get(string[] row, string header)
        {
            int i = IndexOf(header);
            if (i < 0 || row == null || i >= row.Length)
                return "";
            return row[i] ?? "";
        }

        public IEnumerable<string[]> DataRows()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                string[] row = Rows[i];
                if (row.Length == 0)
                    continue;
                string first = row[0] ?? "";
                if (first.Equals("META", StringComparison.OrdinalIgnoreCase))
                    continue;
                yield return row;
            }
        }

        public IEnumerable<string[]> MetaRows()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                string[] row = Rows[i];
                if (row.Length == 0)
                    continue;
                if ((row[0] ?? "").Equals("META", StringComparison.OrdinalIgnoreCase))
                    yield return row;
            }
        }

        public string MetaValue(string key)
        {
            foreach (string[] row in MetaRows())
            {
                if (row.Length > 1 && row[1] == key)
                    return row.Length > 2 ? row[2] : "";
            }

            return "";
        }

        public string Kv(string key)
        {
            int keyIndex = IndexOf("key");
            int metricIndex = IndexOf("metric");
            int valueIndex = IndexOf("value");
            if (valueIndex < 0)
                valueIndex = 1;
            for (int i = 0; i < Rows.Count; i++)
            {
                string[] row = Rows[i];
                if (row.Length == 0)
                    continue;
                string k = keyIndex >= 0 && keyIndex < row.Length ? row[keyIndex] : row[0];
                if (k == key)
                    return valueIndex < row.Length ? row[valueIndex] : "";
                if (metricIndex >= 0 && metricIndex < row.Length && row[metricIndex] == key)
                    return valueIndex < row.Length ? row[valueIndex] : "";
            }

            return "";
        }

        public static float ToFloat(string raw, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            string s = raw.Trim().TrimEnd('′', '\'', '’');
            float v;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        public static int ToInt(string raw, int fallback = 0)
        {
            return (int)ToFloat(raw, fallback);
        }

        public static bool TryClockRange(string raw, out float startMin, out float endMin)
        {
            startMin = 0f;
            endMin = 0f;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string s = raw.Trim().Replace("′", "").Replace("'", "").Replace("’", "");
            int dash = s.IndexOf('-');
            if (dash <= 0 || dash >= s.Length - 1)
                return false;
            return float.TryParse(s.Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out startMin)
                && float.TryParse(s.Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out endMin);
        }

        static List<string> Split(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            int brackets = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes)
                {
                    if (c == '[')
                        brackets++;
                    else if (c == ']' && brackets > 0)
                        brackets--;
                    else if (c == ',' && brackets == 0)
                    {
                        fields.Add(sb.ToString().Trim());
                        sb.Length = 0;
                        continue;
                    }
                }

                sb.Append(c);
            }

            fields.Add(sb.ToString().Trim());
            return fields;
        }

        static string[] Align(List<string> fields, int width)
        {
            var row = new string[Math.Max(width, fields.Count)];
            for (int i = 0; i < row.Length; i++)
                row[i] = i < fields.Count ? fields[i] : "";
            return row;
        }
    }
}
