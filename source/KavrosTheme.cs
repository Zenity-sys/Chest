using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Chest
{
    internal enum ButtonTone { Secondary, Primary, Ghost }

    // Navy and gold, shared with the clue helper. The chest panel itself is
    // painted separately so it can follow the in-game reward window.
    internal static class KavrosTheme
    {
        public static readonly Color Bg = Color.FromArgb(10, 18, 25);
        public static readonly Color BgDeep = Color.FromArgb(7, 13, 19);
        public static readonly Color Panel = Color.FromArgb(19, 33, 44);
        public static readonly Color Input = Color.FromArgb(12, 22, 30);
        public static readonly Color Text = Color.FromArgb(227, 238, 244);
        public static readonly Color TextSoft = Color.FromArgb(163, 184, 198);
        public static readonly Color TextFaint = Color.FromArgb(143, 167, 182);
        public static readonly Color Gold = Color.FromArgb(232, 195, 106);
        public static readonly Color GoldEdge = Color.FromArgb(201, 166, 78);
        public static readonly Color GoldInk = Color.FromArgb(26, 20, 8);
        public static readonly Color Button = Color.FromArgb(26, 42, 56);
        public static readonly Color ButtonLine = Color.FromArgb(61, 90, 114);
        public static readonly Color TitleBar = Color.FromArgb(13, 22, 32);
        public static readonly Color Frame = Color.FromArgb(36, 56, 74);
        public static readonly Color Navy = Color.FromArgb(16, 28, 40);
        public static readonly Color Tab = Color.FromArgb(92, 28, 24);
        public static readonly Color Quantity = Color.FromArgb(255, 220, 40);

        static FontFamily display, uiFamily, semiboldFamily;
        static bool fontsLoaded;
        static readonly List<PrivateFontCollection> collections = new List<PrivateFontCollection>();
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr data, uint length, IntPtr reserved, ref uint fonts);
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        static FontFamily LoadFont(string resource)
        {
            try
            {
                using (Stream stream = typeof(KavrosTheme).Assembly.GetManifestResourceStream("Chest.Branding." + resource))
                {
                    if (stream == null) return null;
                    byte[] bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    IntPtr memory = Marshal.AllocCoTaskMem(bytes.Length);
                    Marshal.Copy(bytes, 0, memory, bytes.Length);
                    PrivateFontCollection collection = new PrivateFontCollection();
                    collection.AddMemoryFont(memory, bytes.Length);
                    collections.Add(collection);
                    uint count = 0;
                    try { AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, ref count); }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                    return collection.Families.Length > 0 ? collection.Families[0] : null;
                }
            }
            catch (Exception) { return null; }
        }

        static void EnsureFonts()
        {
            if (fontsLoaded) return;
            fontsLoaded = true;
            display = LoadFont("CinzelKavros-Regular.ttf");
            uiFamily = LoadFont("OutfitKavros-Regular.ttf");
            semiboldFamily = LoadFont("OutfitKavros-Semibold.ttf");
        }

        public static Font Display(float size) { return FontOf("d" + size, size, true, false); }
        public static Font Ui(float size, bool semibold) { return FontOf((semibold ? "s" : "u") + size, size, false, semibold); }

        static Font FontOf(string key, float size, bool displayFace, bool semibold)
        {
            Font font;
            if (cache.TryGetValue(key, out font)) return font;
            EnsureFonts();
            FontFamily family = displayFace ? display : (semibold ? semiboldFamily : uiFamily);
            font = family != null
                ? new Font(family, size, FontStyle.Regular, GraphicsUnit.Point)
                : new Font(displayFace ? "Georgia" : "Segoe UI", size, semibold || displayFace ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            cache[key] = font;
            return font;
        }

        public static void ApplyWindowChrome(IntPtr handle)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(handle, 19, ref on, 4);
                int caption = ColorRef(TitleBar), border = ColorRef(Frame), text = ColorRef(Text);
                DwmSetWindowAttribute(handle, 35, ref caption, 4);
                DwmSetWindowAttribute(handle, 34, ref border, 4);
                DwmSetWindowAttribute(handle, 36, ref text, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static void StyleCombo(ComboBox combo)
        {
            combo.BackColor = Input;
            combo.ForeColor = Text;
            combo.FlatStyle = FlatStyle.Flat;
        }

        public static void FillRounded(Graphics g, Rectangle r, float radius, Color fill, Color border)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(new RectangleF(r.X + 0.5F, r.Y + 0.5F, r.Width - 1, r.Height - 1), radius))
            {
                if (fill.A > 0) using (Brush b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border.A > 0) using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }
            g.SmoothingMode = old;
        }

        static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Max(0.1F, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class OpaqueButton : Button
    {
        bool hover, pressed;
        internal ButtonTone Tone = ButtonTone.Secondary;
        public OpaqueButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (Brush s = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(s, ClientRectangle);
            Color fill, edge, ink;
            if (Tone == ButtonTone.Primary) { fill = KavrosTheme.Gold; edge = KavrosTheme.GoldEdge; ink = KavrosTheme.GoldInk; if (hover) fill = Color.FromArgb(240, 207, 128); if (pressed) fill = Color.FromArgb(213, 178, 107); }
            else { fill = KavrosTheme.Button; edge = KavrosTheme.ButtonLine; ink = KavrosTheme.Text; if (hover) fill = Color.FromArgb(34, 54, 71); if (pressed) fill = Color.FromArgb(20, 34, 46); }
            if (!Enabled) { fill = Color.FromArgb(40, 50, 58); ink = KavrosTheme.TextFaint; }
            KavrosTheme.FillRounded(g, new Rectangle(0, 0, Width, Height), 3, fill, edge);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ink, fill, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
