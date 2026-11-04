using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyaranTV
{
    /// <summary>
    /// Photos et avatars illustrés des profils. Une photo importée est
    /// recadrée en carré, réduite à 256 px puis conservée en base64 dans
    /// profiles.json : elle suit donc le profil dans les sauvegardes et les
    /// exports, sans dépendre du fichier d'origine.
    /// </summary>
    internal static class ProfileAvatars
    {
        public const string PresetPrefix = "preset:";
        public const int StoredSize = 256;

        static readonly string[] PresetIds = { "aurore", "nuit", "vague", "flamme", "foret", "cosmos", "or", "glacier" };
        static readonly Dictionary<string, ImageSource> PresetCache = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        static readonly Dictionary<string, ImageSource> PhotoCache = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        static readonly object Gate = new object();

        public static string[] Presets { get { return (string[])PresetIds.Clone(); } }

        public static string Label(string preset)
        {
            string id = PresetId(preset);
            if (id == "aurore") return "Aurore";
            if (id == "nuit") return "Nuit";
            if (id == "vague") return "Vague";
            if (id == "flamme") return "Flamme";
            if (id == "foret") return "Forêt";
            if (id == "cosmos") return "Cosmos";
            if (id == "or") return "Or";
            if (id == "glacier") return "Glacier";
            return "";
        }

        public static string PresetValue(string id) { return PresetPrefix + (id ?? ""); }

        public static string PresetId(string value)
        {
            string text = (value ?? "").Trim();
            if (!text.StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase)) return "";
            string id = text.Substring(PresetPrefix.Length).ToLowerInvariant();
            foreach (string preset in PresetIds)
                if (String.Equals(preset, id, StringComparison.Ordinal)) return id;
            return "";
        }

        public static bool HasCustomPhoto(UserProfile profile)
        {
            return profile != null && !String.IsNullOrWhiteSpace(profile.AvatarImage);
        }

        public static ImageSource GetImage(UserProfile profile)
        {
            if (profile == null) return null;
            ImageSource custom = Decode(profile.AvatarImage);
            if (custom != null) return custom;
            string preset = PresetId(profile.AvatarPreset);
            return String.IsNullOrEmpty(preset) ? null : PresetImage(preset);
        }

        public static ImageSource Decode(string base64)
        {
            if (String.IsNullOrWhiteSpace(base64)) return null;
            string value = base64.Trim();
            int comma = value.IndexOf(',');
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
                value = value.Substring(comma + 1);
            lock (Gate)
            {
                ImageSource cached;
                if (PhotoCache.TryGetValue(value, out cached)) return cached;
            }
            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                if (bytes.Length < 24) return null;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = StoredSize;
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.EndInit();
                bitmap.Freeze();
                lock (Gate)
                {
                    if (PhotoCache.Count > 32) PhotoCache.Clear();
                    PhotoCache[value] = bitmap;
                }
                return bitmap;
            }
            catch { return null; }
        }

        /// <summary>
        /// Prépare une image choisie par l'utilisateur. Le recadrage central
        /// évite les visages étirés, et l'orientation EXIF est appliquée avant
        /// l'encodage pour que les photos de téléphone restent droites.
        /// </summary>
        public static string Import(string path, out string error)
        {
            error = "";
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    error = "Cette image est introuvable.";
                    return null;
                }
                var info = new FileInfo(path);
                if (info.Length > 18 * 1024 * 1024)
                {
                    error = "L'image est trop lourde (18 Mo maximum).";
                    return null;
                }

                BitmapFrame frame;
                using (var stream = File.OpenRead(path))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0) throw new InvalidDataException();
                    frame = decoder.Frames[0];
                }

                int orientation = ReadOrientation(frame);
                double angle = 0;
                bool flipX = false, flipY = false;
                switch (orientation)
                {
                    case 2: flipX = true; break;
                    case 3: angle = 180; break;
                    case 4: flipY = true; break;
                    case 5: angle = 90; flipX = true; break;
                    case 6: angle = 90; break;
                    case 7: angle = 90; flipY = true; break;
                    case 8: angle = -90; break;
                }

                int sourceWidth = frame.PixelWidth, sourceHeight = frame.PixelHeight;
                if (sourceWidth < 1 || sourceHeight < 1) throw new InvalidDataException();
                double turnedWidth = (Math.Abs(angle) == 90) ? sourceHeight : sourceWidth;
                double turnedHeight = (Math.Abs(angle) == 90) ? sourceWidth : sourceHeight;
                double scale = StoredSize / Math.Max(turnedWidth, turnedHeight);

                var visual = new DrawingVisual();
                using (DrawingContext context = visual.RenderOpen())
                {
                    context.PushTransform(new TranslateTransform(StoredSize / 2.0, StoredSize / 2.0));
                    context.PushTransform(new RotateTransform(angle));
                    context.PushTransform(new ScaleTransform(flipX ? -scale : scale, flipY ? -scale : scale));
                    context.DrawImage(frame, new Rect(-sourceWidth / 2.0, -sourceHeight / 2.0, sourceWidth, sourceHeight));
                    context.Pop();
                    context.Pop();
                    context.Pop();
                }

                var rendered = new RenderTargetBitmap(StoredSize, StoredSize, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(visual);
                rendered.Freeze();

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rendered));
                using (var output = new MemoryStream())
                {
                    encoder.Save(output);
                    return Convert.ToBase64String(output.ToArray());
                }
            }
            catch
            {
                error = "Ce fichier n'est pas une image lisible.";
                return null;
            }
        }

        static int ReadOrientation(BitmapFrame frame)
        {
            try
            {
                var metadata = frame.Metadata as BitmapMetadata;
                if (metadata == null) return 1;
                object value = metadata.GetQuery("/app1/ifd/{ushort=274}");
                if (value == null) value = metadata.GetQuery("/ifd/{ushort=274}");
                return value == null ? 1 : Convert.ToInt32(value);
            }
            catch { return 1; }
        }

        /// <summary>
        /// Construit l'avatar affiché partout : photo importée, illustration
        /// prédéfinie, ou initiale sur un dégradé de la couleur du profil.
        /// </summary>
        public static FrameworkElement Build(UserProfile profile, double size, double radius, Color fallback, bool gloss)
        {
            if (size <= 0) size = 32;
            if (radius <= 0 || radius > size / 2.0) radius = size / 2.0;
            var host = new Grid { Width = size, Height = size, ClipToBounds = true };
            var frame = new Border { CornerRadius = new CornerRadius(radius), ClipToBounds = true };
            ImageSource source = GetImage(profile);
            if (source != null)
            {
                var image = new Image
                {
                    Source = source, Stretch = Stretch.UniformToFill,
                    SnapsToDevicePixels = true, IsHitTestVisible = false
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                frame.Child = image;
                // Fond neutre : un décalage d'arrondi d'un pixel ne doit jamais
                // laisser apparaître un liseré de la couleur d'accent sur le bord.
                frame.Background = Brushes.Transparent;
            }
            else
            {
                Color color = fallback;
                if (profile != null && !String.IsNullOrWhiteSpace(profile.AvatarColor))
                {
                    Color parsed;
                    if (TryParseColor(profile.AvatarColor, out parsed)) color = parsed;
                }
                frame.Background = new LinearGradientBrush(
                    Lighten(color, Colors.White, 0.18), Darken(color, 0.36), new Point(0, 0), new Point(1, 1));
                string initial = profile == null || String.IsNullOrWhiteSpace(profile.Name)
                    ? "?" : profile.Name.Trim().Substring(0, 1).ToUpperInvariant();
                var label = new TextBlock
                {
                    Text = initial, FontSize = Math.Max(11, Math.Round(size * 0.38)), FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center
                };
                frame.Child = label;
            }
            Round(frame, radius);
            host.Children.Add(frame);

            if (gloss)
            {
                var shine = new Border
                {
                    Background = new LinearGradientBrush(
                        Color.FromArgb(32, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                        new Point(0, 0), new Point(1, 1)),
                    IsHitTestVisible = false
                };
                Round(shine, radius);
                host.Children.Add(shine);
            }
            return host;
        }

        static void Round(FrameworkElement element, double radius)
        {
            Action apply = delegate
            {
                double width = element.ActualWidth, height = element.ActualHeight;
                if (width < 1 || height < 1) return;
                element.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
            };
            element.SizeChanged += delegate { apply(); };
            element.Loaded += delegate { apply(); };
            apply();
        }

        static bool TryParseColor(string value, out Color color)
        {
            color = Colors.Transparent;
            string hex = (value ?? "").Trim().TrimStart('#');
            if (hex.Length != 6) return false;
            try
            {
                color = Color.FromRgb(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
                return true;
            }
            catch { return false; }
        }

        static SolidColorBrush Brush(Color color) { return new SolidColorBrush(color); }

        static Color WithAlpha(Color color, byte alpha) { return Color.FromArgb(alpha, color.R, color.G, color.B); }

        static Color Lighten(Color color, Color to, double amount)
        {
            return Color.FromRgb(
                (byte)Math.Round(color.R + (to.R - color.R) * amount),
                (byte)Math.Round(color.G + (to.G - color.G) * amount),
                (byte)Math.Round(color.B + (to.B - color.B) * amount));
        }

        static Color Darken(Color color, double amount)
        {
            return Color.FromRgb(
                (byte)Math.Round(color.R * (1 - amount)),
                (byte)Math.Round(color.G * (1 - amount)),
                (byte)Math.Round(color.B * (1 - amount)));
        }

        static ImageSource PresetImage(string id)
        {
            lock (Gate)
            {
                ImageSource cached;
                if (PresetCache.TryGetValue(id, out cached)) return cached;
            }
            DrawingGroup drawing = BuildPreset(id);
            if (drawing == null) return null;
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            lock (Gate) PresetCache[id] = image;
            return image;
        }

        static DrawingGroup BuildPreset(string id)
        {
            var group = new DrawingGroup();
            if (id == "aurore") BuildAurore(group);
            else if (id == "nuit") BuildNuit(group);
            else if (id == "vague") BuildVague(group);
            else if (id == "flamme") BuildFlamme(group);
            else if (id == "foret") BuildForet(group);
            else if (id == "cosmos") BuildCosmos(group);
            else if (id == "or") BuildOr(group);
            else if (id == "glacier") BuildGlacier(group);
            else return null;
            return group;
        }

        static void Background(DrawingGroup group, Color top, Color bottom)
        {
            group.Children.Add(new GeometryDrawing(
                new LinearGradientBrush(top, bottom, new Point(0.15, 0), new Point(0.85, 1)), null,
                new RectangleGeometry(new Rect(0, 0, 100, 100))));
        }

        static void Fill(DrawingGroup group, Geometry geometry, Brush brush)
        {
            group.Children.Add(new GeometryDrawing(brush, null, geometry));
        }

        static void Stroke(DrawingGroup group, Geometry geometry, Brush brush, double thickness)
        {
            group.Children.Add(new GeometryDrawing(null, new Pen(brush, thickness), geometry));
        }

        static Geometry Poly(params double[] points)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(new Point(points[0], points[1]), true, true);
                for (int i = 2; i + 1 < points.Length; i += 2)
                    context.LineTo(new Point(points[i], points[i + 1]), true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        static Geometry Bezier(Point start, Point control1, Point control2, Point end, Point control3, Point control4, Point end2, bool close)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(start, true, close);
                context.BezierTo(control1, control2, end, true, false);
                context.BezierTo(control3, control4, end2, true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        static Geometry Circle(double x, double y, double radius)
        {
            return new EllipseGeometry(new Point(x, y), radius, radius);
        }

        static Geometry Ellipse(double x, double y, double radiusX, double radiusY)
        {
            return new EllipseGeometry(new Point(x, y), radiusX, radiusY);
        }

        static Brush Radial(Color center, Color edge)
        {
            var brush = new RadialGradientBrush(center, edge)
            {
                Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5
            };
            return brush;
        }

        static void BuildAurore(DrawingGroup group)
        {
            Background(group, Color.FromRgb(8, 13, 33), Color.FromRgb(47, 27, 76));
            Fill(group, Circle(51, 63, 31), Radial(Color.FromArgb(110, 255, 138, 150), Color.FromArgb(0, 255, 138, 150)));
            Fill(group, Circle(51, 63, 18), Radial(Color.FromRgb(255, 235, 186), Color.FromRgb(255, 112, 137)));
            Fill(group, Poly(0, 78, 18, 60, 36, 74, 56, 53, 78, 70, 100, 59, 100, 100, 0, 100),
                new LinearGradientBrush(Color.FromRgb(32, 20, 51), Color.FromRgb(12, 10, 24), new Point(0, 0), new Point(0, 1)));
            Fill(group, Poly(0, 86, 26, 73, 48, 82, 70, 67, 100, 82, 100, 100, 0, 100),
                new LinearGradientBrush(Color.FromRgb(17, 12, 33), Color.FromRgb(7, 7, 15), new Point(0, 0), new Point(0, 1)));
            Fill(group, Circle(22, 25, 1.1), Brushes.White);
            Fill(group, Circle(79, 18, 1.4), Brush(Color.FromArgb(190, 255, 255, 255)));
        }

        static void BuildNuit(DrawingGroup group)
        {
            Background(group, Color.FromRgb(4, 9, 24), Color.FromRgb(16, 38, 74));
            Fill(group, Circle(69, 29, 21), Radial(Color.FromArgb(80, 247, 234, 203), Color.FromArgb(0, 247, 234, 203)));
            Fill(group, Circle(69, 29, 13), Brush(Color.FromRgb(247, 234, 203)));
            Fill(group, Circle(74, 25, 13), Brush(Color.FromRgb(9, 18, 40)));
            Fill(group, Poly(0, 80, 22, 69, 43, 78, 64, 64, 84, 76, 100, 68, 100, 100, 0, 100),
                new LinearGradientBrush(Color.FromRgb(8, 20, 43), Color.FromRgb(3, 7, 16), new Point(0, 0), new Point(0, 1)));
            double[] stars = { 16, 20, 1.2, 31, 37, 0.8, 48, 16, 1.4, 84, 44, 0.9, 59, 50, 0.7, 24, 56, 0.8, 90, 18, 1.1 };
            for (int i = 0; i + 2 < stars.Length; i += 3)
                Fill(group, Circle(stars[i], stars[i + 1], stars[i + 2]), Brush(Color.FromArgb(220, 226, 240, 255)));
        }

        static void BuildVague(DrawingGroup group)
        {
            Background(group, Color.FromRgb(3, 34, 45), Color.FromRgb(13, 116, 144));
            Fill(group, Circle(74, 28, 17), Radial(Color.FromArgb(110, 253, 230, 138), Color.FromArgb(0, 253, 230, 138)));
            Fill(group, Circle(74, 28, 10), Brush(Color.FromRgb(253, 230, 138)));
            Fill(group, Poly(0, 62, 0, 100, 100, 100, 100, 62), Brush(Color.FromRgb(9, 79, 99)));
            Fill(group, Poly(0, 73, 0, 100, 100, 100, 100, 73), Brush(Color.FromRgb(7, 58, 74)));
            Fill(group, Poly(0, 84, 0, 100, 100, 100, 100, 84), Brush(Color.FromRgb(5, 38, 51)));
            Stroke(group, Poly(0, 62, 22, 57, 43, 63, 66, 56, 84, 61, 100, 57), Brush(Color.FromArgb(150, 165, 243, 252)), 1.5);
            Stroke(group, Poly(0, 73, 18, 69, 39, 75, 61, 68, 82, 73, 100, 69), Brush(Color.FromArgb(100, 165, 243, 252)), 1.2);
        }

        static void BuildFlamme(DrawingGroup group)
        {
            Background(group, Color.FromRgb(26, 10, 18), Color.FromRgb(76, 5, 25));
            Fill(group, Circle(50, 54, 36), Radial(Color.FromArgb(90, 249, 115, 22), Color.FromArgb(0, 249, 115, 22)));
            var outer = Bezier(new Point(50, 88), new Point(26, 66), new Point(31, 39), new Point(49, 17),
                new Point(51, 35), new Point(65, 43), new Point(67, 61), true);
            Fill(group, outer, new LinearGradientBrush(Color.FromRgb(253, 230, 138), Color.FromRgb(220, 38, 38), new Point(0.2, 0), new Point(0.8, 1)));
            var inner = Bezier(new Point(50, 83), new Point(38, 69), new Point(40, 54), new Point(50, 42),
                new Point(54, 55), new Point(61, 60), new Point(58, 72), true);
            Fill(group, inner, new LinearGradientBrush(Color.FromRgb(255, 247, 205), Color.FromRgb(251, 146, 60), new Point(0.3, 0), new Point(0.7, 1)));
            Fill(group, Circle(30, 30, 1.2), Brush(Color.FromArgb(200, 253, 186, 116)));
            Fill(group, Circle(72, 40, 1.5), Brush(Color.FromArgb(170, 253, 186, 116)));
            Fill(group, Circle(64, 22, 0.9), Brush(Color.FromArgb(170, 253, 186, 116)));
        }

        static void BuildForet(DrawingGroup group)
        {
            Background(group, Color.FromRgb(5, 26, 20), Color.FromRgb(6, 78, 59));
            Fill(group, Circle(75, 24, 14), Radial(Color.FromArgb(80, 209, 250, 229), Color.FromArgb(0, 209, 250, 229)));
            Fill(group, Circle(75, 24, 8), Brush(Color.FromRgb(209, 250, 229)));
            Fill(group, Ellipse(50, 70, 52, 17), Brush(Color.FromArgb(26, 167, 243, 208)));
            Fill(group, Poly(0, 88, 15, 51, 30, 88), Brush(Color.FromRgb(6, 48, 38)));
            Fill(group, Poly(18, 94, 38, 38, 58, 94), Brush(Color.FromRgb(5, 38, 31)));
            Fill(group, Poly(48, 90, 67, 43, 86, 90), Brush(Color.FromRgb(4, 30, 25)));
            Fill(group, Poly(70, 96, 86, 55, 100, 96), Brush(Color.FromRgb(3, 23, 19)));
            Fill(group, Poly(0, 92, 100, 92, 100, 100, 0, 100), Brush(Color.FromRgb(3, 17, 14)));
        }

        static void BuildCosmos(DrawingGroup group)
        {
            Background(group, Color.FromRgb(11, 6, 26), Color.FromRgb(46, 16, 101));
            Fill(group, Circle(48, 52, 28), Radial(Color.FromArgb(70, 167, 139, 250), Color.FromArgb(0, 167, 139, 250)));
            var planet = Circle(48, 52, 17);
            Fill(group, planet, new LinearGradientBrush(Color.FromRgb(221, 214, 254), Color.FromRgb(109, 40, 217), new Point(0.2, 0), new Point(0.8, 1)));
            var ring = Ellipse(48, 52, 33, 10);
            ring.Transform = new RotateTransform(-18, 48, 52);
            Stroke(group, ring, Brush(Color.FromArgb(190, 196, 181, 253)), 2.4);
            double[] stars = { 18, 24, 1.2, 31, 70, 0.8, 80, 18, 1.4, 84, 72, 1.0, 61, 13, 0.7, 15, 55, 0.9, 90, 43, 0.8 };
            for (int i = 0; i + 2 < stars.Length; i += 3)
                Fill(group, Circle(stars[i], stars[i + 1], stars[i + 2]), Brush(Color.FromArgb(220, 237, 233, 254)));
        }

        static void BuildOr(DrawingGroup group)
        {
            Background(group, Color.FromRgb(28, 18, 6), Color.FromRgb(120, 53, 15));
            Fill(group, Circle(50, 55, 34), Radial(Color.FromArgb(90, 251, 191, 36), Color.FromArgb(0, 251, 191, 36)));
            Fill(group, Circle(50, 55, 14), new LinearGradientBrush(Color.FromRgb(254, 243, 199), Color.FromRgb(245, 158, 11), new Point(0.2, 0), new Point(0.8, 1)));
            for (int i = 0; i < 3; i++)
            {
                double radius = 25 + i * 13;
                Stroke(group, Ellipse(50, 55, radius, radius * 0.42), Brush(Color.FromArgb((byte)(160 - i * 36), 253, 230, 138)), 1.4);
            }
            Stroke(group, Poly(8, 88, 30, 88), Brush(Color.FromArgb(150, 253, 230, 138)), 1.2);
            Stroke(group, Poly(70, 88, 92, 88), Brush(Color.FromArgb(150, 253, 230, 138)), 1.2);
            Fill(group, Circle(22, 24, 1.3), Brush(Color.FromArgb(210, 254, 243, 199)));
            Fill(group, Circle(79, 34, 1.0), Brush(Color.FromArgb(180, 254, 243, 199)));
        }

        static void BuildGlacier(DrawingGroup group)
        {
            Background(group, Color.FromRgb(11, 27, 43), Color.FromRgb(22, 78, 99));
            Fill(group, Circle(73, 23, 14), Radial(Color.FromArgb(90, 224, 242, 254), Color.FromArgb(0, 224, 242, 254)));
            Fill(group, Circle(73, 23, 8), Brush(Color.FromRgb(224, 242, 254)));
            Fill(group, Poly(0, 100, 28, 39, 52, 100), Brush(Color.FromArgb(120, 103, 232, 249)));
            Fill(group, Poly(28, 100, 61, 22, 92, 100), Brush(Color.FromArgb(100, 165, 243, 252)));
            Fill(group, Poly(56, 100, 82, 51, 100, 100), Brush(Color.FromArgb(135, 34, 211, 238)));
            Stroke(group, Poly(28, 100, 61, 22, 92, 100), Brush(Color.FromArgb(150, 207, 250, 254)), 1.1);
            Fill(group, Poly(0, 100, 100, 100, 100, 94, 0, 96), Brush(Color.FromRgb(7, 21, 34)));
        }
    }
}
