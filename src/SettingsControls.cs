// ===========================================================================
//  SettingsControls.cs  --  split out of Settings.cs so each source file stays
//  a manageable size.  Compiled together with 悬浮时钟.cs.
//  Card / Switch / Segment / Slider / ColorRow / SwatchStrip
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
    //  Card: dark rounded container with an accent bar and a title
    // =======================================================================
    internal class Card : Panel
    {
        private readonly string title;

        public Card(string title)
        {
            this.title = title;
            BackColor = Theme.Panel;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Panel);
            using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 10))
            using (SolidBrush b = new SolidBrush(Theme.Card))
                g.FillPath(b, p);
            using (SolidBrush b = new SolidBrush(Theme.Accent))
                g.FillRectangle(b, 16, 14, 3, 13);
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString(title, Theme.TitleFont, b, 26, 11);
        }
    }

    // =======================================================================
    //  Switch: label + iOS-style pill toggle
    // =======================================================================
    internal class Switch : Control
    {
        public bool Checked;
        public bool Compact;   // draw the pill right after the label instead of at the right edge
        public event EventHandler ValueChanged;

        public Switch(string text, bool value)
        {
            Text = text;
            Checked = value;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush b = new SolidBrush(Checked ? Theme.Text : Theme.Blend(Theme.Muted, Theme.Text, 0.4)))
                    g.DrawString(Text, Theme.UiFont, b, new RectangleF(0, 0, Width - 52, Height), sf);
            }

            float pillX = Width - 44;
            if (Compact)
            {
                SizeF sz = g.MeasureString(Text, Theme.UiFont);
                pillX = Math.Min(Width - 44, sz.Width + 14);
            }
            RectangleF track = new RectangleF(pillX, (Height - 20) / 2f, 42, 20);
            using (GraphicsPath p = Theme.Round(track, 10))
            using (SolidBrush b = new SolidBrush(Checked ? Theme.Accent : Theme.Card2))
                g.FillPath(b, p);
            float knob = Checked ? track.Right - 18 : track.X + 2;
            using (SolidBrush b = new SolidBrush(Checked ? Color.White
                                                  : Theme.Blend(Theme.Card2, Theme.Text, 0.55)))
                g.FillEllipse(b, knob, track.Y + 2, 16, 16);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (!ClientRectangle.Contains(e.Location)) return;
            Checked = !Checked;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    // =======================================================================
    //  Segment: 2-4 mutually exclusive options in one pill
    // =======================================================================
    internal class Segment : Control
    {
        private readonly string[] items;
        public int Selected;
        public event EventHandler ValueChanged;

        public Segment(string[] items, int selected)
        {
            this.items = items;
            Selected = selected;
            BackColor = Theme.Card;
            Height = 32;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 8))
            using (SolidBrush b = new SolidBrush(Theme.Card2))
                g.FillPath(b, p);

            float segW = (Width - 6) / items.Length;
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < items.Length; i++)
                {
                    RectangleF r = new RectangleF(3 + i * segW, 3, segW - 2, Height - 6);
                    if (i == Selected)
                    {
                        using (GraphicsPath p = Theme.Round(r, 7))
                        using (SolidBrush b = new SolidBrush(Theme.Accent))
                            g.FillPath(b, p);
                    }
                    using (SolidBrush b = new SolidBrush(i == Selected
                        ? Color.FromArgb(26, 26, 26) : Theme.Muted))
                        g.DrawString(items[i], Theme.UiFont, b, r, sf);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            float segW = (Width - 6) / items.Length;
            int idx = (int)((e.X - 3) / segW);
            if (idx < 0 || idx >= items.Length) return;
            if (idx == Selected) return;
            Selected = idx;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    // =======================================================================
    //  Slider: label + value on top, track with a knob underneath
    // =======================================================================
    internal class Slider : Control
    {
        public int Min, Max, Value;
        public int Step = 1;
        public string Suffix = "";
        public delegate string Formatter(int v);
        public Formatter Format;
        public event EventHandler ValueChanged;
        // BeforeChange fires continuously while dragging (used for live preview)
        public Action<int> BeforeChange;

        private bool dragging;

        // Programmatic move that behaves exactly like a drag (used by the +-1s buttons)
        public void SetValue(int v)
        {
            v = Math.Max(Min, Math.Min(Max, v));
            if (v == Value) { Invalidate(); return; }
            Value = v;
            Invalidate();
            if (BeforeChange != null) BeforeChange(Value);
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        public Slider(string label, int min, int max, int value)
        {
            Text = label;
            Min = min; Max = max;
            Value = Math.Max(min, Math.Min(max, value));
            BackColor = Theme.Card;
            Height = 44;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private void DrawValueText(Graphics g)
        {
            string text = Format != null ? Format(Value) : (Value + (Suffix.Length > 0 ? "" : ""));
            if (Format == null && Suffix.Length > 0) text = Value + Suffix;
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    g.DrawString(text, Theme.UiFont, b, new RectangleF(0, 0, Width, 22), sf);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush b = new SolidBrush(Enabled ? Theme.Text : Theme.Muted))
                    g.DrawString(Text, Theme.UiFont, b, new RectangleF(0, 0, Width - 90, 22), sf);
            }
            DrawValueText(g);

            RectangleF track = new RectangleF(6, 30, Width - 12, 6);
            using (GraphicsPath p = Theme.Round(track, 3))
            using (SolidBrush b = new SolidBrush(Theme.Card2))
                g.FillPath(b, p);

            float ratio = Max > Min ? (Value - Min) / (float)(Max - Min) : 0f;
            float filled = track.Width * ratio;
            if (filled > 2)
            {
                using (GraphicsPath p = Theme.Round(new RectangleF(track.X, track.Y, filled, track.Height), 3))
                using (SolidBrush b = new SolidBrush(Enabled ? Theme.Accent : Theme.Blend(Theme.Accent, Theme.Card2, 0.5)))
                    g.FillPath(b, p);
            }
            float kx = track.X + filled;
            using (SolidBrush b = new SolidBrush(Enabled ? Color.White : Theme.Muted))
                g.FillEllipse(b, kx - 5, track.Y - 3, 16, 12);
        }

        private void SetFromX(int x)
        {
            if (!Enabled) return;
            float ratio = (x - 6) / (float)Math.Max(1, Width - 12);
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            int raw = Min + (int)Math.Round(ratio * (Max - Min));
            if (Step > 1) raw = Min + (int)(Math.Round((raw - Min) / (float)Step) * Step);
            if (raw < Min) raw = Min;
            if (raw > Max) raw = Max;
            if (raw == Value) return;
            Value = raw;
            Invalidate();
            if (BeforeChange != null) BeforeChange(Value);
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            dragging = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    // =======================================================================
    //  ColorRow: name + hex + swatch, clicking opens the colour picker
    // =======================================================================
    internal class ColorRow : Control
    {
        private readonly string label;
        public Color Value;
        public event EventHandler ValueChanged;

        public ColorRow(string label, Color value)
        {
            this.label = label;
            Value = value;
            BackColor = Theme.Card;
            Height = 26;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    g.DrawString(label, Theme.UiFont, b, new RectangleF(0, 0, Width - 150, Height), sf);
            }
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                sf.Trimming = StringTrimming.None;
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                    g.DrawString(Theme.ToHex(Value), Theme.SmallFont, b,
                                 new RectangleF(Width - 154, 0, 110, Height), sf);
            }
            RectangleF sw = new RectangleF(Width - 36, 3, 34, 20);
            using (GraphicsPath p = Theme.Round(sw, 5))
            using (SolidBrush b = new SolidBrush(Value))
                g.FillPath(b, p);
            using (GraphicsPath p = Theme.Round(sw, 5))
            using (Pen pen = new Pen(Theme.Line))
                g.DrawPath(pen, p);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.FullOpen = true;
                dlg.Color = Value;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Value = dlg.Color;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }
    }

    // =======================================================================
    //  SwatchStrip: the preset themes
    // =======================================================================
    internal class SwatchStrip : Control
    {
        private readonly string[][] palettes;
        public event Action<int> Picked;

        public SwatchStrip(string[][] palettes)
        {
            this.palettes = palettes;
            BackColor = Theme.Card;
            Height = 36;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            int n = palettes.Length;
            float w = (Width - (n - 1) * 6) / n;
            for (int i = 0; i < n; i++)
            {
                RectangleF r = new RectangleF(i * (w + 6), 2, w, Height - 6);
                Color bg = Theme.FromHex(palettes[i][0]);
                using (GraphicsPath p = Theme.Round(r, 6))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);
                using (GraphicsPath p = Theme.Round(r, 6))
                using (Pen pen = new Pen(Theme.FromHex(palettes[i][1])))
                    g.DrawPath(pen, p);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int n = palettes.Length;
            float w = (Width - (n - 1) * 6) / n;
            for (int i = 0; i < n; i++)
            {
                RectangleF r = new RectangleF(i * (w + 6), 2, w, Height - 6);
                if (r.Contains(e.Location))
                {
                    if (Picked != null) Picked(i);
                    return;
                }
            }
        }
    }
}
