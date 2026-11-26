using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;

namespace Chest
{
    // The reward panel, drawn like the in-game chest: dark frame, gold title,
    // icons straight on the panel, the value line, then three buttons.
    internal static class ChestFace
    {
        public const int IconBox = 32;
        public const int IconGap = 12;

        static readonly Color Night = Color.FromArgb(6, 14, 22);
        static readonly Color Banner = Color.FromArgb(54, 16, 14);
        static readonly Color Gold = Color.FromArgb(255, 204, 48);
        static readonly Color GoldDim = Color.FromArgb(146, 104, 36);
        static readonly Color Ink = Color.FromArgb(236, 226, 196);
        static readonly Color ButtonTop = Color.FromArgb(255, 198, 62);
        static readonly Color ButtonBottom = Color.FromArgb(214, 128, 24);
        static readonly Color ButtonInk = Color.FromArgb(48, 26, 8);
        static readonly Color DangerTop = Color.FromArgb(196, 72, 28);
        static readonly Color DangerBottom = Color.FromArgb(122, 28, 14);
        static readonly Color DangerInk = Color.FromArgb(255, 236, 214);

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

        public static void Content(Rectangle bounds, out Rectangle grid, out Rectangle clues, out Rectangle undo, out Rectangle unknown)
        {
            int header = 36;
            int footer = 86;
            int side = 14;
            grid = new Rectangle(bounds.X + side, bounds.Y + header, Math.Max(40, bounds.Width - side * 2), Math.Max(40, bounds.Height - header - footer));
            int gap = 8;
            int by = bounds.Bottom - 14 - 34;
            int bw = Math.Max(40, (grid.Width - gap * 2) / 3);
            clues = new Rectangle(grid.X, by, bw, 34);
            undo = new Rectangle(grid.X + bw + gap, by, bw, 34);
            unknown = new Rectangle(undo.Right + gap, by, Math.Max(40, grid.Right - (undo.Right + gap)), 34);
        }

        public static void Draw(Graphics g, Rectangle bounds, IList<SlotView> slots, int scroll, long value, int caskets, int hot, int down, bool undoArmed, int icon, int gap, string tip, int tipX, int tipY)
        {
            if (icon > IconBox) icon = IconBox;
            if (icon < 16) icon = 16;
            if (gap < 4) gap = 4;
            g.Clear(Night);
            using (Pen edge = new Pen(GoldDim, 2)) g.DrawRectangle(edge, 3, 3, bounds.Width - 7, bounds.Height - 7);
            using (Pen inner = new Pen(Color.FromArgb(70, 48, 22))) g.DrawRectangle(inner, 7, 7, bounds.Width - 15, bounds.Height - 15);
            DrawCorners(g, bounds);
            DrawTitleTab(g);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            Rectangle grid, clues, undo, unknown;
            Content(bounds, out grid, out clues, out undo, out unknown);
            int cellW = icon + gap;
            int cellH = icon + 14;
            int columns = Columns(grid.Width, icon, gap);
            int rows = slots == null || slots.Count == 0 ? 0 : (slots.Count + columns - 1) / columns;
            int contentHeight = rows * cellH;
            int maxScroll = Math.Max(0, contentHeight - grid.Height);
            if (scroll < 0) scroll = 0;
            if (scroll > maxScroll) scroll = maxScroll;

            if (slots == null || slots.Count == 0)
            {
                using (Brush ink = new SolidBrush(Color.FromArgb(150, 170, 160)))
                    g.DrawString("Open a casket.", KavrosTheme.Ui(9F, false), ink, grid.X + 8, grid.Y + 12);
            }
            else
            {
                Region old = g.Clip;
                g.SetClip(grid);
                g.PixelOffsetMode = PixelOffsetMode.Half;
                using (Brush shadow = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
                using (Brush yellow = new SolidBrush(Color.FromArgb(255, 236, 40)))
                {
                    Font qtyFont = KavrosTheme.Ui(8F, true);
                    for (int i = 0; i < slots.Count; i++)
                    {
                        SlotView slot = slots[i];
                        int col = i % columns;
                        int row = i / columns;
                        int x = grid.X + col * cellW;
                        int y = grid.Y + row * cellH - scroll;
                        if (y > grid.Bottom || y + cellH < grid.Top) continue;
                        Rectangle cellRect = new Rectangle(x, y, cellW - 2, 12);
                        g.SetClip(Rectangle.Intersect(grid, cellRect));
                        if (slot.Image != null)
                        {
                            int dw = slot.Image.Width;
                            int dh = slot.Image.Height;
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            if (dw > icon || dh > icon)
                            {
                                float scale = Math.Min((float)icon / dw, (float)icon / dh);
                                dw = Math.Max(1, (int)Math.Round(dw * scale));
                                dh = Math.Max(1, (int)Math.Round(dh * scale));
                            }
                            int ix = x + Math.Max(0, (icon - dw) / 2);
                            int iy = y + 12;
                            g.SetClip(Rectangle.Intersect(grid, new Rectangle(x, iy, cellW - 2, icon)));
                            g.DrawImage(slot.Image, new Rectangle(ix, iy, dw, dh));
                            g.SetClip(Rectangle.Intersect(grid, cellRect));
                        }
                        string qty = StackText.Format(slot.Quantity);
                        g.DrawString(qty, qtyFont, shadow, x + 1, y + 1);
                        g.DrawString(qty, qtyFont, yellow, x, y);
                    }
                }
                g.Clip = old;
                if (maxScroll > 0)
                {
                    int trackX = grid.Right - 4;
                    using (Brush track = new SolidBrush(Color.FromArgb(24, 36, 48))) g.FillRectangle(track, trackX, grid.Y, 3, grid.Height);
                    int thumb = Math.Max(16, grid.Height * grid.Height / contentHeight);
                    int travel = Math.Max(1, grid.Height - thumb);
                    int thumbY = grid.Y + (int)(scroll * (long)travel / maxScroll);
                    using (Brush thumbBrush = new SolidBrush(GoldDim)) g.FillRectangle(thumbBrush, trackX, thumbY, 3, thumb);
                }
            }

            string valueLine = ValueText(value);
            Font valueFont = KavrosTheme.Display(9F);
            SizeF valueSize = g.MeasureString(valueLine, valueFont);
            float vx = grid.X + (grid.Width - valueSize.Width) / 2F;
            float vy = clues.Y - valueSize.Height - 6;
            using (Brush valueInk = new SolidBrush(Ink)) g.DrawString(valueLine, valueFont, valueInk, vx, vy);

            DrawButton(g, clues, CluesText(caskets), false, hot == 0, down == 0);
            DrawButton(g, undo, undoArmed ? "UNDO?" : "UNDO", true, hot == 1, down == 1);
            DrawButton(g, unknown, "UNKNOWN", false, hot == 2, down == 2);
            DrawTip(g, bounds, tip, tipX, tipY);
        }

        static void DrawTitleTab(Graphics g)
        {
            Point[] tab = new Point[] {
                new Point(16, 8),
                new Point(196, 8),
                new Point(214, 30),
                new Point(16, 30)
            };
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush banner = new SolidBrush(Banner)) g.FillPolygon(banner, tab);
            using (Pen gold = new Pen(GoldDim, 2)) g.DrawPolygon(gold, tab);
            g.SmoothingMode = SmoothingMode.None;
            using (Brush shadow = new SolidBrush(Color.FromArgb(180, 20, 8, 6)))
            using (Brush title = new SolidBrush(Gold))
            {
                Font face = KavrosTheme.Display(11F);
                g.DrawString("CLUE REWARDS", face, shadow, 25, 12);
                g.DrawString("CLUE REWARDS", face, title, 24, 11);
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
            if (x < 6) x = 6;
            if (y < 34) y = 34;
            Rectangle box = new Rectangle(x, y, w, h);
            using (Brush fill = new SolidBrush(Color.FromArgb(32, 24, 16))) g.FillRectangle(fill, box);
            using (Pen pen = new Pen(GoldDim)) g.DrawRectangle(pen, box);
            using (Brush ink = new SolidBrush(Ink)) g.DrawString(tip, font, ink, x + 7, y + 3);
        }

        public static int Hit(Rectangle bounds, Point point)
        {
            Rectangle grid, clues, undo, unknown;
            Content(bounds, out grid, out clues, out undo, out unknown);
            if (clues.Contains(point)) return 0;
            if (undo.Contains(point)) return 1;
            if (unknown.Contains(point)) return 2;
            return -1;
        }

        static void DrawButton(Graphics g, Rectangle rect, string text, bool danger, bool hot, bool down)
        {
            Color top = danger ? DangerTop : ButtonTop;
            Color bottom = danger ? DangerBottom : ButtonBottom;
            Color ink = danger ? DangerInk : ButtonInk;
            if (hot && !down)
            {
                top = Color.FromArgb(Math.Min(255, top.R + 16), Math.Min(255, top.G + 16), Math.Min(255, top.B + 8));
            }
            if (down)
            {
                Color swap = top;
                top = bottom;
                bottom = swap;
            }
            using (LinearGradientBrush fill = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
                g.FillRectangle(fill, rect);
            using (Pen light = new Pen(Color.FromArgb(180, 255, 230, 170))) g.DrawLine(light, rect.Left, rect.Top, rect.Right - 1, rect.Top);
            using (Pen dark = new Pen(Color.FromArgb(160, 40, 16, 4))) g.DrawRectangle(dark, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            Font font = KavrosTheme.Display(8.5F);
            SizeF size = g.MeasureString(text, font);
            if (size.Width > rect.Width - 8) font = KavrosTheme.Display(7.5F);
            using (Brush brush = new SolidBrush(ink))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, font, brush, rect, format);
            }
        }

        static void DrawCorners(Graphics g, Rectangle bounds)
        {
            using (Pen pen = new Pen(Gold, 2))
            {
                int m = 8, n = 18;
                g.DrawLines(pen, new Point[] { new Point(m, m + n), new Point(m, m), new Point(m + n, m) });
                g.DrawLines(pen, new Point[] { new Point(bounds.Right - m - n, m), new Point(bounds.Right - m, m), new Point(bounds.Right - m, m + n) });
                g.DrawLines(pen, new Point[] { new Point(m, bounds.Bottom - m - n), new Point(m, bounds.Bottom - m), new Point(m + n, bounds.Bottom - m) });
                g.DrawLines(pen, new Point[] { new Point(bounds.Right - m - n, bounds.Bottom - m), new Point(bounds.Right - m, bounds.Bottom - m), new Point(bounds.Right - m, bounds.Bottom - m - n) });
            }
        }
    }
}
