// ===========================================================================
//  ClockFormUi.cs  --  the floating window (part 3 of 3): hover toolbar,
//  context menu, mode switching, geometry, look / language, settings.
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FloatingClockApp
{
    // =======================================================================
    //  The floating window itself  (part 3 of 3)
    // =======================================================================
    internal partial class ClockForm
    {
        // ---------------------------------------------------------------
        //  Hover toolbar (custom drawn, no child controls)
        // ---------------------------------------------------------------
        private void BuildButtons()
        {
            btnRects.Clear(); btnTexts.Clear(); btnActions.Clear();
            if (mode == Mode.Countdown)
            {
                btnTexts.Add(cdRunning ? L.S("暂停") : L.S("开始"));
                btnActions.Add(ToggleRun);
                btnTexts.Add(L.S("重置"));
                btnActions.Add(ResetCurrent);
            }
            else if (mode == Mode.Stopwatch)
            {
                btnTexts.Add(swRunning ? L.S("暂停") : L.S("开始"));
                btnActions.Add(ToggleRun);
                btnTexts.Add(L.S("重置"));
                btnActions.Add(ResetCurrent);
            }
            btnTexts.Add(L.S("设置"));
            btnActions.Add(OpenSettings);
            btnTexts.Add(L.S("退出"));
            btnActions.Add(Close);
        }

        // The hover toolbar keeps in step with the widget: 1.0 at the default
        // 160-logical-pixel height, shrinking/growing with the window (clamped so
        // the buttons never become unclickable or silly big).
        private float BarZoom()
        {
            float z = Height / (160f * Program.DpiScale);
            return Math.Max(0.8f, Math.Min(2.2f, z));
        }

        private void DrawBar(Graphics g)
        {
            btnRects.Clear();
            if (!barVisible || cfg.ClickThrough) return;
            BuildButtons();

            float zoom = BarZoom();
            float sc = Program.DpiScale * zoom;      // all lengths are logical * sc
            float fontPt = 9f * zoom;

            // Two passes: measure first, then squeeze everything down if the
            // window is too narrow to hold the strip.
            for (int pass = 0; pass < 2; pass++)
            {
            using (Font f = new Font("Microsoft YaHei UI", fontPt, FontStyle.Regular, GraphicsUnit.Point))
            {
                float padX = 10f * sc, gap = 4f * sc, padY = 4f * sc;
                float[] widths = new float[btnTexts.Count];
                float total = 0;
                for (int i = 0; i < btnTexts.Count; i++)
                {
                    widths[i] = g.MeasureString(btnTexts[i], f).Width + padX * 2;
                    total += widths[i] + (i > 0 ? gap : 0);
                }
                float barW = total + 12f * sc;
                if (pass == 0 && barW > Width - 6f)
                {
                    float fit = Math.Max(0.55f, (Width - 6f) / barW);
                    sc *= fit;
                    fontPt *= fit;
                    continue;                 // re-measure with the tighter metrics
                }
                float barH = 24f * sc;
                float bx = (Width - barW) / 2f;
                float by = 6f * sc;

                using (GraphicsPath p = Theme.Round(new RectangleF(bx, by, barW, barH), 8 * sc))
                using (SolidBrush b = new SolidBrush(Theme.Blend(Theme.FromHex(cfg.Bg),
                                                                  Theme.FromHex(cfg.Fg), 0.14)))
                    g.FillPath(b, p);

                float cx = bx + 6f * sc;
                for (int i = 0; i < btnTexts.Count; i++)
                {
                    RectangleF r = new RectangleF(cx, by + padY, widths[i], barH - padY * 2);
                    if (i == hoverBtn)
                    {
                        using (GraphicsPath p = Theme.Round(r, 6 * sc))
                        using (SolidBrush b = new SolidBrush(Theme.Blend(Theme.FromHex(cfg.Bg),
                                                                          Theme.FromHex(cfg.Fg), 0.28)))
                            g.FillPath(b, p);
                    }
                    using (SolidBrush b = new SolidBrush(Theme.FromHex(cfg.Fg)))
                    {
                        SizeF sz = g.MeasureString(btnTexts[i], f);
                        g.DrawString(btnTexts[i], f, b,
                            r.X + (r.Width - sz.Width) / 2f, r.Y + (r.Height - sz.Height) / 2f);
                    }
                    btnRects.Add(Rectangle.Round(r));
                    cx += widths[i] + gap;
                }
            }
            }
        }

        // ---------------------------------------------------------------
        //  Mouse / keyboard
        // ---------------------------------------------------------------
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (cfg.ClickThrough) return;

            lastMove = DateTime.Now;
            if (!dragging && !resizing)
            {
                barVisible = true;

                int hit = -1;
                for (int i = 0; i < btnRects.Count; i++)
                    if (btnRects[i].Contains(e.Location)) { hit = i; break; }
                if (hit != hoverBtn) hoverBtn = hit;

                if (!cfg.Locked && e.X > Width - 26 && e.Y > Height - 26)
                    Cursor = Cursors.SizeNWSE;
                else if (btnRects.Count > 0 && hit >= 0)
                    Cursor = Cursors.Hand;
                else
                    Cursor = cfg.Locked ? Cursors.Default : Cursors.SizeAll;
            }

            if (resizing)
            {
                int dw = e.X - resizeMouse0.X;
                int dh = e.Y - resizeMouse0.Y;
                int minW = (int)(150 * Program.DpiScale), minH = (int)(64 * Program.DpiScale);
                int maxW = (int)(2400 * Program.DpiScale), maxH = (int)(1600 * Program.DpiScale);
                int nw = Math.Max(minW, Math.Min(maxW, resizeSize0.Width + dw));
                int nh = Math.Max(minH, Math.Min(maxH, resizeSize0.Height + dh));
                Size = new Size(nw, nh);
            }
            else if (dragging)
            {
                Point screen = PointToScreen(e.Location);
                Rectangle wa = Screen.FromPoint(screen).WorkingArea;
                int nx = screen.X - dragOffset.X;
                int ny = screen.Y - dragOffset.Y;
                if (Math.Abs(nx - wa.Left) < 16) nx = wa.Left;
                if (Math.Abs(nx + Width - wa.Right) < 16) nx = wa.Right - Width;
                if (Math.Abs(ny - wa.Top) < 16) ny = wa.Top;
                if (Math.Abs(ny + Height - wa.Bottom) < 16) ny = wa.Bottom - Height;
                nx = Math.Max(wa.Left - Width + 100, Math.Min(nx, wa.Right - 100));
                ny = Math.Max(wa.Top, Math.Min(ny, wa.Bottom - 40));
                Location = new Point(nx, ny);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (cfg.ClickThrough) return;

            if (e.Button == MouseButtons.Left)
            {
                // toolbar first, so a click on it never turns into a drag
                for (int i = 0; i < btnRects.Count; i++)
                {
                    if (btnRects[i].Contains(e.Location))
                    {
                        Action a = btnActions[i];
                        if (a != null) a();
                        return;
                    }
                }
                if (!cfg.Locked && e.X > Width - 26 && e.Y > Height - 26)
                {
                    resizing = true;
                    resizeMouse0 = e.Location;
                    resizeSize0 = Size;
                    Capture = true;
                }
                else if (!cfg.Locked)
                {
                    dragging = true;
                    dragOffset = e.Location;
                    Capture = true;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging || resizing) SaveGeometry();
            dragging = false;
            resizing = false;
            Capture = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (cfg.ClickThrough || cfg.Locked) return;
            double factor = e.Delta > 0 ? 1.08 : 1 / 1.08;
            Size = new Size(
                Math.Max((int)(150 * Program.DpiScale), Math.Min((int)(2400 * Program.DpiScale), (int)(Width * factor))),
                Math.Max((int)(64 * Program.DpiScale), Math.Min((int)(1600 * Program.DpiScale), (int)(Height * factor))));
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (cfg.ClickThrough) return;
            OpenSettings();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Right && !cfg.ClickThrough) ShowContextMenu(e.Location);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) { ToggleRun(); e.Handled = true; }
            else if (e.KeyCode == Keys.R) { ResetCurrent(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { barVisible = false; Invalidate(); }
        }

        private void ShowContextMenu(Point p)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.Font = Theme.UiFont;
            menu.BackColor = Theme.Card;

            ToolStripMenuItem clock = new ToolStripMenuItem(L.S("时钟模式"), null,
                delegate { SetMode(Mode.Clock); });
            ToolStripMenuItem count = new ToolStripMenuItem(L.S("倒计时模式"), null,
                delegate { SetMode(Mode.Countdown); });
            ToolStripMenuItem stop = new ToolStripMenuItem(L.S("秒表模式"), null,
                delegate { SetMode(Mode.Stopwatch); });
            clock.Checked = mode == Mode.Clock;
            count.Checked = mode == Mode.Countdown;
            stop.Checked = mode == Mode.Stopwatch;
            menu.Items.Add(clock);
            menu.Items.Add(count);
            menu.Items.Add(stop);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(L.S("开始 / 暂停"), null, delegate { ToggleRun(); }));
            menu.Items.Add(new ToolStripMenuItem(L.S("重置"), null, delegate { ResetCurrent(); }));
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem top = new ToolStripMenuItem(L.S("始终置顶"), null, delegate { ToggleTopmost(); });
            top.Checked = cfg.Topmost;
            ToolStripMenuItem thru = new ToolStripMenuItem(L.S("点击穿透"), null, delegate { ToggleClickThrough(); });
            thru.Checked = cfg.ClickThrough;
            ToolStripMenuItem lockI = new ToolStripMenuItem(L.S("锁定位置"), null, delegate { ToggleLock(); });
            lockI.Checked = cfg.Locked;
            menu.Items.Add(top);
            menu.Items.Add(thru);
            menu.Items.Add(lockI);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(L.S("还原窗口 400×160"), null,
                delegate { Size = new Size(400, 160); }));
            menu.Items.Add(new ToolStripMenuItem(L.S("设置…"), null, delegate { OpenSettings(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(L.S("退出"), null, delegate { Close(); }));

            menu.Show(this, p);
        }

        // ---------------------------------------------------------------
        //  Public actions (also used by the settings dialog)
        // ---------------------------------------------------------------
        public void SetMode(Mode m)
        {
            mode = m;
            cfg.Mode = m == Mode.Clock ? "clock" : (m == Mode.Countdown ? "countdown" : "stopwatch");
            if (m == Mode.Countdown && !cdRunning) ResetCountdown();
            fitKey = "";
            Invalidate();
            cfg.Save();
        }

        public void ToggleRun()
        {
            if (mode == Mode.Countdown)
            {
                if (cdDone || cdLeft <= 0) ResetCountdown();
                if (cdRunning) cdRunning = false;
                else { cdEnd = DateTime.Now.AddSeconds(cdLeft); cdRunning = true; cdDone = false; }
            }
            else if (mode == Mode.Stopwatch)
            {
                if (swRunning) { swBase = swElapsed; swRunning = false; }
                else { swStart = DateTime.Now; swRunning = true; }
            }
            Invalidate();
        }

        public void ResetCurrent()
        {
            if (mode == Mode.Countdown) ResetCountdown();
            else if (mode == Mode.Stopwatch) { swBase = swElapsed = 0; swRunning = false; }
            Invalidate();
        }

        private void ResetCountdown()
        {
            cdTotal = Math.Max(1, cfg.CountdownSecs);
            cdLeft = cdTotal;
            cdRunning = false;
            cdDone = false;
            cdDoneAt = DateTime.MinValue;
        }

        // Persist the window geometry in logical pixels, so a DPI change later
        // does not multiply the size again on the next launch.
        private void SaveGeometry()
        {
            double s = Program.DpiScale;
            cfg.X = (int)Math.Round(Location.X / s);
            cfg.Y = (int)Math.Round(Location.Y / s);
            cfg.W = (int)Math.Round(Width / s);
            cfg.H = (int)Math.Round(Height / s);
        }

        public void ResetToDefaultSize()
        {
            Size = new Size((int)Math.Round(400 * Program.DpiScale),
                            (int)Math.Round(160 * Program.DpiScale));
        }

        // Current size expressed as a percentage of the 400x160 default, so the
        // settings dialog can show one plain "window size" slider.
        public int WindowScalePercent
        {
            get { return (int)Math.Round(Width / (400.0 * Program.DpiScale) * 100); }
        }

        // Scale the window from the slider while keeping whatever aspect ratio the
        // user has dragged it into.
        public void SetWindowScale(int pct)
        {
            float dpi = Program.DpiScale;
            double aspect = Height / (double)Math.Max(1, Width);
            int nw = (int)Math.Round(400 * dpi * pct / 100.0);
            int nh = (int)Math.Round(nw * aspect);
            nw = Math.Max((int)(150 * dpi), Math.Min((int)(2400 * dpi), nw));
            nh = Math.Max((int)(64 * dpi), Math.Min((int)(1600 * dpi), nh));
            Size = new Size(nw, nh);
            SaveGeometry();
        }

        // Called by the settings dialog when the configured length changes.
        public void UpdateCountdownLength()        {
            if (!cdRunning && !cdDone)
            {
                cdTotal = Math.Max(1, cfg.CountdownSecs);
                cdLeft = cdTotal;
            }
            fitKey = "";
            Invalidate();
        }

        public void StartCountdown()
        {
            SetMode(Mode.Countdown);
            ResetCountdown();
            cdEnd = DateTime.Now.AddSeconds(cdLeft);
            cdRunning = true;
            Invalidate();
        }

        public void ToggleTopmost()
        {
            cfg.Topmost = !cfg.Topmost;
            TopMost = cfg.Topmost;
            cfg.Save();
        }

        public void ToggleClickThrough()
        {
            cfg.ClickThrough = !cfg.ClickThrough;
            barVisible = false;
            applyClickThrough();
            cfg.Save();
            Invalidate();
        }

        public void ToggleLock()
        {
            cfg.Locked = !cfg.Locked;
            cfg.Save();
            Invalidate();
        }

        public void ResetLookToDefault()
        {
            cfg.Bg = "#101014";
            cfg.Fg = "#ffffff";
            cfg.Alpha = 0.95;
            cfg.Font = "Bahnschrift";
            cfg.Bold = true;
            cfg.AutoFit = true;
            cfg.FontPx = 100;
            cfg.Spacing = 2;
            cfg.Shadow = true;
            ApplyLook();
        }

        public void ApplyLook()
        {
            BackColor = Theme.FromHex(cfg.Bg);
            try { Opacity = Math.Max(0.35, Math.Min(1.0, cfg.Alpha)); }
            catch (Exception) { }
            TopMost = cfg.Topmost;
            fitKey = "";
            numFontKey = "";
            applyClickThrough();
            Invalidate();
            cfg.Save();
        }

        // Re-label the window after the UI language changed in the settings dialog.
        public void ApplyLanguage()
        {
            L.En = cfg.Lang == "en";
            Text = L.S("悬浮时钟 v") + Program.Ver + " · " + Program.Author;
            capFontKey = "";
            Invalidate();
            cfg.Save();
        }

        public void OpenSettings()
        {
            if (settings != null && !settings.IsDisposed)
            {
                settings.Activate();
                return;
            }
            settings = new SettingsForm(this);
            // Shown without an owner window: Windows re-places owned top-most
            // windows on its own, which broke the centring on high-DPI screens.
            settings.Show();
        }

        // ---------------------------------------------------------------
        //  Presentation watcher: PowerPoint / WPS slideshows AND full-screen PDF
        //  playback (Acrobat, Foxit, WPS, Edge/Chrome "Present" mode ...)
        // ---------------------------------------------------------------
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder sb, int max);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        private bool insideSlideshow;

        // Class names of the slideshow / full-screen presentation windows we know.
        private static readonly string[] SlideClasses = {
            "screenClass",      // PowerPoint
            "PPTFrameClass",
            "wppScreen",        // WPS 演示
            "KWppScreen",
            "KWppFrame",
            "AcrobatSDIWindow", // Adobe Acrobat full screen
            "classFoxitReader", // Foxit Reader / Editor full screen
            "ApolloRuntimeContentWindow" // 福昕新版
        };

        private void OnWatchSlideshow(object sender, EventArgs e)
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                if (h == IntPtr.Zero) { insideSlideshow = false; return; }

                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                GetClassName(h, sb, 256);
                string cls = sb.ToString();

                System.Text.StringBuilder tb = new System.Text.StringBuilder(512);
                GetWindowText(h, tb, 512);
                string title = tb.ToString().ToLowerInvariant();

                bool slide = false;
                foreach (string c in SlideClasses)
                    if (cls == c) { slide = true; break; }

                // Any window showing a PDF at full screen counts as a presentation:
                // Edge / Chrome / Reader / WPS PDF all keep ".pdf" in their title.
                bool pdfFullscreen = title.Contains(".pdf");
                bool full = false;
                if (slide || pdfFullscreen)
                {
                    RECT r;
                    if (GetWindowRect(h, out r))
                    {
                        Rectangle wa = Screen.FromHandle(h).Bounds;
                        int w = r.Right - r.Left, hh = r.Bottom - r.Top;
                        // slideshows only need to be screen-wide; PDF "full screen"
                        // must really cover the screen in both directions
                        full = slide ? w >= wa.Width * 0.9 : (w >= wa.Width * 0.9 && hh >= wa.Height * 0.9);
                    }
                }

                if (full)
                {
                    if (!insideSlideshow)
                    {
                        insideSlideshow = true;
                        if (cfg.PptAuto) StartCountdown();
                    }
                }
                else
                {
                    insideSlideshow = false;
                }
            }
            catch (Exception) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The settings window is owner-less now, so close it explicitly.
            try
            {
                if (settings != null && !settings.IsDisposed) settings.Close();
            }
            catch (Exception) { }
            SaveGeometry();
            cfg.Mode = mode == Mode.Clock ? "clock" : (mode == Mode.Countdown ? "countdown" : "stopwatch");
            cfg.Save();
            base.OnFormClosing(e);
        }
    }

    // tiny helper so the drawing code can ask "is the background dark?"
    internal static class BgExt
    {
        public static bool ShowDarkBg(this Config c)
        {
            Color col = Theme.FromHex(c.Bg);
            return (col.R + col.G + col.B) / 3 < 140;
        }
    }
}
