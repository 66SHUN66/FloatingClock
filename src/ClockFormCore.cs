// ===========================================================================
//  ClockFormCore.cs  --  the floating window (part 2 of 3): fields,
//  constructor, timer, layout maths and the painting of the digits.
//  ClockForm is a partial class; the toolbar / menus / behaviour half
//  lives in ClockFormUi.cs.  Compiled together with FloatingClock.cs.
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
    //  The floating window itself
    // =======================================================================
    internal partial class ClockForm : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;

        public enum Mode { Clock, Countdown, Stopwatch }

        private Config cfg = Config.Load();
        private Mode mode = Mode.Clock;

        // countdown -------------------------------------------------------
        private double cdTotal = 300, cdLeft = 300;
        private DateTime cdEnd;
        private DateTime cdDoneAt;              // when 00:00 was reached (overtime base)
        private bool cdRunning, cdDone;

        // stopwatch -------------------------------------------------------
        private double swBase, swElapsed;
        private DateTime swStart;
        private bool swRunning;

        // interaction -----------------------------------------------------
        private bool dragging, resizing;
        private Point dragOffset;
        private Point resizeMouse0;
        private Size resizeSize0;
        private bool barVisible;
        private DateTime lastMove = DateTime.MinValue;
        private int hoverBtn = -1;
        private readonly List<Rectangle> btnRects = new List<Rectangle>();
        private readonly List<string> btnTexts = new List<string>();
        private readonly List<Action> btnActions = new List<Action>();

        // drawing cache ---------------------------------------------------
        private string fitKey = "";
        private float fitPx = 60;
        private Font numFont;
        private string numFontKey = "";
        private Font capFont;
        private string capFontKey = "";
        private SettingsForm settings;

        private readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer();

        public ClockForm()
        {
            L.En = cfg.Lang == "en";
            Text = L.S("悬浮时钟 v") + Program.Ver + " · " + Program.Author;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            TopMost = cfg.Topmost;
            BackColor = Theme.FromHex(cfg.Bg);
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            AutoScaleMode = AutoScaleMode.None;
            mode = ParseMode(cfg.Mode);
            cdTotal = cdLeft = cfg.CountdownSecs;

            // Sizes in the config are "logical" pixels (96 dpi); multiply by the
            // display scale so the widget keeps the same apparent size everywhere.
            float dpi = Program.DpiScale;
            int w = Math.Max((int)(150 * dpi), (int)Math.Round(cfg.W * dpi));
            int h = Math.Max((int)(64 * dpi), (int)Math.Round(cfg.H * dpi));
            Size = new Size(w, h);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (cfg.X == int.MinValue || cfg.Y == int.MinValue)
            {
                Location = new Point(wa.Right - w - (int)(60 * dpi), wa.Top + (int)(60 * dpi));
            }
            else
            {
                Location = new Point(
                    Math.Max(wa.Left, Math.Min((int)Math.Round(cfg.X * dpi), wa.Right - w - 20)),
                    Math.Max(wa.Top, Math.Min((int)Math.Round(cfg.Y * dpi), wa.Bottom - h - 20)));
            }

            try { Opacity = Math.Max(0.35, Math.Min(1.0, cfg.Alpha)); }
            catch (Exception) { }

            tick.Interval = 50;
            tick.Tick += OnTick;
            tick.Start();

            watch.Interval = 600;
            watch.Tick += OnWatchSlideshow;
            watch.Start();

            if (Program.OpenSettingsOnStart)
                Shown += delegate { OpenSettings(); };
        }

        private static Mode ParseMode(string s)
        {
            if (s == "countdown") return Mode.Countdown;
            if (s == "stopwatch") return Mode.Stopwatch;
            return Mode.Clock;
        }

        public Config Cfg { get { return cfg; } }
        public Mode CurrentMode { get { return mode; } }

        // ---------------------------------------------------------------
        //  Window styling / Win32
        // ---------------------------------------------------------------
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetRoundedRegion();
            applyClickThrough();
            if (!cfg.ClickThrough) RegisterHotkeys();
            else RegisterHotkeys();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            SetRoundedRegion();
            fitKey = "";
        }

        private void SetRoundedRegion()
        {
            try
            {
                using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width, Height), 10))
                {
                    Region old = Region;
                    Region = new Region(p);
                    if (old != null) old.Dispose();
                }
            }
            catch (Exception) { }
        }

        private void RegisterHotkeys()
        {
            try
            {
                RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x54); // Ctrl+Alt+T
                RegisterHotKey(Handle, 2, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x20); // Ctrl+Alt+Space
                RegisterHotKey(Handle, 3, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x52); // Ctrl+Alt+R
            }
            catch (Exception) { }
        }

        private void applyClickThrough()
        {
            try
            {
                int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                if (cfg.ClickThrough) ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
                else ex &= ~WS_EX_TRANSPARENT;
                SetWindowLong(Handle, GWL_EXSTYLE, ex);
                Opacity = Math.Max(0.35, Math.Min(1.0, cfg.Alpha));
            }
            catch (Exception) { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) ToggleClickThrough();
                else if (id == 2) ToggleRun();
                else if (id == 3) ResetCurrent();
                return;
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);

        // ---------------------------------------------------------------
        //  Timer heartbeat
        // ---------------------------------------------------------------
        private void OnTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (cdRunning)
            {
                cdLeft = (cdEnd - now).TotalSeconds;
                if (cdLeft <= 0)
                {
                    cdLeft = 0;
                    cdRunning = false;
                    cdDone = true;
                    cdDoneAt = now;          // start counting the overtime
                    if (cfg.Beep) StartAlarm();
                }
            }
            if (swRunning) swElapsed = swBase + (now - swStart).TotalSeconds;
            if (barVisible && (now - lastMove).TotalSeconds > 2.0) barVisible = false;
            Invalidate();
        }

        private void StartAlarm()
        {
            Thread t = new Thread(delegate()
            {
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        SystemSounds.Exclamation.Play();
                        Thread.Sleep(700);
                    }
                }
                catch (Exception) { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------------------------------------------------------------
        //  State -> text
        // ---------------------------------------------------------------
        private void Compose(out string big, out string small, out double progress,
                             out bool alarm, out bool blink)
        {
            big = ""; small = ""; progress = -1; alarm = false; blink = false;
            string status = "";          // caption the app would show on its own
            string overText = "";        // overtime read-out, only when the countdown ran out

            if (mode == Mode.Clock)
            {
                DateTime t = DateTime.Now;
                int hh = cfg.Hour24 ? t.Hour : (t.Hour % 12 == 0 ? 12 : t.Hour % 12);
                big = string.Format("{0:00}:{1:00}", hh, t.Minute);
                if (cfg.ShowSeconds) big += string.Format(":{0:00}", t.Second);
                if (cfg.ShowDate)
                {
                    string[] wk = L.En
                        ? new string[] { "Sunday", "Monday", "Tuesday", "Wednesday",
                                         "Thursday", "Friday", "Saturday" }
                        : new string[] { "日", "一", "二", "三", "四", "五", "六" };
                    string ampm = cfg.Hour24 ? "" : (L.En
                        ? (t.Hour < 12 ? " AM" : " PM")
                        : (t.Hour < 12 ? " 上午" : " 下午"));
                    status = L.En
                        ? string.Format("{0}-{1:00}-{2:00}  {3}{4}",
                            t.Year, t.Month, t.Day, wk[(int)t.DayOfWeek], ampm)
                        : string.Format("{0}-{1:00}-{2:00}  星期{3}{4}",
                            t.Year, t.Month, t.Day, wk[(int)t.DayOfWeek], ampm);
                }
                small = status;
                if (cfg.FlashColon && !cfg.ShowSeconds)
                    blink = ((int)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 500) % 2) == 0;
            }
            else if (mode == Mode.Countdown)
            {
                int left = (int)Math.Ceiling(cdLeft - 1e-6);
                if (left < 0) left = 0;
                if (cdTotal >= 3600)
                    big = string.Format("{0}:{1:00}:{2:00}", left / 3600, (left % 3600) / 60, left % 60);
                else
                    big = string.Format("{0:00}:{1:00}", left / 60, left % 60);

                if (cdDone)
                {
                    // Overtime: how long we have run past 00:00, shown under the digits.
                    if (cdDoneAt == DateTime.MinValue) cdDoneAt = DateTime.Now;
                    int over = (int)Math.Floor(Math.Max(0, (DateTime.Now - cdDoneAt).TotalSeconds));
                    overText = over >= 3600
                        ? string.Format("{0}:{1:00}:{2:00}", over / 3600, (over % 3600) / 60, over % 60)
                        : string.Format("{0:00}:{1:00}", over / 60, over % 60);
                    status = L.S("已超过 ") + overText + L.S(" · 空格键重新开始");
                    alarm = true;
                    blink = ((int)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 500) % 2) == 0;
                }
                else if (cdRunning) status = L.S("倒计时进行中 · 空格暂停");
                else status = L.S("倒计时已就绪 · 空格开始");
                small = status;

                progress = cdTotal > 0 ? cdLeft / cdTotal : 0;
                if (progress < 0) progress = 0;
                if (progress > 1) progress = 1;
            }
            else
            {
                // stopwatch
                int mins = (int)(swElapsed / 60);
                int secs = (int)(swElapsed % 60);
                int cs = (int)((swElapsed * 100) % 100);
                big = string.Format("{0:00}:{1:00}.{2:00}", mins, secs, cs);
                status = swRunning ? L.S("秒表计时中") : L.S("秒表已暂停");
                small = status;
            }

            // A user-supplied footer replaces the automatic caption.
            if (cfg.Caption != null && cfg.Caption.Trim().Length > 0)
            {
                small = ExpandCaption(cfg.Caption, status, overText);
                // The overtime read-out must survive a custom caption that omits {over}.
                if (overText.Length > 0 && cfg.Caption.IndexOf("{over}", StringComparison.Ordinal) < 0)
                    small = small + L.S(" · 已超过 ") + overText;
            }
        }

        // Supported tokens: {date} {time} {mode} {status} {over}
        private string ExpandCaption(string template, string status, string overText)
        {
            DateTime now = DateTime.Now;
            string modeName = mode == Mode.Clock ? L.S("时钟") : (mode == Mode.Countdown ? L.S("倒计时") : L.S("秒表"));
            string s = template
                .Replace("{date}", now.ToString("yyyy-MM-dd"))
                .Replace("{time}", now.ToString("HH:mm:ss"))
                .Replace("{mode}", modeName)
                .Replace("{status}", status)
                .Replace("{over}", overText);
            return s.Trim();
        }

        // ---------------------------------------------------------------
        //  Painting
        // ---------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Theme.FromHex(cfg.Bg));

            string big, small; double progress; bool alarm, blink;
            Compose(out big, out small, out progress, out alarm, out blink);

            DrawDigits(g, big, small, progress, alarm, blink);

            using (GraphicsPath p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10))
            using (Pen pen = new Pen(Theme.Blend(Theme.FromHex(cfg.Bg), Theme.FromHex(cfg.Fg), 0.10)))
                g.DrawPath(pen, p);

            DrawBar(g);
            DrawGrip(g);
        }

        private float FitSize(Graphics g, string text)
        {
            string key = Width + "x" + Height + "|" + text.Length + "|" + cfg.Font + "|" +
                         cfg.Bold + "|" + cfg.AutoFit + "|" + cfg.FontPx + "|" + cfg.Spacing;
            if (key == fitKey) return fitPx;

            if (!cfg.AutoFit)
            {
                fitPx = Math.Max(8, cfg.FontPx);
                fitKey = key;
                return fitPx;
            }

            bool hasCaption = (mode == Mode.Clock && cfg.ShowDate) || mode != Mode.Clock;
            float maxW = Width * 0.92f;
            float maxH = Height * (hasCaption ? 0.52f : 0.78f);
            int lo = 8, hi = Math.Max(16, (int)(Height * 1.6f)), best = 8;
            StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            for (int i = 0; i < 14; i++)
            {
                int mid = (lo + hi) / 2;
                using (Font probe = new Font(cfg.Font, mid, cfg.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    float total = 0;
                    for (int k = 0; k < text.Length; k++)
                        total += g.MeasureString(text.Substring(k, 1), probe, new PointF(0, 0), sf).Width;
                    total += cfg.Spacing * Math.Max(0, text.Length - 1);
                    float lineH = probe.GetHeight(g);
                    if (total <= maxW && lineH <= maxH) { best = mid; lo = mid; }
                    else hi = mid;
                }
            }
            sf.Dispose();
            fitKey = key;
            fitPx = best;
            return fitPx;
        }

        private Font NumFont(float px)
        {
            string key = cfg.Font + "|" + px + "|" + cfg.Bold;
            if (key != numFontKey)
            {
                if (numFont != null) numFont.Dispose();
                numFont = new Font(cfg.Font, px, cfg.Bold ? FontStyle.Bold : FontStyle.Regular,
                                   GraphicsUnit.Pixel);
                numFontKey = key;
            }
            return numFont;
        }

        private Font CaptionFont(float px)
        {
            string key = px.ToString("0");
            if (key != capFontKey)
            {
                if (capFont != null) capFont.Dispose();
                capFont = new Font("Microsoft YaHei UI", px, FontStyle.Regular, GraphicsUnit.Pixel);
                capFontKey = key;
            }
            return capFont;
        }

        private Color TextColour(bool alarm, bool blink)
        {
            Color baseC = Theme.FromHex(cfg.Fg);
            if (alarm) return blink ? Color.FromArgb(255, 59, 48) : baseC;
            if (mode == Mode.Countdown && cfg.WarnRed && cdTotal > 0)
            {
                // last 20 %, but never more than half of a very short timer
                double warnAt = Math.Min(Math.Max(60.0, cdTotal * 0.2), cdTotal * 0.5);
                if (cdLeft <= warnAt) return Color.FromArgb(255, 77, 61);
            }
            return baseC;
        }

        private void DrawDigits(Graphics g, string big, string small, double progress,
                                bool alarm, bool blink)
        {
            float px = FitSize(g, big);
            Font f = NumFont(px);
            StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags = sf.FormatFlags | StringFormatFlags.MeasureTrailingSpaces;

            float spacing = cfg.Spacing;
            float[] adv = new float[big.Length];
            float total = 0;
            for (int i = 0; i < big.Length; i++)
            {
                adv[i] = g.MeasureString(big.Substring(i, 1), f, new PointF(0, 0), sf).Width;
                total += adv[i];
            }
            if (big.Length > 1) total += spacing * (big.Length - 1);

            float yc = small.Length > 0 ? Height * 0.49f : Height * 0.52f;
            if (progress >= 0) yc -= Height * 0.02f;
            float textH = f.GetHeight(g);
            float y = yc - textH / 2f;
            float x = (Width - total) / 2f;

            Color colour = TextColour(alarm, blink);
            Color bg = Theme.FromHex(cfg.Bg);
            Color shadow = Theme.Blend(bg, cfg.ShowDarkBg() ? Color.Black : Color.White,
                                       cfg.ShowDarkBg() ? 0.45 : 0.30);
            float off = Math.Max(1f, px / 26f);

            // pass 1: shadows
            if (cfg.Shadow)
            {
                using (SolidBrush b = new SolidBrush(shadow))
                {
                    float cx = x;
                    for (int i = 0; i < big.Length; i++)
                    {
                        string ch = big.Substring(i, 1);
                        if (!(ch == ":" && blink))
                            g.DrawString(ch, f, b, cx + off, y + off * 1.6f, sf);
                        cx += adv[i] + spacing;
                    }
                }
            }
            // pass 2: glyphs
            using (SolidBrush b = new SolidBrush(colour))
            {
                float cx = x;
                for (int i = 0; i < big.Length; i++)
                {
                    string ch = big.Substring(i, 1);
                    if (!(ch == ":" && blink))
                        g.DrawString(ch, f, b, cx, y, sf);
                    cx += adv[i] + spacing;
                }
            }
            sf.Dispose();

            // caption
            if (small.Length > 0)
            {
                float capPx = Math.Max(10f, Math.Min(26f, Height * 0.105f));
                Font cf = CaptionFont(capPx);   // cached font: never dispose it here
                SizeF sz = g.MeasureString(small, cf);
                float maxCapW = Width * 0.92f;
                if (sz.Width > maxCapW)         // narrow window: shrink the caption to fit
                {
                    capPx = Math.Max(7f, capPx * maxCapW / sz.Width);
                    cf = CaptionFont(capPx);
                    sz = g.MeasureString(small, cf);
                }
                // the overtime caption is painted in alarm red so it reads at a glance
                Color capCol = (mode == Mode.Countdown && cdDone)
                    ? Color.FromArgb(255, 104, 92)
                    : Theme.Blend(Theme.FromHex(cfg.Fg), bg, 0.35);
                using (SolidBrush b = new SolidBrush(capCol))
                    g.DrawString(small, cf, b, (Width - sz.Width) / 2f, Height * 0.855f - sz.Height / 2f);
            }

            // progress bar
            if (progress >= 0)
            {
                float x0 = Width * 0.06f, x1 = Width * 0.94f;
                float y0 = Height - Math.Max(6f, Height * 0.045f);
                float bh = 3f * Program.DpiScale;
                using (SolidBrush track = new SolidBrush(Theme.Blend(bg, Theme.FromHex(cfg.Fg), 0.18)))
                    g.FillRectangle(track, x0, y0, x1 - x0, bh);
                Color barC = (progress <= 0.2 && cfg.WarnRed) ? Color.FromArgb(255, 59, 48) : Theme.Accent;
                using (SolidBrush fill = new SolidBrush(barC))
                    g.FillRectangle(fill, x0, y0, (x1 - x0) * (float)progress, bh);
            }
        }

        private void DrawGrip(Graphics g)
        {
            if (cfg.Locked || cfg.ClickThrough) return;
            Color bg = Theme.FromHex(cfg.Bg);
            Color grip = cfg.ShowDarkBg() ? Theme.Blend(bg, Color.Black, 0.55)
                                          : Theme.Blend(bg, Color.White, 0.35);
            using (Pen p = new Pen(grip))
            {
                for (int k = 0; k < 3; k++)
                {
                    float d = (6 + k * 5) * Program.DpiScale;
                    float m = 4 * Program.DpiScale;
                    g.DrawLine(p, Width - d - m, Height - m, Width - m, Height - d - m);
                }
            }
        }
    }
}
