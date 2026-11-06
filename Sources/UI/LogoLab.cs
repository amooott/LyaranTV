using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyaranTV
{
    /// <summary>
    /// Vingt propositions de logo, toutes dessinées en vectoriel et inspirées
    /// du signe actuel : un égaliseur et un triangle de lecture dans un carré
    /// arrondi violet. Aucune proposition ne remplace le logo tant que
    /// l'utilisateur n'a pas choisi.
    /// </summary>
    internal static class LogoLab
    {
        internal sealed class Proposal
        {
            public string Name;
            public string Note;
        }

        public static readonly Proposal[] Proposals = new[]
        {
            new Proposal { Name = "Égaliseur", Note = "Le signe actuel, référence." },
            new Proposal { Name = "Lyre", Note = "Lyaran évoque une lyre." },
            new Proposal { Name = "Monogramme L", Note = "Initiale et lecture." },
            new Proposal { Name = "Anneau", Note = "Cercle ouvert, lecture." },
            new Proposal { Name = "Ondes", Note = "Trois ondes douces." },
            new Proposal { Name = "Constellation", Note = "Étoiles reliées." },
            new Proposal { Name = "Vague", Note = "Flux et mouvement." },
            new Proposal { Name = "Prisme", Note = "Triangle fragmenté." },
            new Proposal { Name = "Bouclier", Note = "Protection et lecture." },
            new Proposal { Name = "Hexagone", Note = "Égaliseur géométrique." },
            new Proposal { Name = "Orbite", Note = "Deux arcs, un axe." },
            new Proposal { Name = "Infini", Note = "Ruban continu." },
            new Proposal { Name = "Chevrons", Note = "Navigation et lecture." },
            new Proposal { Name = "Aurore", Note = "Courbes boréales." },
            new Proposal { Name = "Éclat", Note = "Rayons autour du play." },
            new Proposal { Name = "Cube", Note = "Volume et relief." },
            new Proposal { Name = "Segments", Note = "Cercle en mouvement." },
            new Proposal { Name = "Lignes", Note = "Barres horizontales." },
            new Proposal { Name = "Monogramme Y", Note = "Y de Lyaran." },
            new Proposal { Name = "Signature", Note = "Play et égaliseur fondus." }
        };

        static readonly Color Purple = Color.FromRgb(96, 52, 169);
        static readonly Color Plum = Color.FromRgb(57, 33, 113);
        static readonly Color Night = Color.FromRgb(27, 25, 63);
        static readonly Color Cyan = Color.FromRgb(34, 211, 238);
        static readonly Color Lavender = Color.FromRgb(196, 181, 253);
        static readonly Color White = Colors.White;

        public static DrawingImage Create(int index)
        {
            var group = new DrawingGroup();
            using (var dc = group.Open()) Draw(dc, index, 64);
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        public static void Draw(DrawingContext dc, int index, double size)
        {
            double s = size / 64.0;
            dc.PushTransform(new ScaleTransform(s, s));
            DrawBackground(dc, index);
            switch (index)
            {
                case 0: DrawEqualizer(dc); break;
                case 1: DrawLyre(dc); break;
                case 2: DrawMonogramL(dc); break;
                case 3: DrawRing(dc); break;
                case 4: DrawWaves(dc); break;
                case 5: DrawConstellation(dc); break;
                case 6: DrawWave(dc); break;
                case 7: DrawPrism(dc); break;
                case 8: DrawShield(dc); break;
                case 9: DrawHexagon(dc); break;
                case 10: DrawOrbit(dc); break;
                case 11: DrawInfinity(dc); break;
                case 12: DrawChevrons(dc); break;
                case 13: DrawAurora(dc); break;
                case 14: DrawBurst(dc); break;
                case 15: DrawCube(dc); break;
                case 16: DrawSegments(dc); break;
                case 17: DrawLines(dc); break;
                case 18: DrawMonogramY(dc); break;
                default: DrawSignature(dc); break;
            }
            dc.Pop();
        }

        // ------------------------------------------------------------------ fonds

        static void DrawBackground(DrawingContext dc, int index)
        {
            Color top, bottom;
            if (index < 4) { top = Purple; bottom = Night; }
            else if (index < 8) { top = Color.FromRgb(44, 72, 158); bottom = Color.FromRgb(14, 22, 54); }
            else if (index < 12) { top = Color.FromRgb(78, 42, 138); bottom = Color.FromRgb(16, 22, 50); }
            else if (index < 16) { top = Color.FromRgb(20, 104, 138); bottom = Color.FromRgb(12, 18, 42); }
            else { top = Color.FromRgb(92, 46, 88); bottom = Color.FromRgb(18, 18, 40); }
            var brush = new LinearGradientBrush(top, bottom, 90);
            dc.DrawRoundedRectangle(brush, null, new Rect(3.5, 3.5, 57, 57), 12.8, 12.8);
            var glow = new LinearGradientBrush
            {
                StartPoint = new Point(1, 0), EndPoint = new Point(0.15, 0.8)
            };
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(70, 255, 255, 255), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            dc.DrawRoundedRectangle(glow, null, new Rect(3.5, 3.5, 57, 57), 12.8, 12.8);
        }

        // --------------------------------------------------------------- dessins

        static void DrawEqualizer(DrawingContext dc)
        {
            var white = RoundPen(White, 2.7);
            var lavender = RoundPen(Lavender, 2.7);
            dc.DrawLine(white, new Point(16.5, 25.5), new Point(16.5, 38.5));
            dc.DrawLine(lavender, new Point(21.5, 21.25), new Point(21.5, 42.75));
            dc.DrawLine(white, new Point(26.5, 17.25), new Point(26.5, 46.75));
            dc.DrawLine(lavender, new Point(31.5, 21.25), new Point(31.5, 42.75));
            dc.DrawLine(white, new Point(36.5, 25.5), new Point(36.5, 38.5));
            Play(dc, 40, 23.1, 15.4, 17.8, CyanBrush());
        }

        static void DrawLyre(DrawingContext dc)
        {
            var pen = RoundPen(White, 2.4);
            var lav = RoundPen(Lavender, 2.0);
            // Deux montants courbes et quatre cordes.
            Arc(dc, pen, 24, 32, 11, 16, 120, 120);
            Arc(dc, pen, 40, 32, 11, 16, 300, 120);
            dc.DrawLine(lav, new Point(20, 20), new Point(20, 44));
            dc.DrawLine(lav, new Point(28, 17), new Point(28, 47));
            dc.DrawLine(lav, new Point(36, 17), new Point(36, 47));
            dc.DrawLine(lav, new Point(44, 20), new Point(44, 44));
            Play(dc, 45, 24, 12, 16, CyanBrush());
        }

        static void DrawMonogramL(DrawingContext dc)
        {
            var pen = RoundPen(White, 4.2);
            dc.DrawLine(pen, new Point(20, 18), new Point(20, 42));
            dc.DrawLine(pen, new Point(20, 42), new Point(35, 42));
            Play(dc, 36, 23, 15, 18, CyanBrush());
        }

        static void DrawRing(DrawingContext dc)
        {
            dc.DrawEllipse(null, RoundPen(White, 2.5), new Point(28, 32), 15, 15);
            dc.DrawEllipse(null, RoundPen(Lavender, 1.5), new Point(28, 32), 10, 10);
            Play(dc, 37, 23, 15, 18, CyanBrush());
        }

        static void DrawWaves(DrawingContext dc)
        {
            var white = RoundPen(White, 2.4);
            var lav = RoundPen(Lavender, 2.0);
            Arc(dc, white, 28, 24, 18, 7, 200, 140);
            Arc(dc, lav, 28, 32, 18, 7, 200, 140);
            Arc(dc, white, 28, 40, 18, 7, 200, 140);
            Play(dc, 39, 23, 15, 18, CyanBrush());
        }

        static void DrawConstellation(DrawingContext dc)
        {
            var line = RoundPen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.2);
            var star = new SolidColorBrush(White);
            var points = new[]
            {
                new Point(16, 40), new Point(22, 22), new Point(31, 34),
                new Point(40, 18), new Point(47, 31)
            };
            for (int i = 0; i < points.Length - 1; i++) dc.DrawLine(line, points[i], points[i + 1]);
            foreach (var point in points) dc.DrawEllipse(star, null, point, 2.2, 2.2);
            Play(dc, 41, 35, 11, 14, CyanBrush());
        }

        static void DrawWave(DrawingContext dc)
        {
            var pen = RoundPen(White, 2.6);
            var lav = RoundPen(Lavender, 2.0);
            Arc(dc, pen, 30, 30, 20, 11, 190, 160);
            Arc(dc, lav, 30, 38, 20, 8, 190, 160);
            Play(dc, 42, 23, 13, 17, CyanBrush());
        }

        static void DrawPrism(DrawingContext dc)
        {
            var outline = RoundPen(White, 2.2);
            dc.DrawGeometry(null, outline, Geometry.Parse("M 32,14 L 51,47 L 13,47 Z"));
            dc.DrawGeometry(CyanBrush(), null, Geometry.Parse("M 32,14 L 38,47 L 13,47 Z"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(170, 196, 181, 253)), null,
                Geometry.Parse("M 32,14 L 51,47 L 38,47 Z"));
            Play(dc, 34, 26, 10, 13, Brushes.White);
        }

        static void DrawShield(DrawingContext dc)
        {
            var outline = RoundPen(White, 2.3);
            dc.DrawGeometry(null, outline, Geometry.Parse("M 32,12 L 50,19 L 50,33 C 50,42 42,48 32,52 C 22,48 14,42 14,33 L 14,19 Z"));
            Play(dc, 27, 25, 13, 16, CyanBrush());
        }

        static void DrawHexagon(DrawingContext dc)
        {
            var outline = RoundPen(White, 2.2);
            dc.DrawGeometry(null, outline, Geometry.Parse("M 32,12 L 49,22 L 49,42 L 32,52 L 15,42 L 15,22 Z"));
            var lav = RoundPen(Lavender, 2.4);
            var white = RoundPen(White, 2.4);
            dc.DrawLine(white, new Point(25, 26), new Point(25, 38));
            dc.DrawLine(lav, new Point(32, 21), new Point(32, 43));
            dc.DrawLine(white, new Point(39, 26), new Point(39, 38));
        }

        static void DrawOrbit(DrawingContext dc)
        {
            var white = RoundPen(White, 2.2);
            var lav = RoundPen(Lavender, 2.0);
            dc.DrawEllipse(null, white, new Point(32, 32), 18, 11);
            dc.DrawEllipse(null, lav, new Point(32, 32), 11, 18);
            Play(dc, 26, 24, 13, 16, CyanBrush());
        }

        static void DrawInfinity(DrawingContext dc)
        {
            var pen = RoundPen(White, 2.6);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(32, 32), false, false);
                ctx.BezierTo(new Point(22, 17), new Point(10, 24), new Point(16, 32), true, false);
                ctx.BezierTo(new Point(10, 40), new Point(22, 47), new Point(32, 32), true, false);
                ctx.BezierTo(new Point(42, 17), new Point(54, 24), new Point(48, 32), true, false);
                ctx.BezierTo(new Point(54, 40), new Point(42, 47), new Point(32, 32), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
            Play(dc, 27, 25, 11, 14, CyanBrush());
        }

        static void DrawChevrons(DrawingContext dc)
        {
            var white = RoundPen(White, 2.8);
            var lav = RoundPen(Lavender, 2.4);
            dc.DrawGeometry(null, white, Geometry.Parse("M 17,20 L 27,32 L 17,44"));
            dc.DrawGeometry(null, lav, Geometry.Parse("M 27,20 L 37,32 L 27,44"));
            Play(dc, 39, 23, 14, 18, CyanBrush());
        }

        static void DrawAurora(DrawingContext dc)
        {
            var white = RoundPen(White, 2.2);
            var lav = RoundPen(Lavender, 1.8);
            Arc(dc, white, 32, 30, 20, 13, 200, 140);
            Arc(dc, lav, 32, 36, 20, 9, 200, 140);
            Play(dc, 36, 25, 12, 15, CyanBrush());
        }

        static void DrawBurst(DrawingContext dc)
        {
            var pen = RoundPen(White, 1.8);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                double inner = 15, outer = 23;
                dc.DrawLine(pen,
                    new Point(30 + Math.Cos(angle) * inner, 32 + Math.Sin(angle) * inner),
                    new Point(30 + Math.Cos(angle) * outer, 32 + Math.Sin(angle) * outer));
            }
            Play(dc, 29, 23, 16, 18, CyanBrush());
        }

        static void DrawCube(DrawingContext dc)
        {
            var pen = RoundPen(White, 1.9);
            var fill = new SolidColorBrush(Color.FromArgb(80, 196, 181, 253));
            dc.DrawGeometry(fill, pen, Geometry.Parse("M 32,13 L 48,22 L 32,31 L 16,22 Z"));
            dc.DrawGeometry(null, pen, Geometry.Parse("M 16,22 L 16,40 L 32,49 L 32,31 Z"));
            dc.DrawGeometry(null, pen, Geometry.Parse("M 48,22 L 48,40 L 32,49 L 32,31 Z"));
            Play(dc, 27, 25, 10, 13, CyanBrush());
        }

        static void DrawSegments(DrawingContext dc)
        {
            var white = RoundPen(White, 3.0);
            var lav = RoundPen(Lavender, 2.2);
            Arc(dc, white, 32, 32, 18, 18, 200, 70);
            Arc(dc, lav, 32, 32, 18, 18, 290, 70);
            Arc(dc, white, 32, 32, 18, 18, 20, 70);
            Play(dc, 26, 24, 12, 16, CyanBrush());
        }

        static void DrawLines(DrawingContext dc)
        {
            var white = RoundPen(White, 2.8);
            var lav = RoundPen(Lavender, 2.2);
            dc.DrawLine(white, new Point(15, 23), new Point(39, 23));
            dc.DrawLine(lav, new Point(15, 32), new Point(32, 32));
            dc.DrawLine(white, new Point(15, 41), new Point(39, 41));
            Play(dc, 40, 22, 14, 20, CyanBrush());
        }

        static void DrawMonogramY(DrawingContext dc)
        {
            var pen = RoundPen(White, 4.0);
            dc.DrawLine(pen, new Point(17, 17), new Point(30, 33));
            dc.DrawLine(pen, new Point(43, 17), new Point(30, 33));
            dc.DrawLine(pen, new Point(30, 33), new Point(30, 47));
            Play(dc, 36, 22, 13, 16, CyanBrush());
        }

        static void DrawSignature(DrawingContext dc)
        {
            var white = RoundPen(White, 2.6);
            var lav = RoundPen(Lavender, 2.2);
            dc.DrawLine(white, new Point(17, 27), new Point(17, 37));
            dc.DrawLine(lav, new Point(23, 20), new Point(23, 44));
            dc.DrawLine(white, new Point(29, 25), new Point(29, 39));
            Play(dc, 34, 20, 20, 24, CyanBrush());
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), 1.0),
                new Point(44, 44), 8, 8);
        }

        // ---------------------------------------------------------------- outils

        static Brush CyanBrush()
        {
            var brush = new LinearGradientBrush(Lavender, Cyan, 0);
            brush.Freeze();
            return brush;
        }

        static Pen RoundPen(Color color, double thickness)
        {
            return RoundPen(new SolidColorBrush(color), thickness);
        }

        static Pen RoundPen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            return pen;
        }

        static void Play(DrawingContext dc, double x, double y, double width, double height, Brush brush)
        {
            dc.DrawGeometry(brush, null, Geometry.Parse(
                "M " + F(x) + "," + F(y) + " L " + F(x + width) + "," + F(y + height / 2)
                + " L " + F(x) + "," + F(y + height) + " Z"));
        }

        static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static void Arc(DrawingContext dc, Pen pen, double cx, double cy, double rx, double ry,
            double startDegrees, double sweepDegrees)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                Point start = PointOn(cx, cy, rx, ry, startDegrees);
                Point end = PointOn(cx, cy, rx, ry, startDegrees + sweepDegrees);
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(rx, ry), 0, Math.Abs(sweepDegrees) > 180,
                    sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        static Point PointOn(double cx, double cy, double rx, double ry, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Point(cx + Math.Cos(radians) * rx, cy + Math.Sin(radians) * ry);
        }

        // ------------------------------------------------------------ planche PNG

        /// <summary>Rend une planche 5 × 4 avec les vingt propositions et leurs noms.</summary>
        public static void RenderSheet(string path)
        {
            const int columns = 5, rows = 4, cellWidth = 220, cellHeight = 196;
            const int margin = 30, header = 84;
            int width = columns * cellWidth + margin * 2;
            int height = rows * cellHeight + header + margin;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var background = new LinearGradientBrush(Color.FromRgb(13, 13, 24), Color.FromRgb(25, 22, 45), 90);
                dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
                var titleType = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                var title = new FormattedText("LyaranTV · 20 propositions de logo", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, titleType, 22, Brushes.White, 1.0);
                dc.DrawText(title, new Point(margin, 26));
                var subtitle = new FormattedText("Inspirées du signe actuel · aucune ne remplace le logo avant votre choix",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, titleType, 12,
                    new SolidColorBrush(Color.FromRgb(178, 178, 198)), 1.0);
                dc.DrawText(subtitle, new Point(margin, 56));

                for (int i = 0; i < Proposals.Length && i < columns * rows; i++)
                {
                    int column = i % columns, row = i / columns;
                    double x = margin + column * cellWidth;
                    double y = header + row * cellHeight;
                    var cell = new Rect(x + 8, y + 6, cellWidth - 16, cellHeight - 14);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(24, 23, 40)),
                        new Pen(new SolidColorBrush(Color.FromRgb(52, 50, 78)), 1), cell, 14, 14);
                    var logo = new DrawingGroup();
                    using (var logoDc = logo.Open()) Draw(logoDc, i, 64);
                    var logoImage = new DrawingImage(logo);
                    dc.DrawImage(logoImage, new Rect(x + cellWidth / 2 - 42, y + 24, 84, 84));

                    var number = new FormattedText((i + 1).ToString("00", CultureInfo.InvariantCulture),
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, titleType, 11,
                        new SolidColorBrush(Color.FromRgb(34, 211, 238)), 1.0);
                    dc.DrawText(number, new Point(x + 20, y + 20));
                    var name = new FormattedText(Proposals[i].Name, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, titleType, 13, Brushes.White, 1.0);
                    dc.DrawText(name, new Point(x + 20, y + 120));
                    var note = new FormattedText(Proposals[i].Note, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, titleType, 10.5,
                        new SolidColorBrush(Color.FromRgb(160, 158, 184)), 1.0);
                    dc.DrawText(note, new Point(x + 20, y + 142));
                }
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
