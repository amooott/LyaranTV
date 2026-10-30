using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LyaranTV
{
    /// <summary>
    /// Décrit une piste audio ou de sous-titres trouvée dans un fichier local.
    /// </summary>
    internal sealed class MediaTrackInfo
    {
        public string Kind = "";
        public string Container = "";
        public long TrackNumber;
        public int Ordinal;
        public string Codec = "";
        public string CodecLabel = "";
        public string LanguageCode = "";
        public string Title = "";
        public bool IsDefault;
        public bool IsForced;
        public bool IsText;
        public bool IsSelectable = true;
        public int Channels;
        /// <summary>Nom du fichier lu, utilisé quand la piste ne porte aucun titre.</summary>
        public string NameHint = "";
        internal object ContainerData;

        /// <summary>VF, VOSTFR, VO… lu dans le titre de la piste ou le nom du fichier.</summary>
        public string VersionTag
        {
            get
            {
                string tag = MediaTrackScanner.DetectVersionTag(Title);
                if (tag.Length == 0) tag = MediaTrackScanner.DetectVersionTag(NameHint);
                return tag;
            }
        }

        /// <summary>
        /// Libellé complet, à la manière de MyTVOnline : langue, version (VF/VO),
        /// disposition des canaux et codec, par exemple « Français · VF · 5.1 · AC-3 ».
        /// </summary>
        public string Label
        {
            get
            {
                var parts = new List<string>();
                string language = MediaTrackScanner.LanguageName(LanguageCode, "");
                if (String.IsNullOrWhiteSpace(language)) language = (Kind == "Audio" ? "Audio " : "Sous-titre ") + Ordinal.ToString(CultureInfo.InvariantCulture);
                parts.Add(language);
                string version = VersionTag;
                if (version.Length > 0) parts.Add(version);
                if (TitleAddsInfo(Title, language, version)) parts.Add(TextUtil.Ellipsis(Title.Trim(), 44));
                if (Kind == "Audio")
                {
                    // Le conteneur ne déclare pas toujours le nombre de canaux :
                    // on le lit alors dans le titre (« 5.1 », « 7.1 », « AC-3 6ch »)
                    // pour que le menu affiche toujours la langue et le format.
                    int channels = Channels > 0 ? Channels : MediaTrackScanner.InferChannels(Title + " " + NameHint);
                    if (channels > 0) parts.Add(ChannelLabel(channels));
                    if (!String.IsNullOrWhiteSpace(CodecLabel)) parts.Add(CodecLabel);
                    if (IsDefault) parts.Add("par défaut");
                }
                else
                {
                    if (!String.IsNullOrWhiteSpace(CodecLabel)) parts.Add(CodecLabel);
                    if (IsForced) parts.Add("forcé");
                    if (!IsSelectable) parts.Add("image, non pris en charge");
                }
                return String.Join("  ·  ", parts.ToArray());
            }
        }

        /// <summary>Évite « Français · Français (VFF) » : le titre n'est gardé que s'il apporte une information.</summary>
        static bool TitleAddsInfo(string title, string language, string version)
        {
            if (String.IsNullOrWhiteSpace(title)) return false;
            string value = title.Trim();
            if (String.Equals(value, language, StringComparison.OrdinalIgnoreCase)) return false;
            if (version.Length > 0 && value.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (value.StartsWith("audio", StringComparison.OrdinalIgnoreCase) && value.Length <= 12) return false;
            if (value.StartsWith("sous-titre", StringComparison.OrdinalIgnoreCase) && value.Length <= 17) return false;
            if (!String.IsNullOrWhiteSpace(language) && value.IndexOf(language, StringComparison.OrdinalIgnoreCase) >= 0 && value.Length <= language.Length + 8) return false;
            return true;
        }

        static string ChannelLabel(int channels)
        {
            return MediaTrackScanner.ChannelName(channels);
        }
    }

    internal sealed class MediaTrackScan
    {
        public string Path = "";
        public string Container = "";
        public List<MediaTrackInfo> Audio = new List<MediaTrackInfo>();
        public List<MediaTrackInfo> Subtitles = new List<MediaTrackInfo>();
    }

    /// <summary>
    /// Inspecte les conteneurs MP4/MOV et MKV/WebM. Les pistes textuelles sont
    /// extraites en mémoire : le lecteur WPF n'expose ni les sous-titres
    /// embarqués ni la sélection de piste audio.
    /// </summary>
    internal static class MediaTrackScanner
    {
        public static MediaTrackScan Scan(string path)
        {
            var scan = new MediaTrackScan { Path = path ?? "" };
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return scan;
            try
            {
                string container = DetectContainer(path);
                scan.Container = container;
                if (container == "MP4") Mp4TrackReader.Probe(scan);
                else if (container == "MKV") MkvTrackReader.Probe(scan);
                AssignOrdinals(scan.Audio);
                AssignOrdinals(scan.Subtitles);
                // Un seul fichier audio : le nom du fichier porte la version (VFF,
                // TRUEFRENCH…). Avec plusieurs pistes, on s'abstient pour ne pas
                // étiqueter à tort la piste anglaise.
                if (scan.Audio.Count == 1)
                    scan.Audio[0].NameHint = Path.GetFileNameWithoutExtension(path);
            }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("Analyse des pistes (" + Path.GetFileName(path) + ") : " + ex.GetBaseException().Message); }
                catch { }
            }
            return scan;
        }

        public static SubtitleTrack LoadEmbeddedSubtitle(MediaTrackScan scan, MediaTrackInfo info)
        {
            if (scan == null || info == null || !info.IsSelectable || String.IsNullOrWhiteSpace(scan.Path)) return null;
            try
            {
                SubtitleTrack track = scan.Container == "MKV"
                    ? MkvTrackReader.ExtractSubtitle(scan.Path, info)
                    : Mp4TrackReader.ExtractSubtitle(scan.Path, info);
                if (track == null || track.Cues.Count == 0) return null;
                track.Name = info.Label;
                track.Path = scan.Path;
                return track;
            }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("Extraction des sous-titres (" + info.Codec + ") : " + ex.GetBaseException().Message); }
                catch { }
                return null;
            }
        }

        static void AssignOrdinals(List<MediaTrackInfo> tracks)
        {
            for (int i = 0; i < tracks.Count; i++) tracks[i].Ordinal = i + 1;
        }

        /// <summary>
        /// Trie les sous-titres et complète les durées absentes (ASS/SSA dans
        /// MKV n'en fournissent généralement pas) sans laisser de plages
        /// chevauchantes qui perturberaient la recherche binaire du lecteur.
        /// </summary>
        public static void NormalizeCues(SubtitleTrack track)
        {
            if (track == null || track.Cues.Count == 0) return;
            track.Cues.Sort(delegate(SubtitleCue first, SubtitleCue second) { return first.Start.CompareTo(second.Start); });
            for (int i = 0; i < track.Cues.Count; i++)
            {
                SubtitleCue cue = track.Cues[i];
                if (cue.End <= cue.Start)
                {
                    double next = i + 1 < track.Cues.Count ? track.Cues[i + 1].Start : cue.Start + 4;
                    cue.End = next > cue.Start ? next : cue.Start + 4;
                }
                if (i + 1 < track.Cues.Count && cue.End > track.Cues[i + 1].Start)
                    cue.End = track.Cues[i + 1].Start;
                if (cue.End <= cue.Start) cue.End = cue.Start + 0.5;
            }
        }

        static string DetectContainer(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var header = new byte[12];
                    int read = stream.Read(header, 0, header.Length);
                    if (read >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
                        return "MKV";
                    if (read >= 8 && header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p')
                        return "MP4";
                }
            }
            catch { }
            if (extension == ".mkv" || extension == ".mka" || extension == ".webm") return "MKV";
            if (extension == ".mp4" || extension == ".m4v" || extension == ".mov" || extension == ".m4a") return "MP4";
            return "";
        }

        public static string LanguageName(string code, string fallback)
        {
            string key = (code ?? "").Trim().ToLowerInvariant();
            if (key.Length > 3)
            {
                int dash = key.IndexOf('-');
                if (dash > 0) key = key.Substring(0, dash);
            }
            if (key == "und" || key == "undetermined" || key == "zxx" || key == "mis") key = "";
            if (key.Length == 0) return fallback ?? "";
            switch (key)
            {
                case "fr": case "fre": case "fra": return "Français";
                case "en": case "eng": return "Anglais";
                case "es": case "spa": return "Espagnol";
                case "de": case "ger": case "deu": return "Allemand";
                case "it": case "ita": return "Italien";
                case "pt": case "por": return "Portugais";
                case "nl": case "dut": case "nld": return "Néerlandais";
                case "ru": case "rus": return "Russe";
                case "ja": case "jpn": return "Japonais";
                case "zh": case "chi": case "zho": return "Chinois";
                case "ko": case "kor": return "Coréen";
                case "ar": case "ara": return "Arabe";
                case "hi": case "hin": return "Hindi";
                case "tr": case "tur": return "Turc";
                case "pl": case "pol": return "Polonais";
                case "sv": case "swe": return "Suédois";
                case "no": case "nor": return "Norvégien";
                case "da": case "dan": return "Danois";
                case "fi": case "fin": return "Finnois";
                case "cs": case "cze": case "ces": return "Tchèque";
                case "el": case "gre": case "ell": return "Grec";
                case "he": case "heb": return "Hébreu";
                case "th": case "tha": return "Thaï";
                case "vi": case "vie": return "Vietnamien";
                case "uk": case "ukr": return "Ukrainien";
                case "ro": case "ron": case "rum": return "Roumain";
                case "hu": case "hun": return "Hongrois";
                case "bg": case "bul": return "Bulgare";
                case "hr": case "hrv": return "Croate";
                case "sr": case "srp": return "Serbe";
                case "sk": case "slo": case "slk": return "Slovaque";
                case "ca": case "cat": return "Catalan";
                case "eu": case "baq": case "eus": return "Basque";
                case "gl": case "glg": return "Galicien";
                case "fa": case "per": case "fas": return "Persan";
                case "id": case "ind": return "Indonésien";
                case "ms": case "may": case "msa": return "Malais";
                case "tl": case "tgl": return "Tagalog";
                default: return key.Length == 2 ? key.ToUpperInvariant() : fallback ?? key;
            }
        }

        /// <summary>
        /// Nom de langue traduit, ou chaîne vide quand le code n'est pas reconnu.
        /// Contrairement à <see cref="LanguageName"/>, un code inconnu n'est pas
        /// renvoyé tel quel : les menus audio n'affichent ainsi jamais « VF »
        /// comme s'il s'agissait d'une langue.
        /// </summary>
        public static string KnownLanguageName(string code)
        {
            string value = (code ?? "").Trim();
            if (value.Length == 0) return "";
            string label = LanguageName(value, "");
            if (label.Length == 0) return "";
            if (String.Equals(label, value, StringComparison.OrdinalIgnoreCase)) return "";
            if (String.Equals(label, value.ToUpperInvariant(), StringComparison.Ordinal)) return "";
            return label;
        }

        /// <summary>
        /// Devine la disposition des canaux quand le conteneur ne la déclare pas :
        /// « 5.1 », « 7.1 », « 2.0 », « 3/2 », « 6ch », « stéréo », « mono »...
        /// Les libellés de pistes affichent ainsi toujours le format audio.
        /// </summary>
        public static int InferChannels(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return 0;
            string value = " " + text.ToLowerInvariant() + " ";
            string[] layouts = { "7.1", "6.1", "5.1", "4.1", "3.1", "2.1", "5.0", "4.0", "3.0", "2.0", "1.0" };
            foreach (string layout in layouts)
                if (value.IndexOf(layout, StringComparison.Ordinal) >= 0) return LayoutChannels(layout);
            // Annotation Matroska/VLC : « 3/2/2 » (7.1), « 3/2 » (5.1), « 2/0 » (stéréo).
            foreach (string token in value.Split(new[] { ' ', ',', ';', '·', '|', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.IndexOf('/') <= 0) continue;
                string[] parts = token.Split('/');
                int total = 0;
                bool valid = parts.Length >= 2;
                foreach (string part in parts)
                {
                    int number;
                    if (!Int32.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < 0 || number > 8) { valid = false; break; }
                    total += number;
                }
                if (valid && total > 0) return total;
            }
            foreach (string token in value.Split(new[] { ' ', '·', '|', ',', ';', '(', ')' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string digits = token.EndsWith("ch", StringComparison.Ordinal) ? token.Substring(0, token.Length - 2)
                    : token.EndsWith("canaux", StringComparison.Ordinal) ? token.Substring(0, token.Length - 6) : "";
                int number;
                if (digits.Length > 0 && Int32.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                    && number >= 1 && number <= 16) return number;
            }
            if (value.IndexOf("stereo", StringComparison.Ordinal) >= 0 || value.IndexOf("stéréo", StringComparison.Ordinal) >= 0) return 2;
            if (value.IndexOf("mono", StringComparison.Ordinal) >= 0) return 1;
            return 0;
        }

        static int LayoutChannels(string layout)
        {
            switch (layout)
            {
                case "7.1": return 8;
                case "6.1": return 7;
                case "5.1": return 6;
                case "4.1": return 5;
                case "3.1": return 4;
                case "2.1": return 3;
                case "5.0": return 5;
                case "4.0": return 4;
                case "3.0": return 3;
                case "2.0": return 2;
                case "1.0": return 1;
                default: return 0;
            }
        }

        /// <summary>Disposition des canaux, comme dans les fiches de pistes.</summary>
        public static string ChannelName(int channels)
        {
            if (channels <= 0) return "";
            if (channels == 1) return "mono";
            if (channels == 2) return "stéréo";
            if (channels == 3) return "3.0";
            if (channels == 4) return "4.0";
            if (channels == 5) return "5.0";
            if (channels == 6) return "5.1";
            if (channels == 7) return "6.1";
            if (channels == 8) return "7.1";
            return channels.ToString(CultureInfo.InvariantCulture) + " canaux";
        }

        /// <summary>
        /// Reconnaît la version de langue d'une piste à partir de son titre ou du
        /// nom du fichier : VFF/VF, VOSTFR, VO, TRUEFRENCH, MULTI…
        /// </summary>
        public static string DetectVersionTag(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return "";
            string[] tokens = text.ToUpperInvariant().Split(new[] { ' ', '.', '_', '-', '[', ']', '(', ')', '/', '\\', ',', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (token == "VOSTFR" || token == "VOSTF" || token == "VOST") return "VOSTFR";
                if (token == "TRUEFRENCH") return "TRUEFRENCH";
                if (token == "VFF" || token == "VF" || token == "VF2") return "VF";
                if (token == "VFQ") return "VFQ";
                if (token == "VO" || token == "VOA") return "VO";
                if (token == "MULTI") return "MULTI";
            }
            return "";
        }

        /// <summary>
        /// Traduit un code de codec (codec MKV, fourcc MP4 ou fourcc VLC) en nom
        /// lisible. Couvre les codecs audio que VLC décode : AC-3, E-AC-3, DTS et
        /// DTS-HD, TrueHD/MLP, AAC, MP3, Opus, Vorbis, FLAC, ALAC, PCM, WMA, APE,
        /// WavPack, TTA, DSD, AMR… ainsi que les sous-titres intégrés.
        /// </summary>
        public static string CodecName(string codec)
        {
            string value = (codec ?? "").Trim();
            if (value.Length == 0) return "";
            switch (value.ToLowerInvariant())
            {
                case "a_aac": case "mp4a": case "aac": case "aac ": return "AAC";
                case "a_ac3": case "ac-3": case "ac3": case "ac-3 ": return "AC-3";
                case "a_eac3": case "ec-3": case "eac3": case "ec3": return "E-AC-3";
                case "a_dts": case "dtsc": case "dtsh": case "dtsl": case "dtse": case "dts": return "DTS";
                case "a_truehd": case "truehd": case "trhd": return "TrueHD";
                case "a_mlp": case "mlpa": case "mlp": return "MLP";
                case "a_mpeg/l3": case ".mp3": case "mp3": case "mp3 ": case "mpga": return "MP3";
                case "a_mpeg/l2": case "mp2": return "MP2";
                case "a_mpeg/l1": case "mp1": return "MP1";
                case "a_opus": case "opus": return "Opus";
                case "a_vorbis": case "vorb": case "vorbis": return "Vorbis";
                case "a_flac": case "flac": return "FLAC";
                case "a_alac": case "alac": return "ALAC";
                case "a_pcm/int/lit": case "a_pcm/int/big": case "a_pcm/float/ieee":
                case "sowt": case "twos": case "in24": case "in32": case "fl32": case "fl64":
                case "lpcm": case "s16l": case "s24l": case "s32l": case "u8": case "raw ": return "PCM";
                case "a_ms/acm": return "Audio";
                case "wma1": case "wma2": case "wmap": case "wmav": case "wma": return "WMA";
                case "ape": case "ape ": return "Monkey's Audio";
                case "wvpk": case "wavpack": return "WavPack";
                case "tta": case "tta ": return "TTA";
                case "mpc": case "mpc7": case "mpc8": return "Musepack";
                case "dsd ": case "dsf ": case "dff ": return "DSD";
                case "samr": return "AMR";
                case "sawb": return "AMR-WB";
                case "speex": return "Speex";
                case "cook": return "Cook";
                case "qdm2": return "QDM2";
                case "ulaw": return "µ-law";
                case "alaw": return "A-law";
                case "s_text/utf8": case "s_text/ascii": case "s_text/utf-8": return "SubRip";
                case "s_text/ass": case "s_ass": return "ASS";
                case "s_text/ssa": case "s_ssa": return "SSA";
                case "s_text/webvtt": case "d_webvtt/subtitles": case "wvtt": return "WebVTT";
                case "tx3g": case "text": return "Texte";
                case "stpp": return "TTML";
                case "s_hdmv/pgs": case "pgs": return "PGS";
                case "s_vobsub": case "vobsub": return "VobSub";
                case "s_hdmv/textst": return "TextST";
                case "c608": return "CEA-608";
                case "c708": return "CEA-708";
                default: return value;
            }
        }
    }
}
