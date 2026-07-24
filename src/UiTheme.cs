using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal static class UiTheme
    {
        internal static readonly Color Primary = Color.FromArgb(37, 99, 235);
        internal static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
        internal static readonly Color Background = Color.FromArgb(246, 248, 252);
        internal static readonly Color Card = Color.White;
        internal static readonly Color Text = Color.FromArgb(25, 32, 46);
        internal static readonly Color Muted = Color.FromArgb(100, 116, 139);
        internal static readonly Color Border = Color.FromArgb(218, 224, 234);

        internal static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        internal static Button CreatePrimaryButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.Font = Font(9.5f, FontStyle.Bold);
            button.ForeColor = Color.White;
            button.BackColor = Primary;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = PrimaryHover;
            button.Height = 36;
            button.Cursor = Cursors.Hand;
            return button;
        }

        internal static Button CreateSecondaryButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.Font = Font(9.5f, FontStyle.Regular);
            button.ForeColor = Text;
            button.BackColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            button.Height = 36;
            button.Cursor = Cursors.Hand;
            return button;
        }

        internal static Icon CreateAppIcon()
        {
            var bitmap = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                var bounds = new Rectangle(1, 1, 29, 29);
                using (var path = CreateRoundedRectangle(bounds, 8))
                using (var brush = new SolidBrush(Color.FromArgb(248, 248, 248)))
                using (var pen = new Pen(Color.FromArgb(165, 165, 165), 1f))
                {
                    graphics.FillPath(brush, path);
                    graphics.DrawPath(pen, path);
                }
                DrawTranslationGlyph(
                    graphics,
                    new Rectangle(5, 4, 22, 23),
                    Color.FromArgb(35, 35, 35));
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        internal static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var diameter = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void DrawTranslationGlyph(Graphics graphics, Rectangle bounds, Color color)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var latinFont = new Font("Segoe UI", Math.Max(10f, bounds.Width * 0.32f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var chineseFont = new Font("Microsoft YaHei UI", Math.Max(11f, bounds.Width * 0.34f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            {
                graphics.DrawString(
                    "A",
                    latinFont,
                    brush,
                    bounds.Left + bounds.Width * 0.12f,
                    bounds.Top + bounds.Height * 0.06f);
                graphics.DrawString(
                    "文",
                    chineseFont,
                    brush,
                    bounds.Left + bounds.Width * 0.42f,
                    bounds.Top + bounds.Height * 0.42f);
            }
        }
    }
}
