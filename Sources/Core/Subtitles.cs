using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LyaranTV
{
    internal sealed class SubtitleCue
    {
        public double Start;
        public double End;
        public string Text;
    }

    internal sealed class SubtitleTrack
    {
        public string Path = "";
        public string Name = "";
        public string Format = "";
        public List<SubtitleCue> Cues = new List<SubtitleCue>();
        int lastIndex;

        public SubtitleCue At(double seconds)
        {
            if (Cues.Count == 0) return null;
            if (lastIndex >= 0 && lastIndex < Cues.Count)
            {
                SubtitleCue current = Cues[lastIndex];
                if (seconds >= current.Start && seconds <= current.End) return current;
            }
            int low = 0;
            int high = Cues.Count - 1;
            while (low <= high)
            {
                int middle = (low + high) / 2;
                SubtitleCue cue = Cues[middle];
                if (seconds < cue.Start) high = middle - 1;
                else if (seconds > cue.End) low = middle + 1;
                else { lastIndex = middle; return cue; }
            }
            return null;
        }
    }

    internal static class SubtitleParser
    {
        static readonly Regex TimePattern = new Regex(
            @"(?<start>\d{1,2}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>\d{1,2}:\d{2}:\d{2}[,.]\d{1,3})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex TagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        static readonly Regex StylePattern = new Regex(@"\{\\[^}]*\}", RegexOptions.Compiled);
        static readonly Regex SpacePattern = new Regex(@"[ \t]+", RegexOptions.Compiled);
        static readonly Regex EntityPattern = new Regex(@"&(?:#(?<code>\d+)|(?<name>[a-zA-Z]+));", RegexOptions.Compiled);

        public static SubtitleTrack Parse(string content, string name, string path)
        {
            var track = new SubtitleTrack
            {
                Name = name ?? "",
                Path = path ?? "",
                Format = LooksLikeWebVtt(content) ? "WebVTT" : "SubRip"
            };
            if (String.IsNullOrEmpty(content)) return track;
            string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            int index = 0;
            while (index < lines.Length)
            {
                string line = lines[index].Trim();
                if (line.Length == 0) { index++; continue; }
                Match match = TimePattern.Match(line);
                if (!match.Success) { index++; continue; }
                double start;
                double end;
                if (!TryParseTime(match.Groups["start"].Value, out start) || !TryParseTime(match.Groups["end"].Value, out end))
                {
                    index++;
                    continue;
                }
                index++;
                var body = new StringBuilder();
                while (index < lines.Length)
                {
                    string row = lines[index];
                    if (row.Trim().Length == 0) { index++; break; }
                    if (TimePattern.IsMatch(row)) break;
                    if (body.Length > 0) body.Append('\n');
                    body.Append(row.Trim());
                    index++;
                }
                string text = CleanText(body.ToString());
                if (text.Length > 0 && end > start)
                    track.Cues.Add(new SubtitleCue { Start = start, End = end, Text = text });
            }
            track.Cues.Sort(delegate(SubtitleCue first, SubtitleCue second) { return first.Start.CompareTo(second.Start); });
            return track;
        }

        public static bool TryParseTime(string value, out double seconds)
        {
            seconds = 0;
            if (String.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Trim().Replace(',', '.').Split(':');
            if (parts.Length != 3) return false;
            int hours;
            int minutes;
            double remainder;
            if (!Int32.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours)) return false;
            if (!Int32.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes)) return false;
            if (!Double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out remainder)) return false;
            if (hours < 0 || minutes < 0 || remainder < 0) return false;
            seconds = hours * 3600.0 + minutes * 60.0 + remainder;
            return true;
        }

        public static string CleanText(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string cleaned = TagPattern.Replace(value, " ");
            cleaned = StylePattern.Replace(cleaned, " ");
            cleaned = DecodeEntities(cleaned);
            var rows = new List<string>();
            foreach (string raw in cleaned.Replace("\r", "").Split('\n'))
            {
                string row = SpacePattern.Replace(raw, " ").Trim();
                if (row.Length > 0) rows.Add(row);
            }
            return String.Join("\n", rows.ToArray());
        }

        static string DecodeEntities(string value)
        {
            return EntityPattern.Replace(value, delegate(Match match)
            {
                string code = match.Groups["code"].Value;
                if (code.Length > 0)
                {
                    int number;
                    if (Int32.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0 && number <= 0x10FFFF)
                    {
                        try { return Char.ConvertFromUtf32(number); } catch { return match.Value; }
                    }
                    return match.Value;
                }
                switch (match.Groups["name"].Value.ToLowerInvariant())
                {
                    case "amp": return "&";
                    case "lt": return "<";
                    case "gt": return ">";
                    case "quot": return "\"";
                    case "apos": return "'";
                    case "nbsp": return " ";
                    default: return match.Value;
                }
            });
        }

        static bool LooksLikeWebVtt(string content)
        {
            if (String.IsNullOrEmpty(content)) return false;
            string trimmed = content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return trimmed.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class SubtitleLoader
    {
        public static readonly string[] SupportedExtensions = { ".srt", ".vtt" };

        public static SubtitleTrack Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            string text = ReadText(path);
            string name = Path.GetFileName(path);
            var track = SubtitleParser.Parse(text, name, path);
            if (track.Cues.Count == 0) return null;
            return track;
        }

        public static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            try
            {
                var strict = new UTF8Encoding(false, true);
                return strict.GetString(bytes);
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        public static List<string> FindSidecars(string mediaPath)
        {
            var results = new List<string>();
            if (String.IsNullOrWhiteSpace(mediaPath)) return results;
            string path = TextUtil.LocalPathFromUrl(mediaPath);
            try
            {
                if (!File.Exists(path)) return results;
                string directory = Path.GetDirectoryName(path);
                if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return results;
                string baseName = Path.GetFileNameWithoutExtension(path);
                var exact = new List<string>();
                var variants = new List<string>();
                foreach (string candidate in Directory.GetFiles(directory))
                {
                    string extension = Path.GetExtension(candidate);
                    if (!IsSupported(extension)) continue;
                    string fileName = Path.GetFileName(candidate);
                    if (String.Equals(Path.GetFileNameWithoutExtension(fileName), baseName, StringComparison.OrdinalIgnoreCase))
                        exact.Add(candidate);
                    else if (fileName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase))
                        variants.Add(candidate);
                }
                exact.Sort(StringComparer.OrdinalIgnoreCase);
                variants.Sort(StringComparer.OrdinalIgnoreCase);
                results.AddRange(exact);
                results.AddRange(variants);
            }
            catch { }
            return results;
        }

        public static bool IsSupported(string extension)
        {
            if (String.IsNullOrWhiteSpace(extension)) return false;
            foreach (string supported in SupportedExtensions)
                if (String.Equals(extension, supported, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Format lisible d'un fichier de sous-titres externe.</summary>
        public static string SidecarFormat(string path)
        {
            string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
            if (extension == ".srt") return "SubRip";
            if (extension == ".vtt") return "WebVTT";
            return extension.TrimStart('.').ToUpperInvariant();
        }

        /// <summary>
        /// Langue reconnue dans le nom du fichier (« film.fr.srt »,
        /// « film.VOSTFR.srt »). Vide quand le nom ne déclare rien.
        /// </summary>
        public static string SidecarLanguage(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? "");
            return AudioStream.LanguageFromText(name);
        }

        /// <summary>
        /// Libellé de menu d'un sous-titre externe : langue, version et format,
        /// par exemple « Français · VOSTFR · SubRip ».
        /// </summary>
        public static string SidecarLabel(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            var parts = new List<string>();
            string language = SidecarLanguage(path);
            parts.Add(language.Length > 0 ? language : "Langue non déclarée");
            string version = MediaTrackScanner.DetectVersionTag(Path.GetFileNameWithoutExtension(path));
            if (version.Length > 0) parts.Add(version);
            string format = SidecarFormat(path);
            if (format.Length > 0) parts.Add(format);
            return String.Join("  ·  ", parts.ToArray());
        }

        public static string Filter
        {
            get { return "Sous-titres (*.srt;*.vtt)|*.srt;*.vtt|SubRip (*.srt)|*.srt|WebVTT (*.vtt)|*.vtt|Tous les fichiers (*.*)|*.*"; }
        }

        public static string DisplayName(SubtitleTrack track)
        {
            if (track == null) return "";
            string name = String.IsNullOrWhiteSpace(track.Name) ? Path.GetFileName(track.Path) : track.Name;
            if (String.IsNullOrWhiteSpace(name)) name = "Sous-titres";
            return name;
        }
    }
}
