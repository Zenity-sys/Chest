using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Chest
{
    internal sealed class SplashForm : Form
    {
        public Action OpenClicked;
        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(640, 420);
            BackColor = KavrosTheme.Bg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = "Kavros Chest";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ChestFace.DrawSplash(e.Graphics, ClientRectangle);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (ChestFace.SplashButton(ClientRectangle).Contains(e.Location) && OpenClicked != null)
                OpenClicked();
        }
    }

    internal sealed class SettingsForm : Form
    {
        readonly ComboBox windows;
        public Action RefreshClicked;
        public Action ResetClicked;
        public Action SplashClicked;
        public Action UpdatesClicked;
        public Action OpenPanelClicked;
        public Action<string> ModeClicked;
        public Func<string> Mode;
        public Func<string> ClientName;
        public Func<int> Opened;
        public Func<long> SessionValue;
        public Func<IList<long>> Opens;

        public SettingsForm(ComboBox windows)
        {
            this.windows = windows;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(512, 680);
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = "Kavros Chest";
            KavrosTheme.StyleCombo(windows);
            windows.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(windows);
            Resize += delegate { PlaceClient(); };
        }

        public void PlaceBeside(Form owner)
        {
            if (owner == null) return;
            int x = owner.Right + 12;
            Rectangle desk = Screen.FromControl(owner).WorkingArea;
            if (x + Width > desk.Right) x = Math.Max(desk.Left, owner.Left - Width - 12);
            int y = owner.Top;
            if (y + Height > desk.Bottom) y = Math.Max(desk.Top, desk.Bottom - Height);
            Location = new Point(x, y);
        }

        void PlaceClient()
        {
            Rectangle slot = ChestFace.ClientSlot(ClientRectangle);
            windows.SetBounds(slot.X, slot.Y, slot.Width, slot.Height);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PlaceClient();
            windows.BringToFront();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            string mode = Mode == null ? "grid" : Mode();
            string client = ClientName == null ? "" : ClientName();
            int opened = Opened == null ? 0 : Opened();
            long value = SessionValue == null ? 0 : SessionValue();
            IList<long> opens = Opens == null ? null : Opens();
            ChestFace.DrawSettings(e.Graphics, ClientRectangle, client, mode != "list", opened, value, opens);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            string hit = ChestFace.HitSettings(ClientRectangle, e.Location);
            if (hit == "close") Hide();
            else if (hit == "refresh" && RefreshClicked != null) RefreshClicked();
            else if (hit == "open" && OpenPanelClicked != null) OpenPanelClicked();
            else if (hit == "reset" && ResetClicked != null) ResetClicked();
            else if (hit == "splash" && SplashClicked != null) SplashClicked();
            else if (hit == "updates" && UpdatesClicked != null) UpdatesClicked();
            else if ((hit == "grid" || hit == "list") && ModeClicked != null) ModeClicked(hit);
            Invalidate();
        }
    }
}
