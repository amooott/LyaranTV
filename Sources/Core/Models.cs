using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace LyaranTV
{
    [DataContract]
    internal sealed class PlaylistSource
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string Username { get; set; }
        [DataMember] public string Password { get; set; }
        [DataMember] public string PasswordProtected { get; set; }
        [DataMember] public string MacAddress { get; set; }
        [DataMember] public string MacAddressProtected { get; set; }
        [DataMember] public string DeviceType { get; set; }
        [DataMember] public string FilePath { get; set; }
        [DataMember] public string EpgUrl { get; set; }
        [DataMember] public int LastCount { get; set; }
        [DataMember] public string LastLoaded { get; set; }
        [DataMember] public bool IsDemo { get; set; }

        public string Location { get { return !String.IsNullOrWhiteSpace(FilePath) ? FilePath : Url; } }
        public string DisplayType
        {
            get
            {
                if (IsDemo || Type == "Démo") return "Démonstration";
                if (Type == "Xtream") return "Xtream Codes";
                if (Type == "Portail MAC") return "Portail MAC";
                return "M3U / M3U8";
            }
        }
        public override string ToString() { return Name; }
    }

    internal sealed class Channel
    {
        public string Name, Group, Url, AlternateUrl, Logo, Id, TvgId, Kind, StreamId, Number;
        public string Description, Rating, Year, SeriesId, Season, Episode;
        public string TmdbId, PosterUrl, BackdropUrl, Overview, Genres, Runtime, Certification, TrailerUrl, Cast, StillUrl;
        public string HttpUserAgent, HttpReferer, HttpOrigin, HttpCookie;
        public double TmdbRating;
        public int VoteCount;
        public bool TmdbChecked;
        /// <summary>Une recherche d'affiche de secours a déjà été demandée pour ce contenu.</summary>
        public bool ArtworkRequested;
        public bool IsSeries;
        public int Order;
        public bool IsPlayable { get { return IsSeries || !String.IsNullOrWhiteSpace(Url); } }
        public string DisplayNumber { get { return String.IsNullOrWhiteSpace(Number) ? "" : Number; } }
        public Channel CopyWithUrl(string url)
        {
            return new Channel
            {
                Name = Name, Group = Group, Url = url, AlternateUrl = AlternateUrl, Logo = Logo, Id = Id,
                TvgId = TvgId, Kind = Kind, StreamId = StreamId, Number = Number, Description = Description,
                Rating = Rating, Year = Year, SeriesId = SeriesId, Season = Season, Episode = Episode,
                TmdbId = TmdbId, PosterUrl = PosterUrl, BackdropUrl = BackdropUrl, Overview = Overview,
                Genres = Genres, Runtime = Runtime, Certification = Certification, TrailerUrl = TrailerUrl,
                Cast = Cast, StillUrl = StillUrl, TmdbRating = TmdbRating, VoteCount = VoteCount, TmdbChecked = TmdbChecked,
                IsSeries = false, Order = Order,
                HttpUserAgent = HttpUserAgent, HttpReferer = HttpReferer, HttpOrigin = HttpOrigin, HttpCookie = HttpCookie
            };
        }
        public override string ToString() { return Name; }
    }

    internal sealed class EpgProgram
    {
        public string ChannelId, ChannelName, Title, Description, Category, Episode, Icon;
        public DateTimeOffset Start, Stop;
        public TimeSpan Duration { get { return Stop > Start ? Stop - Start : TimeSpan.Zero; } }
        public bool IsLive(DateTimeOffset now) { return Start <= now && Stop > now; }
        public string TimeRange
        {
            get { return Start.ToLocalTime().ToString("HH:mm") + " – " + Stop.ToLocalTime().ToString("HH:mm"); }
        }
    }

    internal sealed class PlaylistLoadResult
    {
        public List<Channel> Items = new List<Channel>();
        public List<EpgProgram> Guide = new List<EpgProgram>();
        public List<string> Warnings = new List<string>();
        public string EpgUrl = "";
        /// <summary>Source Xtream issue d'une conversion automatique M3U, le cas échéant.</summary>
        public PlaylistSource SourceOverride;
        /// <summary>Explication de la conversion, sans identifiants.</summary>
        public string ConversionNote = "";
    }

    [DataContract]
    internal sealed class AppSettings
    {
        [DataMember] public string Theme = "Sombre";
        [DataMember] public string Accent = "#8B5CF6";
        [DataMember] public bool AutoPlayLast = false;
        [DataMember] public bool LoadLastSource = false;
        [DataMember] public bool ResumePlayback = true;
        [DataMember] public double Volume = 1.0;
        [DataMember] public bool Muted = false;
        [DataMember] public string SortMode = "Nom (A → Z)";
        [DataMember] public bool GridView = false;
        [DataMember] public string StartPage = "Accueil";
        [DataMember] public bool Maximized = false;
        [DataMember] public string ExternalPlayer = "";
        [DataMember] public string LastSourceName = "";
        [DataMember] public string LastChannelId = "";
        [DataMember] public string AspectMode = "Adapter";
        [DataMember] public double PlaybackSpeed = 1.0;
        [DataMember] public bool AutoPlayNext = true;
        [DataMember] public double SubtitleSize = 16;
        [DataMember] public bool SubtitleBackground = true;
        [DataMember] public bool AlwaysOnTop = false;
        [DataMember] public string SubtitleLanguage = "fr";
        [DataMember] public bool AutoSubtitles = true;
        [DataMember] public string PreferredAudioLanguage = "auto";
        // Moteur de lecture : « auto » privilégie VLC (tous les codecs audio),
        // « windows » force Media Foundation, « vlc » impose VLC.
        [DataMember] public string PlaybackEngine = "auto";
        // Sortie audio du moteur VLC : « auto », « mmdevice », « directsound » ou
        // « waveout ». Permet de retrouver du son quand la carte refuse le format
        // d'une piste (TrueHD 7.1, DTS-HD…) ou reste occupée en mode exclusif.
        [DataMember] public string AudioOutput = "auto";
        [DataMember] public bool ShowEpgInList = true;
        [DataMember] public bool ReduceMotion = false;
        [DataMember] public bool TmdbEnabled = true;
        [DataMember] public string TmdbApiKey = "";
        [DataMember] public string OpenSubtitlesKey = "";
        [DataMember] public string TmdbLanguage = "fr-FR";
        [DataMember] public int TmdbCacheLimit = 300;
        [DataMember] public string ActiveProfile = "Principal";
        // Langue de l'interface : « auto » suit celle de Windows, sinon fr, en,
        // es, ru ou pt. Le choix est mémorisé par profil d'ordinateur.
        [DataMember] public string Language = I18n.SystemDefault;
        // Jeton GitHub du dépôt privé, chiffré par CredentialVault. Il n'est
        // jamais écrit en clair ni compilé dans le programme.
        [DataMember] public string UpdateTokenProtected = "";
        [DataMember] public bool CheckUpdatesOnStart = true;
        [DataMember] public string LastUpdateCheck = "";
        [DataMember] public bool StartWithWindows = false;
        [DataMember] public bool MinimizeToTray = false;
        [DataMember] public bool OnboardingDone = false;
        [DataMember] public bool ParentalEnabled = false;
        [DataMember] public string ParentalPinProtected = "";
        [DataMember] public string LockedGroups = "";

        public void Normalize()
        {
            if (String.IsNullOrWhiteSpace(Theme)) Theme = "Sombre";
            if (String.IsNullOrWhiteSpace(Accent)) Accent = "#8B5CF6";
            if (String.Equals(Accent, "#F2A33C", StringComparison.OrdinalIgnoreCase)) Accent = "#8B5CF6";
            if (String.IsNullOrWhiteSpace(SortMode)) SortMode = "Nom (A → Z)";
            if (String.IsNullOrWhiteSpace(StartPage)) StartPage = "Accueil";
            if (String.IsNullOrWhiteSpace(AspectMode)) AspectMode = "Adapter";
            if (String.IsNullOrWhiteSpace(TmdbLanguage)) TmdbLanguage = "fr-FR";
            if (String.IsNullOrWhiteSpace(ActiveProfile)) ActiveProfile = "Principal";
            if (String.IsNullOrWhiteSpace(Language)) Language = I18n.SystemDefault;
            string language = (Language ?? "").Trim().ToLowerInvariant();
            if (language != I18n.SystemDefault && language != "fr" && language != "en"
                && language != "es" && language != "ru" && language != "pt") Language = I18n.Detect();
            if (TmdbCacheLimit <= 0) TmdbCacheLimit = 300;
            if (Volume <= 0 || Volume > 1) Volume = 1.0;
            if (PlaybackSpeed < 0.5 || PlaybackSpeed > 2.0) PlaybackSpeed = 1.0;
            if (SubtitleSize < 12 || SubtitleSize > 30) SubtitleSize = 16;
            if (String.IsNullOrWhiteSpace(SubtitleLanguage)) SubtitleLanguage = "fr";
            if (String.IsNullOrWhiteSpace(PreferredAudioLanguage)) PreferredAudioLanguage = "auto";
            if (PlaybackEngine != "auto" && PlaybackEngine != "windows" && PlaybackEngine != "vlc") PlaybackEngine = "auto";
            if (AudioOutput != "mmdevice" && AudioOutput != "directsound" && AudioOutput != "waveout") AudioOutput = "auto";
            // Ancien volume par défaut (72 %) : on remonte au volume plein, qui
            // n'atténue plus la piste audio.
            if (Math.Abs(Volume - 0.72) < 0.001) Volume = 1.0;
        }
    }

    /// <summary>
    /// Trace légère d'un épisode lu : permet de retrouver un épisode en cours
    /// (titre de la série, affiche, saison) dans « Reprendre la lecture » même
    /// après un redémarrage, sans recharger la fiche de la série.
    /// </summary>
    [DataContract]
    internal sealed class EpisodeRecord
    {
        [DataMember] public string Id = "";
        [DataMember] public string Name = "";
        [DataMember] public string Url = "";
        [DataMember] public string Group = "";
        [DataMember] public string Season = "";
        [DataMember] public string Episode = "";
        [DataMember] public string SeriesId = "";
        [DataMember] public string SeriesName = "";
        [DataMember] public string PosterUrl = "";
    }

    [DataContract]
    internal sealed class UserProfile
    {
        [DataMember] public string Name = "Principal";
        [DataMember] public string AvatarColor = "#8B5CF6";
        [DataMember] public bool IsChild = false;
        [DataMember] public string PinProtected = "";
        [DataMember] public string Created = "";
        public override string ToString() { return Name; }
    }

    internal static class ProfileScope
    {
        public static string Current = "Principal";

        public static string Sanitize(string name)
        {
            string value = (name ?? "").Trim();
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (Char.IsLetterOrDigit(c) || c == '-' || c == '_') builder.Append(c);
                else if (Char.IsWhiteSpace(c) || c == '.' || c == '\\' || c == '/') builder.Append('_');
            }
            string result = builder.ToString().Trim('_');
            if (result.Length == 0) result = "Principal";
            return result.Length > 40 ? result.Substring(0, 40) : result;
        }

        public static bool IsScoped(string name)
        {
            return String.Equals(name, "favorites.txt", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "recent.txt", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "positions.json", StringComparison.OrdinalIgnoreCase);
        }

        public static string FileName(string name)
        {
            return FileNameFor(Current, name);
        }

        public static string FileNameFor(string profile, string name)
        {
            if (String.IsNullOrWhiteSpace(name) || !IsScoped(name)) return name;
            string scope = Sanitize(profile);
            if (String.Equals(scope, "Principal", StringComparison.OrdinalIgnoreCase)) return name;
            int dot = name.LastIndexOf('.');
            string suffix = "." + scope;
            if (dot <= 0) return name + suffix;
            return name.Substring(0, dot) + suffix + name.Substring(dot);
        }
    }

    internal static class ProfileStore
    {
        public static List<UserProfile> Load()
        {
            string file = AppPaths.File("profiles.json");
            if (File.Exists(file))
            {
                try
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<UserProfile>));
                    using (var stream = File.OpenRead(file))
                    {
                        var loaded = serializer.ReadObject(stream) as List<UserProfile>;
                        if (loaded != null && loaded.Count > 0)
                        {
                            foreach (var profile in loaded)
                            {
                                if (String.IsNullOrWhiteSpace(profile.Name)) profile.Name = "Principal";
                                if (String.IsNullOrWhiteSpace(profile.AvatarColor)) profile.AvatarColor = "#8B5CF6";
                                if (String.IsNullOrWhiteSpace(profile.Created)) profile.Created = DateTime.Now.ToString("s");
                            }
                            return loaded;
                        }
                    }
                }
                catch { }
            }
            var defaults = new List<UserProfile> { new UserProfile { Name = "Principal", AvatarColor = "#8B5CF6", Created = DateTime.Now.ToString("s") } };
            Save(defaults);
            return defaults;
        }

        public static void Save(IEnumerable<UserProfile> profiles)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new DataContractJsonSerializer(typeof(List<UserProfile>));
                using (var stream = File.Create(AppPaths.File("profiles.json"))) serializer.WriteObject(stream, profiles.ToList());
            }
            catch { }
        }

        public static bool VerifyPin(UserProfile profile, string pin)
        {
            if (profile == null || String.IsNullOrWhiteSpace(profile.PinProtected)) return true;
            return String.Equals(CredentialVault.Unprotect(profile.PinProtected), pin ?? "", StringComparison.Ordinal);
        }
    }

    [DataContract]
    internal sealed class PortableSource
    {
        [DataMember] public string Name = "";
        [DataMember] public string Type = "";
        [DataMember] public string Url = "";
        [DataMember] public string Username = "";
        [DataMember] public string FilePath = "";
        [DataMember] public string EpgUrl = "";
        [DataMember] public string DeviceType = "";
        [DataMember] public bool IsDemo = false;
    }

    [DataContract]
    internal sealed class PortableData
    {
        [DataMember] public string Format = "lyarantv-portable";
        [DataMember] public int Version = 1;
        [DataMember] public string ExportedAt = "";
        [DataMember] public AppSettings Settings = new AppSettings();
        [DataMember] public List<PortableSource> Sources = new List<PortableSource>();
        [DataMember] public List<string> Favorites = new List<string>();
        [DataMember] public List<string> Recent = new List<string>();
        [DataMember] public Dictionary<string, double> Positions = new Dictionary<string, double>();
        [DataMember] public List<UserProfile> Profiles = new List<UserProfile>();
        [DataMember] public List<string> HiddenChannels = new List<string>();
        [DataMember] public List<string> HiddenGroups = new List<string>();
    }

    internal static class AppPaths
    {
        public static string DataDir
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("LYARANTV_DATA_DIR");
                if (!String.IsNullOrWhiteSpace(custom)) return Path.GetFullPath(custom);
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LyaranTV");
            }
        }

        public static string ImageCacheDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyaranTV", "images"); }
        }

        public static string File(string name) { return Path.Combine(DataDir, name); }
        public static string LogFile { get { return File("error.log"); } }
        public static void EnsureDataDir() { Directory.CreateDirectory(DataDir); }
    }

    internal static class TextUtil
    {
        static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

        public static bool Contains(string source, string query)
        {
            if (String.IsNullOrEmpty(query)) return true;
            if (String.IsNullOrEmpty(source)) return false;
            return Compare.IndexOf(source, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        public static string NormalizeKey(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(Char.ToLowerInvariant(c));
            }
            return RegexSpace.Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
        }

        static readonly System.Text.RegularExpressions.Regex RegexSpace = new System.Text.RegularExpressions.Regex("\\s+");

        public static string FormatTime(DateTimeOffset value) { return value.ToLocalTime().ToString("HH:mm"); }
        public static string FormatDate(DateTime value) { return value.ToString("dddd d MMMM", CultureInfo.GetCultureInfo("fr-FR")); }
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1) return "moins d’une minute";
            if (duration.TotalHours >= 1) return ((int)duration.TotalHours) + " h " + duration.Minutes.ToString("00");
            return ((int)duration.TotalMinutes) + " min";
        }
        public static string FormatPosition(TimeSpan value)
        {
            if (value.TotalHours >= 1) return value.ToString(@"h\:mm\:ss");
            return value.ToString(@"m\:ss");
        }
        public static string HumanCount(int count) { return count.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")); }
        public static string FormatEta(double seconds)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds <= 0) return "";
            if (seconds < 3) return "Moins de 3 s";
            if (seconds < 60) return "≈ " + ((int)Math.Round(seconds)).ToString(CultureInfo.InvariantCulture) + " s";
            int minutes = (int)Math.Round(seconds / 60.0);
            if (minutes < 60) return "≈ " + minutes.ToString(CultureInfo.InvariantCulture) + " min";
            return "≈ " + (minutes / 60).ToString(CultureInfo.InvariantCulture) + " h " + (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + " min";
        }
        public static string Ellipsis(string value, int max)
        {
            if (String.IsNullOrEmpty(value) || value.Length <= max) return value ?? "";
            return value.Substring(0, Math.Max(0, max - 1)).TrimEnd() + "…";
        }
        public static int ParseInt(string value, int fallback)
        {
            int parsed; return Int32.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
        public static bool IsHttp(string value)
        {
            return !String.IsNullOrWhiteSpace(value) &&
                (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }
        public static bool IsStreamScheme(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("udp://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("rtp://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }
        public static string ResolveUrl(string value, string baseLocation)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0) return null;
            if (IsStreamScheme(value)) return value;
            // Les entrées de playlist ne sont pas toujours des chemins Windows :
            // une URL relative peut contenir un « | » ou un autre caractère interdit.
            // Path.IsPathRooted lève alors ArgumentException et faisait échouer tout
            // l'ajout de la playlist avec « caractères non conformes dans le chemin ».
            try { if (Path.IsPathRooted(value)) return value; }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            if (IsHttp(baseLocation))
            {
                Uri baseUri, result;
                if (Uri.TryCreate(baseLocation, UriKind.Absolute, out baseUri) && Uri.TryCreate(baseUri, value, out result)) return result.AbsoluteUri;
            }
            if (!String.IsNullOrWhiteSpace(baseLocation) && !IsHttp(baseLocation))
            {
                try
                {
                    string directory = Path.GetDirectoryName(baseLocation);
                    if (!String.IsNullOrWhiteSpace(directory)) return Path.GetFullPath(Path.Combine(directory, value));
                }
                catch { }
            }
            return value;
        }
        public static bool IsVideoFile(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            string ext;
            try { ext = Path.GetExtension(path).ToLowerInvariant(); }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".wmv" || ext == ".mov" || ext == ".m4v"
                || ext == ".mpg" || ext == ".mpeg" || ext == ".ts" || ext == ".m2ts" || ext == ".webm" || ext == ".flv"
                || ext == ".ogv" || ext == ".3gp" || ext == ".m3u8";
        }
        /// <summary>
        /// Fichiers audio que le lecteur intégré peut ouvrir seul : musique,
        /// livres audio, pistes de concert, fichiers MKA, AC-3/E-AC-3/DTS bruts…
        /// </summary>
        public static bool IsAudioFile(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            string ext;
            try { ext = Path.GetExtension(path).ToLowerInvariant(); }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            return ext == ".mp3" || ext == ".flac" || ext == ".wav" || ext == ".m4a" || ext == ".m4b" || ext == ".aac"
                || ext == ".ogg" || ext == ".oga" || ext == ".opus" || ext == ".wma" || ext == ".mka" || ext == ".ape"
                || ext == ".aiff" || ext == ".aif" || ext == ".alac" || ext == ".ac3" || ext == ".eac3" || ext == ".ec3"
                || ext == ".dts" || ext == ".dtshd" || ext == ".truehd" || ext == ".mlp" || ext == ".wv" || ext == ".tta"
                || ext == ".mpc" || ext == ".dsf" || ext == ".dff" || ext == ".caf" || ext == ".amr" || ext == ".au"
                || ext == ".mid" || ext == ".midi";
        }
        /// <summary>Vrai pour tout média que le lecteur intégré accepte d'ouvrir.</summary>
        public static bool IsMediaFile(string path)
        {
            return IsVideoFile(path) || IsAudioFile(path);
        }
        public static string SafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = (value ?? "").Replace(c, '_');
            return String.IsNullOrWhiteSpace(value) ? "lyarantv" : value.Trim();
        }
        public static string Hash(string value)
        {
            using (var sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
        public static string LocalPathFromUrl(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return value;
            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.IsFile) return uri.LocalPath;
            return value;
        }
        public static string M3uEscape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal static class SourceStore
    {
        public static List<PlaylistSource> Load()
        {
            string file = AppPaths.File("playlists.json");
            if (!File.Exists(file)) return new List<PlaylistSource>();
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<PlaylistSource>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as List<PlaylistSource>;
                    if (loaded == null) return new List<PlaylistSource>();
                    RestoreCredentials(loaded);
                    return loaded;
                }
            }
            catch { return new List<PlaylistSource>(); }
        }

        public static void Save(IEnumerable<PlaylistSource> sources)
        {
            AppPaths.EnsureDataDir();
            var serializer = new DataContractJsonSerializer(typeof(List<PlaylistSource>));
            using (var stream = File.Create(AppPaths.File("playlists.json"))) serializer.WriteObject(stream, ForStorage(sources));
        }

        static List<PlaylistSource> ForStorage(IEnumerable<PlaylistSource> items)
        {
            return items.Select(p => new PlaylistSource
            {
                Name = p.Name, Type = p.Type, Url = p.Url, Username = p.Username,
                Password = null, PasswordProtected = CredentialVault.Protect(p.Password),
                MacAddress = null, MacAddressProtected = CredentialVault.Protect(p.MacAddress),
                DeviceType = p.DeviceType, FilePath = p.FilePath, EpgUrl = p.EpgUrl,
                LastCount = p.LastCount, LastLoaded = p.LastLoaded, IsDemo = p.IsDemo
            }).ToList();
        }

        static void RestoreCredentials(IEnumerable<PlaylistSource> items)
        {
            foreach (var source in items)
            {
                if (!String.IsNullOrWhiteSpace(source.PasswordProtected)) source.Password = CredentialVault.Unprotect(source.PasswordProtected);
                if (!String.IsNullOrWhiteSpace(source.MacAddressProtected)) source.MacAddress = CredentialVault.Unprotect(source.MacAddressProtected);
            }
        }
    }

    internal static class SettingsStore
    {
        public static AppSettings Load()
        {
            string file = AppPaths.File("settings.json");
            if (!File.Exists(file)) return new AppSettings();
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                using (var stream = File.OpenRead(file))
                {
                    var settings = serializer.ReadObject(stream) as AppSettings;
                    if (settings == null) return new AppSettings();
                    settings.Normalize();
                    return settings;
                }
            }
            catch { return new AppSettings(); }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                using (var stream = File.Create(AppPaths.File("settings.json"))) serializer.WriteObject(stream, settings);
            }
            catch { }
        }
    }

    internal static class StateStore
    {
        public static string ScopedFile(string name) { return AppPaths.File(ProfileScope.FileName(name)); }

        public static void RenameProfile(string oldName, string newName)
        {
            if (String.IsNullOrWhiteSpace(oldName) || String.IsNullOrWhiteSpace(newName)) return;
            foreach (string name in new[] { "favorites.txt", "recent.txt", "positions.json", "durations.json", "episodes.json" })
            {
                try
                {
                    string from = AppPaths.File(ProfileScope.FileNameFor(oldName, name));
                    string to = AppPaths.File(ProfileScope.FileNameFor(newName, name));
                    if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
                }
                catch { }
            }
        }

        public static void DeleteProfile(string profile)
        {
            if (String.IsNullOrWhiteSpace(profile)) return;
            foreach (string name in new[] { "favorites.txt", "recent.txt", "positions.json", "durations.json", "episodes.json" })
            {
                try
                {
                    string file = AppPaths.File(ProfileScope.FileNameFor(profile, name));
                    if (File.Exists(file)) File.Delete(file);
                }
                catch { }
            }
        }

        /// <summary>
        /// Propriétaire (nom de playlist) de chaque contenu présent dans
        /// l'historique. Ce fichier reste global : supprimer une playlist doit
        /// effacer son historique dans tous les profils, pas seulement le profil
        /// actif.
        /// </summary>
        public static Dictionary<string, string> LoadHistorySources()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string file = AppPaths.File("history-sources.json");
            if (!File.Exists(file)) return result;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as Dictionary<string, string>;
                    if (loaded != null)
                        foreach (var pair in loaded)
                            if (!String.IsNullOrWhiteSpace(pair.Key)) result[pair.Key] = pair.Value ?? "";
                }
            }
            catch { }
            return result;
        }

        public static void SaveHistorySources(Dictionary<string, string> map)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>));
                using (var stream = File.Create(AppPaths.File("history-sources.json")))
                    serializer.WriteObject(stream, map ?? new Dictionary<string, string>());
            }
            catch { }
        }

        public static void RecordHistorySources(IEnumerable<string> ids, string sourceName)
        {
            if (String.IsNullOrWhiteSpace(sourceName) || ids == null) return;
            var map = LoadHistorySources();
            bool changed = false;
            foreach (string id in ids)
            {
                if (String.IsNullOrWhiteSpace(id)) continue;
                string current;
                if (map.TryGetValue(id, out current)
                    && String.Equals(current, sourceName, StringComparison.OrdinalIgnoreCase)) continue;
                map[id] = sourceName;
                changed = true;
            }
            if (changed) SaveHistorySources(map);
        }

        public static void RenameHistorySource(string oldName, string newName)
        {
            if (String.IsNullOrWhiteSpace(oldName) || String.IsNullOrWhiteSpace(newName)) return;
            var map = LoadHistorySources();
            bool changed = false;
            foreach (string id in map.Keys.ToList())
            {
                if (!String.Equals(map[id], oldName, StringComparison.OrdinalIgnoreCase)) continue;
                map[id] = newName;
                changed = true;
            }
            if (changed) SaveHistorySources(map);
        }

        static string HistoryFileFor(string profile, string name)
        {
            return AppPaths.File(ProfileScope.FileNameFor(profile, name));
        }

        static List<string> ReadListFor(string profile, string name)
        {
            var list = new List<string>();
            string file = HistoryFileFor(profile, name);
            if (!File.Exists(file)) return list;
            try { foreach (string line in File.ReadAllLines(file)) if (line.Trim().Length > 0) list.Add(line.Trim()); } catch { }
            return list;
        }

        static void WriteListFor(string profile, string name, IEnumerable<string> values)
        {
            try { AppPaths.EnsureDataDir(); File.WriteAllLines(HistoryFileFor(profile, name), values.ToArray()); } catch { }
        }

        static Dictionary<string, double> LoadPositionsFor(string profile)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string file = HistoryFileFor(profile, "positions.json");
            if (!File.Exists(file)) return result;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, double>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as Dictionary<string, double>;
                    if (loaded != null) foreach (var pair in loaded) result[pair.Key] = pair.Value;
                }
            }
            catch { }
            return result;
        }

        static void SavePositionsFor(string profile, Dictionary<string, double> positions)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var trimmed = positions.OrderByDescending(p => p.Value).Take(250).ToDictionary(p => p.Key, p => p.Value);
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, double>));
                using (var stream = File.Create(HistoryFileFor(profile, "positions.json"))) serializer.WriteObject(stream, trimmed);
            }
            catch { }
        }

        /// <summary>
        /// Supprime l'historique d'une playlist : contenus récents, positions de
        /// reprise, durées connues et épisodes en cours. Les identifiants connus
        /// de la source complètent le fichier de propriété, ce qui couvre aussi
        /// les entrées créées avant le suivi automatique.
        /// </summary>
        public static HashSet<string> PurgeHistoryForSource(string sourceName, IEnumerable<string> knownIds, IEnumerable<string> profiles)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (knownIds != null)
                foreach (string id in knownIds)
                    if (!String.IsNullOrWhiteSpace(id)) ids.Add(id);

            var map = LoadHistorySources();
            foreach (string id in map.Keys.ToList())
            {
                if (String.Equals(map[id], sourceName, StringComparison.OrdinalIgnoreCase)) ids.Add(id);
            }

            // Les épisodes ne figurent pas toujours dans la bibliothèque : leur
            // trace porte l'identifiant de la série, qui suffit à les rattacher.
            var episodes = LoadEpisodes();
            var removedEpisodes = new List<EpisodeRecord>();
            foreach (EpisodeRecord record in episodes)
            {
                if (record == null) continue;
                if (ids.Contains(record.Id) || (!String.IsNullOrWhiteSpace(record.SeriesId) && ids.Contains(record.SeriesId)))
                {
                    if (!String.IsNullOrWhiteSpace(record.Id)) ids.Add(record.Id);
                    removedEpisodes.Add(record);
                }
            }
            if (removedEpisodes.Count > 0)
            {
                foreach (EpisodeRecord record in removedEpisodes) episodes.Remove(record);
                SaveEpisodes(episodes);
            }

            if (ids.Count == 0) return ids;

            foreach (string id in ids) map.Remove(id);
            SaveHistorySources(map);

            var profileNames = new List<string>();
            if (profiles != null)
                foreach (string profile in profiles)
                    if (!String.IsNullOrWhiteSpace(profile)) profileNames.Add(profile);
            if (!profileNames.Contains(ProfileScope.Current)) profileNames.Add(ProfileScope.Current);

            foreach (string profile in profileNames)
            {
                var recent = ReadListFor(profile, "recent.txt");
                int before = recent.Count;
                recent.RemoveAll(id => ids.Contains(id));
                if (recent.Count != before) WriteListFor(profile, "recent.txt", recent);

                var positions = LoadPositionsFor(profile);
                bool changed = false;
                foreach (string id in ids)
                    if (positions.Remove(id)) changed = true;
                if (changed) SavePositionsFor(profile, positions);
            }

            var durations = LoadDurations();
            bool durationsChanged = false;
            foreach (string id in ids)
                if (durations.Remove(id)) durationsChanged = true;
            if (durationsChanged) SaveDurations(durations);
            return ids;
        }

        public static HashSet<string> ReadSet(string name)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string file = ScopedFile(name);
            if (!File.Exists(file)) return set;
            try { foreach (string line in File.ReadAllLines(file)) if (line.Trim().Length > 0) set.Add(line.Trim()); } catch { }
            return set;
        }

        public static void WriteSet(string name, IEnumerable<string> values)
        {
            try { AppPaths.EnsureDataDir(); File.WriteAllLines(ScopedFile(name), values.ToArray()); } catch { }
        }

        public static List<string> ReadList(string name)
        {
            var list = new List<string>();
            string file = ScopedFile(name);
            if (!File.Exists(file)) return list;
            try { foreach (string line in File.ReadAllLines(file)) if (line.Trim().Length > 0) list.Add(line.Trim()); } catch { }
            return list;
        }

        public static void WriteList(string name, IEnumerable<string> values)
        {
            try { AppPaths.EnsureDataDir(); File.WriteAllLines(ScopedFile(name), values.ToArray()); } catch { }
        }

        public static Dictionary<string, double> LoadPositions()
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string file = ScopedFile("positions.json");
            if (!File.Exists(file)) return result;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, double>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as Dictionary<string, double>;
                    if (loaded != null) foreach (var pair in loaded) result[pair.Key] = pair.Value;
                }
            }
            catch { }
            return result;
        }

        public static void SavePositions(Dictionary<string, double> positions)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var trimmed = positions.OrderByDescending(p => p.Value).Take(250).ToDictionary(p => p.Key, p => p.Value);
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, double>));
                using (var stream = File.Create(ScopedFile("positions.json"))) serializer.WriteObject(stream, trimmed);
            }
            catch { }
        }

        /// <summary>
        /// Durée totale connue de chaque contenu : sert à afficher la progression
        /// des cartes « Reprendre la lecture » et le temps restant.
        /// </summary>
        public static Dictionary<string, double> LoadDurations()
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string file = ScopedFile("durations.json");
            if (!File.Exists(file)) return result;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, double>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as Dictionary<string, double>;
                    if (loaded != null) foreach (var pair in loaded) result[pair.Key] = pair.Value;
                }
            }
            catch { }
            return result;
        }

        public static List<EpisodeRecord> LoadEpisodes()
        {
            var result = new List<EpisodeRecord>();
            string file = ScopedFile("episodes.json");
            if (!File.Exists(file)) return result;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<EpisodeRecord>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as List<EpisodeRecord>;
                    if (loaded != null) result.AddRange(loaded);
                }
            }
            catch { }
            return result;
        }

        public static void SaveEpisodes(List<EpisodeRecord> records)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var trimmed = records.Take(120).ToList();
                var serializer = new DataContractJsonSerializer(typeof(List<EpisodeRecord>));
                using (var stream = File.Create(ScopedFile("episodes.json"))) serializer.WriteObject(stream, trimmed);
            }
            catch { }
        }

        public static void SaveDurations(Dictionary<string, double> durations)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var trimmed = durations.OrderByDescending(p => p.Value).Take(250).ToDictionary(p => p.Key, p => p.Value);
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, double>));
                using (var stream = File.Create(ScopedFile("durations.json"))) serializer.WriteObject(stream, trimmed);
            }
            catch { }
        }

        public static PortableData BuildPortable(AppSettings settings, IEnumerable<PlaylistSource> sources, IEnumerable<string> favorites,
            IEnumerable<string> recent, IDictionary<string, double> positions, IEnumerable<UserProfile> profiles,
            IEnumerable<string> hiddenChannels, IEnumerable<string> hiddenGroups)
        {
            var data = new PortableData
            {
                ExportedAt = DateTime.Now.ToString("s"),
                Settings = CloneSettings(settings),
                Favorites = favorites == null ? new List<string>() : favorites.ToList(),
                Recent = recent == null ? new List<string>() : recent.ToList(),
                Positions = positions == null ? new Dictionary<string, double>() : positions.ToDictionary(p => p.Key, p => p.Value),
                HiddenChannels = hiddenChannels == null ? new List<string>() : hiddenChannels.ToList(),
                HiddenGroups = hiddenGroups == null ? new List<string>() : hiddenGroups.ToList()
            };
            data.Settings.TmdbApiKey = "";
            data.Settings.OpenSubtitlesKey = "";
            data.Settings.ParentalPinProtected = "";
            data.Sources = (sources ?? new List<PlaylistSource>()).Select(s => new PortableSource
            {
                Name = s.Name ?? "", Type = s.Type ?? "", Url = s.Url ?? "", Username = s.Username ?? "",
                FilePath = s.FilePath ?? "", EpgUrl = s.EpgUrl ?? "", DeviceType = s.DeviceType ?? "", IsDemo = s.IsDemo
            }).ToList();
            data.Profiles = (profiles ?? new List<UserProfile>()).Select(p => new UserProfile
            {
                Name = p.Name, AvatarColor = p.AvatarColor, IsChild = p.IsChild, Created = p.Created, PinProtected = ""
            }).ToList();
            return data;
        }

        static AppSettings CloneSettings(AppSettings settings)
        {
            var serializer = new DataContractJsonSerializer(typeof(AppSettings));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings ?? new AppSettings());
                stream.Position = 0;
                var clone = serializer.ReadObject(stream) as AppSettings;
                if (clone == null) clone = new AppSettings();
                clone.Normalize();
                return clone;
            }
        }

        public static void WritePortable(string path, PortableData data)
        {
            if (data == null) throw new InvalidDataException("Aucune donnée à exporter.");
            var serializer = new DataContractJsonSerializer(typeof(PortableData));
            using (var stream = File.Create(path)) serializer.WriteObject(stream, data);
        }

        public static PortableData ReadPortable(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Fichier introuvable.", path);
            var serializer = new DataContractJsonSerializer(typeof(PortableData));
            using (var stream = File.OpenRead(path))
            {
                var data = serializer.ReadObject(stream) as PortableData;
                if (data == null || !String.Equals(data.Format, "lyarantv-portable", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Ce fichier n'est pas un export LyaranTV valide.");
                if (data.Version > 1) throw new InvalidDataException("Cet export provient d'une version plus récente de LyaranTV.");
                if (data.Settings == null) data.Settings = new AppSettings();
                data.Settings.Normalize();
                if (data.Sources == null) data.Sources = new List<PortableSource>();
                if (data.Favorites == null) data.Favorites = new List<string>();
                if (data.Recent == null) data.Recent = new List<string>();
                if (data.Positions == null) data.Positions = new Dictionary<string, double>();
                if (data.Profiles == null) data.Profiles = new List<UserProfile>();
                if (data.HiddenChannels == null) data.HiddenChannels = new List<string>();
                if (data.HiddenGroups == null) data.HiddenGroups = new List<string>();
                return data;
            }
        }

        public static void ResetAll()
        {
            try
            {
                if (Directory.Exists(AppPaths.DataDir))
                    foreach (string file in Directory.GetFiles(AppPaths.DataDir)) File.Delete(file);
                if (Directory.Exists(AppPaths.ImageCacheDir)) Directory.Delete(AppPaths.ImageCacheDir, true);
            }
            catch { }
        }

        public static void CreateBackup(string destination)
        {
            AppPaths.EnsureDataDir();
            string temp = Path.Combine(Path.GetTempPath(), "lyarantv-backup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                foreach (string file in Directory.GetFiles(AppPaths.DataDir))
                    File.Copy(file, Path.Combine(temp, Path.GetFileName(file)), true);
                if (File.Exists(destination)) File.Delete(destination);
                System.IO.Compression.ZipFile.CreateFromDirectory(temp, destination);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        public static void RestoreBackup(string source)
        {
            string temp = Path.Combine(Path.GetTempPath(), "lyarantv-restore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(source, temp);
                if (!File.Exists(Path.Combine(temp, "playlists.json")) && !File.Exists(Path.Combine(temp, "settings.json")))
                    throw new InvalidDataException("Cette sauvegarde ne contient pas les données LyaranTV attendues.");
                AppPaths.EnsureDataDir();
                foreach (string file in Directory.GetFiles(temp))
                    File.Copy(file, Path.Combine(AppPaths.DataDir, Path.GetFileName(file)), true);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
