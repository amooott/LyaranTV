using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyaranTV
{
    /// <summary>
    /// Échelle typographique unique de l'application : chaque taille a un rôle,
    /// ce qui évite les valeurs choisies au cas par cas et donne aux écrans une
    /// hiérarchie constante. Les valeurs basses restent au-dessus du plancher de
    /// lisibilité appliqué par le créateur de textes.
    /// </summary>
    internal static class TypeScale
    {
        public const double Display = 34;   // titre d'une fiche
        public const double Title = 27;     // titre de page
        public const double Section = 19;   // étagère, section
        public const double Subtitle = 18;
        public const double Body = 13;
        public const double Meta = 12.5;
        public const double Caption = 11.5;
        public const double Micro = 10.5;
    }

    /// <summary>
    /// Teinte dominante d'une affiche : elle sert à éclairer une fiche aux
    /// couleurs de l'œuvre plutôt qu'à celles de l'application. L'échantillon
    /// est réduit à 18 × 18 pixels, donc le calcul reste gratuit.
    /// </summary>
    internal static class ArtworkColor
    {
        public static Color Extract(BitmapSource source)
        {
            if (source == null || source.PixelWidth <= 0 || source.PixelHeight <= 0) return Colors.Transparent;
            try
            {
                const int size = 18;
                BitmapSource sample = source;
                if (source.PixelWidth > size || source.PixelHeight > size)
                {
                    sample = new TransformedBitmap(source, new ScaleTransform(
                        (double)size / source.PixelWidth, (double)size / source.PixelHeight));
                }
                var converted = new FormatConvertedBitmap(sample, PixelFormats.Bgra32, null, 0);
                int width = converted.PixelWidth, height = converted.PixelHeight;
                int stride = width * 4;
                var pixels = new byte[stride * height];
                converted.CopyPixels(pixels, stride, 0);

                double weightSum = 0, red = 0, green = 0, blue = 0;
                for (int i = 0; i + 3 < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] < 40) continue;
                    double b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    double max = Math.Max(r, Math.Max(g, b)) / 255.0;
                    double min = Math.Min(r, Math.Min(g, b)) / 255.0;
                    double saturation = max <= 0.0001 ? 0 : (max - min) / max;
                    // Un gris n'apporte rien : plus le pixel est coloré, plus il
                    // pèse dans la moyenne. La luminance module ensuite le poids,
                    // pour éviter que les noirs profonds écrasent la teinte.
                    double weight = Math.Pow(saturation, 2.2) * (0.30 + max * 0.70) + 0.0001;
                    weightSum += weight; red += r * weight; green += g * weight; blue += b * weight;
                }
                if (weightSum <= 0.0002) return Colors.Transparent;
                var average = Color.FromRgb(
                    (byte)Math.Round(red / weightSum), (byte)Math.Round(green / weightSum), (byte)Math.Round(blue / weightSum));
                return Temper(average);
            }
            catch { return Colors.Transparent; }
        }

        /// <summary>
        /// Ramène la teinte dans une plage utilisable comme éclairage : assez
        /// saturée pour se voir, assez sombre pour ne jamais éblouir sur une
        /// interface sombre.
        /// </summary>
        static Color Temper(Color color)
        {
            double hue, saturation, luminance;
            ToHsl(color, out hue, out saturation, out luminance);
            if (saturation < 0.46) saturation = 0.46;
            if (saturation > 0.92) saturation = 0.92;
            if (luminance < 0.40) luminance = 0.40;
            if (luminance > 0.62) luminance = 0.62;
            return FromHsl(hue, saturation, luminance);
        }

        static void ToHsl(Color color, out double hue, out double saturation, out double luminance)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            luminance = (max + min) / 2;
            hue = 0; saturation = 0;
            if (Math.Abs(max - min) < 0.0001) return;
            double delta = max - min;
            saturation = luminance > 0.5 ? delta / (2 - max - min) : delta / (max + min);
            if (max == r) hue = ((g - b) / delta + (g < b ? 6 : 0)) / 6;
            else if (max == g) hue = ((b - r) / delta + 2) / 6;
            else hue = ((r - g) / delta + 4) / 6;
        }

        static Color FromHsl(double hue, double saturation, double luminance)
        {
            if (saturation <= 0)
            {
                byte grey = (byte)Math.Round(luminance * 255);
                return Color.FromRgb(grey, grey, grey);
            }
            double q = luminance < 0.5 ? luminance * (1 + saturation) : luminance + saturation - luminance * saturation;
            double p = 2 * luminance - q;
            return Color.FromRgb(Channel(p, q, hue + 1.0 / 3), Channel(p, q, hue), Channel(p, q, hue - 1.0 / 3));
        }

        static byte Channel(double p, double q, double value)
        {
            if (value < 0) value += 1;
            if (value > 1) value -= 1;
            double result;
            if (value < 1.0 / 6) result = p + (q - p) * 6 * value;
            else if (value < 1.0 / 2) result = q;
            else if (value < 2.0 / 3) result = p + (q - p) * (2.0 / 3 - value) * 6;
            else result = p;
            return (byte)Math.Round(Math.Max(0, Math.Min(1, result)) * 255);
        }
    }

    /// <summary>
    /// Sons d'interface : quelques notes très courtes, synthétisées à la demande
    /// pour ne rien embarquer dans l'installateur. Le volume est volontairement
    /// bas et l'utilisateur peut tout couper depuis les réglages.
    /// </summary>
    internal static class UiSound
    {
        public static bool Enabled = true;

        static readonly Dictionary<string, SoundPlayer> Players = new Dictionary<string, SoundPlayer>(StringComparer.Ordinal);
        static readonly object Gate = new object();

        public static void Navigate() { Play("nav", 528, 85, 0.15); }
        public static void Confirm() { Play("ok", 660, 130, 0.17); }
        public static void Launch() { Play("launch", 392, 260, 0.20); }

        static void Play(string key, double frequency, int durationMs, double amplitude)
        {
            if (!Enabled) return;
            try
            {
                SoundPlayer player;
                lock (Gate)
                {
                    if (!Players.TryGetValue(key, out player))
                    {
                        player = new SoundPlayer(new MemoryStream(BuildWave(frequency, durationMs, amplitude)));
                        player.Load();
                        Players[key] = player;
                    }
                }
                player.Play();
            }
            catch { }
        }

        /// <summary>
        /// Petit WAV 16 bits mono : une fondamentale et son octave, attaque très
        /// courte puis extinction quadratique. Aucun fichier, aucune dépendance.
        /// </summary>
        static byte[] BuildWave(double frequency, int durationMs, double amplitude)
        {
            const int rate = 44100;
            int samples = Math.Max(1, rate * Math.Max(20, durationMs) / 1000);
            using (var stream = new MemoryStream(44 + samples * 2))
            using (var writer = new BinaryWriter(stream))
            {
                WriteTag(writer, "RIFF");
                writer.Write(36 + samples * 2);
                WriteTag(writer, "WAVE");
                WriteTag(writer, "fmt ");
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                WriteTag(writer, "data");
                writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate;
                    double progress = (double)i / samples;
                    double envelope = Math.Min(1, progress / 0.05) * Math.Pow(1 - progress, 2.6);
                    double wave = Math.Sin(2 * Math.PI * frequency * t) + 0.28 * Math.Sin(2 * Math.PI * frequency * 2.01 * t);
                    double value = Math.Max(-1, Math.Min(1, wave * envelope * amplitude));
                    writer.Write((short)Math.Round(value * 32767));
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        static void WriteTag(BinaryWriter writer, string tag)
        {
            foreach (char letter in tag) writer.Write((byte)letter);
        }
    }
}
