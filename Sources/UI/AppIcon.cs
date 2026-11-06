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
    /// Génère l'icône multi-résolutions de LyaranTV à partir du même dessin
    /// vectoriel que la fenêtre et la page À propos, ainsi qu'une planche de
    /// contrôle du logo final.
    /// </summary>
    internal static class AppIcon
    {
        static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        public static void Save(string path)
        {
            var pngs = new List<byte[]>();
            foreach (int size in Sizes) pngs.Add(RenderPng(size));

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)Sizes.Length);
                int offset = 6 + 16 * Sizes.Length;
                for (int i = 0; i < Sizes.Length; i++)
                {
                    writer.Write((byte)(Sizes[i] >= 256 ? 0 : Sizes[i]));
                    writer.Write((byte)(Sizes[i] >= 256 ? 0 : Sizes[i]));
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)pngs[i].Length);
                    writer.Write((uint)offset);
                    offset += pngs[i].Length;
                }
                foreach (var png in pngs) writer.Write(png);
            }
        }

        static byte[] RenderPng(int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) MainWindow.DrawLyaranMark(dc, size, true);
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>Planche de contrôle : le logo final aux tailles réelles.</summary>
        public static void SavePreview(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            const int margin = 30, gap = 22, header = 78, labels = 42;
            int width = margin * 2;
            foreach (int size in sizes) width += size + gap;
            width -= gap;
            int height = header + 256 + labels + margin;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(13, 13, 24), Color.FromRgb(25, 22, 45), 90),
                    null, new Rect(0, 0, width, height));
                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                    FontWeights.SemiBold, FontStretches.Normal);
                var title = new FormattedText("LyaranTV · logo final", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, 22, Brushes.White, 1.0);
                dc.DrawText(title, new Point(margin, 24));
                var subtitle = new FormattedText("Proposition n° 24 « Voile », sans triangle de lecture · tailles réelles 16 à 256 px",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12,
                    new SolidColorBrush(Color.FromRgb(178, 178, 198)), 1.0);
                dc.DrawText(subtitle, new Point(margin, 54));

                int x = margin;
                int baseline = header + 256;
                foreach (int size in sizes)
                {
                    double y = header + (256 - size) / 2.0;
                    var group = new DrawingGroup();
                    using (var logo = group.Open()) MainWindow.DrawLyaranMark(logo, size, true);
                    dc.DrawImage(new DrawingImage(group), new Rect(x, y, size, size));
                    var label = new FormattedText(size + " px", CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, 11,
                        new SolidColorBrush(Color.FromRgb(160, 158, 184)), 1.0);
                    dc.DrawText(label, new Point(x, baseline + 10));
                    x += size + gap;
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
