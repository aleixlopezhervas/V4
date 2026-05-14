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

    public class WaypointMarker : GMapMarker
    {
        private int waypointNumber;
        private int size = 20;

        public WaypointMarker(PointLatLng pos, int number) : base(pos)
        {
            this.waypointNumber = number;
            Size = new Size(size, size);
            Offset = new Point(-size / 2, -size / 2);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Dibujar círculo azul
            using (SolidBrush brush = new SolidBrush(Color.DeepSkyBlue))
            {
                g.FillEllipse(brush, LocalPosition.X, LocalPosition.Y, size, size);
            }

            // Dibujar borde oscuro
            using (Pen pen = new Pen(Color.DarkBlue, 2))
            {
                g.DrawEllipse(pen, LocalPosition.X, LocalPosition.Y, size, size);
            }

            // Dibujar número
            string text = waypointNumber.ToString();
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                Rectangle textRect = new Rectangle(LocalPosition.X, LocalPosition.Y, size, size);
                g.DrawString(text, font, Brushes.White, textRect, format);
            }
        }
    }

    public class ArrowMarker : GMapMarker
    {
        private double heading; // Ángulo en grados
        private int arrowSize = 5; // Más pequeño

        public ArrowMarker(PointLatLng pos, double heading) : base(pos)
        {
            this.heading = heading;
            Size = new Size(arrowSize * 2, arrowSize * 2);
            Offset = new Point(-arrowSize, -arrowSize);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int cx = LocalPosition.X + arrowSize;
            int cy = LocalPosition.Y + arrowSize;

            // Convertir ángulo a radianes
            double angleRad = heading * Math.PI / 180.0;

            // Punto principal de la flecha (punta)
            int tipX = cx + (int)(arrowSize * 0.8 * Math.Cos(angleRad));
            int tipY = cy + (int)(arrowSize * 0.8 * Math.Sin(angleRad));

            // Puntos de la base de la flecha (cola) - ángulo de 150 grados
            double backAngle1 = angleRad + 2.618; // 150 grados
            double backAngle2 = angleRad - 2.618; // -150 grados

            int baseX1 = cx + (int)(arrowSize * 0.6 * Math.Cos(backAngle1));
            int baseY1 = cy + (int)(arrowSize * 0.6 * Math.Sin(backAngle1));
            int baseX2 = cx + (int)(arrowSize * 0.6 * Math.Cos(backAngle2));
            int baseY2 = cy + (int)(arrowSize * 0.6 * Math.Sin(backAngle2));

            // Dibujar la flecha (triángulo relleno) - más compacta
            Point[] arrowPoints = { 
                new Point(tipX, tipY), 
                new Point(baseX1, baseY1), 
                new Point(baseX2, baseY2) 
            };

            using (SolidBrush brush = new SolidBrush(Color.Blue))
            {
                g.FillPolygon(brush, arrowPoints);
            }

            // Dibujar borde azul oscuro
            using (Pen pen = new Pen(Color.DodgerBlue, 1))
            {
                g.DrawPolygon(pen, arrowPoints);
            }
        }
    }

    public class TraceMarker : GMapMarker
    {
        private int traceSize = 4;

        public TraceMarker(PointLatLng pos) : base(pos)
        {
            Size = new Size(traceSize, traceSize);
            Offset = new Point(-traceSize / 2, -traceSize / 2);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(Color.Red))
            {
                g.FillEllipse(brush, LocalPosition.X, LocalPosition.Y, traceSize, traceSize);
            }
        }
    }
}