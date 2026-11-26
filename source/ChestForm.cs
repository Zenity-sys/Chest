using System;
using System.Collections.Generic;
using System.Text;
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
        public byte[] Look;
    }

    // The chest itself. Icons sit on the dark panel. Only the icon area scrolls, and only vertically.
    internal sealed class ChestView : Panel
    {
        public ChestLayout Grid = new ChestLayout();
        public List<SlotView> Slots = new List<SlotView>();
        public List<long> Opens = new List<long>();
        public long RewardValue;
        public int Caskets;
        public bool UndoArmed;
        public bool Logged;
        public bool HistoryOpen;
        public string Mode = "grid";
        public string Detail;
        public Action UndoClicked;
        public Action UnknownClicked;
        public Action DebugClicked;
        public Action<string> ModeChanged;
        int scroll, hot = -1, down = -1;
        string tip;
        Point tipAt;
        public ChestView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = KavrosTheme.Bg;
            DoubleBuffered = true;
        }
        public void ShowSlots(List<SlotView> slots)
        {
            Slots = slots ?? new List<SlotView>();
            if (scroll > MaxScroll()) scroll = MaxScroll();
            Invalidate();
        }
        int IconSize { get { return Grid.Icon > ChestFace.IconBox ? ChestFace.IconBox : Math.Max(16, Grid.Icon); } }
        int HistoryCount { get { return Opens == null ? 0 : Opens.Count; } }
        int MaxScroll()
        {
            return ChestFace.MaxScroll(ClientRectangle, Mode, IconSize, Slots.Count, HistoryOpen, HistoryCount);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int next = scroll - (e.Delta / 3);
            if (next < 0) next = 0;
            int max = MaxScroll();
            if (next > max) next = max;
            if (next != scroll) { scroll = next; Invalidate(); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = ChestFace.Hit(ClientRectangle, e.Location, Mode, IconSize, Slots.Count, HistoryOpen, HistoryCount);
            if (hit != hot) { hot = hit; Invalidate(); }
            int index = ChestFace.HitSlot(ClientRectangle, e.Location, scroll, Mode, IconSize, Slots.Count, HistoryOpen, HistoryCount);
            SlotView slot = index < 0 ? null : Slots[index];
            string next = slot == null || string.IsNullOrEmpty(slot.Name) ? null : slot.Name + " × " + ChestFace.Group(slot.Quantity);
            if (next != tip || tipAt != e.Location) { tip = next; tipAt = e.Location; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1;
            down = -1;
            tip = null;
            Invalidate();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            down = ChestFace.Hit(ClientRectangle, e.Location, Mode, IconSize, Slots.Count, HistoryOpen, HistoryCount);
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int hit = ChestFace.Hit(ClientRectangle, e.Location, Mode, IconSize, Slots.Count, HistoryOpen, HistoryCount);
            int was = down;
            down = -1;
            Invalidate();
            if (hit < 0 || hit != was) return;
            if (hit == 0) { HistoryOpen = !HistoryOpen; scroll = 0; }
            if (hit == 1 && UndoClicked != null) UndoClicked();
            if (hit == 2 && UnknownClicked != null) UnknownClicked();
            if (hit == 3 && DebugClicked != null) DebugClicked();
            if (hit == 4 || hit == 5)
            {
                Mode = hit == 5 ? "list" : "grid";
                scroll = 0;
                if (ModeChanged != null) ModeChanged(Mode);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            ChestFace.Draw(e.Graphics, ClientRectangle, Slots, scroll, RewardValue, Caskets, hot, down, UndoArmed, IconSize, ChestFace.IconGap, tip, tipAt.X, tipAt.Y, Mode, HistoryOpen, Opens, Detail, Logged);
        }
    }

    public sealed class ChestForm : Form
    {
        readonly ChestLayout layout;
        readonly ComboBox windows = new ComboBox();
        readonly Label status = new Label();
        readonly ChestView chest = new ChestView();
        readonly OpaqueButton restart = new OpaqueButton();
        readonly LootBook book;
        readonly RewardSession session = new RewardSession();
        readonly List<Glyph> stackGlyphs = new List<Glyph>();
        readonly List<Glyph> valueGlyphs = new List<Glyph>();
        List<IconRef> icons = new List<IconRef>();
        readonly Dictionary<string, Bitmap> pictures = new Dictionary<string, Bitmap>();
        readonly List<byte[]> looks = new List<byte[]>();
        readonly List<string> lookIds = new List<string>();
        readonly List<string> lookNames = new List<string>();
        readonly string assetPath;
        readonly string dataRoot;
        List<GameWindow> clients = new List<GameWindow>();
        IntPtr selected = IntPtr.Zero;
        uint selectedPid;
        readonly Panel goldBar = new Panel();
        readonly Panel titleBar = new Panel();
        readonly Label heading = new Label();
        readonly Label clientLabel = new Label();
        readonly OpaqueButton settingsButton = new OpaqueButton();
        readonly OpaqueButton collapse = new OpaqueButton();
        readonly OpaqueButton closeButton = new OpaqueButton();
        SettingsForm settingsForm;
        SplashForm splashForm;
        string viewMode = "grid";
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
        readonly object grabLock = new object();
        Frame lastGrab;
        Rectangle lastPanel;
        bool lastTitle;
        int lastCharges = -1;
        string lastAction = "";
        RewardShot lastShot;
        Thread watcher;
        Action restartClick;

        public ChestForm()
        {
            assetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
            if (!Directory.Exists(assetPath)) assetPath = Path.Combine(Directory.GetCurrentDirectory(), "assets");
            layout = ChestLayout.Load(Path.Combine(assetPath, "chest-layout.txt"));
            session.MissesBeforeClose = layout.MissesBeforeClose;
            dataRoot = AppPaths.DataRoot;
            book = new LootBook(dataRoot);
            book.Load();
            LoadPanel();
            LoadView();
            Text = "Chest";
            try { Icon = AppBranding.LoadIcon(); } catch (Exception) { }
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            Opacity = 0;
            ClientSize = new Size(layout.WindowWidth, collapsed ? 32 : layout.WindowHeight);
            ChestFace.LoadCrest(Path.Combine(assetPath, "branding", "kavros-crest.png"));
            BuildChrome();
            chest.Mode = viewMode;
            chest.UndoClicked = delegate { OnUndo(); };
            chest.UnknownClicked = delegate { OpenUnknown(); };
            chest.DebugClicked = delegate { SaveDebug(); };
            chest.ModeChanged = delegate(string mode) { viewMode = mode; chest.Mode = mode; SaveView(); };
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
                Say(message);
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
                ShowSplash();
                AppUpdates.Check(this, false);
            };
            FormClosed += delegate
            {
                running = false;
                follow.Stop();
                foreach (Bitmap bitmap in pictures.Values) bitmap.Dispose();
            };
        }

        protected override void WndProc(ref Message message)
        {
            // Let an installer or a restart close this process immediately,
            // even while the panel is hidden behind another window.
            if (message.Msg == 0x0010 || message.Msg == 0x0011 || message.Msg == 0x0016)
            {
                running = false;
                if (message.Msg == 0x0011) message.Result = new IntPtr(1);
                Close();
                return;
            }
            base.WndProc(ref message);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x80;
                parameters.Style |= 0x02000000;
                return parameters;
            }
        }

        void BuildChrome()
        {
            goldBar.BackColor = KavrosTheme.Gold;
            Controls.Add(goldBar);
            titleBar.BackColor = Color.FromArgb(15, 26, 35);
            Controls.Add(titleBar);
            heading.Text = "CHEST";
            heading.Font = KavrosTheme.Display(11F);
            heading.ForeColor = KavrosTheme.Text;
            heading.BackColor = titleBar.BackColor;
            heading.SetBounds(10, 4, 70, 22);
            titleBar.Controls.Add(heading);
            clientLabel.Text = "|";
            clientLabel.Font = KavrosTheme.Ui(9F, false);
            clientLabel.ForeColor = KavrosTheme.TextSoft;
            clientLabel.BackColor = titleBar.BackColor;
            clientLabel.AutoEllipsis = true;
            titleBar.Controls.Add(clientLabel);
            HookDrag(titleBar);
            HookDrag(heading);
            HookDrag(clientLabel);
            settingsButton.Text = "Settings";
            settingsButton.Font = KavrosTheme.Ui(8F, false);
            settingsButton.Click += delegate { ShowSettings(); };
            titleBar.Controls.Add(settingsButton);
            collapse.Text = collapsed ? "+" : "−";
            collapse.Font = KavrosTheme.Ui(8F, false);
            collapse.Click += delegate { SetCollapsed(!collapsed); };
            titleBar.Controls.Add(collapse);
            closeButton.Text = "×";
            closeButton.Font = KavrosTheme.Ui(8F, false);
            closeButton.Click += delegate { Close(); };
            titleBar.Controls.Add(closeButton);
            windows.DropDownStyle = ComboBoxStyle.DropDownList;
            KavrosTheme.StyleCombo(windows);
            windows.SelectedIndexChanged += delegate { if (!refreshing) Attach(); };
            status.ForeColor = KavrosTheme.TextFaint;
            status.Font = KavrosTheme.Ui(8F, false);
            Say("Open a clue casket");
            chest.Grid = layout;
            chest.Mode = viewMode;
            Controls.Add(chest);
            restart.Text = "Restart";
            restart.Tone = ButtonTone.Primary;
            restart.Visible = false;
            restart.Font = KavrosTheme.Ui(8F, false);
            restart.Click += delegate { if (restartClick != null) restartClick(); };
            titleBar.Controls.Add(restart);
            settingsForm = new SettingsForm(windows);
            settingsForm.RefreshClicked = delegate { RefreshWindows(); };
            settingsForm.ResetClicked = delegate { OnReset(); };
            settingsForm.SplashClicked = delegate { ShowSplash(); };
            settingsForm.UpdatesClicked = delegate { AppUpdates.Check(this, true); };
            settingsForm.OpenPanelClicked = delegate { Opacity = 1; settingsForm.Hide(); };
            settingsForm.ModeClicked = delegate(string mode) { viewMode = mode; chest.Mode = mode; SaveView(); chest.Invalidate(); };
            settingsForm.Mode = delegate { return viewMode; };
            settingsForm.ClientName = delegate { GameWindow window = windows.SelectedItem as GameWindow; return window == null ? "" : window.Display; };
            settingsForm.Opened = delegate { return book.Caskets; };
            settingsForm.SessionValue = delegate { return book.Value; };
            settingsForm.Opens = delegate { return book.Opens; };
            LayoutChrome();
        }

        void Say(string message)
        {
            status.Text = message ?? "";
            chest.Detail = message;
            chest.Invalidate();
            if (settingsForm != null) settingsForm.Invalidate();
        }

        void ShowSplash()
        {
            Opacity = 0;
            if (splashForm == null || splashForm.IsDisposed) splashForm = new SplashForm();
            splashForm.OpenClicked = delegate
            {
                Opacity = 1;
                splashForm.Hide();
                if (settingsForm != null) settingsForm.Hide();
            };
            if (!splashForm.Visible) splashForm.Show(this);
            splashForm.BringToFront();
        }

        void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed) return;
            Opacity = 1;
            settingsForm.PlaceBeside(this);
            if (!settingsForm.Visible) settingsForm.Show(this);
            settingsForm.BringToFront();
            settingsForm.Invalidate();
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
            int head = collapsed ? h : 30;
            goldBar.SetBounds(0, 0, w, 2);
            titleBar.SetBounds(0, 2, w, Math.Max(0, head - 2));
            closeButton.SetBounds(w - 30, 3, 24, 22);
            collapse.SetBounds(w - 56, 3, 24, 22);
            settingsButton.SetBounds(w - 142, 3, 80, 22);
            restart.SetBounds(w - 210, 3, 64, 22);
            clientLabel.SetBounds(78, 5, Math.Max(40, w - 300), 20);
            bool body = !collapsed;
            chest.Visible = body;
            goldBar.Visible = true;
            if (!body) return;
            chest.SetBounds(0, head, Math.Max(80, w), Math.Max(80, h - head));
        }

        void SetCollapsed(bool value)
        {
            collapsed = value;
            collapse.Text = collapsed ? "+" : "−";
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
                Say("Click Reset again to clear the chest.");
                return;
            }
            resetArmed = false;
            book.Reset();
            RefreshGrid();
            Say("Chest cleared.");
        }

        void OnUndo()
        {
            if (book.Last == null)
            {
                Say("Nothing to undo.");
                return;
            }
            if (!undoArmed || (DateTime.UtcNow - undoArmedAt).TotalSeconds > 4)
            {
                undoArmed = true;
                undoArmedAt = DateTime.UtcNow;
                chest.UndoArmed = true;
                chest.Invalidate();
                Say("Click Undo again to remove the last casket.");
                return;
            }
            undoArmed = false;
            chest.UndoArmed = false;
            if (!book.UndoLast()) return;
            RefreshGrid();
            Say("Removed the last casket.");
        }

        void OpenUnknown()
        {
            try
            {
                Directory.CreateDirectory(book.UnknownDirectory);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + book.UnknownDirectory + "\"", UseShellExecute = true });
                Say("Opened the unknown folder.");
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                Say("Could not open the unknown folder.");
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
                clientLabel.Text = "|  " + chosen.Display;
                Say("Opening " + book.Caskets);
            }
            else if (windows.Items.Count > 0) windows.SelectedIndex = 0;
            else
            {
                clientLabel.Text = "|";
                Say("Looking for the game window.");
            }
            refreshing = false;
            if (settingsForm != null) settingsForm.Invalidate();
        }

        void Attach()
        {
            GameWindow window = windows.SelectedItem as GameWindow;
            selected = window == null ? IntPtr.Zero : window.Handle;
            selectedPid = window == null ? 0 : window.ProcessId;
            session.MissesBeforeClose = layout.MissesBeforeClose;
            session.Reset();
            clientEpoch++;
            clientLabel.Text = window == null ? "|" : "|  " + window.Display;
            Say(window == null ? "Looking for the game window." : "Opening " + book.Caskets);
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
                session.See(false, -1, null);
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
                session.See(false, -1, null);
                lock (grabLock) { lastGrab = frame; lastPanel = Rectangle.Empty; lastTitle = false; lastAction = "closed"; }
                DetectionLog.Write("no TRAIL COMPLETE");
                return;
            }
            RewardShot shot = RewardReader.Read(frame, local, stackGlyphs, valueGlyphs);
            if (shot.Items.Count == 0)
            {
                DetectionLog.Write("title found, no items");
                return;
            }
            int charges = RewardReader.RerollCount(frame, local);
            string action = session.See(true, charges, shot.Fingerprint);
            lock (grabLock)
            {
                lastGrab = frame;
                lastPanel = local;
                lastTitle = true;
                lastCharges = charges;
                lastAction = action;
                lastShot = shot;
            }
            DetectionLog.Write("title n=" + charges + " value=" + shot.Value + " items=" + shot.Items.Count + " " + action);
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
                LootLine line = new LootLine();
                byte[] pixels = item.Bgra;
                int pw = item.Width, ph = item.Height;
                if (Noted.LooksNoted(pixels, pw, ph)) pixels = Noted.WithoutPaper(pixels, pw, ph);
                byte[] cleaned = Noted.CleanForDisplay(pixels, pw, ph);
                IconHit hit = IconLibrary.Match(icons, cleaned, pw, ph, layout.IconMatch);
                byte[] sig = IconLibrary.Signature(cleaned, pw, ph);
                line.Quantity = item.Quantity;
                int knownLook = FindLook(sig);
                if (hit.Known)
                {
                    line.Id = string.IsNullOrEmpty(hit.Group) ? hit.Name : hit.Group;
                    line.Name = hit.Name;
                }
                else if (knownLook >= 0)
                {
                    line.Id = lookIds[knownLook];
                    line.Name = lookNames[knownLook];
                    line.Unknown = line.Id.StartsWith("item-");
                }
                else
                {
                    string hash = IconLibrary.HashCrop(cleaned, pw, ph);
                    line.Id = "item-" + hash;
                    line.Name = "Unknown";
                    line.Unknown = true;
                    string file = Path.Combine(book.UnknownDirectory, hash + ".png");
                    if (!File.Exists(file))
                        FramePng.Save(new Frame(cleaned, pw, ph), file);
                    Remember(line.Id, new Frame(cleaned, pw, ph));
                }
                RememberLook(line.Id, line.Name, sig);
                lines.Add(line);
            }
            if (replace) book.ReplaceLast(lines, shot.Value);
            else book.Commit(lines, shot.Value);
            RefreshGrid();
            Say(book.Caskets > 0 ? "Opening " + book.Caskets : "Open a clue casket");
        }

        int FindLook(byte[] sig)
        {
            for (int i = 0; i < looks.Count; i++)
                if (IconLibrary.SameLook(looks[i], sig)) return i;
            return -1;
        }

        void RememberLook(string id, string name, byte[] sig)
        {
            for (int i = 0; i < lookIds.Count; i++)
                if (lookIds[i] == id) { looks[i] = sig; lookNames[i] = name; return; }
            looks.Add(sig);
            lookIds.Add(id);
            lookNames.Add(name);
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
            chest.RewardValue = book.Value;
            chest.Caskets = book.Caskets;
            chest.UndoArmed = undoArmed;
            chest.Logged = book.Caskets > 0;
            chest.Opens = book.Opens;
            chest.ShowSlots(slots);
            if (settingsForm != null) settingsForm.Invalidate();
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
                byte[] cleaned = Noted.CleanForDisplay(frame.Bgra, frame.Width, frame.Height);
                return Remember(line.Id, new Frame(cleaned, frame.Width, frame.Height));
            }
            IconRef fallback = null;
            foreach (IconRef icon in icons)
            {
                string group = string.IsNullOrEmpty(icon.Group) ? icon.Name : icon.Group;
                if (group != line.Id && icon.Name != line.Name) continue;
                if (icon.Show) return Remember(line.Id, new Frame(icon.Bgra, icon.Width, icon.Height));
                if (fallback == null) fallback = icon;
            }
            if (fallback != null)
            {
                byte[] cleaned = Noted.CleanForDisplay(fallback.Bgra, fallback.Width, fallback.Height);
                return Remember(line.Id, new Frame(cleaned, fallback.Width, fallback.Height));
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

        void SaveDebug()
        {
            try
            {
                Frame grab;
                Rectangle panel;
                bool title;
                int charges;
                string action;
                RewardShot shot;
                lock (grabLock)
                {
                    grab = lastGrab;
                    panel = lastPanel;
                    title = lastTitle;
                    charges = lastCharges;
                    action = lastAction;
                    shot = lastShot;
                }
                string dir = Path.Combine(dataRoot, "debug");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                List<string> names = new List<string>();
                List<byte[]> blobs = new List<byte[]>();
                if (grab != null)
                {
                    names.Add("client.png");
                    blobs.Add(FramePng.Encode(grab));
                    if (title && panel.Width > 0)
                    {
                        names.Add("reward.png");
                        blobs.Add(FramePng.Encode(grab.Crop(panel)));
                    }
                }
                StringBuilder state = new StringBuilder();
                state.AppendLine("version " + AppBranding.FullVersion);
                state.AppendLine("title " + (title ? "yes" : "no"));
                state.AppendLine("action " + action);
                state.AppendLine("reroll " + charges);
                state.AppendLine("value " + (shot == null ? 0 : shot.Value));
                state.AppendLine("caskets " + book.Caskets);
                if (shot != null)
                    foreach (SeenItem item in shot.Items)
                        state.AppendLine("item " + item.Quantity + " at " + item.X + "," + item.Y);
                foreach (LootLine line in book.Lines)
                    state.AppendLine("stack " + line.Id + " " + line.Quantity + " " + line.Name);
                names.Add("state.txt");
                blobs.Add(Encoding.UTF8.GetBytes(state.ToString()));
                names.Add("detect.log");
                blobs.Add(Encoding.UTF8.GetBytes(DetectionLog.Tail()));
                string zip = Path.Combine(dir, "chest-debug-" + stamp + ".zip");
                StoredZip.Create(zip, names, blobs);
                Say("Saved a debug pack.");
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + dir + "\"", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                Say("Could not save the debug pack.");
            }
        }

        void SaveCapture(Frame frame)
        {
            try
            {
                string file = Path.Combine(dataRoot, "captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                FramePng.Save(frame, file);
                Say("Saved a capture.");
            }
            catch (Exception ex) { CrashLog.Write(ex); Say("Could not save the capture."); }
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
        string ViewPath { get { return Path.Combine(dataRoot, "view.txt"); } }

        void LoadView()
        {
            try
            {
                if (!File.Exists(ViewPath)) return;
                string mode = File.ReadAllText(ViewPath).Trim();
                if (mode == "list" || mode == "grid") viewMode = mode;
            }
            catch (Exception ex) { CrashLog.Write(ex); }
        }

        void SaveView()
        {
            try
            {
                Directory.CreateDirectory(dataRoot);
                File.WriteAllText(ViewPath, viewMode);
            }
            catch (Exception ex) { CrashLog.Write(ex); }
        }

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
