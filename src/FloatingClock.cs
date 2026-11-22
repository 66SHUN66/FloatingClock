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
        public const string Ver = "1.2";
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
