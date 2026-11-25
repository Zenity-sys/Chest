using System.Drawing;

namespace Chest
{
    // Same placement rules as the Toolkit clue panel: stay inside the chosen
    // client, sit above that client only, and get out of the way when it is not
    // the window in front.
    internal static class FloatingPanelLayout
    {
        public static Rectangle Place(Rectangle game, Size panel, Point offset, Rectangle desktop)
        {
            Rectangle area = Rectangle.Intersect(game, desktop);
            if (area.Width < 1 || area.Height < 1) area = game;
            int x = game.Left + offset.X;
            int y = game.Top + offset.Y;
            int limitX = area.Right - panel.Width;
            int limitY = area.Bottom - panel.Height;
            if (limitX < area.Left) x = area.Left;
            else x = System.Math.Max(area.Left, System.Math.Min(x, limitX));
            if (limitY < area.Top) y = area.Top;
            else y = System.Math.Max(area.Top, System.Math.Min(y, limitY));
            return new Rectangle(x, y, panel.Width, panel.Height);
        }

        public static bool Shown(bool gameExists, bool minimised, bool gameForeground, bool panelFocused)
        {
            if (!gameExists || minimised) return false;
            return gameForeground || panelFocused;
        }

        public static bool WantTopMost(bool gameExists, bool minimised, bool gameForeground, bool panelForeground)
        {
            if (!gameExists || minimised) return false;
            return gameForeground || panelForeground;
        }
    }

    internal static class StackText
    {
        public static string Format(long quantity)
        {
            if (quantity >= 1000000) return Trim(quantity / 1000000.0) + "M";
            if (quantity >= 10000) return Trim(quantity / 1000.0) + "k";
            return quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Trim(double value)
        {
            string text = value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (text.EndsWith(".0")) text = text.Substring(0, text.Length - 2);
            return text;
        }
    }
}
