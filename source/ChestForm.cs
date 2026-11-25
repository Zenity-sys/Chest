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

    // The combined chest. One grid, gold frame, navy fill, like the reward panel.
    internal sealed class ChestView : Panel
    {
        public ChestLayout Grid = new ChestLayout();
        public List<SlotView> Slots = new List<SlotView>();
        public ChestView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KavrosTheme.Navy;
            AutoScroll = true;
        }
        public void ShowSlots(List<SlotView> slots)
        {
            Slots = slots ?? new List<SlotView>();
            int columns = Math.Max(1, Grid.Columns);
            int rows = Math.Max(1, (Slots.Count + columns - 1) / columns);
            int cellW = Grid.Icon + Grid.Gap;
            int cellH = Grid.Icon + Grid.NameHeight + Grid.Gap;
            AutoScrollMinSize = new Size(Grid.Pad * 2 + columns * cellW, Grid.Pad * 2 + 36 + rows * cellH);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(KavrosTheme.Navy);
            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            Rectangle frame = new Rectangle(8, 8, Math.Max(40, ClientSize.Width - 16), Math.Max(40, Math.Max(ClientSize.Height, AutoScrollMinSize.Height) - 16));
            using (Pen gold = new Pen(KavrosTheme.GoldEdge, 2)) g.DrawRectangle(gold, frame);
            using (Brush tab = new SolidBrush(KavrosTheme.Tab)) g.FillRectangle(tab, frame.X + 14, frame.Y + 8, 150, 22);
            TextRenderer.DrawText(g, "REWARDS", KavrosTheme.Display(11F), new Rectangle(frame.X + 14, frame.Y + 8, 150, 22), KavrosTheme.Gold, KavrosTheme.Tab, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Slots.Count == 0)
            {
                TextRenderer.DrawText(g, "Open a casket. Rewards land here when the window closes.", KavrosTheme.Ui(9F, false), new Rectangle(frame.X + 20, frame.Y + 70, frame.Width - 40, 40), KavrosTheme.TextSoft, KavrosTheme.Navy, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
                return;
            }
            int columns = Math.Max(1, Grid.Columns);
            int cellW = Grid.Icon + Grid.Gap;
            int cellH = Grid.Icon + Grid.NameHeight + Grid.Gap;
            int originX = frame.X + Grid.Pad;
            int originY = frame.Y + 40;
            for (int i = 0; i < Slots.Count; i++)
            {
                SlotView slot = Slots[i];
                int x = originX + (i % columns) * cellW;
                int y = originY + (i / columns) * cellH;
                Rectangle box = new Rectangle(x, y, Grid.Icon, Grid.Icon);
                using (Brush well = new SolidBrush(Color.FromArgb(10, 22, 32))) g.FillRectangle(well, box);
                using (Pen edge = new Pen(Color.FromArgb(40, 70, 90))) g.DrawRectangle(edge, box);
                if (slot.Image != null)
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(slot.Image, box);
                }
                string qty = slot.Quantity.ToString();
                TextRenderer.DrawText(g, qty, KavrosTheme.Ui(9F, true), new Rectangle(box.X + 2, box.Y + 1, box.Width - 4, 18), KavrosTheme.Quantity, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                Color nameColor = slot.Unknown ? KavrosTheme.TextSoft : KavrosTheme.Gold;
                TextRenderer.DrawText(g, slot.Name, KavrosTheme.Ui(7.5F, false), new Rectangle(x, box.Bottom + 2, cellW - 4, Grid.NameHeight), nameColor, KavrosTheme.Navy, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
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
        readonly RewardLatch latch = new RewardLatch();
        readonly List<Glyph> stackGlyphs = new List<Glyph>();
        readonly List<Glyph> valueGlyphs = new List<Glyph>();
        List<IconRef> icons = new List<IconRef>();
        readonly Dictionary<string, Bitmap> pictures = new Dictionary<string, Bitmap>();
        readonly string assetPath;
        readonly string dataRoot;
        List<GameWindow> clients = new List<GameWindow>();
        IntPtr selected = IntPtr.Zero;
        uint selectedPid;
        bool refreshing;
        bool resetArmed;
        DateTime resetArmedAt;
        RewardShot pending;
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
            latch.MissesBeforeClose = layout.MissesBeforeClose;
            dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chest");
            book = new LootBook(dataRoot);
            book.Load();
            Text = "Chest";
            try { Icon = AppBranding.LoadIcon(); } catch (Exception) { }
            ClientSize = new Size(layout.WindowWidth, layout.WindowHeight);
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            BuildChrome();
            stackGlyphs.AddRange(RewardReader.LoadGlyphs(Path.Combine(assetPath, "digits", "stack.txt")));
            valueGlyphs.AddRange(RewardReader.LoadGlyphs(Path.Combine(assetPath, "digits", "value.txt")));
            icons = IconLibrary.Load(Path.Combine(assetPath, "icons"), Path.Combine(assetPath, "icons", "catalog.txt"));
            RefreshGrid();
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
                AppUpdates.Check(this, false);
            };
            FormClosed += delegate
            {
                running = false;
                foreach (Bitmap bitmap in pictures.Values) bitmap.Dispose();
            };
        }

        void BuildChrome()
        {
            Label title = new Label();
            title.Text = "CHEST";
            title.Font = KavrosTheme.Display(20F);
            title.ForeColor = KavrosTheme.Gold;
            title.SetBounds(20, 12, 220, 34);
            Controls.Add(title);
            Label version = new Label();
            version.Text = AppBranding.DisplayVersion + "   ·   Made by Zenity";
            version.Font = KavrosTheme.Ui(8F, false);
            version.ForeColor = KavrosTheme.TextFaint;
            version.SetBounds(20, 44, 320, 18);
            Controls.Add(version);
            Label game = new Label();
            game.Text = "Game window";
            game.ForeColor = KavrosTheme.TextSoft;
            game.SetBounds(20, 72, 110, 22);
            Controls.Add(game);
            windows.DropDownStyle = ComboBoxStyle.DropDownList;
            windows.SetBounds(130, 68, 360, 28);
            KavrosTheme.StyleCombo(windows);
            windows.SelectedIndexChanged += delegate { if (!refreshing) Attach(); };
            Controls.Add(windows);
            OpaqueButton refresh = new OpaqueButton();
            refresh.Text = "Refresh";
            refresh.SetBounds(500, 66, 90, 28);
            refresh.Click += delegate { RefreshWindows(); };
            Controls.Add(refresh);
            OpaqueButton capture = new OpaqueButton();
            capture.Text = "Save capture";
            capture.SetBounds(600, 66, 120, 28);
            capture.Click += delegate { saveRequested = true; status.Text = "Saving a capture of the game window..."; };
            Controls.Add(capture);
            status.SetBounds(20, 100, ClientSize.Width - 40, 22);
            status.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            status.ForeColor = KavrosTheme.TextSoft;
            status.Text = "Looking for the game window.";
            Controls.Add(status);
            chest.Grid = layout;
            chest.SetBounds(16, 128, ClientSize.Width - 32, ClientSize.Height - 196);
            chest.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(chest);
            totals.SetBounds(20, ClientSize.Height - 56, 460, 36);
            totals.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            totals.ForeColor = KavrosTheme.Gold;
            totals.Font = KavrosTheme.Ui(10F, true);
            Controls.Add(totals);
            OpaqueButton reset = new OpaqueButton();
            reset.Text = "Reset";
            reset.Tone = ButtonTone.Primary;
            reset.SetBounds(ClientSize.Width - 280, ClientSize.Height - 58, 90, 30);
            reset.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            reset.Click += delegate { OnReset(); };
            Controls.Add(reset);
            OpaqueButton updates = new OpaqueButton();
            updates.Text = "Check for updates";
            updates.SetBounds(ClientSize.Width - 180, ClientSize.Height - 58, 150, 30);
            updates.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            updates.Click += delegate { AppUpdates.Check(this, true); };
            Controls.Add(updates);
            restart.Text = "Restart";
            restart.Tone = ButtonTone.Primary;
            restart.Visible = false;
            restart.SetBounds(ClientSize.Width - 450, ClientSize.Height - 58, 90, 30);
            restart.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            restart.Click += delegate { if (restartClick != null) restartClick(); };
            Controls.Add(restart);
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
            latch.MissesBeforeClose = layout.MissesBeforeClose;
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
                if (pending != null) PostCommit(pending);
                latch.Reset();
            }
            IntPtr handle = selected;
            if (handle == IntPtr.Zero || !Native.IsWindow(handle))
            {
                if (latch.Miss() && pending != null) PostCommit(pending);
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
            if (local.IsEmpty)
            {
                if (latch.Miss() && pending != null) PostCommit(pending);
                return;
            }
            string quick = RewardReader.Fingerprint(frame, local);
            if (latch.Unchanged(quick + (pending == null ? "" : ":" + pending.Value)))
            {
                latch.Touch();
                return;
            }
            RewardShot shot = RewardReader.Read(frame, local, stackGlyphs, valueGlyphs);
            if (shot.Items.Count == 0)
            {
                if (latch.Miss() && pending != null) PostCommit(pending);
                return;
            }
            latch.Note(quick + ":" + shot.Value);
            pending = shot;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    status.Text = "Casket open. " + shot.Items.Count + " items, value " + Format(shot.Value) + ". Counted when it closes.";
                }));
            }
            catch (InvalidOperationException) { }
        }

        void PostCommit(RewardShot shot)
        {
            pending = null;
            RewardShot commit = shot;
            try { BeginInvoke(new Action(delegate { Apply(commit); })); }
            catch (InvalidOperationException) { }
        }

        void Apply(RewardShot shot)
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
            book.Commit(lines, shot.Value);
            RefreshGrid();
            status.Text = "Logged a casket. " + lines.Count + " items, value " + Format(shot.Value) + ".";
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
            totals.Text = "Caskets opened  " + book.Caskets + "      Reward value  " + Format(book.Value);
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
    }
}
