// ===========================================================================
//  Lang.cs  --  tiny two-language string table (Chinese source text -> English)
//
//  Every user-visible string is written in Chinese in the source and wrapped
//  with L.S("中文").  When L.En is true the English translation is returned,
//  otherwise the original Chinese text is used untouched.
//
//  Two builds share one source tree:
//      csc /define:EN ...   ->  English UI by default  (FloatingClock.exe)
//      csc ...              ->  Chinese UI by default  (悬浮时钟.exe)
// ===========================================================================

using System.Collections.Generic;

namespace FloatingClockApp
{
    internal static class L
    {
        public static bool En = false;

        public static string S(string zh)
        {
            if (!En || zh == null) return zh;
            string en;
            return Map.TryGetValue(zh, out en) ? en : zh;
        }

        private static readonly Dictionary<string, string> Map = Build();

        private static Dictionary<string, string> Build()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();

            // ---- main window: captions, menu, errors -----------------------
            d["悬浮时钟"] = "Floating Clock";
            d["悬浮时钟 v"] = "Floating Clock v";
            d["悬浮时钟 出错了"] = "Floating Clock crashed";
            d["未知错误"] = "Unknown error";

            d["时钟"] = "Clock";
            d["倒计时"] = "Countdown";
            d["秒表"] = "Stopwatch";
            d["时钟模式"] = "Clock mode";
            d["倒计时模式"] = "Countdown mode";
            d["秒表模式"] = "Stopwatch mode";

            d["开始"] = "Start";
            d["暂停"] = "Pause";
            d["重置"] = "Reset";
            d["退出"] = "Exit";
            d["设置"] = "Settings";
            d["设置…"] = "Settings…";
            d["开始 / 暂停"] = "Start / Pause";

            d["始终置顶"] = "Always on top";
            d["点击穿透"] = "Click-through";
            d["锁定位置"] = "Lock position";
            d["还原窗口 400×160"] = "Reset window 400×160";

            d["倒计时进行中 · 空格暂停"] = "Counting down · Space to pause";
            d["倒计时已就绪 · 空格开始"] = "Ready · Space to start";
            d["秒表计时中"] = "Stopwatch running";
            d["秒表已暂停"] = "Stopwatch paused";
            d["已超过 "] = "Overtime ";
            d[" · 空格键重新开始"] = " · press Space to restart";
            d[" · 已超过 "] = " · overtime ";

            // ---- settings window -------------------------------------------
            d[" · 设置"] = " · Settings";
            d[" · 语言"] = " · Language";
            d["PPT / PDF 汇报专用悬浮计时"] = "Presentation timer for PPT / PDF";
            d["语言"] = "Language";
            d["切换后立即生效"] = "applies immediately";

            d["显示模式"] = "Display mode";
            d["显示秒"] = "Show seconds";
            d["24 小时制"] = "24-hour clock";
            d["显示日期"] = "Show date";
            d["空格 开始/暂停　R 重置"] = "Space: start/pause · R: reset";
            d["Ctrl+Alt+Space 开始/暂停"] = "Ctrl+Alt+Space: start/pause";
            d["Ctrl+Alt+T 穿透 · Ctrl+Alt+R 重置"] = "Ctrl+Alt+T: click-through";

            d["背景与颜色"] = "Background & colour";
            d["背景颜色"] = "Background colour";
            d["文字颜色"] = "Text colour";
            d["不透明度"] = "Opacity";

            d["数字字体"] = "Digit font";
            d["字体"] = "Font";
            d["字号自动适配窗口"] = "Auto-fit to window";
            d["字号大小"] = "Font size";
            d["字间距"] = "Letter spacing";
            d["加粗"] = "Bold";
            d["立体阴影"] = "Drop shadow";

            d["底部文字"] = "Footer text";
            d["自定义"] = "Custom";
            d["留空 = 自动显示（日期 / 倒计时状态 / 已超时读数）"] =
                "Empty = automatic (date / countdown status / overtime)";
            d["可用占位符：{date} {time} {mode} {status} {over}"] =
                "Placeholders: {date} {time} {mode} {status} {over}";
            d["例：{status}　—— 第 3 页"] = "e.g. {status}  —  Slide 3";

            d["窗口行为"] = "Window behaviour";
            d["锁定位置和大小"] = "Lock position & size";
            d["点击穿透（不挡 PPT 操作）"] = "Click-through (clicks reach the slides)";
            d["PPT / PDF 放映自动开始倒计时"] = "Auto-start on PPT / PDF presentation";
            d["窗口大小"] = "Window size";
            d["居中显示"] = "Centre on screen";
            d["恢复默认尺寸"] = "Default size";

            d["倒计时时长"] = "Countdown length";
            d["精确到 1 秒"] = "1-second steps";
            d["分"] = " min";
            d["到时响铃（默认关闭）"] = "Beep at zero (off by default)";
            d["最后 20% 变红"] = "Turn red in the last 20%";
            d["冒号每分钟闪烁"] = "Blink colon every minute";

            d["完成"] = "Done";
            d["恢复默认外观"] = "Reset look";
            d["　改动即时保存"] = "  changes save instantly";

            return d;
        }
    }
}
