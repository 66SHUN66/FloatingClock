// ===========================================================================
//  FontPicker: a hand-drawn dark font chooser with live digit previews
//  (replaces the standard ComboBox, which cannot be themed dark)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FloatingClockApp
{
    internal class FontPicker : Control
    {
        private readonly string[] families;
        private FontPopup popup;
        public string SelectedFamily;
        public event EventHandler ValueChanged;

        public FontPicker(string[] families, string current)
        {
            this.families = families;
            SelectedFamily = current;
            BackColor = Theme.Card;
            Height = 28;
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

            RectangleF box = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Round(box, 6))
            using (SolidBrush b = new SolidBrush(Theme.Card2))
                g.FillPath(b, p);
            using (GraphicsPath p = Theme.Round(box, 6))
            using (Pen pen = new Pen(Theme.Line))
                g.DrawPath(pen, p);

            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    g.DrawString(SelectedFamily, Theme.UiFont, b,
                                 new RectangleF(10, 0, Width - 40, Height), sf);
            }

            // small chevron
            float cx = Width - 18, cy = Height / 2f;
            using (Pen pen = new Pen(Theme.Muted, 2f))
            {
                g.DrawLine(pen, cx - 5, cy - 2, cx, cy + 3);
                g.DrawLine(pen, cx, cy + 3, cx + 5, cy - 2);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (popup != null && !popup.IsDisposed) { popup.Close(); return; }
            popup = new FontPopup(families, SelectedFamily);
            popup.Picked += delegate(string fam)
            {
                SelectedFamily = fam;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            };
            popup.ShowBelow(this);
        }
    }

    // -----------------------------------------------------------------------
    //  The drop-down list.  Each row is drawn in the font it represents,
    //  together with a "12:34" sample, so choosing a font is a visual decision.
    // -----------------------------------------------------------------------
    internal class FontPopup : Form
    {
        private readonly ListBox list;
        private readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        private Font sampleFont;
        public event Action<string> Picked;

        public FontPopup(string[] families, string current)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Card;
            TopMost = true;
            DoubleBuffered = true;

            float s = Program.DpiScale;
            ClientSize = new Size((int)(380 * s), (int)(340 * s));
            Padding = new Padding((int)(2 * s));

            list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.Card;
            list.ForeColor = Theme.Text;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = (int)(26 * s);
            list.IntegralHeight = false;
            list.Font = Theme.UiFont;
            list.SetBounds((int)(4 * s), (int)(4 * s),
                           ClientSize.Width - (int)(8 * s), ClientSize.Height - (int)(8 * s));
            list.Items.AddRange(families);
            if (Array.IndexOf(families, current) >= 0) list.SelectedItem = current;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.DrawItem += DrawRow;
            list.DoubleClick += delegate { Commit(); };
            list.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Commit(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) { Close(); }
            };
            Controls.Add(list);

            Deactivate += delegate { Close(); };
        }

        public void ShowBelow(Control anchor)
        {
            float s = Program.DpiScale;
            list.ItemHeight = (int)(26 * s);
            Point below = anchor.PointToScreen(new Point(0, anchor.Height + 2));
            Rectangle wa = Screen.FromControl(anchor).WorkingArea;
            int y = below.Y;
            if (y + Height > wa.Bottom) y = anchor.PointToScreen(Point.Empty).Y - Height - 2;
            int x = Math.Max(wa.Left, Math.Min(below.X, wa.Right - Width));
            Location = new Point(x, Math.Max(wa.Top, y));
            Show(anchor.TopLevelControl);
            if (list.SelectedIndex >= 0)
                list.TopIndex = Math.Max(0, Math.Min(list.SelectedIndex - 3, list.Items.Count - 1));
            list.Focus();
        }

        private void Commit()
        {
            if (list.SelectedItem != null && Picked != null)
                Picked(list.SelectedItem.ToString());
            Close();
        }

        private void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(sel ? Theme.Card2 : Theme.Card))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (sel)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(b, e.Bounds.X, e.Bounds.Y + 3, (int)(3 * Program.DpiScale),
                                             e.Bounds.Height - 6);
            }

            string name = list.Items[e.Index].ToString();
            Font f = SampleFont(name, (int)(13 * Program.DpiScale));
            string sample = "12:34";
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                    e.Graphics.DrawString(sample, f, b, new RectangleF(e.Bounds.X, e.Bounds.Y,
                                                                      e.Bounds.Width - 12, e.Bounds.Height), sf);
            }
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    e.Graphics.DrawString(name, f, b, new RectangleF(e.Bounds.X + 10, e.Bounds.Y,
                                                                    e.Bounds.Width - 90, e.Bounds.Height), sf);
            }
        }

        // Fonts are expensive: build one per family, reuse it for every redraw.
        private Font SampleFont(string family, int px)
        {
            string key = family + "|" + px;
            if (cache.ContainsKey(key)) return cache[key];
            Font f;
            try { f = new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel); }
            catch (Exception) { f = Theme.UiFont; }
            cache[key] = f;
            return f;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (sampleFont != null) sampleFont.Dispose();
            foreach (Font f in cache.Values)
                if (!ReferenceEquals(f, Theme.UiFont)) f.Dispose();
            cache.Clear();
        }
    }
}
