using System;
using System.Drawing;
using GMap.NET;
using GMap.NET.WindowsForms;

namespace Formulario
{
    public class RedDotMarker : GMapMarker
    {
        private int radius;
        public double Heading { get; set; }

        public RedDotMarker(PointLatLng pos, int radius = 8, double heading = 0) : base(pos)
        {
            this.radius = radius;
            this.Heading = heading;
            Size = new Size(radius * 2, radius * 2);
            Offset = new Point(-radius, -radius);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.Red, LocalPosition.X, LocalPosition.Y, radius * 2, radius * 2);

            int cx = LocalPosition.X + radius;
            int cy = LocalPosition.Y + radius;
            double rad = (Heading - 90) * Math.PI / 180.0;
            int lineLength = radius + 6;
            int ex = cx + (int)(lineLength * Math.Cos(rad));
            int ey = cy + (int)(lineLength * Math.Sin(rad));

            using (Pen pen = new Pen(Color.Black, 2))
                g.DrawLine(pen, cx, cy, ex, ey);
        }
    }

    public class BlueXMarker : GMapMarker
    {
        private int size;

        public BlueXMarker(PointLatLng pos, int size = 8) : base(pos)
        {
            this.size = size;
            Size = new Size(size * 2, size * 2);
            Offset = new Point(-size, -size);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Color.Blue, 2))
            {
                g.DrawLine(pen, LocalPosition.X, LocalPosition.Y,
                                LocalPosition.X + size * 2, LocalPosition.Y + size * 2);
                g.DrawLine(pen, LocalPosition.X + size * 2, LocalPosition.Y,
                                LocalPosition.X, LocalPosition.Y + size * 2);
            }
        }
    }
}