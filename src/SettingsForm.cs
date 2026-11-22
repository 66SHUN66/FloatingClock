// ===========================================================================
//  Settings window + hand-drawn dark UI controls for FloatingClock
//  (dialog itself; the dark UI controls live in SettingsControls.cs)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace FloatingClockApp
{
    // =======================================================================
    //  Settings dialog
    // =======================================================================
    internal class SettingsForm : Form
    {
        private readonly ClockForm host;
        private readonly Config cfg;

        // background/text preset pairs
        private static readonly string[][] Palettes = new string[][]
        {
            new string[] { "#101014", "#ffffff" },   // 深空黑
            new string[] { "#f2f2f5", "#151518" },   // 磨砂白
            new string[] { "#ff6700", "#ffffff" },   // 小米橙
            new string[] { "#06131f", "#38e0ff" },   // 赛博蓝
            new string[] { "#04120a", "#2bff88" },   // 荧光绿
            new string[] { "#2a0a10", "#ff7a86" },   // 玫瑰红
            new string[] { "#e7dcc8", "#2b2118" }    // 羊皮纸
        };

        private int cursorY = 14;
        private ColorRow rowBg;
        private ColorRow rowFg;
        private FontPicker picker;
        private Slider sldSize;
        private Slider sldDur;

        public SettingsForm(ClockForm h)
        {
            host = h;
            cfg = h.Cfg;

            Text = L.S("悬浮时钟 v") + Program.Ver + L.S(" · 设置");
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            ClientSize = new Size(600, 720);
            AutoScroll = true;
            DoubleBuffered = true;
            TopMost = true;
            // Layout is authored in 96-dpi logical pixels and scaled as a whole
            // below; GDI fonts already follow the system DPI on their own.
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };

            BuildUi();
            if (Program.DpiScale > 1.01f)
                Scale(new SizeF(Program.DpiScale, Program.DpiScale));

            // Own placement: CenterScreen gets confused by a top-most, borderless
            // owner on high-DPI displays, so we centre it ourselves on the monitor
            // the clock currently lives on.
            Rectangle wa = Screen.FromControl(host).WorkingArea;
            Point wanted = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2),
                                     wa.Top + Math.Max(0, (wa.Height - Height) / 2));
            Location = wanted;

            // The OS re-places owned top-most windows while showing them, so
            // insist on the centred position once more a moment later.
            Shown += delegate
            {
                AutoScrollPosition = new Point(0, 0);
                System.Windows.Forms.Timer settle = new System.Windows.Forms.Timer();
                settle.Interval = 250;
                settle.Tick += delegate
                {
                    settle.Stop();
                    settle.Dispose();
                    Location = wanted;
                };
                settle.Start();
            };
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Focusing a child (e.g. clicking a preset button) normally makes the
        // scrolled panel jump to bring that control into view - keep the scroll
        // position where the user left it instead.
        protected override Point ScrollToControl(Control activeControl)
        {
            return AutoScrollPosition;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                DwmSetWindowAttribute(Handle, 20, ref on, 4);   // dark immersive title bar
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------
        //  Small factories
        // ------------------------------------------------------------------
        private Card AddCard(string title, int height)
        {
            Card c = new Card(title);
            c.SetBounds(18, cursorY, 560, height);
            Controls.Add(c);
            cursorY += height + 10;
            return c;
        }

        private static Label MakeLabel(string text, bool muted, int x, int y, int w, int h)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.SetBounds(x, y, w, h);
            l.BackColor = Theme.Card;
            l.ForeColor = muted ? Theme.Muted : Theme.Text;
            l.Font = muted ? Theme.SmallFont : Theme.UiFont;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private static Button MakeButton(string text, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = accent ? 0 : 1;
            b.FlatAppearance.BorderColor = Theme.Line;
            b.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(255, 133, 64) : Theme.Card2;
            b.BackColor = accent ? Theme.Accent : Theme.Card2;
            b.ForeColor = accent ? Color.FromArgb(26, 26, 26) : Theme.Text;
            b.Font = accent ? new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) : Theme.UiFont;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private Switch AddSwitch(Control parent, string text, bool value,
                                 EventHandler handler, int x, int y, int w, bool compact = false)
        {
            Switch s = new Switch(text, value);
            s.Compact = compact;
            s.SetBounds(x, y, w, 26);
            if (handler != null) s.ValueChanged += handler;
            parent.Controls.Add(s);
            return s;
        }

        private Slider AddSlider(Control parent, string label, int min, int max, int value,
                                 int x, int y, int w)
        {
            Slider s = new Slider(label, min, max, value);
            s.SetBounds(x, y, w, 44);
            parent.Controls.Add(s);
            return s;
        }

        // ------------------------------------------------------------------
        //  Layout
        // ------------------------------------------------------------------
        private void BuildUi()
        {
            // --- header: name / version / author ----------------------------
            // "Floating Clock" is much wider than the Chinese name, so the header
            // slots shift right in English.
            int nameW = L.En ? 132 : 88;
            Label appName = MakeLabel(L.S("悬浮时钟"), false, 18, 10, nameW, 28);
            appName.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            appName.BackColor = Theme.Panel;
            Controls.Add(appName);

            Label verTag = MakeLabel("v" + Program.Ver, true, 18 + nameW + 10, 12, 52, 24);
            verTag.ForeColor = Theme.Accent;
            verTag.BackColor = Theme.Panel;
            Controls.Add(verTag);

            Label authorTag = MakeLabel("by " + Program.Author, true, 18 + nameW + 58, 12, 120, 24);
            authorTag.BackColor = Theme.Panel;
            Controls.Add(authorTag);

            Label slogan = MakeLabel(L.S("PPT / PDF 汇报专用悬浮计时"), true, 290, 12, 288, 24);
            slogan.BackColor = Theme.Panel;
            slogan.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(slogan);

            cursorY = 48;

            // --- 0. language ------------------------------------------------
            Card c0 = AddCard(L.S("语言"), 78);
            Segment langSeg = new Segment(new string[] { "中文", "English" }, L.En ? 1 : 0);
            langSeg.SetBounds(18, 40, 200, 30);
            langSeg.ValueChanged += delegate
            {
                cfg.Lang = langSeg.Selected == 1 ? "en" : "zh";
                cfg.Save();
                host.ApplyLanguage();
                // The whole dialog is laid out with text, so rebuild it:
                BeginInvoke((MethodInvoker)delegate { Close(); host.OpenSettings(); });
            };
            c0.Controls.Add(langSeg);
            Label langHint = MakeLabel(L.S("切换后立即生效"), true, 236, 42, 290, 24);
            c0.Controls.Add(langHint);

            // --- 1. mode ---------------------------------------------------
            Card c1 = AddCard(L.S("显示模式"), 138);
            Segment seg = new Segment(new string[] { L.S("时钟"), L.S("倒计时"), L.S("秒表") }, (int)host.CurrentMode);
            seg.SetBounds(18, 40, 300, 32);
            seg.ValueChanged += delegate
            {
                host.SetMode((ClockForm.Mode)seg.Selected);
            };
            c1.Controls.Add(seg);

            AddSwitch(c1, L.S("显示秒"), cfg.ShowSeconds, delegate(object s, EventArgs e)
            {
                cfg.ShowSeconds = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 100, 164, true);
            AddSwitch(c1, L.S("24 小时制"), cfg.Hour24, delegate(object s, EventArgs e)
            {
                cfg.Hour24 = ((Switch)s).Checked; host.ApplyLook();
            }, 190, 100, 164, true);
            AddSwitch(c1, L.S("显示日期"), cfg.ShowDate, delegate(object s, EventArgs e)
            {
                cfg.ShowDate = ((Switch)s).Checked; host.ApplyLook();
            }, 362, 100, 164, true);

            // shortcut cheat-sheet, right next to the mode pill (as asked).
            // English needs one more line than Chinese, hence the shared 4-slot stack.
            Label hk1 = MakeLabel(L.S("空格 开始/暂停　R 重置"), true, 328, 28, 218, 17);
            c1.Controls.Add(hk1);
            Label hk2 = MakeLabel(L.S("Ctrl+Alt+Space 开始/暂停"), true, 328, 45, 218, 17);
            c1.Controls.Add(hk2);
            Label hk3 = MakeLabel(L.S("Ctrl+Alt+T 穿透 · Ctrl+Alt+R 重置"), true, 328, 62, 218, 17);
            c1.Controls.Add(hk3);
            Label hk4 = MakeLabel("Ctrl+Alt+R: reset", true, 328, 79, 218, 17);
            hk4.Visible = L.En;
            c1.Controls.Add(hk4);

            // --- 2. colours ------------------------------------------------
            Card c2 = AddCard(L.S("背景与颜色"), 202);

            SwatchStrip strip = new SwatchStrip(Palettes);
            strip.SetBounds(18, 40, 508, 36);
            c2.Controls.Add(strip);

            rowBg = new ColorRow(L.S("背景颜色"), Theme.FromHex(cfg.Bg));
            rowBg.SetBounds(18, 84, 508, 26);
            c2.Controls.Add(rowBg);

            rowFg = new ColorRow(L.S("文字颜色"), Theme.FromHex(cfg.Fg));
            rowFg.SetBounds(18, 114, 508, 26);
            c2.Controls.Add(rowFg);

            Slider alpha = AddSlider(c2, L.S("不透明度"), 35, 100, (int)(cfg.Alpha * 100), 18, 146, 508);
            alpha.Format = delegate(int v) { return v + "%"; };

            strip.Picked += delegate(int index)
            {
                cfg.Bg = Palettes[index][0];
                cfg.Fg = Palettes[index][1];
                rowBg.Value = Theme.FromHex(cfg.Bg);
                rowFg.Value = Theme.FromHex(cfg.Fg);
                host.ApplyLook();
            };
            rowBg.ValueChanged += delegate
            {
                cfg.Bg = Theme.ToHex(rowBg.Value); host.ApplyLook();
            };
            rowFg.ValueChanged += delegate
            {
                cfg.Fg = Theme.ToHex(rowFg.Value); host.ApplyLook();
            };
            alpha.BeforeChange = delegate(int v)
            {
                cfg.Alpha = v / 100.0;
                host.ApplyLook();          // live preview while dragging
            };

            // --- 3. font ----------------------------------------------------
            Card c3 = AddCard(L.S("数字字体"), 272);
            Label fontLabel = MakeLabel(L.S("字体"), true, 18, 40, 66, 24);
            c3.Controls.Add(fontLabel);

            picker = new FontPicker(FontFamilies(), cfg.Font);
            picker.SetBounds(88, 38, 400, 28);
            picker.ValueChanged += delegate
            {
                cfg.Font = picker.SelectedFamily;
                host.ApplyLook();
            };
            c3.Controls.Add(picker);

            AddSwitch(c3, L.S("字号自动适配窗口"), cfg.AutoFit, delegate(object s, EventArgs e)
            {
                cfg.AutoFit = ((Switch)s).Checked;
                sldSize.Enabled = cfg.AutoFit == false;
                host.ApplyLook();
            }, 18, 74, 508);

            sldSize = AddSlider(c3, L.S("字号大小"), 16, 260, cfg.FontPx, 18, 104, 508);
            sldSize.Suffix = "px";
            sldSize.Enabled = !cfg.AutoFit;
            sldSize.BeforeChange = delegate(int v)
            {
                cfg.FontPx = v;
                cfg.AutoFit = false;
                host.ApplyLook();
            };

            Slider spacing = AddSlider(c3, L.S("字间距"), -12, 40, cfg.Spacing, 18, 152, 508);
            spacing.Suffix = "px";
            spacing.BeforeChange = delegate(int v)
            {
                cfg.Spacing = v; host.ApplyLook();
            };

            AddSwitch(c3, L.S("加粗"), cfg.Bold, delegate(object s, EventArgs e)
            {
                cfg.Bold = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 202, 508);
            AddSwitch(c3, L.S("立体阴影"), cfg.Shadow, delegate(object s, EventArgs e)
            {
                cfg.Shadow = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 230, 508);

            // --- 4. footer text --------------------------------------------
            Card c35 = AddCard(L.S("底部文字"), 150);
            Label capLabel = MakeLabel(L.S("自定义"), true, 18, 40, 60, 24);
            c35.Controls.Add(capLabel);

            Panel capBorder = new Panel();
            capBorder.BackColor = Theme.Line;
            capBorder.SetBounds(78, 34, 458, 40);
            c35.Controls.Add(capBorder);

            Panel capFill = new Panel();
            capFill.BackColor = Theme.Card2;
            capFill.Dock = DockStyle.Fill;
            capFill.Padding = new Padding(10, 9, 10, 4);
            capBorder.Controls.Add(capFill);

            TextBox capBox = new TextBox();
            capBox.Multiline = true;
            capBox.AcceptsReturn = false;
            capBox.BorderStyle = BorderStyle.None;
            capBox.BackColor = Theme.Card2;
            capBox.ForeColor = Theme.Text;
            capBox.Font = new Font("Microsoft YaHei UI", 10.5F);
            capBox.Dock = DockStyle.Fill;
            capBox.Text = cfg.Caption;
            capBox.TextChanged += delegate
            {
                cfg.Caption = capBox.Text;
                host.ApplyLook();
            };
            capFill.Controls.Add(capBox);

            Label capHint1 = MakeLabel(L.S("留空 = 自动显示（日期 / 倒计时状态 / 已超时读数）"), true, 18, 82, 508, 18);
            c35.Controls.Add(capHint1);
            Label capHint2 = MakeLabel(L.S("可用占位符：{date} {time} {mode} {status} {over}"), true, 18, 104, 508, 18);
            c35.Controls.Add(capHint2);
            Label capHint3 = MakeLabel(L.S("例：{status}　—— 第 3 页"), true, 18, 124, 508, 18);
            c35.Controls.Add(capHint3);

            // --- 5. window behaviour ---------------------------------------
            Card c4 = AddCard(L.S("窗口行为"), 254);
            AddSwitch(c4, L.S("始终置顶"), cfg.Topmost, delegate(object s, EventArgs e)
            {
                cfg.Topmost = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 40, 508);
            AddSwitch(c4, L.S("锁定位置和大小"), cfg.Locked, delegate(object s, EventArgs e)
            {
                cfg.Locked = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 68, 508);
            AddSwitch(c4, L.S("点击穿透（不挡 PPT 操作）"), cfg.ClickThrough, delegate(object s, EventArgs e)
            {
                cfg.ClickThrough = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 96, 508);
            AddSwitch(c4, L.S("PPT / PDF 放映自动开始倒计时"), cfg.PptAuto, delegate(object s, EventArgs e)
            {
                cfg.PptAuto = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 124, 508);

            // dedicated resize slider: one place to set the widget size exactly
            Slider winSize = AddSlider(c4, L.S("窗口大小"), 40, 400, host.WindowScalePercent, 18, 158, 508);
            winSize.Suffix = "%";
            winSize.BeforeChange = delegate(int v)
            {
                host.SetWindowScale(v);
            };

            Button bCenter = MakeButton(L.S("居中显示"), false);
            bCenter.SetBounds(18, 214, 120, 30);
            bCenter.Click += delegate
            {
                Rectangle wa = Screen.FromControl(host).WorkingArea;
                host.Location = new Point(wa.Left + (wa.Width - host.Width) / 2,
                                          wa.Top + (wa.Height - host.Height) / 2);
                host.ApplyLook();
                winSize.Value = host.WindowScalePercent;
                winSize.Invalidate();
            };
            c4.Controls.Add(bCenter);

            Button bReset = MakeButton(L.S("恢复默认尺寸"), false);
            bReset.SetBounds(146, 214, 150, 30);
            bReset.Click += delegate
            {
                host.ResetToDefaultSize();
                host.ApplyLook();
                winSize.Value = 100;
                winSize.Invalidate();
            };
            c4.Controls.Add(bReset);

            // --- 6. countdown ---------------------------------------------
            Card c5 = AddCard(L.S("倒计时"), 250);
            sldDur = AddSlider(c5, L.S("倒计时时长"), 15, 3600, cfg.CountdownSecs, 18, 40, 508);
            sldDur.Step = 1;                 // 1 s precision, not 5 s
            sldDur.Format = delegate(int v) { return string.Format("{0}:{1:00}", v / 60, v % 60); };
            sldDur.BeforeChange = delegate(int v)
            {
                cfg.CountdownSecs = v;
                host.UpdateCountdownLength();
                host.ApplyLook();
            };

            // the slider is far too coarse to hit an exact second, so give it +-1 s
            Button bMinus1 = MakeButton("-1s", false);
            bMinus1.SetBounds(18, 90, 56, 26);
            bMinus1.Font = Theme.SmallFont;
            bMinus1.Click += delegate { NudgeCountdown(-1); };
            c5.Controls.Add(bMinus1);

            Button bPlus1 = MakeButton("+1s", false);
            bPlus1.SetBounds(80, 90, 56, 26);
            bPlus1.Font = Theme.SmallFont;
            bPlus1.Click += delegate { NudgeCountdown(+1); };
            c5.Controls.Add(bPlus1);

            Label unitHint = MakeLabel(L.S("精确到 1 秒"), true, 144, 90, 120, 26);
            c5.Controls.Add(unitHint);

            int[] presets = { 60, 180, 300, 600, 900, 1800, 2700, 3600 };
            for (int i = 0; i < presets.Length; i++)
            {
                int secs = presets[i];
                string caption = secs < 60 ? (secs + "s") : ((secs / 60) + L.S("分"));
                Button b = MakeButton(caption, false);
                b.SetBounds(18 + i * 56, 124, 50, 26);
                b.Font = Theme.SmallFont;
                b.Click += delegate
                {
                    cfg.CountdownSecs = secs;
                    sldDur.Value = Math.Max(sldDur.Min, Math.Min(sldDur.Max, secs));
                    sldDur.Invalidate();
                    host.SetMode(ClockForm.Mode.Countdown);
                    host.ResetCurrent();
                    host.ApplyLook();
                };
                c5.Controls.Add(b);
            }

            AddSwitch(c5, L.S("到时响铃（默认关闭）"), cfg.Beep, delegate(object s, EventArgs e)
            {
                cfg.Beep = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 160, 508, false);
            AddSwitch(c5, L.S("最后 20% 变红"), cfg.WarnRed, delegate(object s, EventArgs e)
            {
                cfg.WarnRed = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 188, 508, false);
            AddSwitch(c5, L.S("冒号每分钟闪烁"), cfg.FlashColon, delegate(object s, EventArgs e)
            {
                cfg.FlashColon = ((Switch)s).Checked; host.ApplyLook();
            }, 18, 216, 508, false);

            // --- footer ----------------------------------------------------
            Button done = MakeButton(L.S("完成"), true);
            done.SetBounds(18, cursorY + 4, 120, 32);
            done.Click += delegate { Close(); };
            Controls.Add(done);

            Button restore = MakeButton(L.S("恢复默认外观"), false);
            restore.SetBounds(146, cursorY + 4, 140, 32);
            restore.Click += delegate
            {
                host.ResetLookToDefault();
                rowBg.Value = Theme.FromHex(cfg.Bg);
                rowFg.Value = Theme.FromHex(cfg.Fg);
                sldSize.Value = cfg.FontPx;
            };
            Controls.Add(restore);

            Label tip = MakeLabel("v" + Program.Ver + " · " + Program.Author + L.S("　改动即时保存"), true, 300, cursorY + 4, 260, 32);
            tip.BackColor = Theme.Panel;
            Controls.Add(tip);

            AutoScrollPosition = new Point(0, 0);
        }

        // +-1 s nudge for the countdown length (slider alone is too coarse)
        private void NudgeCountdown(int delta)
        {
            int v = Math.Max(sldDur.Min, Math.Min(sldDur.Max, cfg.CountdownSecs + delta));
            cfg.CountdownSecs = v;
            sldDur.Value = v;
            sldDur.Invalidate();
            host.UpdateCountdownLength();
            host.ApplyLook();
        }

        private static string[] FontFamilies()
        {
            List<string> all = new List<string>();
            using (InstalledFontCollection col = new InstalledFontCollection())
            {
                foreach (FontFamily f in col.Families)
                {
                    try { all.Add(f.Name); }
                    catch (Exception) { }
                }
            }
            all.Sort(StringComparer.OrdinalIgnoreCase);

            string[] preferred = { "Bahnschrift", "DS-Digital", "Impact", "Agency FB",
                                   "Consolas", "Cascadia Mono", "Segoe UI", "Elephant",
                                   "Haettenschweiler", "Algerian", "微软雅黑", "思源黑体" };
            List<string> ordered = new List<string>();
            foreach (string p in preferred)
                if (all.Contains(p)) ordered.Add(p);
            foreach (string n in all)
                if (!ordered.Contains(n)) ordered.Add(n);
            return ordered.ToArray();
        }


    }
}
