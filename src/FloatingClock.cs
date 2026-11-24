// ===========================================================================
//  FloatingClock  --  悬浮时钟  (clock / countdown / stopwatch overlay)
//  Part 1 of 3: Program / Theme / Config and the menu renderer.
//  A borderless, always-on-top desktop widget.  Compiled to a single .exe with
//  the .NET Framework compiler, so it runs on any Windows box without Python
//  or any other runtime installed by the user.
//
//  Highlights
//    * free resizing (corner grip + mouse wheel) with digits that auto-scale
//    * background colour / text colour / transparency / font family / tracking
//    * click-through mode so the overlay never steals clicks from PowerPoint
//    * global hotkeys, persisted settings, PPT-slideshow auto start
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
    //  Entry point
    // =======================================================================
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        // 1.0 on a 100 % display, 2.0 on a 200 % display ... used to keep the
        // on-screen physical size identical no matter the scaling factor.
        public static float DpiScale = 1f;
        public static bool OpenSettingsOnStart = false;

        // Shown in the settings header, the footer and the taskbar tooltip.
        public const string Ver = "1.3.3";
        public const string Author = "SHUN";

        // Language baked into this build; a config value can override it at run time.
#if EN
        public const string DefaultLang = "en";
#else
        public const string DefaultLang = "zh";
#endif

        [STAThread]
        private static void Main(string[] args)
        {
            // Crisp digits on a scaled display: tell Windows we handle DPI.
            try { SetProcessDPIAware(); }
            catch (Exception) { }

            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    DpiScale = g.DpiX / 96f;
            }
            catch (Exception) { }
            if (DpiScale < 1f) DpiScale = 1f;

            string[] argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "--settings") OpenSettingsOnStart = true;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Never die silently: log anything unexpected to %APPDATA%\FloatingClock\error.log
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                LogCrash(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                LogCrash(e.ExceptionObject as Exception);
            };

            try
            {
                Application.Run(new ClockForm());
            }
            catch (Exception ex)
            {
                LogCrash(ex);
            }
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FloatingClock");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    (ex == null ? "(null)" : ex.ToString()) + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception) { }
            MessageBox.Show(ex == null ? L.S("未知错误") : ex.Message, L.S("悬浮时钟 出错了"));
            Environment.Exit(1);
        }
    }

    // =======================================================================
    //  Palette / drawing helpers
    // =======================================================================
    internal static class Theme
    {
        public static readonly Color Panel = Color.FromArgb(22, 22, 27);
        public static readonly Color Card = Color.FromArgb(32, 32, 39);
        public static readonly Color Card2 = Color.FromArgb(43, 43, 51);
        public static readonly Color Accent = Color.FromArgb(255, 103, 0);
        public static readonly Color Text = Color.FromArgb(233, 233, 238);
        public static readonly Color Muted = Color.FromArgb(139, 139, 149);
        public static readonly Color Line = Color.FromArgb(58, 58, 68);

        public static readonly Font TitleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        public static readonly Font UiFont = new Font("Microsoft YaHei UI", 9F);
        public static readonly Font SmallFont = new Font("Microsoft YaHei UI", 8F);

        public static string ToHex(Color c)
        {
            return string.Format("#{0:x2}{1:x2}{2:x2}", c.R, c.G, c.B);
        }

        public static Color FromHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return Color.Black;
            s = s.Trim().TrimStart('#');
            if (s.Length == 3) s = "" + s[0] + s[0] + s[1] + s[1] + s[2] + s[2];
            try
            {
                int v = Convert.ToInt32(s, 16);
                return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
            }
            catch (Exception) { return Color.Black; }
        }

        // t = 0 -> a, t = 1 -> b
        public static Color Blend(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // =======================================================================
    //  Settings file: a tiny key=value store in %APPDATA%\FloatingClock
    // =======================================================================
    internal class Config
    {
        public string Mode = "clock";
        public int X = int.MinValue, Y = int.MinValue, W = 400, H = 160;
        public string Bg = "#101014";
        public string Fg = "#ffffff";
        public double Alpha = 0.95;
        public string Font = "Bahnschrift";
        public bool Bold = true;
        public bool AutoFit = true;
        public int FontPx = 100;
        public int Spacing = 2;
        public bool Shadow = true;
        public bool ShowSeconds = true;
        public bool Hour24 = true;
        public bool ShowDate = true;
        public bool Topmost = true;
        public bool Locked = false;
        public bool ClickThrough = false;
        public bool PptAuto = false;
        // Second trigger, independent of the PPT/PDF rule: any frameless window that
        // covers the whole screen (a full-screen video, a viewer, anything) starts
        // the countdown. Ships OFF - it is the broadest rule, so it stays opt-in.
        public bool FullAuto = false;
        public int CountdownSecs = 300;
        // "Get ready" countdown that runs before the real countdown starts, so a
        // speaker can click "play" and only then walk to the first slide.
        // 0 = start counting immediately.
        public int PreRoll = 3;
        // silent by default: a beep during a presentation is the last thing you want
        public bool Beep = false;
        public bool WarnRed = true;
        public bool FlashColon = true;
        // Custom footer text under the digits; empty = automatic caption.
        public string Caption = "";
        // UI language: "zh" or "en".  Baked-in default depends on the build.
        public string Lang = Program.DefaultLang;

        private static string Path_
        {
            get
            {
                // The English build keeps its own file, so running both builds side
                // by side does not flip either one's language or geometry.
                string file = Program.DefaultLang == "en" ? "config-en.ini" : "config.ini";
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FloatingClock", file);
            }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(Path_)) return c;
                foreach (string raw in File.ReadAllLines(Path_))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "mode": c.Mode = v; break;
                        case "x": c.X = Int(v, c.X); break;
                        case "y": c.Y = Int(v, c.Y); break;
                        case "w": c.W = Int(v, c.W); break;
                        case "h": c.H = Int(v, c.H); break;
                        case "bg": c.Bg = v; break;
                        case "fg": c.Fg = v; break;
                        case "alpha": c.Alpha = Dbl(v, c.Alpha); break;
                        case "font": c.Font = v; break;
                        case "bold": c.Bold = Bool(v, c.Bold); break;
                        case "autofit": c.AutoFit = Bool(v, c.AutoFit); break;
                        case "fontpx": c.FontPx = Int(v, c.FontPx); break;
                        case "spacing": c.Spacing = Int(v, c.Spacing); break;
                        case "shadow": c.Shadow = Bool(v, c.Shadow); break;
                        case "showseconds": c.ShowSeconds = Bool(v, c.ShowSeconds); break;
                        case "hour24": c.Hour24 = Bool(v, c.Hour24); break;
                        case "showdate": c.ShowDate = Bool(v, c.ShowDate); break;
                        case "topmost": c.Topmost = Bool(v, c.Topmost); break;
                        case "locked": c.Locked = Bool(v, c.Locked); break;
                        case "clickthrough": c.ClickThrough = Bool(v, c.ClickThrough); break;
                        case "pptauto": c.PptAuto = Bool(v, c.PptAuto); break;
                        case "fullauto": c.FullAuto = Bool(v, c.FullAuto); break;
                        case "countdownsecs": c.CountdownSecs = Int(v, c.CountdownSecs); break;
                        case "preroll": c.PreRoll = Int(v, c.PreRoll); break;
                        case "beep": c.Beep = Bool(v, c.Beep); break;
                        case "warnred": c.WarnRed = Bool(v, c.WarnRed); break;
                        case "flashcolon": c.FlashColon = Bool(v, c.FlashColon); break;
                        case "caption": c.Caption = v; break;
                        case "lang": c.Lang = v; break;
                    }
                }
            }
            catch (Exception) { }
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_));
                List<string> lines = new List<string>();
                lines.Add("# FloatingClock settings");
                lines.Add("mode=" + Mode);
                lines.Add("x=" + X);
                lines.Add("y=" + Y);
                lines.Add("w=" + W);
                lines.Add("h=" + H);
                lines.Add("bg=" + Bg);
                lines.Add("fg=" + Fg);
                lines.Add("alpha=" + Alpha.ToString("0.###"));
                lines.Add("font=" + Font);
                lines.Add("bold=" + Bold);
                lines.Add("autofit=" + AutoFit);
                lines.Add("fontpx=" + FontPx);
                lines.Add("spacing=" + Spacing);
                lines.Add("shadow=" + Shadow);
                lines.Add("showseconds=" + ShowSeconds);
                lines.Add("hour24=" + Hour24);
                lines.Add("showdate=" + ShowDate);
                lines.Add("topmost=" + Topmost);
                lines.Add("locked=" + Locked);
                lines.Add("clickthrough=" + ClickThrough);
                lines.Add("pptauto=" + PptAuto);
                lines.Add("fullauto=" + FullAuto);
                lines.Add("countdownsecs=" + CountdownSecs);
                lines.Add("preroll=" + PreRoll);
                lines.Add("beep=" + Beep);
                lines.Add("warnred=" + WarnRed);
                lines.Add("flashcolon=" + FlashColon);
                lines.Add("caption=" + (Caption == null ? "" : Caption.Replace("\r", "").Replace("\n", "")));
                lines.Add("lang=" + (Lang == "en" ? "en" : "zh"));
                File.WriteAllLines(Path_, lines);
            }
            catch (Exception) { }
        }

        private static int Int(string s, int d)
        {
            try { return int.Parse(s); } catch (Exception) { return d; }
        }
        private static double Dbl(string s, double d)
        {
            try { return double.Parse(s); } catch (Exception) { return d; }
        }
        private static bool Bool(string s, bool d)
        {
            if (s == "True" || s == "true" || s == "1") return true;
            if (s == "False" || s == "false" || s == "0") return false;
            return d;
        }
    }

    // =======================================================================
    //  Dark tool-strip menu renderer
    // =======================================================================
    internal class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.Line; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuItemSelected { get { return Theme.Card2; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Card2; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Card2; } }
        public override Color SeparatorDark { get { return Theme.Line; } }
        public override Color SeparatorLight { get { return Theme.Card; } }
    }

    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Muted;
            base.OnRenderItemText(e);
        }
    }
}
