using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LyaranTV
{
    /// <summary>
    /// Localise et charge libVLC installé sur la machine. Windows Media Foundation
    /// ne décode pas l'AC-3, l'E-AC-3, le DTS ni le TrueHD : ces pistes sortent
    /// muettes du MediaElement. libVLC, lui, décode tout, et sait aussi changer de
    /// piste audio. Le moteur reste facultatif : s'il manque, l'application revient
    /// au lecteur Windows.
    /// </summary>
    internal static class VlcRuntime
    {
        static bool probed;
        static bool available;
        static string directory = "";
        static string failure = "";

        public static bool Available
        {
            get { Probe(); return available; }
        }

        public static string Directory
        {
            get { Probe(); return directory; }
        }

        public static string Failure
        {
            get { Probe(); return failure; }
        }

        static void Probe()
        {
            if (probed) return;
            probed = true;
            foreach (string candidate in CandidateDirectories())
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(candidate)) continue;
                    string dir = Path.GetFullPath(candidate);
                    if (!File.Exists(Path.Combine(dir, "libvlc.dll"))) continue;
                    VlcPlayback.Native.SetDllDirectory(dir);
                    Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", Path.Combine(dir, "plugins"));
                    string version = Marshal.PtrToStringAnsi(VlcPlayback.Native.libvlc_get_version());
                    if (String.IsNullOrWhiteSpace(version)) continue;
                    available = true;
                    directory = dir;
                    failure = "";
                    // Trace le dossier retenu : indispensable pour savoir si
                    // c'est le libVLC embarqué ou une installation système.
                    HttpUtil.AppendLog("libVLC · " + version + " · " + dir);
                    return;
                }
                catch (Exception ex)
                {
                    failure = ex.GetBaseException().Message;
                }
            }
            if (failure.Length == 0) failure = "libVLC introuvable : VLC n'est pas installé.";
        }

        /// <summary>Dossiers essayés, du plus explicite au plus générique.</summary>
        static IEnumerable<string> CandidateDirectories()
        {
            // libVLC embarqué à côté du programme : la version livrée avec
            // LyaranTV est connue et complète, elle passe avant une installation
            // système qui pourrait être absente, ancienne ou 32 bits.
            string bundled = "";
            try { bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VLC"); }
            catch { }
            if (!String.IsNullOrWhiteSpace(bundled)) yield return bundled;

            string configured = "";
            try
            {
                AppSettings settings = SettingsStore.Load();
                if (settings != null && !String.IsNullOrWhiteSpace(settings.ExternalPlayer))
                {
                    string exe = settings.ExternalPlayer.Trim().Trim('"');
                    if (File.Exists(exe)) configured = Path.GetDirectoryName(exe);
                }
            }
            catch { }
            if (!String.IsNullOrWhiteSpace(configured)) yield return configured;

            string registry = "";
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VideoLAN\VLC"))
                {
                    if (key != null) registry = Convert.ToString(key.GetValue("InstallDir"), CultureInfo.InvariantCulture);
                }
                if (String.IsNullOrWhiteSpace(registry))
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\VideoLAN\VLC"))
                    {
                        if (key != null) registry = Convert.ToString(key.GetValue("InstallDir"), CultureInfo.InvariantCulture);
                    }
                }
            }
            catch { }
            if (!String.IsNullOrWhiteSpace(registry)) yield return registry;

            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!String.IsNullOrWhiteSpace(programFiles)) yield return Path.Combine(programFiles, "VideoLAN", "VLC");
            if (!String.IsNullOrWhiteSpace(programFilesX86)) yield return Path.Combine(programFilesX86, "VideoLAN", "VLC");

        }
    }

    /// <summary>
    /// Description complète d'une piste audio telle que libVLC la connaît :
    /// identifiant de lecture, langue, codec, canaux, fréquence et débit. Les
    /// flux distants (TS, HLS, MKV) n'ont souvent qu'un nom vide ; ces métadonnées
    /// permettent d'afficher « Français · VF · 5.1 · AC-3 » dans le menu.
    /// </summary>
    internal sealed class AudioStream
    {
        /// <summary>Identifiant libVLC utilisé par libvlc_audio_set_track.</summary>
        public int Id = -1;
        /// <summary>Position dans la liste, à partir de 1.</summary>
        public int Index;
        public string Name = "";
        public string Description = "";
        public string LanguageCode = "";
        public string Codec = "";
        public string CodecLabel = "";
        public int Channels;
        public int Rate;
        public int Bitrate;

        /// <summary>Titre lisible : nom VLC, sinon description du conteneur.</summary>
        public string Text
        {
            get
            {
                string value = (Name ?? "").Trim();
                if (value.Length == 0) value = (Description ?? "").Trim();
                return value;
            }
        }

        /// <summary>Langue traduite, vide quand le flux ne la déclare pas.</summary>
        public string Language
        {
            get
            {
                string label = MediaTrackScanner.KnownLanguageName(LanguageCode);
                if (label.Length > 0) return label;
                return LanguageFromText(Text);
            }
        }

        /// <summary>VF, VO, VOSTFR… reconnu dans le nom ou la description.</summary>
        public string Version
        {
            get { return MediaTrackScanner.DetectVersionTag(Text); }
        }

        public string CodecText
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(CodecLabel)) return CodecLabel;
                return MediaTrackScanner.CodecName(Codec);
            }
        }

        /// <summary>
        /// Complète les métadonnées de VLC avec l'analyse locale du fichier. Les
        /// flux TS/HLS publient parfois une langue « und » ou aucun canal : le
        /// conteneur, lui, connaît souvent « Français · 5.1 · AC-3 ».
        /// </summary>
        public void Enrich(MediaTrackInfo local)
        {
            if (local == null) return;
            if (String.IsNullOrWhiteSpace(MediaTrackScanner.KnownLanguageName(LanguageCode))
                && !String.IsNullOrWhiteSpace(MediaTrackScanner.KnownLanguageName(local.LanguageCode)))
                LanguageCode = local.LanguageCode;
            if (Channels <= 0) Channels = local.Channels;
            if (String.IsNullOrWhiteSpace(CodecText))
            {
                Codec = local.Codec;
                CodecLabel = local.CodecLabel;
            }
            if (String.IsNullOrWhiteSpace(Description)) Description = local.Title;
            if (String.IsNullOrWhiteSpace(Name) && String.IsNullOrWhiteSpace(Description)) Name = local.Title;
        }

        /// <summary>Libellé du menu : langue, version, titre, canaux, codec, débit.</summary>
        public string Label
        {
            get
            {
                var parts = new List<string>();
                string language = Language;
                if (language.Length == 0)
                    language = "Piste audio " + (Index > 0 ? Index : 1).ToString(CultureInfo.InvariantCulture);
                parts.Add(language);
                string version = Version;
                if (version.Length > 0) parts.Add(version);
                string title = Text;
                if (AddsInfo(title, language, version)) parts.Add(TextUtil.Ellipsis(title, 44));
                // Beaucoup de flux TS/HLS ne déclarent pas le nombre de canaux :
                // on le lit dans le nom de piste pour afficher « Français · 5.1 ».
                int channelCount = Channels > 0 ? Channels : MediaTrackScanner.InferChannels(Text + " " + Description);
                string channels = MediaTrackScanner.ChannelName(channelCount);
                if (channels.Length > 0) parts.Add(channels);
                string codec = CodecText;
                if (codec.Length > 0) parts.Add(codec);
                if (Bitrate >= 32000) parts.Add((Bitrate / 1000).ToString(CultureInfo.InvariantCulture) + " kb/s");
                return String.Join("  ·  ", parts.ToArray());
            }
        }

        /// <summary>Vrai si la piste correspond au code de langue des réglages.</summary>
        public bool MatchesLanguage(string code)
        {
            string key = (code ?? "").Trim().ToLowerInvariant();
            if (key.Length == 0 || key == "auto") return false;
            if (key.Length > 3) key = key.Substring(0, 2);
            string declared = (LanguageCode ?? "").Trim().ToLowerInvariant();
            if (declared.Length >= 2 && key.Length >= 2
                && String.Equals(declared.Substring(0, 2), key.Substring(0, 2), StringComparison.Ordinal))
                return true;
            string wanted = MediaTrackScanner.KnownLanguageName(key);
            if (wanted.Length > 0 && String.Equals(Language, wanted, StringComparison.OrdinalIgnoreCase)) return true;
            return VlcPlayback.TrackMatchesLanguage(Text, key);
        }

        static bool AddsInfo(string title, string language, string version)
        {
            if (String.IsNullOrWhiteSpace(title)) return false;
            string value = title.Trim();
            if (value.Length <= 3) return false;
            if (String.Equals(value, language, StringComparison.OrdinalIgnoreCase)) return false;
            if (value.StartsWith("audio", StringComparison.OrdinalIgnoreCase) && value.Length <= 12) return false;
            if (value.StartsWith("track", StringComparison.OrdinalIgnoreCase) && value.Length <= 10) return false;
            if (value.StartsWith("piste", StringComparison.OrdinalIgnoreCase) && value.Length <= 16) return false;
            if (String.Equals(value, "stereo", StringComparison.OrdinalIgnoreCase)
                || String.Equals(value, "mono", StringComparison.OrdinalIgnoreCase)) return false;
            bool hasLanguage = language.Length > 0 && value.IndexOf(language, StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasVersion = version.Length > 0 && value.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0;
            if (hasLanguage && (version.Length == 0 || hasVersion)) return false;
            if (hasVersion && value.Length <= version.Length + 8) return false;
            string titleLanguage = LanguageFromText(value);
            if (titleLanguage.Length > 0 && (language.Length == 0 || String.Equals(titleLanguage, language, StringComparison.OrdinalIgnoreCase)))
                return false;
            return true;
        }

        static readonly string[,] LanguageWords =
        {
            { "francais", "Français" }, { "français", "Français" }, { "french", "Français" },
            { "vf", "Français" }, { "vff", "Français" }, { "vfq", "Français" },
            { "vostfr", "Français" }, { "vostf", "Français" }, { "truefrench", "Français" },
            { "anglais", "Anglais" }, { "english", "Anglais" },
            { "espagnol", "Espagnol" }, { "spanish", "Espagnol" }, { "espanol", "Espagnol" },
            { "allemand", "Allemand" }, { "german", "Allemand" }, { "deutsch", "Allemand" },
            { "italien", "Italien" }, { "italian", "Italien" }, { "italiano", "Italien" },
            { "portugais", "Portugais" }, { "portuguese", "Portugais" },
            { "neerlandais", "Néerlandais" }, { "dutch", "Néerlandais" },
            { "russe", "Russe" }, { "russian", "Russe" }, { "japonais", "Japonais" },
            { "japanese", "Japonais" }, { "chinois", "Chinois" }, { "chinese", "Chinois" },
            { "coreen", "Coréen" }, { "korean", "Coréen" }, { "arabe", "Arabe" }, { "arabic", "Arabe" },
            { "hindi", "Hindi" }, { "turc", "Turc" }, { "turkish", "Turc" }, { "polonais", "Polonais" },
            { "polish", "Polonais" }, { "suedois", "Suédois" }, { "swedish", "Suédois" },
            { "norvegien", "Norvégien" }, { "norwegian", "Norvégien" }, { "danois", "Danois" },
            { "danish", "Danois" }, { "finnois", "Finnois" }, { "finnish", "Finnois" },
            { "tcheque", "Tchèque" }, { "czech", "Tchèque" }, { "grec", "Grec" }, { "greek", "Grec" },
            { "hebreu", "Hébreu" }, { "hebrew", "Hébreu" }, { "thai", "Thaï" },
            { "vietnamien", "Vietnamien" }, { "vietnamese", "Vietnamien" }, { "ukrainien", "Ukrainien" },
            { "ukrainian", "Ukrainien" }, { "roumain", "Roumain" }, { "romanian", "Roumain" },
            { "hongrois", "Hongrois" }, { "hungarian", "Hongrois" }, { "bulgare", "Bulgare" },
            { "bulgarian", "Bulgare" }, { "croate", "Croate" }, { "croatian", "Croate" },
            { "serbe", "Serbe" }, { "serbian", "Serbe" }, { "slovaque", "Slovaque" },
            { "slovak", "Slovaque" }, { "catalan", "Catalan" }, { "basque", "Basque" },
            { "galicien", "Galicien" }, { "galician", "Galicien" }, { "persan", "Persan" },
            { "persian", "Persan" }, { "indonesien", "Indonésien" }, { "indonesian", "Indonésien" },
            { "malais", "Malais" }, { "malay", "Malais" }, { "tagalog", "Tagalog" }
        };

        /// <summary>Reconnaît « French », « Français » ou « eng » dans un titre de piste.</summary>
        internal static string LanguageFromText(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return "";
            string[] tokens = text.ToLowerInvariant().Split(
                new[] { ' ', '-', '_', '[', ']', '(', ')', '/', '\\', ',', '.', ':', ';', '·', '|' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                for (int i = 0; i < LanguageWords.GetLength(0); i++)
                    if (String.Equals(token, LanguageWords[i, 0], StringComparison.Ordinal)) return LanguageWords[i, 1];
                string code = MediaTrackScanner.KnownLanguageName(token);
                if (code.Length > 0) return code;
            }
            return "";
        }
    }

    /// <summary>
    /// Piste de sous-titres telle que libVLC la voit : identifiant de sélection,
    /// langue et codec. Sert au menu « sous-titres » pour les pistes intégrées
    /// d'un MKV, d'un TS ou d'un flux distant, y compris les pistes image (PGS).
    /// </summary>
    internal sealed class SubtitleStream
    {
        /// <summary>Identifiant libvlc_video_set_spu.</summary>
        public int Id = -1;
        /// <summary>Position dans la liste, à partir de 1.</summary>
        public int Index;
        public string Name = "";
        public string Description = "";
        public string LanguageCode = "";
        public string Codec = "";

        public string Text
        {
            get
            {
                string value = (Name ?? "").Trim();
                if (value.Length == 0) value = (Description ?? "").Trim();
                return value;
            }
        }

        public string Language
        {
            get
            {
                string label = MediaTrackScanner.KnownLanguageName(LanguageCode);
                if (label.Length > 0) return label;
                return AudioStream.LanguageFromText(Text);
            }
        }

        public string CodecText
        {
            get { return MediaTrackScanner.CodecName(Codec); }
        }

        /// <summary>Vrai pour les sous-titres image (PGS, VobSub, TextST).</summary>
        public bool IsBitmap
        {
            get
            {
                string codec = (Codec ?? "").ToLowerInvariant();
                return codec.IndexOf("pgs", StringComparison.Ordinal) >= 0
                    || codec.IndexOf("vobsub", StringComparison.Ordinal) >= 0
                    || codec.IndexOf("textst", StringComparison.Ordinal) >= 0
                    || codec.IndexOf("dvdsub", StringComparison.Ordinal) >= 0;
            }
        }

        /// <summary>Libellé du menu : langue, titre, type. Ex. « Français · PGS ».</summary>
        public string Label
        {
            get
            {
                var parts = new List<string>();
                string language = Language;
                if (language.Length == 0) language = "Sous-titre " + (Index > 0 ? Index : 1).ToString(CultureInfo.InvariantCulture);
                parts.Add(language);
                string title = Text;
                if (title.Length > 0 && title.Length > 3
                    && !String.Equals(title, language, StringComparison.OrdinalIgnoreCase)
                    && title.IndexOf(language, StringComparison.OrdinalIgnoreCase) < 0)
                    parts.Add(TextUtil.Ellipsis(title, 44));
                string codec = CodecText;
                if (codec.Length > 0) parts.Add(codec);
                if (IsBitmap) parts.Add("image");
                return String.Join("  ·  ", parts.ToArray());
            }
        }
    }

    /// <summary>
    /// Lecteur libVLC minimal : décodage audio universel et rendu vidéo en mémoire
    /// (aucune fenêtre enfant, donc les contrôles et les sous-titres WPF restent
    /// au-dessus de l'image).
    /// </summary>
    internal sealed class VlcPlayback : IDisposable
    {
        // -1 : le lecteur n'a pas encore rendu d'image.
        IntPtr instance;
        IntPtr mediaPlayer;
        IntPtr media;
        bool disposed;

        // Une seule instance libVLC pour toute l'application. La créér à chaque
        // lancement rechargeait tous les plug-ins (plusieurs secondes sur une
        // machine froide) pendant que la fenêtre attendait, ce qui donnait
        // l'impression que le programme se figeait à l'ouverture d'un film.
        static IntPtr sharedInstance;
        static readonly object sharedGate = new object();

        readonly object audioGate = new object();
        // Modules de sortie essayés quand le périphérique refuse le format du
        // flux (TrueHD 7.1 sur une sortie stéréo, carte occupée en exclusif…).
        readonly HashSet<string> triedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        readonly object frameGate = new object();
        // Tampons de décodage (VLC écrit dedans) puis tampons de copie lus par
        // l'interface : la copie se fait pendant le rappel « display », donc
        // aucune trame n'est lue à moitié écrite.
        readonly object copyGate = new object();
        IntPtr[] frames = new IntPtr[2];
        IntPtr[] staging = new IntPtr[2];
        int frameIndex;
        int stagingIndex;
        int readyIndex = -1;
        bool frameReady;
        int videoWidth;
        int videoHeight;
        int stride;
        int pendingSeekMs = -1;

        // Les délégués doivent rester référencés tant que libVLC les appelle.
        readonly Native.VideoLockCb lockCallback;
        readonly Native.VideoUnlockCb unlockCallback;
        readonly Native.VideoDisplayCb displayCallback;
        readonly Native.VideoFormatCb formatCallback;
        readonly Native.VideoCleanupCb cleanupCallback;

        /// <summary>
        /// Prépare libVLC hors du fil d'interface : au premier lancement, le
        /// chargement des greffons peut prendre plusieurs secondes. L'appeler
        /// pendant l'affichage de l'accueil évite ce délai au premier film.
        /// </summary>
        internal static void WarmUp()
        {
            try { SharedInstance(); } catch { }
        }

        static IntPtr SharedInstance()
        {
            if (!VlcRuntime.Available) throw new InvalidOperationException("libVLC indisponible : " + VlcRuntime.Failure);
            lock (sharedGate)
            {
                if (sharedInstance != IntPtr.Zero) return sharedInstance;
                string[] arguments = new[]
                {
                    "--no-osd", "--no-video-title-show", "--quiet", "--no-snapshot-preview",
                    "--no-sub-autodetect-file", "--network-caching=1200",
                    // Aucun flux audio n'est envoyé tel quel à un décodeur externe :
                    // VLC décode toujours la piste (AC-3, E-AC-3, DTS, TrueHD, MLP,
                    // AAC, Opus, FLAC, PCM…), puis la convertit pour le périphérique.
                    "--no-spdif",
                    // Le rendu passe par des rappels mémoire : le décodage matériel
                    // écrirait dans des surfaces Direct3D incompatibles avec ce tampon.
                    "--avcodec-hw=none"
                };
                IntPtr created = Native.libvlc_new(arguments.Length, arguments);
                if (created == IntPtr.Zero) throw new InvalidOperationException("VLC n'a pas pu démarrer son moteur.");
                sharedInstance = created;
                return sharedInstance;
            }
        }

        public VlcPlayback()
        {
            lockCallback = OnLock;
            unlockCallback = OnUnlock;
            displayCallback = OnDisplay;
            formatCallback = OnFormat;
            cleanupCallback = OnCleanup;

            instance = SharedInstance();
            mediaPlayer = Native.libvlc_media_player_new(instance);
            if (mediaPlayer == IntPtr.Zero) throw new InvalidOperationException("VLC n'a pas pu créer de lecteur.");
            // Sortie audio imposée par les réglages, si elle existe sur cette machine.
            string preferredOutput = AudioOutputPreference;
            if (preferredOutput.Length > 0 && AudioOutputs().Contains(preferredOutput)) SetAudioOutput(preferredOutput);

            Native.libvlc_video_set_callbacks(mediaPlayer,
                Marshal.GetFunctionPointerForDelegate(lockCallback),
                Marshal.GetFunctionPointerForDelegate(unlockCallback),
                Marshal.GetFunctionPointerForDelegate(displayCallback), IntPtr.Zero);
            Native.libvlc_video_set_format_callbacks(mediaPlayer,
                Marshal.GetFunctionPointerForDelegate(formatCallback),
                Marshal.GetFunctionPointerForDelegate(cleanupCallback));
        }

        public int VideoWidth { get { return videoWidth; } }
        public int VideoHeight { get { return videoHeight; } }
        public bool HasVideo { get { return videoWidth > 0 && videoHeight > 0; } }

        public bool Open(string mrl, string userAgent, string referer, string cookie)
        {
            if (disposed || mediaPlayer == IntPtr.Zero || String.IsNullOrWhiteSpace(mrl)) return false;
            ReleaseMedia();
            lock (frameGate)
            {
                frameReady = false;
                readyIndex = -1;
                videoWidth = 0;
                videoHeight = 0;
                stride = 0;
                pendingSeekMs = -1;
            }
            media = Native.libvlc_media_new_location(instance, mrl);
            if (media == IntPtr.Zero) return false;
            Native.libvlc_media_add_option(media, ":network-caching=1200");
            Native.libvlc_media_add_option(media, ":live-caching=1200");
            Native.libvlc_media_add_option(media, ":file-caching=700");
            // Meilleure qualité possible : sur un manifeste HLS/DASH adaptatif,
            // VLC retient la variante au débit le plus élevé au lieu de la plus
            // faible, et le désentrelacement automatique est activé.
            Native.libvlc_media_add_option(media, ":adaptive-logic=highest");
            Native.libvlc_media_add_option(media, ":deinterlace=-1");
            if (!String.IsNullOrWhiteSpace(userAgent)) Native.libvlc_media_add_option(media, ":http-user-agent=" + userAgent.Trim());
            if (!String.IsNullOrWhiteSpace(referer)) Native.libvlc_media_add_option(media, ":http-referrer=" + referer.Trim());
            // Langue audio préférée : VLC démarre directement sur la bonne piste
            // quand le fichier en contient plusieurs.
            string audioLanguage = PreferredAudioLanguage;
            if (audioLanguage.Length > 0) Native.libvlc_media_add_option(media, ":audio-language=" + audioLanguage);
            Native.libvlc_media_player_set_media(mediaPlayer, media);
            // Les sous-titres sont affichés par l'application : VLC ne doit pas
            // superposer les siens.
            try { Native.libvlc_video_set_spu(mediaPlayer, -1); } catch { }
            return Native.libvlc_media_player_play(mediaPlayer) == 0;
        }

        public void Play()
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return;
            int state = Native.libvlc_media_player_get_state(mediaPlayer);
            if (state == 4) { Native.libvlc_media_player_set_pause(mediaPlayer, 0); return; }
            if (state == 5 || state == 6 || state == 7) Native.libvlc_media_player_play(mediaPlayer);
        }

        public void Pause()
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return;
            Native.libvlc_media_player_set_pause(mediaPlayer, 1);
        }

        public void Stop()
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return;
            try { Native.libvlc_media_player_stop(mediaPlayer); } catch { }
        }

        public int State
        {
            get { return disposed || mediaPlayer == IntPtr.Zero ? 0 : Native.libvlc_media_player_get_state(mediaPlayer); }
        }

        /// <summary>Position en millisecondes, -1 si inconnue.</summary>
        public long TimeMs
        {
            get { return disposed || mediaPlayer == IntPtr.Zero ? -1 : Native.libvlc_media_player_get_time(mediaPlayer); }
        }

        public long LengthMs
        {
            get { return disposed || mediaPlayer == IntPtr.Zero ? 0 : Native.libvlc_media_player_get_length(mediaPlayer); }
        }

        /// <summary>
        /// Secondes de contenu déjà lues par le démultiplexeur. Le rapport entre
        /// les octets lus et le débit moyen donne une estimation du tampon réseau,
        /// utilisée pour la partie claire de la barre de progression. -1 quand
        /// les statistiques ne sont pas encore disponibles.
        /// </summary>
        public double BufferedSeconds
        {
            get
            {
                if (disposed || mediaPlayer == IntPtr.Zero) return -1;
                IntPtr current = Native.libvlc_media_player_get_media(mediaPlayer);
                if (current == IntPtr.Zero) return -1;
                var stats = new Native.MediaStats();
                if (Native.libvlc_media_get_stats(current, ref stats) == 0) return -1;
                if (stats.DemuxBitrate <= 0.000001f || stats.DemuxReadBytes <= 0) return -1;
                // libVLC expose un débit en octets par microseconde : le rapport
                // donne des microsecondes, converties en secondes.
                double seconds = stats.DemuxReadBytes / (stats.DemuxBitrate * 1000000.0);
                if (seconds <= 0 || Double.IsNaN(seconds) || Double.IsInfinity(seconds)) return -1;
                return seconds;
            }
        }

        public void SeekTo(double seconds)
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return;
            long ms = (long)Math.Max(0, seconds * 1000);
            lock (frameGate)
            {
                if (LengthMs <= 0) { pendingSeekMs = (int)Math.Min(Int32.MaxValue, ms); return; }
                pendingSeekMs = -1;
            }
            Native.libvlc_media_player_set_time(mediaPlayer, ms);
        }

        /// <summary>Applique un déplacement demandé avant que la durée ne soit connue.</summary>
        public void ApplyPendingSeek()
        {
            int pending;
            lock (frameGate) { pending = pendingSeekMs; }
            if (pending < 0) return;
            if (LengthMs <= 0) return;
            lock (frameGate) { pendingSeekMs = -1; }
            Native.libvlc_media_player_set_time(mediaPlayer, pending);
        }

        public double Volume
        {
            get
            {
                if (disposed || mediaPlayer == IntPtr.Zero) return 1;
                return Math.Max(0, Math.Min(1, Native.libvlc_audio_get_volume(mediaPlayer) / 100.0));
            }
            set
            {
                if (disposed || mediaPlayer == IntPtr.Zero) return;
                int level = (int)Math.Round(Math.Max(0, Math.Min(1, value)) * 100);
                Native.libvlc_audio_set_volume(mediaPlayer, level);
            }
        }

        public double Rate
        {
            get { return disposed || mediaPlayer == IntPtr.Zero ? 1 : Native.libvlc_media_player_get_rate(mediaPlayer); }
            set
            {
                if (disposed || mediaPlayer == IntPtr.Zero) return;
                Native.libvlc_media_player_set_rate(mediaPlayer, (float)Math.Max(0.25, Math.Min(4, value)));
            }
        }

        public int AudioTrackCount
        {
            get
            {
                if (disposed || mediaPlayer == IntPtr.Zero) return 0;
                return Math.Max(0, Native.libvlc_audio_get_track_count(mediaPlayer));
            }
        }

        /// <summary>Vrai si le flux contient au moins une piste audio réelle.</summary>
        public bool HasAudioTrack
        {
            get { return AudioStreams().Count > 0; }
        }

        /// <summary>
        /// Volume renvoyé par VLC : −1 quand aucune sortie audio n'a pu être
        /// ouverte (périphérique occupé, format refusé). C'est le seul signal
        /// fiable d'un flux qui joue sans son.
        /// </summary>
        public int AudioVolumeRaw
        {
            get
            {
                if (disposed || mediaPlayer == IntPtr.Zero) return -1;
                return Native.libvlc_audio_get_volume(mediaPlayer);
            }
        }

        /// <summary>Vrai quand le flux a du son mais qu'aucune sortie audio n'est active.</summary>
        public bool AudioOutputMissing
        {
            get { return HasAudioTrack && AudioVolumeRaw < 0; }
        }

        /// <summary>Modules de sortie audio réellement installés (« mmdevice », « directsound »…).</summary>
        public List<string> AudioOutputs()
        {
            var names = new List<string>();
            if (disposed || instance == IntPtr.Zero) return names;
            IntPtr head = IntPtr.Zero;
            try
            {
                head = Native.libvlc_audio_output_list_get(instance);
                IntPtr node = head;
                int guard = 0;
                while (node != IntPtr.Zero && guard++ < 16)
                {
                    var output = (Native.AudioOutputDescription)Marshal.PtrToStructure(node, typeof(Native.AudioOutputDescription));
                    string name = output.Name == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(output.Name);
                    if (!String.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
                    node = output.Next;
                }
            }
            catch { }
            finally
            {
                if (head != IntPtr.Zero) Native.libvlc_audio_output_list_release(head);
            }
            return names;
        }

        /// <summary>Change la sortie audio à la volée : VLC rouvre le périphérique.</summary>
        public bool SetAudioOutput(string module)
        {
            if (disposed || mediaPlayer == IntPtr.Zero || String.IsNullOrWhiteSpace(module)) return false;
            lock (audioGate)
            {
                triedOutputs.Add(module.Trim());
                return Native.libvlc_audio_output_set(mediaPlayer, module.Trim()) == 0;
            }
        }

        /// <summary>
        /// Essaie les sorties audio disponibles, de la plus moderne à la plus
        /// rudimentaire, et renvoie celle qui a été retenue (null si aucune).
        /// </summary>
        public string TryRecoverAudioOutput()
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return null;
            List<string> installed = AudioOutputs();
            string[] chain = { "mmdevice", "directsound", "waveout" };
            for (int i = 0; i < chain.Length; i++)
            {
                string candidate = chain[i];
                if (installed.Count > 0 && !ContainsModule(installed, candidate)) continue;
                lock (audioGate) { if (triedOutputs.Contains(candidate)) continue; }
                if (!SetAudioOutput(candidate)) continue;
                if (AudioVolumeRaw >= 0) return candidate;
            }
            return null;
        }

        static bool ContainsModule(List<string> names, string module)
        {
            foreach (string name in names)
                if (String.Equals(name, module, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Langue audio préférée des réglages, vide quand elle est automatique.</summary>
        static string PreferredAudioLanguage
        {
            get
            {
                try
                {
                    AppSettings settings = SettingsStore.Load();
                    string code = settings == null ? "" : (settings.PreferredAudioLanguage ?? "");
                    code = code.Trim().ToLowerInvariant();
                    if (code.Length == 0 || code == "auto") return "";
                    if (code.Length > 3) code = code.Substring(0, 2);
                    return code;
                }
                catch { return ""; }
            }
        }

        /// <summary>Sortie audio choisie dans les réglages, vide en automatique.</summary>
        static string AudioOutputPreference
        {
            get
            {
                try
                {
                    AppSettings settings = SettingsStore.Load();
                    string mode = settings == null ? "" : (settings.AudioOutput ?? "").Trim().ToLowerInvariant();
                    if (mode == "mmdevice" || mode == "directsound" || mode == "waveout") return mode;
                }
                catch { }
                return "";
            }
        }

        /// <summary>
        /// Reconnaît la langue d'une piste d'après son nom VLC (« Français - VF »,
        /// « English 5.1 »…). Sert à privilégier la langue choisie dans les réglages.
        /// </summary>
        public static bool TrackMatchesLanguage(string trackName, string code)
        {
            if (String.IsNullOrWhiteSpace(trackName) || String.IsNullOrWhiteSpace(code)) return false;
            string key = code.Trim().ToLowerInvariant();
            if (key == "auto" || key.Length < 2) return false;
            string language = MediaTrackScanner.LanguageName(key, "").ToLowerInvariant();
            string text = trackName.ToLowerInvariant();
            if (language.Length > 2 && text.IndexOf(language, StringComparison.Ordinal) >= 0) return true;
            foreach (string token in text.Split(new[] { ' ', '-', '_', '[', ']', '(', ')', '/', ',', '.', '·', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length == 0) continue;
                if (String.Equals(token, key, StringComparison.Ordinal)) return true;
                if (token.Length == 3 && String.Equals(MediaTrackScanner.LanguageName(token, ""), MediaTrackScanner.LanguageName(key, ""), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public int CurrentAudioTrack
        {
            get { return disposed || mediaPlayer == IntPtr.Zero ? -1 : Native.libvlc_audio_get_track(mediaPlayer); }
        }

        public bool SelectAudioTrack(int identifier)
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return false;
            return Native.libvlc_audio_set_track(mediaPlayer, identifier) == 0;
        }

        /// <summary>Pistes audio réelles du flux, telles que VLC les voit.</summary>
        public List<KeyValuePair<int, string>> AudioTracks()
        {
            var tracks = new List<KeyValuePair<int, string>>();
            if (disposed || mediaPlayer == IntPtr.Zero) return tracks;
            IntPtr head = IntPtr.Zero;
            try
            {
                head = Native.libvlc_audio_get_track_description(mediaPlayer);
                IntPtr node = head;
                int guard = 0;
                while (node != IntPtr.Zero && guard++ < 32)
                {
                    var description = (Native.TrackDescription)Marshal.PtrToStructure(node, typeof(Native.TrackDescription));
                    if (description.Id >= 0)
                    {
                        string name = description.Name == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(description.Name);
                        tracks.Add(new KeyValuePair<int, string>(description.Id, name ?? ""));
                    }
                    node = description.Next;
                }
            }
            catch { }
            finally
            {
                if (head != IntPtr.Zero) Native.libvlc_track_description_list_release(head);
            }
            return tracks;
        }

        /// <summary>
        /// Pistes audio enrichies : libVLC fournit la langue, le codec, les canaux,
        /// la fréquence et le débit. Les descriptions de pistes ne contiennent
        /// souvent qu'un nom vide sur les flux TS/HLS ; on fusionne les deux
        /// sources pour que le menu reste lisible partout.
        /// </summary>
        public List<AudioStream> AudioStreams()
        {
            var result = new List<AudioStream>();
            if (disposed || mediaPlayer == IntPtr.Zero) return result;
            List<KeyValuePair<int, string>> described = AudioTracks();
            List<AudioStream> metadata = MediaAudioStreams();
            var byId = new Dictionary<int, AudioStream>();
            foreach (AudioStream stream in metadata)
            {
                if (stream.Id < 0 || byId.ContainsKey(stream.Id)) continue;
                byId[stream.Id] = stream;
            }
            for (int i = 0; i < described.Count; i++)
            {
                int id = described[i].Key;
                AudioStream stream = null;
                if (!byId.TryGetValue(id, out stream) && i < metadata.Count) stream = metadata[i];
                if (stream == null) stream = new AudioStream();
                stream.Id = id;
                stream.Index = i + 1;
                if (String.IsNullOrWhiteSpace(stream.Name)) stream.Name = described[i].Value ?? "";
                result.Add(stream);
            }
            // Certains démultiplexeurs ne publient pas encore la liste des pistes
            // au moment du menu : les métadonnées du média suffisent alors.
            if (result.Count == 0)
            {
                for (int i = 0; i < metadata.Count; i++)
                {
                    metadata[i].Index = i + 1;
                    result.Add(metadata[i]);
                }
            }
            return result;
        }

        /// <summary>Métadonnées de pistes lues dans le média (libvlc_media_tracks_get).</summary>
        List<AudioStream> MediaAudioStreams()
        {
            var streams = new List<AudioStream>();
            if (disposed || mediaPlayer == IntPtr.Zero) return streams;
            IntPtr tracks = IntPtr.Zero;
            uint count = 0;
            try
            {
                IntPtr mediaPtr = Native.libvlc_media_player_get_media(mediaPlayer);
                if (mediaPtr == IntPtr.Zero) return streams;
                count = Native.libvlc_media_tracks_get(mediaPtr, out tracks);
                if (tracks == IntPtr.Zero || count == 0) return streams;
                // libvlc_media_tracks_get renvoie un tableau de pointeurs vers
                // libvlc_media_track_t, pas un tableau de structures.
                for (uint i = 0; i < count; i++)
                {
                    IntPtr node = Marshal.ReadIntPtr(tracks, (int)i * IntPtr.Size);
                    if (node == IntPtr.Zero) continue;
                    var track = (Native.MediaTrack)Marshal.PtrToStructure(node, typeof(Native.MediaTrack));
                    if (track.Type != 0) continue; // libvlc_track_audio
                    var stream = new AudioStream
                    {
                        Id = track.Id,
                        Codec = FourCC(track.Codec),
                        Bitrate = track.Bitrate > Int32.MaxValue ? Int32.MaxValue : (int)track.Bitrate,
                        LanguageCode = ReadAnsi(track.Language),
                        Description = ReadAnsi(track.Description)
                    };
                    if (track.Track != IntPtr.Zero)
                    {
                        var audio = (Native.MediaAudioTrack)Marshal.PtrToStructure(track.Track, typeof(Native.MediaAudioTrack));
                        stream.Channels = audio.Channels > Int32.MaxValue ? Int32.MaxValue : (int)audio.Channels;
                        stream.Rate = audio.Rate > Int32.MaxValue ? Int32.MaxValue : (int)audio.Rate;
                    }
                    streams.Add(stream);
                }
            }
            catch { }
            finally
            {
                if (tracks != IntPtr.Zero && count > 0)
                {
                    try { Native.libvlc_media_tracks_release(tracks, count); } catch { }
                }
            }
            return streams;
        }

        // ------------------------------------------------------- sous-titres

        /// <summary>Pistes de sous-titres proposées par le démultiplexeur.</summary>
        public List<KeyValuePair<int, string>> SubtitleTracks()
        {
            var tracks = new List<KeyValuePair<int, string>>();
            if (disposed || mediaPlayer == IntPtr.Zero) return tracks;
            IntPtr head = IntPtr.Zero;
            try
            {
                head = Native.libvlc_video_get_spu_description(mediaPlayer);
                IntPtr node = head;
                int guard = 0;
                while (node != IntPtr.Zero && guard++ < 32)
                {
                    var description = (Native.TrackDescription)Marshal.PtrToStructure(node, typeof(Native.TrackDescription));
                    if (description.Id >= 0)
                    {
                        string name = description.Name == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(description.Name);
                        tracks.Add(new KeyValuePair<int, string>(description.Id, name ?? ""));
                    }
                    node = description.Next;
                }
            }
            catch { }
            finally
            {
                if (head != IntPtr.Zero) Native.libvlc_track_description_list_release(head);
            }
            return tracks;
        }

        /// <summary>
        /// Sous-titres réels du flux, avec leur langue et leur codec : le menu
        /// peut ainsi annoncer « Français · PGS » même pour un fichier distant.
        /// </summary>
        public List<SubtitleStream> SubtitleStreams()
        {
            var result = new List<SubtitleStream>();
            if (disposed || mediaPlayer == IntPtr.Zero) return result;
            List<KeyValuePair<int, string>> described = SubtitleTracks();
            List<SubtitleStream> metadata = MediaSubtitleStreams();
            var byId = new Dictionary<int, SubtitleStream>();
            foreach (SubtitleStream stream in metadata)
            {
                if (stream.Id < 0 || byId.ContainsKey(stream.Id)) continue;
                byId[stream.Id] = stream;
            }
            for (int i = 0; i < described.Count; i++)
            {
                int id = described[i].Key;
                SubtitleStream stream = null;
                // Les identifiants de piste du média ne coïncident pas toujours
                // avec les identifiants SPU : on complète par position quand
                // l'identifiant exact n'est pas connu, mais seul un identifiant
                // issu de la liste SPU est utilisable pour la sélection.
                if (!byId.TryGetValue(id, out stream) && i < metadata.Count) stream = metadata[i];
                if (stream == null) stream = new SubtitleStream();
                stream.Id = id;
                stream.Index = i + 1;
                if (String.IsNullOrWhiteSpace(stream.Name)) stream.Name = described[i].Value ?? "";
                result.Add(stream);
            }
            // Sans liste SPU, aucun identifiant n'est sélectionnable : le menu se
            // replie sur l'analyse locale du fichier plutôt que d'afficher des
            // pistes vouées à échouer.
            return result;
        }

        /// <summary>Métadonnées des pistes texte lues dans le média.</summary>
        List<SubtitleStream> MediaSubtitleStreams()
        {
            var streams = new List<SubtitleStream>();
            if (disposed || mediaPlayer == IntPtr.Zero) return streams;
            IntPtr tracks = IntPtr.Zero;
            uint count = 0;
            try
            {
                IntPtr mediaPtr = Native.libvlc_media_player_get_media(mediaPlayer);
                if (mediaPtr == IntPtr.Zero) return streams;
                count = Native.libvlc_media_tracks_get(mediaPtr, out tracks);
                if (tracks == IntPtr.Zero || count == 0) return streams;
                for (uint i = 0; i < count; i++)
                {
                    IntPtr node = Marshal.ReadIntPtr(tracks, (int)i * IntPtr.Size);
                    if (node == IntPtr.Zero) continue;
                    var track = (Native.MediaTrack)Marshal.PtrToStructure(node, typeof(Native.MediaTrack));
                    if (track.Type != 1) continue; // libvlc_track_text
                    streams.Add(new SubtitleStream
                    {
                        Id = track.Id,
                        Codec = FourCC(track.Codec),
                        LanguageCode = ReadAnsi(track.Language),
                        Description = ReadAnsi(track.Description)
                    });
                }
            }
            catch { }
            finally
            {
                if (tracks != IntPtr.Zero && count > 0)
                {
                    try { Native.libvlc_media_tracks_release(tracks, count); } catch { }
                }
            }
            return streams;
        }

        /// <summary>Piste de sous-titres active, -1 quand elles sont désactivées.</summary>
        public int CurrentSubtitle
        {
            get { return disposed || mediaPlayer == IntPtr.Zero ? -1 : Native.libvlc_video_get_spu(mediaPlayer); }
        }

        /// <summary>
        /// Affiche une piste intégrée : VLC la dessine sur l'image. Le code de
        /// retour de <c>libvlc_video_set_spu</c> n'est pas fiable selon les
        /// versions de VLC : on contrôle la piste réellement sélectionnée.
        /// </summary>
        public bool SelectSubtitle(int identifier)
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return false;
            try
            {
                Native.libvlc_video_set_spu(mediaPlayer, identifier);
                return Native.libvlc_video_get_spu(mediaPlayer) == identifier;
            }
            catch { return false; }
        }

        /// <summary>Désactive les sous-titres dessinés par VLC.</summary>
        public void DisableSubtitles()
        {
            if (disposed || mediaPlayer == IntPtr.Zero) return;
            try { Native.libvlc_video_set_spu(mediaPlayer, -1); } catch { }
        }

        static string ReadAnsi(IntPtr value)
        {
            if (value == IntPtr.Zero) return "";
            try { return Marshal.PtrToStringAnsi(value) ?? ""; }
            catch { return ""; }
        }

        /// <summary>Fourcc VLC (entier little-endian) vers texte : « ac-3 », « ec-3 »…</summary>
        public static string FourCC(uint value)
        {
            if (value == 0) return "";
            var chars = new char[4];
            chars[0] = (char)(value & 0xFF);
            chars[1] = (char)((value >> 8) & 0xFF);
            chars[2] = (char)((value >> 16) & 0xFF);
            chars[3] = (char)((value >> 24) & 0xFF);
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] < 32 || chars[i] > 126) return "";
            return new string(chars).Trim();
        }

        /// <summary>Reçoit une image BGRA complète (appelé sous verrou, sur le thread d'interface).</summary>
        public delegate void FrameSink(IntPtr data, int width, int height, int pixelsStride);

        /// <summary>
        /// Copie la dernière image rendue. La copie se fait sous le même verrou que
        /// l'écriture VLC : l'image ne peut donc jamais être lue à moitié.
        /// </summary>
        public bool CopyLatestFrame(FrameSink sink)
        {
            if (sink == null) return false;
            lock (copyGate)
            {
                if (!frameReady || readyIndex < 0 || videoWidth <= 0 || videoHeight <= 0 || stride <= 0) return false;
                frameReady = false;
                IntPtr source = staging[readyIndex];
                if (source == IntPtr.Zero) return false;
                sink(source, videoWidth, videoHeight, stride);
                return true;
            }
        }

        // ------------------------------------------------------------- rappels VLC

        IntPtr OnLock(IntPtr opaque, IntPtr planes)
        {
            IntPtr buffer;
            lock (frameGate)
            {
                if (frames[0] == IntPtr.Zero) return IntPtr.Zero;
                frameIndex = 1 - frameIndex;
                buffer = frames[frameIndex];
            }
            Marshal.WriteIntPtr(planes, buffer);
            return buffer;
        }

        void OnUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
        }

        void OnDisplay(IntPtr opaque, IntPtr picture)
        {
            if (picture == IntPtr.Zero) return;
            int width, height, pixelsStride;
            lock (frameGate)
            {
                width = videoWidth;
                height = videoHeight;
                pixelsStride = stride;
            }
            if (width <= 0 || height <= 0 || pixelsStride <= 0) return;
            lock (copyGate)
            {
                stagingIndex = 1 - stagingIndex;
                IntPtr target = staging[stagingIndex];
                if (target == IntPtr.Zero) return;
                Native.CopyMemory(target, picture, new IntPtr(pixelsStride * height));
                readyIndex = stagingIndex;
                frameReady = true;
            }
        }

        uint OnFormat(IntPtr opaque, IntPtr chroma, IntPtr width, IntPtr height, IntPtr pitches, IntPtr lines)
        {
            int w = Marshal.ReadInt32(width);
            int h = Marshal.ReadInt32(height);
            if (w <= 0 || h <= 0) return 0;
            byte[] chromaBytes = Encoding.ASCII.GetBytes("RV32");
            Marshal.Copy(chromaBytes, 0, chroma, chromaBytes.Length);
            int pitch = w * 4;
            Marshal.WriteInt32(pitches, pitch);
            Marshal.WriteInt32(lines, h);
            lock (frameGate)
            {
                if (videoWidth != w || videoHeight != h)
                {
                    lock (copyGate)
                    {
                        for (int i = 0; i < frames.Length; i++)
                        {
                            if (frames[i] != IntPtr.Zero) Marshal.FreeHGlobal(frames[i]);
                            frames[i] = Marshal.AllocHGlobal(pitch * h);
                            if (staging[i] != IntPtr.Zero) Marshal.FreeHGlobal(staging[i]);
                            staging[i] = Marshal.AllocHGlobal(pitch * h);
                        }
                        frameReady = false;
                        readyIndex = -1;
                    }
                }
                videoWidth = w;
                videoHeight = h;
                stride = pitch;
            }
            return 1;
        }

        void OnCleanup(IntPtr opaque)
        {
        }

        // ---------------------------------------------------------------- ménage

        void ReleaseMedia()
        {
            if (media == IntPtr.Zero) return;
            try { Native.libvlc_media_player_set_media(mediaPlayer, IntPtr.Zero); } catch { }
            try { Native.libvlc_media_release(media); } catch { }
            media = IntPtr.Zero;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (mediaPlayer != IntPtr.Zero) Native.libvlc_media_player_stop(mediaPlayer); } catch { }
            ReleaseMedia();
            try { if (mediaPlayer != IntPtr.Zero) Native.libvlc_media_player_release(mediaPlayer); } catch { }
            mediaPlayer = IntPtr.Zero;
            // L'instance partagée reste vivante : seul le lecteur média est
            // libéré. La détruire obligerait à recharger les greffons au film
            // suivant.
            instance = IntPtr.Zero;
            lock (frameGate)
            {
                lock (copyGate)
                {
                    for (int i = 0; i < frames.Length; i++)
                    {
                        if (frames[i] != IntPtr.Zero) { try { Marshal.FreeHGlobal(frames[i]); } catch { } }
                        frames[i] = IntPtr.Zero;
                        if (staging[i] != IntPtr.Zero) { try { Marshal.FreeHGlobal(staging[i]); } catch { } }
                        staging[i] = IntPtr.Zero;
                    }
                    frameReady = false;
                    readyIndex = -1;
                }
            }
        }

        /// <summary>Déclarations natives de libVLC 3 (Cdecl).</summary>
        internal static class Native
        {
            const string Library = "libvlc.dll";

            // libvlc_track_description_t : l'identifiant vient en premier.
            [StructLayout(LayoutKind.Sequential)]
            public struct TrackDescription
            {
                public int Id;
                public IntPtr Name;
                public IntPtr Next;
            }

            public delegate IntPtr VideoLockCb(IntPtr opaque, IntPtr planes);
            public delegate void VideoUnlockCb(IntPtr opaque, IntPtr picture, IntPtr planes);
            public delegate void VideoDisplayCb(IntPtr opaque, IntPtr picture);
            public delegate uint VideoFormatCb(IntPtr opaque, IntPtr chroma, IntPtr width, IntPtr height, IntPtr pitches, IntPtr lines);
            public delegate void VideoCleanupCb(IntPtr opaque);

            [StructLayout(LayoutKind.Sequential)]
            public struct AudioOutputDescription
            {
                public IntPtr Name;
                public IntPtr Description;
                public IntPtr Next;
            }

            /// <summary>libvlc_media_track_t (libVLC 3).</summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct MediaTrack
            {
                public uint Codec;
                public uint OriginalFourcc;
                public int Id;
                public int Type;
                public int Profile;
                public int Level;
                public IntPtr Track;
                public uint Bitrate;
                public IntPtr Language;
                public IntPtr Description;
            }

            /// <summary>libvlc_audio_track_t : canaux et fréquence d'échantillonnage.</summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct MediaAudioTrack
            {
                public uint Channels;
                public uint Rate;
            }

            /// <summary>libvlc_media_stats_t (libVLC 3) : compteurs de lecture.</summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct MediaStats
            {
                public int ReadBytes;
                public float InputBitrate;
                public int DemuxReadBytes;
                public float DemuxBitrate;
                public int DemuxCorrupted;
                public int DemuxDiscontinuity;
                public int DecodedVideo;
                public int DecodedAudio;
                public int DisplayedPictures;
                public int LostPictures;
                public int PlayedAbuffers;
                public int LostAbuffers;
                public int SentPackets;
                public int SentBytes;
                public float SendBitrate;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr SetDllDirectory(string path);

            [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", CallingConvention = CallingConvention.StdCall)]
            public static extern void CopyMemory(IntPtr destination, IntPtr source, IntPtr length);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_get_version();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_new(int argc, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] argv);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_release(IntPtr instance);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_media_new_location(IntPtr instance, string mrl);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_add_option(IntPtr media, string option);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_release(IntPtr media);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_media_player_new(IntPtr instance);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_player_set_media(IntPtr player, IntPtr media);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_media_player_get_media(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_media_get_stats(IntPtr media, ref MediaStats stats);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint libvlc_media_tracks_get(IntPtr media, out IntPtr tracks);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_tracks_release(IntPtr tracks, uint count);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_media_player_play(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_player_set_pause(IntPtr player, int doPause);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_player_stop(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_player_release(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_media_player_get_state(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern long libvlc_media_player_get_time(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_media_player_set_time(IntPtr player, long time);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern long libvlc_media_player_get_length(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern float libvlc_media_player_get_rate(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_media_player_set_rate(IntPtr player, float rate);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_audio_get_volume(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_audio_set_volume(IntPtr player, int volume);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_audio_get_track_count(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_audio_get_track(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_audio_set_track(IntPtr player, int track);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_audio_get_track_description(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_audio_output_set(IntPtr player, string name);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_audio_output_list_get(IntPtr instance);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_audio_output_list_release(IntPtr list);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libvlc_track_description_list_release(IntPtr description);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_video_set_callbacks(IntPtr player, IntPtr videoLock, IntPtr videoUnlock, IntPtr videoDisplay, IntPtr opaque);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_video_set_format_callbacks(IntPtr player, IntPtr videoFormat, IntPtr videoCleanup);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_video_set_spu(IntPtr player, int spu);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libvlc_video_get_spu(IntPtr player);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libvlc_video_get_spu_description(IntPtr player);
        }
    }
}
