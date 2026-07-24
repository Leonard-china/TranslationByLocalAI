using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class FloatingButtonForm : Form
    {
        private readonly Timer _hideTimer;
        private bool _hovered;
        private bool _pressed;

        internal event EventHandler TranslateRequested;

        internal FloatingButtonForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(40, 40);
            BackColor = Color.FromArgb(250, 250, 250);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            using (var path = UiTheme.CreateRoundedRectangle(
                new Rectangle(0, 0, Width, Height),
                10))
            {
                Region = new Region(path);
            }

            _hideTimer = new Timer();
            _hideTimer.Tick += delegate
            {
                _hideTimer.Stop();
                Hide();
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;
                var parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return parameters;
            }
        }

        internal void ShowNear(Point cursorPosition, int timeoutSeconds)
        {
            var screen = Screen.FromPoint(cursorPosition);
            var area = screen.WorkingArea;
            var x = cursorPosition.X + 12;
            var y = cursorPosition.Y + 16;

            if (x + Width > area.Right)
            {
                x = cursorPosition.X - Width - 10;
            }
            if (y + Height > area.Bottom)
            {
                y = cursorPosition.Y - Height - 10;
            }
            x = Math.Max(area.Left, Math.Min(x, area.Right - Width));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - Height));

            Location = new Point(x, y);
            _hideTimer.Stop();
            _hideTimer.Interval = Math.Max(2, timeoutSeconds) * 1000;
            _hideTimer.Start();
            Show();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var background = _pressed
                ? Color.FromArgb(225, 235, 247)
                : (_hovered ? Color.FromArgb(242, 247, 253) : Color.FromArgb(250, 250, 250));
            var border = _hovered
                ? Color.FromArgb(126, 162, 205)
                : Color.FromArgb(196, 196, 196);
            e.Graphics.Clear(background);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiTheme.CreateRoundedRectangle(bounds, 10))
            using (var brush = new SolidBrush(background))
            using (var pen = new Pen(border, 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            UiTheme.DrawTranslationGlyph(
                e.Graphics,
                new Rectangle(bounds.X + 5, bounds.Y + 4, bounds.Width - 10, bounds.Height - 9),
                Color.FromArgb(38, 38, 38));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            _hideTimer.Stop();
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            _hideTimer.Stop();
            _hideTimer.Interval = 1200;
            _hideTimer.Start();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _pressed)
            {
                _pressed = false;
                Invalidate();
                var handler = TranslateRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            base.OnMouseUp(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hideTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
