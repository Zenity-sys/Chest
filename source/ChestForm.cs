using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Chest
{
    internal sealed class SlotView
    {
        public string Name;
        public long Quantity;
        public Bitmap Image;
        public bool Unknown;
    }

    // Items sit directly on the navy panel. The title stays put while the grid scrolls.
    internal sealed class ChestView : Panel
    {
        public ChestLayout Grid = new ChestLayout();
        public List<SlotView> Slots = new List<SlotView>();
        const int Header = 34;
        readonly ToolTip names = new ToolTip();
        public ChestView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = KavrosTheme.Navy;
            AutoScroll = true;
            DoubleBuffered = true;
            names.InitialDelay = 400;
            names.ReshowDelay = 100;
        }
        public void ShowSlots(List<SlotView> slots)
        {
            Slots = slots ?? new List<SlotView>();
            int columns = Math.Max(1, Grid.Columns);
            int rows = (Slots.Count + columns - 1) / columns;
            int cell = Grid.Icon + Grid.Gap;
            AutoScrollMinSize = new Size(Grid.Pad * 2 + columns * cell, Header + Grid.Pad + Math.Max(1, rows) * cell + Grid.Pad);
            Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SlotView slot = Hit(e.Location);
            string tip = slot == null ? "" : (string.IsNullOrEmpty(slot.Name) ? "" : slot.Name);
            if (names.GetToolTip(this) != tip) names.SetToolTip(this, tip);
        }
        SlotView Hit(Point point)
        {
            int columns = Math.Max(1, Grid.Columns);
            int cell = Grid.Icon + Grid.Gap;
            int x = point.X - Grid.Pad;
            int y = point.Y - Header - Grid.Pad - AutoScrollPosition.Y;
            if (x < 0 || y < 0) return null;
            int col = x / cell;
            int row = y / cell;
            if (col >= columns || (x % cell) >= Grid.Icon || (y % cell) >= Grid.Icon) return null;
            int index = row * columns + col;
            if (index < 0 || index >= Slots.Count) return null;
            return Slots[index];
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(KavrosTheme.Navy);
            int columns = Math.Max(1, Grid.Columns);
            int cell = Grid.Icon + Grid.Gap;
            int scrollY = AutoScrollPosition.Y;
            if (Slots.Count == 0)
            {
                using (Brush ink = new SolidBrush(KavrosTheme.TextSoft))
                    g.DrawString("Open a casket.", KavrosTheme.Ui(9F, false), ink, new RectangleF(16, Header + 28, Math.Max(40, ClientSize.Width - 32), 40));
            }
            else
            {
                g.SetClip(new Rectangle(0, Header, ClientSize.Width, Math.Max(1, ClientSize.Height - Header)));
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                using (Brush shadow = new SolidBrush(Color.FromArgb(230, 8, 10, 14)))
                using (Brush yellow = new SolidBrush(KavrosTheme.Quantity))
                {
                    Font qtyFont = KavrosTheme.Ui(8F, true);
                    for (int i = 0; i < Slots.Count; i++)
                    {
                        SlotView slot = Slots[i];
                        int x = Grid.Pad + (i % columns) * cell;
                        int y = Header + Grid.Pad + (i / columns) * cell + scrollY;
                        if (y > ClientSize.Height || y + Grid.Icon < Header) continue;
                        if (slot.Image != null) g.DrawImage(slot.Image, new Rectangle(x, y, Grid.Icon, Grid.Icon));
                        string qty = StackText.Format(slot.Quantity);
                        float qx = x + 1;
                        float qy = y + 1;
                        g.DrawString(qty, qtyFont, shadow, qx + 1, qy + 1);
                        g.DrawString(qty, qtyFont, yellow, qx, qy);
                    }
                }
                g.ResetClip();
            }
            using (Brush tab = new SolidBrush(KavrosTheme.Tab)) g.FillRectangle(tab, 0, 0, 168, 24);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold))
                g.DrawString("Clue Rewards", KavrosTheme.Display(11F), gold, 10, 3);
        }
    }

    public sealed class ChestForm : Form
    {
        readonly ChestLayout layout;
        readonly ComboBox windows = new ComboBox();
        readonly Label status = new Label();
        readonly Label totals = new Label();
        readonly ChestView chest = new ChestView();
        readonly OpaqueButton restart = new OpaqueButton();
        readonly LootBook book;
        readonly RewardSession session = new RewardSession();
        readonly List<Glyph> stackGlyphs = new List<Glyph>();
        readonly List<Glyph> valueGlyphs = new List<Glyph>();
        List<IconRef> icons = new List<IconRef>();
        readonly Dictionary<string, Bitmap> pictures = new Dictionary<string, Bitmap>();
        readonly string assetPath;
        readonly string dataRoot;
        List<GameWindow> clients = new List<GameWindow>();
        IntPtr selected = IntPtr.Zero;
        uint selectedPid;
        readonly Panel titleBar = new Panel();
        readonly Label heading = new Label();
        readonly OpaqueButton collapse = new OpaqueButton();
        readonly OpaqueButton closeButton = new OpaqueButton();
        readonly OpaqueButton unknownFolder = new OpaqueButton();
        readonly OpaqueButton undo = new OpaqueButton();
        readonly System.Windows.Forms.Timer follow = new System.Windows.Forms.Timer();
        bool refreshing;
        bool resetArmed;
        bool undoArmed;
        bool collapsed;
        bool dragging;
        bool offsetReady;
        bool topKnown;
        bool topMost;
        DateTime resetArmedAt;
        DateTime undoArmedAt;
        Point offset;
        Point dragStart;
        Point offsetAtDrag;
        volatile bool running = true;
        volatile bool saveRequested;
        volatile int clientEpoch;
        int watchEpoch;
        Thread watcher;
        Action restartClick;

        public ChestForm()
        {
            assetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
            if (!Directory.Exists(assetPath)) assetPath = Path.Combine(Directory.GetCurrentDirectory(), "assets");
            layout = ChestLayout.Load(Path.Combine(assetPath, "chest-layout.txt"));
            session.MissesBeforeClose = layout.MissesBeforeClose;
            dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chest");
            book = new LootBook(dataRoot);
            book.Load();
            LoadPanel();
            Text = "Chest";
            try { Icon = AppBranding.LoadIcon(); } catch (Exception) { }
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            BackColor = KavrosTheme.Navy;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            ClientSize = new Size(layout.WindowWidth, collapsed ? 32 : layout.WindowHeight);
            BuildChrome();
            stackGlyphs.AddRange(RewardReader.LoadGlyphs(Path.Combine(assetPath, "digits", "stack.txt")));
            valueGlyphs.AddRange(RewardReader.LoadGlyphs(Path.Combine(assetPath, "digits", "value.txt")));
            icons = IconLibrary.Load(Path.Combine(assetPath, "icons"), Path.Combine(assetPath, "icons", "catalog.txt"));
            RewardReader.LoadTitle(Path.Combine(assetPath, "reward", "trail-title.png"));
            RefreshGrid();
            follow.Interval = 50;
            follow.Tick += Follow;
            Resize += delegate { LayoutChrome(); };
            AppUpdates.Present = delegate(string message, bool error, string action, Action click)
            {
                status.ForeColor = error ? Color.FromArgb(242, 180, 180) : KavrosTheme.TextSoft;
                status.Text = message;
                restart.Visible = !string.IsNullOrEmpty(action);
                restart.Text = action ?? "";
                restartClick = click;
            };
            Shown += delegate
            {
                KavrosTheme.ApplyWindowChrome(Handle);
                RefreshWindows();
                watcher = new Thread(Watch);
                watcher.IsBackground = true;
                watcher.Name = "ChestWatch";
                watcher.Start();
                follow.Start();
                AppUpdates.Check(this, false);
            };
            FormClosed += delegate
            {
                running = false;
                follow.Stop();
                foreach (Bitmap bitmap in pictures.Values) bitmap.Dispose();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x80;
                return parameters;
            }
        }

        void BuildChrome()
        {
            titleBar.BackColor = KavrosTheme.TitleBar;
            Controls.Add(titleBar);
            heading.Text = "CHEST";
            heading.Font = KavrosTheme.Display(13F);
            heading.ForeColor = KavrosTheme.Gold;
            heading.BackColor = KavrosTheme.TitleBar;
            heading.SetBounds(12, 4, 90, 24);
            titleBar.Controls.Add(heading);
            Label version = new Label();
            version.Text = AppBranding.DisplayVersion + "  ·  Zenity";
            version.Font = KavrosTheme.Ui(8F, false);
            version.ForeColor = KavrosTheme.TextFaint;
            version.BackColor = KavrosTheme.TitleBar;
            version.SetBounds(104, 8, 180, 18);
            titleBar.Controls.Add(version);
            HookDrag(titleBar);
            HookDrag(heading);
            HookDrag(version);
            collapse.Text = collapsed ? "+" : "-";
            collapse.Click += delegate { SetCollapsed(!collapsed); };
            titleBar.Controls.Add(collapse);
            closeButton.Text = "x";
            closeButton.Click += delegate { Close(); };
            titleBar.Controls.Add(closeButton);
            windows.DropDownStyle = ComboBoxStyle.DropDownList;
            KavrosTheme.StyleCombo(windows);
            windows.SelectedIndexChanged += delegate { if (!refreshing) Attach(); };
            Controls.Add(windows);
            OpaqueButton refresh = new OpaqueButton();
            refresh.Text = "Refresh";
            refresh.Click += delegate { RefreshWindows(); };
            Controls.Add(refresh);
            OpaqueButton capture = new OpaqueButton();
            capture.Text = "Save capture";
            capture.Click += delegate { saveRequested = true; status.Text = "Saving a capture of the game window..."; };
            Controls.Add(capture);
            status.ForeColor = KavrosTheme.TextFaint;
            status.Font = KavrosTheme.Ui(8F, false);
            status.Text = "Looking for the game window.";
            Controls.Add(status);
            chest.Grid = layout;
            Controls.Add(chest);
            totals.ForeColor = KavrosTheme.TextFaint;
            totals.Font = KavrosTheme.Ui(8F, false);
            Controls.Add(totals);
            unknownFolder.Text = "Unknown folder";
            unknownFolder.Click += delegate { OpenUnknown(); };
            Controls.Add(unknownFolder);
            undo.Text = "Undo";
            undo.Click += delegate { OnUndo(); };
            Controls.Add(undo);
            OpaqueButton reset = new OpaqueButton();
            reset.Text = "Reset";
            reset.Click += delegate { OnReset(); };
            Controls.Add(reset);
            OpaqueButton updates = new OpaqueButton();
            updates.Text = "Updates";
            updates.Click += delegate { AppUpdates.Check(this, true); };
            Controls.Add(updates);
            restart.Text = "Restart";
            restart.Tone = ButtonTone.Primary;
            restart.Visible = false;
            restart.Click += delegate { if (restartClick != null) restartClick(); };
            Controls.Add(restart);
            LayoutChrome();
        }

        void HookDrag(Control control)
        {
            control.MouseDown += TitleDown;
            control.MouseMove += TitleMove;
            control.MouseUp += TitleUp;
        }

        void LayoutChrome()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            titleBar.SetBounds(0, 0, w, 32);
            collapse.SetBounds(w - 64, 4, 28, 24);
            closeButton.SetBounds(w - 32, 4, 28, 24);
            bool body = !collapsed;
            windows.Visible = body;
            status.Visible = body;
            chest.Visible = body;
            totals.Visible = body;
            unknownFolder.Visible = body;
            undo.Visible = body;
            foreach (Control control in Controls)
            {
                if (control == titleBar) continue;
                if (!body) control.Visible = false;
            }
            if (!body) return;
            windows.Visible = true;
            status.Visible = true;
            chest.Visible = true;
            totals.Visible = true;
            unknownFolder.Visible = true;
            undo.Visible = true;
            windows.SetBounds(12, 40, Math.Max(80, w - 250), 26);
            int right = w - 12;
            foreach (Control control in Controls)
            {
                OpaqueButton button = control as OpaqueButton;
                if (button == null || button == undo || button == unknownFolder || button == restart) continue;
                if (button.Text == "Refresh") { right -= 78; button.SetBounds(right, 38, 74, 26); button.Visible = true; }
                if (button.Text == "Save capture") { right -= 108; button.SetBounds(right, 38, 104, 26); button.Visible = true; }
                if (button.Text == "Reset") { button.SetBounds(w - 12 - 64, h - 40, 64, 28); button.Visible = true; }
                if (button.Text == "Updates") { button.SetBounds(w - 12 - 64 - 8 - 80, h - 40, 80, 28); button.Visible = true; }
            }
            restart.SetBounds(w - 12 - 64 - 8 - 80 - 8 - 80, h - 40, 80, 28);
            undo.SetBounds(12, h - 40, 64, 28);
            unknownFolder.SetBounds(84, h - 40, 130, 28);
            status.SetBounds(12, 70, w - 24, 16);
            totals.SetBounds(12, h - 58, w - 24, 16);
            chest.SetBounds(8, 90, Math.Max(40, w - 16), Math.Max(40, h - 90 - 66));
        }

        void SetCollapsed(bool value)
        {
            collapsed = value;
            collapse.Text = collapsed ? "+" : "-";
            ClientSize = new Size(layout.WindowWidth, collapsed ? 32 : layout.WindowHeight);
            LayoutChrome();
            SavePanel();
        }

        void TitleDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragStart = Cursor.Position;
            offsetAtDrag = offset;
            Control control = sender as Control;
            if (control != null) control.Capture = true;
        }

        void TitleMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Point cursor = Cursor.Position;
            int dx = cursor.X - dragStart.X;
            int dy = cursor.Y - dragStart.Y;
            bool exists = selected != IntPtr.Zero && Native.IsWindow(selected) && !Native.IsIconic(selected);
            if (!exists)
            {
                Location = new Point(Location.X + cursor.X - dragStart.X, Location.Y + cursor.Y - dragStart.Y);
                dragStart = cursor;
                return;
            }
            offset = new Point(offsetAtDrag.X + dx, offsetAtDrag.Y + dy);
            offsetReady = true;
            Rectangle game = Native.ClientBounds(selected);
            Rectangle desktop = Screen.FromRectangle(game).WorkingArea;
            Native.PlaceNoZOrder(Handle, FloatingPanelLayout.Place(game, Size, offset, desktop));
        }

        void TitleUp(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            Control control = sender as Control;
            if (control != null) control.Capture = false;
            SavePanel();
        }

        void OnReset()
        {
            if (!resetArmed || (DateTime.UtcNow - resetArmedAt).TotalSeconds > 4)
            {
                resetArmed = true;
                resetArmedAt = DateTime.UtcNow;
                status.Text = "Click Reset again to clear the chest.";
                return;
            }
            resetArmed = false;
            book.Reset();
            RefreshGrid();
            status.Text = "Chest cleared.";
        }

        void OnUndo()
        {
            if (book.Last == null)
            {
                status.Text = "Nothing to undo.";
                return;
            }
            if (!undoArmed || (DateTime.UtcNow - undoArmedAt).TotalSeconds > 4)
            {
                undoArmed = true;
                undoArmedAt = DateTime.UtcNow;
                status.Text = "Click Undo again to remove the last casket.";
                return;
            }
            undoArmed = false;
            if (!book.UndoLast()) return;
            RefreshGrid();
            status.Text = "Removed the last casket.";
        }

        void OpenUnknown()
        {
            try
            {
                Directory.CreateDirectory(book.UnknownDirectory);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + book.UnknownDirectory + "\"", UseShellExecute = true });
                status.Text = "Opened the unknown folder.";
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                status.Text = "Could not open the unknown folder.";
            }
        }

        void RefreshWindows()
        {
            int self = Process.GetCurrentProcess().Id;
            clients = WindowLocator.Clients(WindowLocator.Enumerate(self));
            WindowLocator.NameClients(clients);
            refreshing = true;
            windows.Items.Clear();
            foreach (GameWindow client in clients) windows.Items.Add(client);
            GameWindow chosen = WindowLocator.Choose(clients, selected, selectedPid, Native.GetForegroundWindow());
            if (chosen != null)
            {
                windows.SelectedItem = chosen;
                selected = chosen.Handle;
                selectedPid = chosen.ProcessId;
                status.Text = "Watching " + chosen.Display + ".";
            }
            else if (windows.Items.Count > 0) windows.SelectedIndex = 0;
            else status.Text = "Looking for the game window.";
            refreshing = false;
        }

        void Attach()
        {
            GameWindow window = windows.SelectedItem as GameWindow;
            selected = window == null ? IntPtr.Zero : window.Handle;
            selectedPid = window == null ? 0 : window.ProcessId;
            session.MissesBeforeClose = layout.MissesBeforeClose;
            session.Reset();
            clientEpoch++;
            status.Text = window == null ? "Looking for the game window." : "Watching " + window.Display + ".";
        }

        void Watch()
        {
            while (running)
            {
                try { Tick(); }
                catch (Exception ex) { CrashLog.Write(ex); }
                Thread.Sleep(Math.Max(30, layout.PollMilliseconds));
            }
        }

        void Tick()
        {
            if (watchEpoch != clientEpoch)
            {
                watchEpoch = clientEpoch;
                session.Reset();
            }
            IntPtr handle = selected;
            if (handle == IntPtr.Zero || !Native.IsWindow(handle))
            {
                session.See(false, null);
                return;
            }
            if (Native.IsIconic(handle)) return;
            Rectangle bounds = Native.ClientBounds(handle);
            Rectangle visible = Rectangle.Intersect(bounds, Native.VirtualScreen());
            if (visible.Width < 200 || visible.Height < 200) return;
            Frame frame = ScreenCapture.Grab(visible);
            if (frame == null) return;
            if (saveRequested)
            {
                saveRequested = false;
                Frame copy = frame;
                try { BeginInvoke(new Action(delegate { SaveCapture(copy); })); }
                catch (InvalidOperationException) { }
            }
            Rectangle local = RewardReader.Find(frame);
            bool titled = !local.IsEmpty && RewardReader.TitleMatches(frame, local);
            if (!titled)
            {
                session.See(false, null);
                return;
            }
            RewardShot shot = RewardReader.Read(frame, local, stackGlyphs, valueGlyphs);
            if (shot.Items.Count == 0) return;
            string action = session.See(true, RewardReader.RerollKey(frame, local));
            if (action == "none" || action == "closed") return;
            bool replace = action == "replaced";
            try { BeginInvoke(new Action(delegate { Apply(shot, replace); })); }
            catch (InvalidOperationException) { }
        }

        void Apply(RewardShot shot, bool replace)
        {
            if (IsDisposed || shot == null) return;
            List<LootLine> lines = new List<LootLine>();
            foreach (SeenItem item in shot.Items)
            {
                IconHit hit = IconLibrary.Match(icons, item.Bgra, item.Width, item.Height, layout.IconMatch);
                LootLine line = new LootLine();
                line.Quantity = item.Quantity;
                if (hit.Known)
                {
                    line.Id = string.IsNullOrEmpty(hit.Group) ? hit.Name : hit.Group;
                    line.Name = hit.Name;
                }
                else
                {
                    string hash = IconLibrary.HashCrop(item.Bgra, item.Width, item.Height);
                    line.Id = "unknown-" + hash;
                    line.Name = "Unknown";
                    line.Unknown = true;
                    string file = Path.Combine(book.UnknownDirectory, hash + ".png");
                    if (!File.Exists(file))
                        FramePng.Save(new Frame(item.Bgra, item.Width, item.Height), file);
                    Remember(line.Id, new Frame(item.Bgra, item.Width, item.Height));
                }
                lines.Add(line);
            }
            if (replace) book.ReplaceLast(lines, shot.Value);
            else book.Commit(lines, shot.Value);
            RefreshGrid();
            status.Text = (replace ? "Updated the open casket. " : "Logged a casket. ") + lines.Count + " items, value " + Format(shot.Value) + ".";
        }

        void RefreshGrid()
        {
            List<SlotView> slots = new List<SlotView>();
            foreach (LootLine line in book.Lines)
            {
                SlotView slot = new SlotView();
                slot.Name = line.Name;
                slot.Quantity = line.Quantity;
                slot.Unknown = line.Unknown;
                slot.Image = PictureFor(line);
                slots.Add(slot);
            }
            chest.ShowSlots(slots);
            totals.Text = book.Caskets + " caskets   ·   " + Format(book.Value);
        }

        Bitmap PictureFor(LootLine line)
        {
            Bitmap cached;
            if (pictures.TryGetValue(line.Id, out cached)) return cached;
            if (line.Unknown)
            {
                string file = Path.Combine(book.UnknownDirectory, line.Id.Substring("unknown-".Length) + ".png");
                Frame frame = File.Exists(file) ? FramePng.Decode(file) : null;
                if (frame == null) return null;
                return Remember(line.Id, frame);
            }
            foreach (IconRef icon in icons)
            {
                string group = string.IsNullOrEmpty(icon.Group) ? icon.Name : icon.Group;
                if (group == line.Id || icon.Name == line.Name)
                    return Remember(line.Id, new Frame(icon.Bgra, icon.Width, icon.Height));
            }
            return null;
        }

        Bitmap Remember(string id, Frame frame)
        {
            Bitmap bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                if (stride == frame.Width * 4) System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, data.Scan0, frame.Bgra.Length);
                else
                    for (int y = 0; y < frame.Height; y++)
                        System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, y * frame.Width * 4, IntPtr.Add(data.Scan0, y * stride), frame.Width * 4);
            }
            finally { bitmap.UnlockBits(data); }
            pictures[id] = bitmap;
            return bitmap;
        }

        void SaveCapture(Frame frame)
        {
            try
            {
                string file = Path.Combine(dataRoot, "captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                FramePng.Save(frame, file);
                status.Text = "Saved a capture to " + file;
            }
            catch (Exception ex) { CrashLog.Write(ex); status.Text = "Could not save the capture."; }
        }

        static string Format(long value)
        {
            return value.ToString("N0");
        }

        void Follow(object sender, EventArgs e)
        {
            if (IsDisposed || dragging) return;
            bool exists = selected != IntPtr.Zero && Native.IsWindow(selected);
            if (!exists)
            {
                try { if (IsHandleCreated) Native.SetOwner(Handle, IntPtr.Zero); }
                catch (Exception) { }
                topKnown = false;
                if (!Visible) Show();
                return;
            }
            bool minimised = Native.IsIconic(selected);
            bool gameFront = WindowLocator.IsForeground(selected);
            IntPtr front = Native.GetForegroundWindow();
            bool panelFront = front == Handle || ContainsFocus || (IsHandleCreated && Native.GetAncestor(front, Native.GaRoot) == Handle);
            if (!FloatingPanelLayout.Shown(true, minimised, gameFront, panelFront))
            {
                if (Visible) Hide();
                topKnown = false;
                return;
            }
            try { if (IsHandleCreated) Native.SetOwner(Handle, selected); }
            catch (Exception) { }
            Rectangle game = Native.ClientBounds(selected);
            if (!offsetReady && game.Width > 0)
            {
                offset = new Point(Math.Max(8, game.Width - Width - 16), 40);
                offsetReady = true;
            }
            Rectangle desktop = Screen.FromRectangle(game).WorkingArea;
            Rectangle place = FloatingPanelLayout.Place(game, Size, offset, desktop);
            if (Bounds != place) Native.PlaceNoZOrder(Handle, place);
            if (!Visible) Show();
            bool want = FloatingPanelLayout.WantTopMost(true, minimised, gameFront, panelFront);
            if (!topKnown || want != topMost)
            {
                Native.SetTopMostNoActivate(Handle, want);
                topMost = want;
                topKnown = true;
            }
        }

        string PanelPath { get { return Path.Combine(dataRoot, "panel.txt"); } }

        void LoadPanel()
        {
            try
            {
                if (!File.Exists(PanelPath)) return;
                foreach (string line in File.ReadAllLines(PanelPath))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length == 2 && parts[0] == "collapsed") collapsed = parts[1] == "true";
                    int x, y;
                    if (parts.Length == 3 && parts[0] == "offset" && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out y))
                    {
                        offset = new Point(x, y);
                        offsetReady = true;
                    }
                }
            }
            catch (Exception ex) { CrashLog.Write(ex); }
        }

        void SavePanel()
        {
            try
            {
                Directory.CreateDirectory(dataRoot);
                File.WriteAllLines(PanelPath, new string[] {
                    "collapsed\t" + (collapsed ? "true" : "false"),
                    "offset\t" + offset.X + "\t" + offset.Y
                });
            }
            catch (Exception ex) { CrashLog.Write(ex); }
        }
    }
}
