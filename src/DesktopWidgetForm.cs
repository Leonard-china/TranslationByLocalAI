using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal sealed class DesktopWidgetForm : Form
    {
        private readonly ToolTip _toolTip;
        private Point _mouseDownScreen;
        private Point _locationAtMouseDown;
        private bool _hovered;
        private bool _pressed;
        private bool _dragged;

        internal event EventHandler ManualTranslationRequested;
        internal event EventHandler WidgetMoved;

        internal DesktopWidgetForm()
        {
            Text = "本地 AI 翻译入口";
            AccessibleName = "本地 AI 翻译入口";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(54, 54);
            BackColor = Color.FromArgb(250, 250, 250);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            using (var path = UiTheme.CreateRoundedRectangle(
                new Rectangle(0, 0, Width, Height),
                15))
            {
                Region = new Region(path);
            }

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(this, "本地 AI 翻译 · 点击打开，拖动调整位置");
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

        internal void ShowAtSavedPosition(int savedX, int savedY)
        {
            var proposed = new Point(savedX, savedY);
            var screen = savedX >= 0 && savedY >= 0
                ? Screen.FromPoint(proposed)
                : Screen.PrimaryScreen;
            var area = screen.WorkingArea;

            if (savedX < area.Left || savedX + Width > area.Right
                || savedY < area.Top || savedY + Height > area.Bottom)
            {
                proposed = new Point(
                    area.Right - Width - 18,
                    area.Top + Math.Max(80, (area.Height - Height) / 3));
            }

            Location = proposed;
            Show();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var background = _pressed
                ? Color.FromArgb(227, 235, 245)
                : (_hovered ? Color.FromArgb(244, 248, 252) : Color.FromArgb(250, 250, 250));
            var border = _hovered
                ? Color.FromArgb(122, 155, 194)
                : Color.FromArgb(190, 190, 190);
            e.Graphics.Clear(background);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiTheme.CreateRoundedRectangle(bounds, 15))
            using (var brush = new SolidBrush(background))
            using (var pen = new Pen(border, 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            UiTheme.DrawTranslationGlyph(
                e.Graphics,
                new Rectangle(bounds.X + 8, bounds.Y + 7, bounds.Width - 16, bounds.Height - 15),
                Color.FromArgb(32, 32, 32));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            if (!_pressed)
            {
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                _dragged = false;
                _mouseDownScreen = Cursor.Position;
                _locationAtMouseDown = Location;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_pressed && e.Button == MouseButtons.Left)
            {
                var current = Cursor.Position;
                var dx = current.X - _mouseDownScreen.X;
                var dy = current.Y - _mouseDownScreen.Y;
                if (!_dragged && (Math.Abs(dx) >= 3 || Math.Abs(dy) >= 3))
                {
                    _dragged = true;
                }
                if (_dragged)
                {
                    Location = ClampToWorkingArea(new Point(
                        _locationAtMouseDown.X + dx,
                        _locationAtMouseDown.Y + dy));
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _pressed)
            {
                _pressed = false;
                Invalidate();
                if (_dragged)
                {
                    var moved = WidgetMoved;
                    if (moved != null)
                    {
                        moved(this, EventArgs.Empty);
                    }
                }
                else
                {
                    var requested = ManualTranslationRequested;
                    if (requested != null)
                    {
                        requested(this, EventArgs.Empty);
                    }
                }
            }
            base.OnMouseUp(e);
        }

        private Point ClampToWorkingArea(Point proposed)
        {
            var area = Screen.FromPoint(proposed).WorkingArea;
            return new Point(
                Math.Max(area.Left, Math.Min(proposed.X, area.Right - Width)),
                Math.Max(area.Top, Math.Min(proposed.Y, area.Bottom - Height)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
