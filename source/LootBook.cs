using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Chest
{
    internal sealed class LootLine
    {
        public string Id = "";
        public string Name = "Unknown";
        public long Quantity;
        public bool Unknown;
    }

    internal sealed class LootCommit
    {
        public long Value;
        public readonly List<LootLine> Items = new List<LootLine>();
    }

    // One combined chest. A commit is the reward that was on screen when the
    // panel closed, after any rerolls.
    internal sealed class LootBook
    {
        public int Caskets;
        public long Value;
        public LootCommit Last;
        public readonly List<LootLine> Lines = new List<LootLine>();
        public readonly List<long> Opens = new List<long>();
        readonly string root;

        public LootBook(string root) { this.root = root; }

        public string TotalsPath { get { return Path.Combine(root, "totals.json"); } }
        public string UnknownDirectory { get { return Path.Combine(root, "unknown"); } }

        public void Load()
        {
            Lines.Clear();
            Caskets = 0;
            Value = 0;
            Opens.Clear();
            if (File.Exists(TotalsPath))
            {
                try { Parse(File.ReadAllText(TotalsPath, Encoding.UTF8)); }
                catch (Exception ex) { CrashLog.Write(ex); }
            }
            LoadOpens();
        }

        string HistoryPath { get { return Path.Combine(root, "history.txt"); } }

        void LoadOpens()
        {
            Opens.Clear();
            if (!File.Exists(HistoryPath)) return;
            try
            {
                foreach (string line in File.ReadAllLines(HistoryPath))
                {
                    long value;
                    if (long.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        Opens.Add(value);
                }
            }
            catch (Exception ex) { CrashLog.Write(ex); }
        }

        void SaveOpens()
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < Opens.Count; i++)
            {
                if (i > 0) text.Append("\r\n");
                text.Append(Opens[i].ToString(CultureInfo.InvariantCulture));
            }
            text.Append("\r\n");
            File.WriteAllText(HistoryPath, text.ToString(), Encoding.UTF8);
        }

        public void Commit(IList<LootLine> items, long value)
        {
            if (items == null || items.Count == 0) return;
            Add(items);
            Caskets++;
            Value += value;
            Last = Snapshot(items, value);
            Opens.Add(value);
            Save();
        }

        // Same open window, new reroll: swap the previous items for these.
        public void ReplaceLast(IList<LootLine> items, long value)
        {
            if (Last == null)
            {
                Commit(items, value);
                return;
            }
            Subtract(Last.Items);
            Value -= Last.Value;
            if (Value < 0) Value = 0;
            Add(items);
            Value += value;
            Last = Snapshot(items, value);
            if (Opens.Count > 0) Opens[Opens.Count - 1] = value;
            else Opens.Add(value);
            Save();
        }

        public bool UndoLast()
        {
            if (Last == null) return false;
            Subtract(Last.Items);
            if (Caskets > 0) Caskets--;
            Value -= Last.Value;
            if (Value < 0) Value = 0;
            Last = null;
            if (Opens.Count > 0) Opens.RemoveAt(Opens.Count - 1);
            Save();
            return true;
        }

        public void Reset()
        {
            Lines.Clear();
            Opens.Clear();
            Caskets = 0;
            Value = 0;
            Last = null;
            Save();
        }

        void Add(IList<LootLine> items)
        {
            foreach (LootLine item in items)
            {
                LootLine line = Find(item.Id);
                if (line == null)
                {
                    line = new LootLine { Id = item.Id, Name = item.Name, Unknown = item.Unknown };
                    Lines.Add(line);
                }
                line.Quantity += item.Quantity;
            }
        }

        void Subtract(IList<LootLine> items)
        {
            if (items == null) return;
            foreach (LootLine item in items)
            {
                LootLine line = Find(item.Id);
                if (line == null) continue;
                line.Quantity -= item.Quantity;
                if (line.Quantity <= 0) Lines.Remove(line);
            }
        }

        LootLine Find(string id)
        {
            foreach (LootLine existing in Lines)
                if (existing.Id == id) return existing;
            return null;
        }

        static LootCommit Snapshot(IList<LootLine> items, long value)
        {
            LootCommit commit = new LootCommit();
            commit.Value = value;
            foreach (LootLine item in items)
                commit.Items.Add(new LootLine { Id = item.Id, Name = item.Name, Quantity = item.Quantity, Unknown = item.Unknown });
            return commit;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(root);
                StringBuilder json = new StringBuilder();
                json.Append("{\r\n  \"caskets\": ").Append(Caskets.ToString(CultureInfo.InvariantCulture));
                json.Append(",\r\n  \"value\": ").Append(Value.ToString(CultureInfo.InvariantCulture));
                json.Append(",\r\n  \"items\": [");
                for (int i = 0; i < Lines.Count; i++)
                {
                    LootLine line = Lines[i];
                    if (i > 0) json.Append(',');
                    json.Append("\r\n    {\"id\": \"").Append(Escape(line.Id));
                    json.Append("\", \"name\": \"").Append(Escape(line.Name));
                    json.Append("\", \"qty\": ").Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
                    json.Append(", \"unknown\": ").Append(line.Unknown ? "true" : "false").Append('}');
                }
                if (Lines.Count > 0) json.Append("\r\n  ");
                json.Append("]\r\n}\r\n");
                File.WriteAllText(TotalsPath, json.ToString(), Encoding.UTF8);
                SaveOpens();
            }
            catch (Exception ex) { CrashLog.Write(ex); }
        }

        void Parse(string json)
        {
            Caskets = ReadInt(json, "caskets");
            Value = ReadLong(json, "value");
            int cursor = 0;
            while (true)
            {
                int idAt = json.IndexOf("\"id\"", cursor, StringComparison.Ordinal);
                if (idAt < 0) break;
                LootLine line = new LootLine();
                line.Id = ReadString(json, idAt);
                int nameAt = json.IndexOf("\"name\"", idAt, StringComparison.Ordinal);
                line.Name = nameAt < 0 ? line.Id : ReadString(json, nameAt);
                int qtyAt = json.IndexOf("\"qty\"", idAt, StringComparison.Ordinal);
                line.Quantity = qtyAt < 0 ? 0 : ReadLongAt(json, qtyAt);
                int unknownAt = json.IndexOf("\"unknown\"", idAt, StringComparison.Ordinal);
                line.Unknown = unknownAt >= 0 && json.IndexOf("true", unknownAt, StringComparison.Ordinal) >= 0 && json.IndexOf("true", unknownAt, StringComparison.Ordinal) < unknownAt + 20;
                Lines.Add(line);
                cursor = idAt + 4;
            }
        }

        static int ReadInt(string json, string key)
        {
            long value = ReadLong(json, key);
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
        static long ReadLong(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            return at < 0 ? 0 : ReadLongAt(json, at);
        }
        static long ReadLongAt(string json, int at)
        {
            int colon = json.IndexOf(':', at);
            if (colon < 0) return 0;
            int i = colon + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\r' || json[i] == '\n')) i++;
            int start = i;
            if (i < json.Length && json[i] == '-') i++;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9') i++;
            long value;
            return long.TryParse(json.Substring(start, i - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }
        static string ReadString(string json, int keyAt)
        {
            int colon = json.IndexOf(':', keyAt);
            int quote = json.IndexOf('"', colon + 1);
            if (quote < 0) return "";
            int end = json.IndexOf('"', quote + 1);
            if (end < 0) return "";
            return json.Substring(quote + 1, end - quote - 1).Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
        static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
