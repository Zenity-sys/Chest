using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Chest
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct PointStruct { public int X, Y; }
        public delegate bool EnumProc(IntPtr window, IntPtr param);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref PointStruct point);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);

        public const int Srccopy = 0x00CC0020;
        public const uint GaRoot = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct BitmapInfoHeader
        {
            public int Size, Width, Height;
            public short Planes, BitCount;
            public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        public static Rectangle VirtualScreen()
        {
            try
            {
                int width = GetSystemMetrics(78), height = GetSystemMetrics(79);
                if (width > 0 && height > 0) return new Rectangle(GetSystemMetrics(76), GetSystemMetrics(77), width, height);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            return System.Windows.Forms.SystemInformation.VirtualScreen;
        }

        // ClientToScreen is physical pixels once the process is per-monitor aware.
        // At 96 DPI that is the same as the client's own pixels.
        public static Rectangle ClientBounds(IntPtr window)
        {
            Rect r;
            PointStruct p = new PointStruct();
            if (!GetClientRect(window, out r) || !ClientToScreen(window, ref p)) return Rectangle.Empty;
            return new Rectangle(p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top);
        }

        public static int Dpi(IntPtr window)
        {
            try { uint dpi = GetDpiForWindow(window); if (dpi > 0) return (int)dpi; }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            return 96;
        }
    }

    // A screen copy owned as bytes. The bitmap used to read a file never leaves
    // the method that created it, so two threads never share a GDI+ image.
    internal sealed class Frame
    {
        public readonly byte[] Bgra;
        public readonly int Width, Height;
        public Frame(byte[] bgra, int width, int height)
        {
            Bgra = bgra; Width = width; Height = height;
        }
        public void Get(int x, int y, out int r, out int g, out int b)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) { r = g = b = 0; return; }
            int i = (y * Width + x) * 4;
            b = Bgra[i]; g = Bgra[i + 1]; r = Bgra[i + 2];
        }
        public Frame Crop(Rectangle area)
        {
            area = Rectangle.Intersect(area, new Rectangle(0, 0, Width, Height));
            byte[] data = new byte[Math.Max(0, area.Width * area.Height * 4)];
            for (int y = 0; y < area.Height; y++)
                Buffer.BlockCopy(Bgra, ((area.Y + y) * Width + area.X) * 4, data, y * area.Width * 4, area.Width * 4);
            return new Frame(data, area.Width, area.Height);
        }
    }

    internal static class ScreenCapture
    {
        // BitBlt into a private DIB. No System.Drawing.Bitmap is created, so the
        // capture thread and the window thread do not share a GDI+ image.
        public static Frame Grab(Rectangle screen)
        {
            if (screen.Width < 2 || screen.Height < 2) return null;
            IntPtr desktop = Native.GetDC(IntPtr.Zero);
            if (desktop == IntPtr.Zero) return null;
            IntPtr memory = IntPtr.Zero;
            IntPtr dib = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                memory = Native.CreateCompatibleDC(desktop);
                Native.BitmapInfo info = new Native.BitmapInfo();
                info.Header.Size = Marshal.SizeOf(typeof(Native.BitmapInfoHeader));
                info.Header.Width = screen.Width;
                info.Header.Height = -screen.Height;
                info.Header.Planes = 1;
                info.Header.BitCount = 32;
                IntPtr bits;
                dib = Native.CreateDIBSection(memory, ref info, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return null;
                old = Native.SelectObject(memory, dib);
                if (!Native.BitBlt(memory, 0, 0, screen.Width, screen.Height, desktop, screen.X, screen.Y, Native.Srccopy)) return null;
                byte[] data = new byte[screen.Width * screen.Height * 4];
                Marshal.Copy(bits, data, 0, data.Length);
                return new Frame(data, screen.Width, screen.Height);
            }
            catch (Exception ex) { CrashLog.Write(ex); return null; }
            finally
            {
                if (old != IntPtr.Zero && memory != IntPtr.Zero) Native.SelectObject(memory, old);
                if (dib != IntPtr.Zero) Native.DeleteObject(dib);
                if (memory != IntPtr.Zero) Native.DeleteDC(memory);
                Native.ReleaseDC(IntPtr.Zero, desktop);
            }
        }
    }
}
