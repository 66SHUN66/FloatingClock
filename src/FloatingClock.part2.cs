
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
        public int CountdownSecs = 300;
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
                        case "shadow": c.Shared = Bool(v, c.Shared); break;
                        case "showseconds": c.ShowSeconds = Bool(v, c.ShowSeconds); break;
                        case "hour24": c.Hour24 = Bool(v, c.Hour24); break;
                        case "showdate": c.ShowDate = Bool(v, c.ShowDate); break;
                        case "topmost": c.Topmost = Bool(v, c.Topmost); break;
                        case "locked": c.Locked = Bool(v, c.Locked); break;
                        case "clickthrough": c.ClickThrough = Bool(v, c.ClickThrough); break;
                        case "pptauto": c.PptAuto = Bool(v, c.PptAuto); break;
                        case "countdownsecs": c.CountdownSecs = Int(v, c.CountdownSecs); break;
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
