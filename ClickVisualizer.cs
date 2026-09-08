using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LightweightAutoClicker
{
    internal sealed class ClickVisualizer : Control
    {
        private readonly List<ClickPointConfig> points = new List<ClickPointConfig>();
        private int sourceWidth = 1;
        private int sourceHeight = 1;
        private int pulseX;
        private int pulseY;
        private int pulseStartedAt = int.MinValue;

        internal ClickVisualizer()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(248, 249, 251);
            MinimumSize = new Size(180, 130);
        }

        internal void SetPoints(IEnumerable<ClickPointConfig> source, int width, int height)
        {
            points.Clear();
            if (source != null)
            {
                foreach (ClickPointConfig point in source)
                    points.Add(point.Copy());
            }
            sourceWidth = Math.Max(1, width);
            sourceHeight = Math.Max(1, height);
            Invalidate();
        }

        internal void Pulse(int x, int y)
        {
            pulseX = x;
            pulseY = y;
            pulseStartedAt = Environment.TickCount;
            Invalidate();
        }

        internal void Advance()
        {
            if (pulseStartedAt == int.MinValue)
                return;
            int elapsed = unchecked(Environment.TickCount - pulseStartedAt);
            if (elapsed >= 0 && elapsed < 600)
                Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = ClientRectangle;
            bounds.Inflate(-12, -12);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            float scale = Math.Min(bounds.Width / (float)sourceWidth, bounds.Height / (float)sourceHeight);
            int viewportWidth = Math.Max(1, (int)(sourceWidth * scale));
            int viewportHeight = Math.Max(1, (int)(sourceHeight * scale));
            Rectangle viewport = new Rectangle(
                bounds.Left + (bounds.Width - viewportWidth) / 2,
                bounds.Top + (bounds.Height - viewportHeight) / 2,
                viewportWidth,
                viewportHeight);

            using (var screenBrush = new SolidBrush(Color.White))
            using (var borderPen = new Pen(Color.FromArgb(190, 198, 208)))
            using (var gridPen = new Pen(Color.FromArgb(230, 234, 239)))
            {
                e.Graphics.FillRectangle(screenBrush, viewport);
                e.Graphics.DrawRectangle(borderPen, viewport);
                for (int line = 1; line < 4; line++)
                {
                    float x = viewport.Left + viewport.Width * line / 4F;
                    float y = viewport.Top + viewport.Height * line / 4F;
                    e.Graphics.DrawLine(gridPen, x, viewport.Top, x, viewport.Bottom);
                    e.Graphics.DrawLine(gridPen, viewport.Left, y, viewport.Right, y);
                }
            }

            foreach (ClickPointConfig point in points)
            {
                PointF location = ToPreview(point.X, point.Y, viewport);
                Color color = point.Enabled ? Color.FromArgb(27, 111, 218) : Color.FromArgb(160, 169, 179);
                using (var fill = new SolidBrush(color))
                using (var outline = new Pen(Color.White, 1.5F))
                {
                    e.Graphics.FillEllipse(fill, location.X - 5, location.Y - 5, 10, 10);
                    e.Graphics.DrawEllipse(outline, location.X - 5, location.Y - 5, 10, 10);
                }
            }

            int elapsed = pulseStartedAt == int.MinValue ? int.MaxValue : unchecked(Environment.TickCount - pulseStartedAt);
            if (elapsed >= 0 && elapsed < 600)
            {
                PointF location = ToPreview(pulseX, pulseY, viewport);
                float progress = elapsed / 600F;
                float radius = 8 + 22 * progress;
                int alpha = (int)(220 * (1 - progress));
                using (var pulsePen = new Pen(Color.FromArgb(alpha, 226, 104, 30), 2.5F))
                    e.Graphics.DrawEllipse(pulsePen, location.X - radius, location.Y - radius, radius * 2, radius * 2);
            }
        }

        private PointF ToPreview(int x, int y, Rectangle viewport)
        {
            return new PointF(
                viewport.Left + Math.Max(0, Math.Min(sourceWidth - 1, x)) * viewport.Width / (float)sourceWidth,
                viewport.Top + Math.Max(0, Math.Min(sourceHeight - 1, y)) * viewport.Height / (float)sourceHeight);
        }
    }
}
