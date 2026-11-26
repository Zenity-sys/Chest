using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace Chest
{
    internal sealed class FaceLayout
    {
        public Rectangle Well;
        public Rectangle Session;
        public Rectangle HistoryButton;
        public Rectangle UndoButton;
        public Rectangle ResetButton;
        public Rectangle UnknownButton;
        public Rectangle DebugButton;
        public Rectangle GridToggle;
        public Rectangle ListToggle;
        public int CellW;
        public int CellH;
        public int Columns;
        public int ContentHeight;
    }

    internal sealed class SettingsLayout
    {
        public Rectangle Close;
        public Rectangle Refresh;
        public Rectangle Client;
        public Rectangle Grid;
        public Rectangle List;
        public Rectangle Open;
        public Rectangle Reset;
        public Rectangle Splash;
        public Rectangle Updates;
    }

    // Kavros Chest overlay. Icons sit on the dark panel. The scroll is vertical only.
    internal static class ChestFace
    {
        public const int IconBox = 32;
        public const int IconGap = 4;
        static readonly Color Well = Color.FromArgb(13, 23, 32);
        static readonly Color RowLine = Color.FromArgb(25, 40, 51);
        static readonly Color Mint = Color.FromArgb(135, 220, 181);
        static Bitmap crest;

        public static void LoadCrest(string path)
        {
            if (crest != null || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                using (MemoryStream stream = new MemoryStream(File.ReadAllBytes(path)))
                using (Bitmap raw = new Bitmap(stream))
                    crest = new Bitmap(raw);
            }
            catch (Exception) { }
        }

        public static int Columns(int gridWidth, int icon, int gap)
        {
            int cell = icon + gap;
            if (cell < 1 || gridWidth < cell) return 1;
            return gridWidth / cell;
        }

        public static string ValueText(long value)
        {
            return "CURRENT REWARD VALUE: " + Group(value);
        }

        public static string CluesText(int caskets)
        {
            return "OPENED CLUES: " + Group(caskets);
        }

        public static string Group(long value)
        {
            bool neg = value < 0;
            string raw = (neg ? -value : value).ToString(CultureInfo.InvariantCulture);
            StringBuilder text = new StringBuilder();
            if (neg) text.Append('-');
            int lead = raw.Length % 3;
            if (lead == 0) lead = 3;
            if (raw.Length <= 3) lead = raw.Length;
            text.Append(raw, 0, lead);
            for (int i = lead; i < raw.Length; i += 3)
            {
                text.Append(',');
                text.Append(raw, i, 3);
            }
            return text.ToString();
        }

        public static FaceLayout Measure(Rectangle bounds, string mode, int icon, int count, bool history, int historyCount)
        {
            if (icon > IconBox) icon = IconBox;
            if (icon < 16) icon = 16;
            FaceLayout face = new FaceLayout();
            int pad = 12;
            int top = bounds.Y + 8;
            face.GridToggle = new Rectangle(bounds.Right - pad - 112, top, 52, 20);
            face.ListToggle = new Rectangle(bounds.Right - pad - 56, top, 52, 20);
            int wellTop = bounds.Y + 82;
            int buttonH = 28;
            int buttonY = bounds.Bottom - 12 - buttonH;
            int sessionY = buttonY - 24;
            int wellW = Math.Max(40, bounds.Width - pad * 2);
            int wellH = Math.Max(36, sessionY - 6 - wellTop);
            face.Well = new Rectangle(bounds.X + pad, wellTop, wellW, wellH);
            face.Session = new Rectangle(face.Well.X, sessionY, wellW, 18);
            int gap = 4;
            int inner = Math.Max(40, wellW - 8);
            int bw = Math.Max(32, (inner - gap * 4) / 5);
            int x = face.Well.X;
            face.HistoryButton = new Rectangle(x, buttonY, bw, buttonH);
            face.UndoButton = new Rectangle(x + (bw + gap), buttonY, bw, buttonH);
            face.ResetButton = new Rectangle(x + (bw + gap) * 2, buttonY, bw, buttonH);
            face.UnknownButton = new Rectangle(x + (bw + gap) * 3, buttonY, bw, buttonH);
            face.DebugButton = new Rectangle(face.Well.Right - 8 - bw, buttonY, bw, buttonH);
            int usable = Math.Max(32, wellW - 8);
            if (history || mode == "list")
            {
                face.CellW = usable;
                face.CellH = 28;
                face.Columns = 1;
                int rows = history ? Math.Max(0, historyCount) : Math.Max(0, count);
                int header = !history && mode == "list" ? 18 : 0;
                face.ContentHeight = rows * face.CellH + header;
            }
            else
            {
                face.CellW = icon + 2;
                face.CellH = icon + 2;
                face.Columns = Math.Max(1, usable / face.CellW);
                int rows = count <= 0 ? 0 : (count + face.Columns - 1) / face.Columns;
                face.ContentHeight = rows * face.CellH;
            }
            return face;
        }

        public static int Hit(Rectangle bounds, Point point, string mode, int icon, int count, bool history, int historyCount)
        {
            FaceLayout face = Measure(bounds, mode, icon, count, history, historyCount);
            if (face.HistoryButton.Contains(point)) return 0;
            if (face.UndoButton.Contains(point)) return 1;
            if (face.ResetButton.Contains(point)) return 6;
            if (face.UnknownButton.Contains(point)) return 2;
            if (face.DebugButton.Contains(point)) return 3;
            if (face.GridToggle.Contains(point)) return 4;
            if (face.ListToggle.Contains(point)) return 5;
            return -1;
        }

        public static int HitSlot(Rectangle bounds, Point point, int scroll, string mode, int icon, int count, bool history, int historyCount)
        {
            if (history || count <= 0) return -1;
            FaceLayout face = Measure(bounds, mode, icon, count, false, 0);
            if (!face.Well.Contains(point)) return -1;
            int x = point.X - face.Well.X;
            int y = point.Y - face.Well.Y + scroll;
            if (x < 0 || y < 0 || x >= face.Columns * face.CellW) return -1;
            int col = x / face.CellW;
            int row = y / face.CellH;
            if (col < 0 || col >= face.Columns) return -1;
            if (mode != "list" && (x % face.CellW) >= icon) return -1;
            int index = row * face.Columns + col;
            if (index < 0 || index >= count) return -1;
            return index;
        }

        public static int MaxScroll(Rectangle bounds, string mode, int icon, int count, bool history, int historyCount)
        {
            FaceLayout face = Measure(bounds, mode, icon, count, history, historyCount);
            return Math.Max(0, face.ContentHeight - face.Well.Height);
        }

        public static void Draw(Graphics g, Rectangle bounds, IList<SlotView> slots, int scroll, long value, int caskets, int hot, int down, bool undoArmed, int icon, int gap, string tip, int tipX, int tipY, string mode = "grid", bool history = false, IList<long> opens = null, string detail = null, bool logged = false, bool resetArmed = false)
        {
            if (string.IsNullOrEmpty(mode)) mode = "grid";
            int count = slots == null ? 0 : slots.Count;
            int historyCount = opens == null ? 0 : opens.Count;
            if (scroll < 0) scroll = 0;
            int maxScroll = MaxScroll(bounds, mode, icon, count, history, historyCount);
            if (scroll > maxScroll) scroll = maxScroll;
            FaceLayout face = Measure(bounds, mode, icon, count, history, historyCount);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Brush bg = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(bg, bounds);

            string chip = logged ? "CASKET LOGGED" : "WATCHING";
            DrawChip(g, bounds.X + 12, bounds.Y + 8, chip, logged);
            string detailText = string.IsNullOrEmpty(detail) ? (caskets > 0 ? "Opening " + caskets : "Open a clue casket") : detail;
            using (Brush ink = new SolidBrush(KavrosTheme.Text))
                g.DrawString(detailText, KavrosTheme.Ui(9F, false), ink, bounds.X + 12, bounds.Y + 30);
            DrawFlat(g, face.GridToggle, "Grid", mode != "list", hot == 4, down == 4);
            DrawFlat(g, face.ListToggle, "List", mode == "list", hot == 5, down == 5);

            using (Brush gold = new SolidBrush(KavrosTheme.Gold))
            using (Brush soft = new SolidBrush(KavrosTheme.Text))
            {
                g.DrawString("CLUE REWARDS", KavrosTheme.Display(11F), gold, bounds.X + 12, bounds.Y + 52);
                string worth = Group(value) + " gp";
                SizeF size = g.MeasureString(worth, KavrosTheme.Ui(11F, true));
                g.DrawString(worth, KavrosTheme.Ui(11F, true), soft, face.Well.Right - size.Width, bounds.Y + 52);
            }

            using (Brush well = new SolidBrush(Well)) g.FillRectangle(well, face.Well);
            if (history) DrawHistory(g, face, opens, scroll);
            else if (count == 0)
            {
                using (Brush ink = new SolidBrush(KavrosTheme.TextSoft))
                    g.DrawString("Open a clue casket to see your rewards.", KavrosTheme.Ui(9F, false), ink, face.Well.X + 8, face.Well.Y + 12);
            }
            else if (mode == "list") DrawList(g, face, slots, scroll, icon);
            else DrawGrid(g, face, slots, scroll, icon);

            if (face.ContentHeight > face.Well.Height) DrawScroll(g, face, scroll);

            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            using (Brush ink = new SolidBrush(KavrosTheme.Text))
            {
                Font font = KavrosTheme.Ui(9F, false);
                g.DrawString("Opened clues  " + caskets.ToString(CultureInfo.InvariantCulture), font, ink, face.Session.X, face.Session.Y);
                string right = "Session value  " + Group(value) + " gp";
                SizeF rightSize = g.MeasureString(right, font);
                g.DrawString(right, font, soft, face.Session.Right - rightSize.Width, face.Session.Y);
            }

            DrawFlat(g, face.HistoryButton, "History", history, hot == 0, down == 0);
            DrawFlat(g, face.UndoButton, undoArmed ? "Undo?" : "Undo", false, hot == 1, down == 1);
            DrawFlat(g, face.ResetButton, resetArmed ? "Reset?" : "Reset", false, hot == 6, down == 6);
            DrawFlat(g, face.UnknownButton, "Unknown", false, hot == 2, down == 2);
            DrawFlat(g, face.DebugButton, "Debug", false, hot == 3, down == 3);
            DrawTip(g, bounds, tip, tipX, tipY);
        }

        public static void DrawOverlay(Graphics g, Rectangle bounds, string client, IList<SlotView> slots, int scroll, long value, int caskets, string tip, int tipX, int tipY, string mode, bool history, IList<long> opens, string detail, bool logged)
        {
            using (Brush bg = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(bg, bounds);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, bounds.X, bounds.Y, bounds.Width, 2);
            using (Brush head = new SolidBrush(Color.FromArgb(15, 26, 35))) g.FillRectangle(head, bounds.X, bounds.Y + 2, bounds.Width, 28);
            using (Pen edge = new Pen(Color.FromArgb(36, 56, 74))) g.DrawRectangle(edge, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Brush title = new SolidBrush(KavrosTheme.Text))
            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            {
                g.DrawString("CHEST", KavrosTheme.Display(11F), title, bounds.X + 10, bounds.Y + 6);
                string who = string.IsNullOrEmpty(client) ? "" : "|  " + client;
                g.DrawString(who, KavrosTheme.Ui(9F, false), soft, bounds.X + 78, bounds.Y + 8);
            }
            DrawFlat(g, new Rectangle(bounds.Right - 86, bounds.Y + 5, 52, 22), "Settings", false, false, false);
            DrawFlat(g, new Rectangle(bounds.Right - 30, bounds.Y + 5, 22, 22), "−", false, false, false);
            Rectangle body = new Rectangle(bounds.X, bounds.Y + 30, bounds.Width, Math.Max(80, bounds.Height - 30));
            int count = slots == null ? 0 : slots.Count;
            if (string.IsNullOrEmpty(tip) && mode != "list" && slots != null && count > 0 && !history)
            {
                FaceLayout face = Measure(body, mode, 32, count, false, 0);
                tip = slots[0].Name + " × " + slots[0].Quantity.ToString(CultureInfo.InvariantCulture);
                tipX = face.Well.X + (mode == "list" ? 24 : 2);
                tipY = face.Well.Y + (mode == "list" ? 6 : 20);
            }
            Draw(g, body, slots, scroll, value, caskets, -1, -1, false, 32, IconGap, tip, tipX, tipY, mode, history, opens, detail, logged);
        }

        public static SettingsLayout SettingsMap(Rectangle bounds)
        {
            SettingsLayout map = new SettingsLayout();
            int pad = 12;
            int inner = bounds.Width - pad * 2;
            map.Close = new Rectangle(bounds.Right - 32, 8, 26, 22);
            int y = 118;
            map.Client = new Rectangle(pad + 12, y + 32, Math.Max(80, inner - 36 - 96), 26);
            map.Refresh = new Rectangle(bounds.Right - pad - 12 - 84, y + 32, 84, 26);
            y = 228;
            map.Grid = new Rectangle(pad + 12, y + 32, 88, 26);
            map.List = new Rectangle(pad + 108, y + 32, 88, 26);
            y = 308;
            map.Open = new Rectangle(pad + 12, y + 156, 120, 28);
            map.Reset = new Rectangle(pad + 140, y + 156, 130, 28);
            y = 518;
            map.Splash = new Rectangle(pad + 12, y + 30, 200, 30);
            map.Updates = new Rectangle(pad + 220, y + 30, 200, 30);
            return map;
        }

        public static Rectangle ClientSlot(Rectangle bounds)
        {
            return SettingsMap(bounds).Client;
        }

        public static string HitSettings(Rectangle bounds, Point point)
        {
            SettingsLayout map = SettingsMap(bounds);
            if (map.Close.Contains(point)) return "close";
            if (map.Refresh.Contains(point)) return "refresh";
            if (map.Grid.Contains(point)) return "grid";
            if (map.List.Contains(point)) return "list";
            if (map.Open.Contains(point)) return "open";
            if (map.Reset.Contains(point)) return "reset";
            if (map.Splash.Contains(point)) return "splash";
            if (map.Updates.Contains(point)) return "updates";
            return null;
        }

        public static void DrawSettings(Graphics g, Rectangle bounds, string client, bool grid, int caskets, long value, IList<long> opens)
        {
            SettingsLayout map = SettingsMap(bounds);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Brush bg = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(bg, bounds);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, 0, 0, bounds.Width, 2);
            using (Brush bar = new SolidBrush(Color.FromArgb(13, 22, 32))) g.FillRectangle(bar, 0, 2, bounds.Width, 34);
            using (Pen edge = new Pen(Color.FromArgb(36, 56, 74))) g.DrawRectangle(edge, 0, 0, bounds.Width - 1, bounds.Height - 1);
            DrawCrest(g, new Rectangle(10, 10, 18, 18));
            DrawLeft(g, "Kavros Chest  ·  " + AppBranding.FullVersion, KavrosTheme.Ui(9F, false), KavrosTheme.Text, new Rectangle(32, 8, bounds.Width - 72, 22));
            DrawFlat(g, map.Close, "×", false, false, false);

            DrawCrest(g, new Rectangle(16, 40, 64, 68));
            using (Brush gold = new SolidBrush(KavrosTheme.Gold))
            using (Brush ink = new SolidBrush(KavrosTheme.Text))
            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            {
                g.DrawString("KAVROS", KavrosTheme.Display(9F), gold, 88, 46);
                g.DrawString("CHEST", KavrosTheme.Display(18F), ink, 88, 60);
                g.DrawString("Clue rewards", KavrosTheme.Ui(9F, false), soft, 90, 88);
            }

            DrawPanel(g, new Rectangle(12, 118, bounds.Width - 24, 100), "CLIENT");
            DrawFlat(g, map.Refresh, "Refresh", false, false, false);
            using (Brush input = new SolidBrush(KavrosTheme.Input)) g.FillRectangle(input, map.Client);
            using (Pen line = new Pen(KavrosTheme.ButtonLine)) g.DrawRectangle(line, map.Client.X, map.Client.Y, map.Client.Width - 1, map.Client.Height - 1);
            using (Brush ink = new SolidBrush(KavrosTheme.Text))
                g.DrawString(string.IsNullOrEmpty(client) ? "Looking for the game window" : client, KavrosTheme.Ui(9F, false), ink, map.Client.X + 8, map.Client.Y + 4);
            DrawChip(g, 24, 184, string.IsNullOrEmpty(client) ? "WAITING" : "CLIENT SELECTED", !string.IsNullOrEmpty(client));

            DrawPanel(g, new Rectangle(12, 228, bounds.Width - 24, 70), "REWARD OVERLAY");
            DrawFlat(g, map.Grid, "Grid", grid, false, false);
            DrawFlat(g, map.List, "List", !grid, false, false);

            DrawPanel(g, new Rectangle(12, 308, bounds.Width - 24, 200), "SESSION");
            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            using (Brush ink = new SolidBrush(KavrosTheme.Text))
            {
                Font font = KavrosTheme.Ui(9F, false);
                g.DrawString("Opened clues  " + caskets.ToString(CultureInfo.InvariantCulture), font, soft, 24, 338);
                string total = "Session value  " + Group(value) + " gp";
                SizeF size = g.MeasureString(total, font);
                g.DrawString(total, font, ink, bounds.Right - 24 - size.Width, 338);
            }
            int rowY = 364;
            if (opens != null)
            {
                int start = opens.Count - 1;
                int shown = 0;
                for (int i = start; i >= 0 && shown < 3; i--, shown++)
                {
                    using (Pen line = new Pen(RowLine)) g.DrawLine(line, 24, rowY + 24, bounds.Right - 24, rowY + 24);
                    using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
                    using (Brush ink = new SolidBrush(KavrosTheme.Text))
                    {
                        string index = "#" + (i + 1).ToString(CultureInfo.InvariantCulture);
                        g.DrawString(index, KavrosTheme.Ui(8F, false), soft, 24, rowY + 4);
                        g.DrawString("Casket", KavrosTheme.Ui(9F, false), ink, 58, rowY + 3);
                        string worth = Group(opens[i]) + " gp";
                        SizeF size = g.MeasureString(worth, KavrosTheme.Ui(9F, false));
                        g.DrawString(worth, KavrosTheme.Ui(9F, false), ink, bounds.Right - 28 - size.Width, rowY + 3);
                    }
                    rowY += 26;
                }
            }
            DrawFlat(g, map.Open, "Open panel", true, false, false);
            DrawFlat(g, map.Reset, "Reset session", false, false, false);

            DrawPanel(g, new Rectangle(12, 518, bounds.Width - 24, 72), "APP");
            DrawFlat(g, map.Splash, "Show splash screen", false, false, false);
            DrawFlat(g, map.Updates, "Check for updates", false, false, false);
            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            {
                string credit = "Made by Zenity  ·  " + AppBranding.FullVersion;
                SizeF size = g.MeasureString(credit, KavrosTheme.Ui(8F, false));
                g.DrawString(credit, KavrosTheme.Ui(8F, false), soft, (bounds.Width - size.Width) / 2F, bounds.Height - 28);
            }
        }

        public static Rectangle SplashButton(Rectangle bounds)
        {
            return new Rectangle(bounds.X + (bounds.Width - 140) / 2, bounds.Y + 292, 140, 32);
        }

        public static void DrawSplash(Graphics g, Rectangle bounds)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Brush bg = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(bg, bounds);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, bounds.X, bounds.Y, bounds.Width, 2);
            using (Pen edge = new Pen(Color.FromArgb(36, 56, 74))) g.DrawRectangle(edge, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            DrawCrest(g, new Rectangle(bounds.X, bounds.Y + 28, bounds.Width, 150));
            using (Brush gold = new SolidBrush(KavrosTheme.Gold))
            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            {
                string title = "KAVROS CHEST";
                Font display = KavrosTheme.Display(26F);
                SizeF size = g.MeasureString(title, display);
                g.DrawString(title, display, gold, bounds.X + (bounds.Width - size.Width) / 2F, bounds.Y + 190);
                string sub = "Clue rewards";
                SizeF subSize = g.MeasureString(sub, KavrosTheme.Ui(11F, false));
                g.DrawString(sub, KavrosTheme.Ui(11F, false), soft, bounds.X + (bounds.Width - subSize.Width) / 2F, bounds.Y + 236);
                string credit = "Made by Zenity  ·  " + AppBranding.FullVersion;
                SizeF creditSize = g.MeasureString(credit, KavrosTheme.Ui(9F, false));
                g.DrawString(credit, KavrosTheme.Ui(9F, false), soft, bounds.X + (bounds.Width - creditSize.Width) / 2F, bounds.Bottom - 36);
            }
            DrawFlat(g, SplashButton(bounds), "Open rewards", true, false, false);
        }

        static void DrawPanel(Graphics g, Rectangle rect, string title)
        {
            using (Brush fill = new SolidBrush(KavrosTheme.Panel)) g.FillRectangle(fill, rect);
            using (Pen edge = new Pen(Color.FromArgb(42, 59, 72))) g.DrawRectangle(edge, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold))
                g.DrawString(title, KavrosTheme.Display(9F), gold, rect.X + 12, rect.Y + 8);
        }

        static void DrawHistory(Graphics g, FaceLayout face, IList<long> opens, int scroll)
        {
            if (opens == null || opens.Count == 0)
            {
                using (Brush ink = new SolidBrush(KavrosTheme.TextSoft))
                    g.DrawString("No caskets yet.", KavrosTheme.Ui(9F, false), ink, face.Well.X + 8, face.Well.Y + 10);
                return;
            }
            Region old = g.Clip;
            g.SetClip(face.Well);
            for (int n = 0; n < opens.Count; n++)
            {
                int index = opens.Count - 1 - n;
                int y = face.Well.Y + n * face.CellH - scroll;
                if (y > face.Well.Bottom || y + face.CellH < face.Well.Top) continue;
                using (Pen line = new Pen(RowLine)) g.DrawLine(line, face.Well.X + 4, y + face.CellH - 1, face.Well.Right - 10, y + face.CellH - 1);
                using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
                using (Brush ink = new SolidBrush(KavrosTheme.Text))
                {
                    g.DrawString("#" + (index + 1).ToString(CultureInfo.InvariantCulture), KavrosTheme.Ui(8F, false), soft, face.Well.X + 6, y + 5);
                    g.DrawString("Casket", KavrosTheme.Ui(9F, false), ink, face.Well.X + 40, y + 4);
                    string worth = Group(opens[index]) + " gp";
                    SizeF size = g.MeasureString(worth, KavrosTheme.Ui(9F, false));
                    g.DrawString(worth, KavrosTheme.Ui(9F, false), ink, face.Well.Right - 12 - size.Width, y + 4);
                }
            }
            g.Clip = old;
        }

        static void DrawList(Graphics g, FaceLayout face, IList<SlotView> slots, int scroll, int icon)
        {
            Region old = g.Clip;
            g.SetClip(face.Well);
            using (Brush soft = new SolidBrush(KavrosTheme.TextSoft))
            {
                g.DrawString("Item", KavrosTheme.Ui(8F, false), soft, face.Well.X + 4, face.Well.Y + 2);
                g.DrawString("Qty", KavrosTheme.Ui(8F, false), soft, face.Well.Right - 48, face.Well.Y + 2);
            }
            int header = 18;
            g.SetClip(new Rectangle(face.Well.X, face.Well.Y + header, face.Well.Width, Math.Max(1, face.Well.Height - header)));
            for (int i = 0; i < slots.Count; i++)
            {
                SlotView slot = slots[i];
                int y = face.Well.Y + header + i * face.CellH - scroll;
                if (y > face.Well.Bottom || y + face.CellH < face.Well.Top) continue;
                using (Pen line = new Pen(RowLine)) g.DrawLine(line, face.Well.X + 2, y + face.CellH - 1, face.Well.Right - 10, y + face.CellH - 1);
                DrawSprite(g, slot.Image, new Rectangle(face.Well.X + 4, y + 3, 22, 22), icon);
                using (Brush ink = new SolidBrush(KavrosTheme.Text))
                {
                    string name = string.IsNullOrEmpty(slot.Name) ? "Unknown" : slot.Name;
                    Rectangle nameBox = new Rectangle(face.Well.X + 32, y + 4, Math.Max(20, face.Well.Width - 90), 20);
                    using (StringFormat format = new StringFormat())
                    {
                        format.Trimming = StringTrimming.EllipsisCharacter;
                        format.FormatFlags = StringFormatFlags.NoWrap;
                        g.DrawString(name, KavrosTheme.Ui(9F, false), ink, nameBox, format);
                    }
                    string qty = StackText.Format(slot.Quantity);
                    SizeF size = g.MeasureString(qty, KavrosTheme.Ui(9F, true));
                    g.DrawString(qty, KavrosTheme.Ui(9F, true), ink, face.Well.Right - 12 - size.Width, y + 4);
                }
            }
            g.Clip = old;
        }

        static void DrawGrid(Graphics g, FaceLayout face, IList<SlotView> slots, int scroll, int icon)
        {
            Region old = g.Clip;
            g.SetClip(face.Well);
            g.PixelOffsetMode = PixelOffsetMode.Half;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotView slot = slots[i];
                int col = i % face.Columns;
                int row = i / face.Columns;
                int x = face.Well.X + col * face.CellW;
                int y = face.Well.Y + row * face.CellH - scroll;
                if (y > face.Well.Bottom || y + face.CellH < face.Well.Top) continue;
                DrawSprite(g, slot.Image, new Rectangle(x, y, icon, icon), icon);
                DrawCount(g, StackText.Format(slot.Quantity), x + 1, y);
            }
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.Clip = old;
        }

        static void DrawSprite(Graphics g, Image image, Rectangle box, int cap)
        {
            if (image == null) return;
            int dw = image.Width;
            int dh = image.Height;
            int limit = Math.Min(cap, Math.Min(box.Width, box.Height));
            if (dw > limit || dh > limit)
            {
                float scale = Math.Min((float)limit / dw, (float)limit / dh);
                dw = Math.Max(1, (int)Math.Round(dw * scale));
                dh = Math.Max(1, (int)Math.Round(dh * scale));
            }
            int ix = box.X + Math.Max(0, (box.Width - dw) / 2);
            int iy = box.Y + Math.Max(0, (box.Height - dh) / 2);
            InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(image, new Rectangle(ix, iy, dw, dh));
            g.InterpolationMode = old;
        }

        static Font countFont;

        static Font CountFont()
        {
            if (countFont != null) return countFont;
            try { countFont = new Font("Tahoma", 7.5F, FontStyle.Bold, GraphicsUnit.Point); }
            catch (Exception) { countFont = null; }
            if (countFont == null)
            {
                try { countFont = new Font("Segoe UI", 7.5F, FontStyle.Bold, GraphicsUnit.Point); }
                catch (Exception) { countFont = new Font(FontFamily.GenericSansSerif, 7.5F, FontStyle.Bold, GraphicsUnit.Point); }
            }
            return countFont;
        }

        static void DrawCount(Graphics g, string text, float x, float y)
        {
            TextRenderingHint hint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            Font font = CountFont();
            using (Brush shadow = new SolidBrush(Color.Black))
            using (Brush yellow = new SolidBrush(Color.FromArgb(255, 255, 0)))
            {
                g.DrawString(text, font, shadow, x + 1, y + 1);
                g.DrawString(text, font, yellow, x, y);
            }
            g.TextRenderingHint = hint;
        }

        static void DrawScroll(Graphics g, FaceLayout face, int scroll)
        {
            int maxScroll = Math.Max(1, face.ContentHeight - face.Well.Height);
            int trackX = face.Well.Right - 6;
            using (Brush track = new SolidBrush(Color.FromArgb(24, 36, 48))) g.FillRectangle(track, trackX, face.Well.Y + 2, 4, face.Well.Height - 4);
            int thumb = Math.Max(18, face.Well.Height * face.Well.Height / face.ContentHeight);
            if (thumb > face.Well.Height - 4) thumb = face.Well.Height - 4;
            int travel = Math.Max(1, face.Well.Height - 4 - thumb);
            int thumbY = face.Well.Y + 2 + (int)(scroll * (long)travel / maxScroll);
            using (Brush thumbBrush = new SolidBrush(Color.FromArgb(120, 144, 162))) g.FillRectangle(thumbBrush, trackX, thumbY, 4, thumb);
        }

        static void DrawChip(Graphics g, int x, int y, string text, bool ok)
        {
            Font font = KavrosTheme.Ui(8F, true);
            SizeF size = g.MeasureString(text, font);
            Rectangle box = new Rectangle(x, y, (int)size.Width + 16, 18);
            Color fill = ok ? Color.FromArgb(23, 46, 40) : Color.FromArgb(40, 39, 30);
            Color edge = ok ? Color.FromArgb(59, 102, 87) : Color.FromArgb(99, 83, 50);
            Color ink = ok ? Mint : Color.FromArgb(236, 212, 154);
            KavrosTheme.FillRounded(g, box, 9, fill, edge);
            using (Brush dot = new SolidBrush(ink)) g.FillEllipse(dot, x + 6, y + 6, 5, 5);
            using (Brush brush = new SolidBrush(ink)) g.DrawString(text, font, brush, x + 14, y + 1);
        }

        static void DrawFlat(Graphics g, Rectangle rect, string text, bool primary, bool hot, bool down)
        {
            Color fill = primary ? KavrosTheme.Gold : KavrosTheme.Button;
            Color edge = primary ? KavrosTheme.GoldEdge : KavrosTheme.ButtonLine;
            Color ink = primary ? KavrosTheme.GoldInk : KavrosTheme.Text;
            if (hot && !primary) fill = Color.FromArgb(34, 54, 71);
            if (down) fill = primary ? Color.FromArgb(213, 178, 107) : Color.FromArgb(20, 34, 46);
            KavrosTheme.FillRounded(g, rect, 3, fill, edge);
            DrawCentered(g, text, KavrosTheme.Ui(8F, primary), ink, rect);
        }

        static void DrawCentered(Graphics g, string text, Font font, Color color, Rectangle rect)
        {
            Rectangle padded = new Rectangle(rect.X + 6, rect.Y + 3, Math.Max(1, rect.Width - 12), Math.Max(1, rect.Height - 6));
            DrawFitted(g, text, font, color, padded, true);
        }

        static void DrawLeft(Graphics g, string text, Font font, Color color, Rectangle rect)
        {
            DrawFitted(g, text, font, color, rect, false);
        }

        static void DrawFitted(Graphics g, string text, Font font, Color color, Rectangle rect, bool center)
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            flags |= center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
            try
            {
                TextRenderer.DrawText(g, text, font, rect, color, flags);
                return;
            }
            catch (Exception) { }
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat(StringFormat.GenericDefault))
            {
                format.Alignment = center ? StringAlignment.Center : StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                format.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, brush, rect, format);
            }
        }

        static void DrawTip(Graphics g, Rectangle bounds, string tip, int tipX, int tipY)
        {
            if (string.IsNullOrEmpty(tip)) return;
            Font font = KavrosTheme.Ui(8F, false);
            SizeF size = g.MeasureString(tip, font);
            int w = (int)size.Width + 14;
            int h = (int)size.Height + 8;
            int x = tipX + 14;
            int y = tipY + 16;
            if (x + w > bounds.Right - 6) x = tipX - w - 8;
            if (y + h > bounds.Bottom - 6) y = tipY - h - 8;
            if (x < bounds.X + 6) x = bounds.X + 6;
            if (y < bounds.Y + 6) y = bounds.Y + 6;
            Rectangle box = new Rectangle(x, y, w, h);
            using (Brush fill = new SolidBrush(Color.FromArgb(19, 33, 44))) g.FillRectangle(fill, box);
            using (Pen pen = new Pen(KavrosTheme.GoldEdge)) g.DrawRectangle(pen, box);
            using (Brush ink = new SolidBrush(KavrosTheme.Text)) g.DrawString(tip, font, ink, x + 7, y + 3);
        }

        static void DrawCrest(Graphics g, Rectangle box)
        {
            if (crest == null) return;
            InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            float scale = Math.Min((float)box.Width / crest.Width, (float)box.Height / crest.Height);
            int w = Math.Max(1, (int)(crest.Width * scale));
            int h = Math.Max(1, (int)(crest.Height * scale));
            g.DrawImage(crest, box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
            g.InterpolationMode = old;
        }
    }
}
