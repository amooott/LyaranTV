using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace LyaranTV
{
    [DataContract]
    internal sealed class ArtworkEpisode
    {
        [DataMember] public string Season = "";
        [DataMember] public string Number = "";
        [DataMember] public string Still = "";
        [DataMember] public string Overview = "";
        [DataMember] public string Runtime = "";
        [DataMember] public string Name = "";
    }

    /// <summary>
    /// Affiche et informations de secours trouvées hors TMDb (TVmaze pour les
    /// séries et leurs épisodes, iTunes pour les films).
    /// </summary>
    [DataContract]
    internal sealed class ArtworkRecord
    {
        [DataMember] public string Key = "";
        [DataMember] public string Title = "";
        [DataMember] public string Poster = "";
        [DataMember] public string Overview = "";
        [DataMember] public string Genres = "";
        [DataMember] public string Year = "";
        [DataMember] public double Rating;
        [DataMember] public string Source = "";
        [DataMember] public string Updated = "";
        [DataMember] public List<ArtworkEpisode> Episodes = new List<ArtworkEpisode>();

        public bool HasPoster { get { return !String.IsNullOrWhiteSpace(Poster); } }

        public ArtworkEpisode Find(string season, string number)
        {
            if (Episodes == null || Episodes.Count == 0) return null;
            string wantedSeason = (season ?? "").Trim();
            string wantedNumber = (number ?? "").Trim();
            if (wantedSeason.Length == 0 || wantedNumber.Length == 0) return null;
            foreach (var episode in Episodes)
            {
                if (episode == null) continue;
                if (String.Equals(episode.Season, wantedSeason, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(episode.Number, wantedNumber, StringComparison.OrdinalIgnoreCase)) return episode;
            }
            return null;
        }
    }

    internal static class ArtworkStore
    {
        static readonly object Gate = new object();
        static Dictionary<string, ArtworkRecord> entries;

        public static string Key(string title, bool series)
        {
            return TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(title)) + "|" + (series ? "tv" : "movie");
        }

        static void EnsureLoaded()
        {
            if (entries != null) return;
            entries = new Dictionary<string, ArtworkRecord>(StringComparer.OrdinalIgnoreCase);
            string file = AppPaths.File("artwork-cache.json");
            if (!File.Exists(file)) return;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<ArtworkRecord>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as List<ArtworkRecord>;
                    if (loaded != null) foreach (var entry in loaded)
                        if (entry != null && !String.IsNullOrWhiteSpace(entry.Key)) entries[entry.Key] = entry;
                }
            }
            catch { }
        }

        public static ArtworkRecord Get(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) return null;
            lock (Gate)
            {
                EnsureLoaded();
                ArtworkRecord record;
                return entries.TryGetValue(key, out record) ? record : null;
            }
        }

        public static void Put(string key, ArtworkRecord record)
        {
            if (String.IsNullOrWhiteSpace(key) || record == null) return;
            record.Key = key;
            record.Updated = DateTime.Now.ToString("s");
            lock (Gate)
            {
                EnsureLoaded();
                entries[key] = record;
            }
        }

        /// <summary>Mémorise qu'aucune affiche n'existe pour ce titre, pour ne pas redemander.</summary>
        public static void PutMissing(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) return;
            lock (Gate)
            {
                EnsureLoaded();
                entries[key] = new ArtworkRecord { Key = key, Source = "aucun", Updated = DateTime.Now.ToString("s") };
            }
        }

        public static bool HasMissing(string key)
        {
            var record = Get(key);
            if (record == null || record.Source != "aucun") return false;
            DateTime updated;
            if (!DateTime.TryParse(record.Updated, out updated)) return false;
            // Un échec est retenté au bout de cinq jours : les catalogues
            // en ligne évoluent et un titre peut désormais être référencé.
            return (DateTime.Now - updated).TotalDays < 5;
        }

        public static int Count
        {
            get { lock (Gate) { EnsureLoaded(); return entries.Count; } }
        }

        public static void Save()
        {
            lock (Gate)
            {
                if (entries == null) return;
                try
                {
                    AppPaths.EnsureDataDir();
                    var serializer = new DataContractJsonSerializer(typeof(List<ArtworkRecord>));
                    using (var stream = File.Create(AppPaths.File("artwork-cache.json")))
                        serializer.WriteObject(stream, entries.Values.OrderBy(e => e.Key).ToList());
                }
                catch { }
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                entries = new Dictionary<string, ArtworkRecord>(StringComparer.OrdinalIgnoreCase);
                try { if (File.Exists(AppPaths.File("artwork-cache.json"))) File.Delete(AppPaths.File("artwork-cache.json")); } catch { }
            }
        }
    }

    /// <summary>
    /// Complète les affiches manquantes sans clé API : TVmaze (séries, épisodes,
    /// synopsis) et iTunes (films). Les requêtes sont mises en file, limitées en
    /// cadence et mises en cache sur disque, comme TMDb.
    /// </summary>
    internal static class ArtworkResolver
    {
        public const string Attribution = "Affiches et synopsis complémentaires : TVmaze, IMDb et Wikipédia.";

        sealed class Job
        {
            public string Key;
            public string Title;
            public bool Series;
            public string Year;
            public List<Action<ArtworkRecord>> Callbacks = new List<Action<ArtworkRecord>>();
        }

        static readonly object Gate = new object();
        static readonly List<Job> Queue = new List<Job>();
        static int running;
        // Trois requêtes en parallèle et une cadence plus vive : les affiches de
        // l'accueil apparaissent en quelques secondes, pas après une minute.
        const int MaxWorkers = 3;
        static int requestsThisSession;
        const int MaxRequestsPerSession = 600;
        static DateTime lastSave = DateTime.MinValue;

        public static bool IsDone(string title, bool series)
        {
            return ArtworkStore.Get(ArtworkStore.Key(title, series)) != null;
        }

        public static ArtworkRecord Peek(string title, bool series)
        {
            return ArtworkStore.Get(ArtworkStore.Key(title, series));
        }

        /// <summary>Demande une fiche ; le rappel reçoit null si rien n'a été trouvé.</summary>
        public static void Request(string title, bool series, string year, Action<ArtworkRecord> callback)
        {
            string clean = TmdbTitleCleaner.CleanTitle(title);
            if (String.IsNullOrWhiteSpace(clean))
            {
                if (callback != null) callback(null);
                return;
            }
            string key = ArtworkStore.Key(clean, series);
            ArtworkRecord cached = ArtworkStore.Get(key);
            if (cached != null && (cached.HasPoster || cached.Source != "aucun"))
            {
                if (callback != null) callback(cached);
                return;
            }
            // Une absence n'est plus définitive : après quelques jours, une
            // nouvelle source ou une meilleure correspondance peut la combler.
            if (cached != null && ArtworkStore.HasMissing(key))
            {
                if (callback != null) callback(null);
                return;
            }
            lock (Gate)
            {
                Job pending = Queue.FirstOrDefault(j => String.Equals(j.Key, key, StringComparison.OrdinalIgnoreCase));
                if (pending != null)
                {
                    if (callback != null) pending.Callbacks.Add(callback);
                    return;
                }
                if (requestsThisSession >= MaxRequestsPerSession)
                {
                    if (callback != null) callback(null);
                    return;
                }
                var job = new Job { Key = key, Title = clean, Series = series, Year = year };
                if (callback != null) job.Callbacks.Add(callback);
                Queue.Add(job);
                if (running < MaxWorkers)
                {
                    running++;
                    ThreadPool.QueueUserWorkItem(Run);
                }
            }
        }

        static void Run(object state)
        {
            while (true)
            {
                Job job;
                lock (Gate)
                {
                    if (Queue.Count == 0) { running--; SaveIfNeeded(); return; }
                    job = Queue[0];
                    Queue.RemoveAt(0);
                    if (requestsThisSession >= MaxRequestsPerSession) { Queue.Clear(); running--; return; }
                    requestsThisSession++;
                }
                ArtworkRecord record = null;
                try { record = job.Series ? FetchSeries(job.Title, job.Year) : FetchMovie(job.Title, job.Year); }
                catch (Exception ex)
                {
                    try { HttpUtil.AppendLog("Affiche " + (job.Series ? "série" : "film") + " « " + job.Title + " » : " + ex.GetBaseException().Message); }
                    catch { }
                }
                if (record == null) ArtworkStore.PutMissing(job.Key);
                else ArtworkStore.Put(job.Key, record);

                var dispatcher = System.Windows.Application.Current == null ? null : System.Windows.Application.Current.Dispatcher;
                if (dispatcher != null)
                {
                    var captured = record;
                    var callbacks = job.Callbacks;
                    dispatcher.BeginInvoke(new Action(delegate
                    {
                        foreach (var callback in callbacks) { try { callback(captured); } catch { } }
                    }));
                }
                // TVmaze et IMDb sont gratuits mais pas illimités : une requête
                // 280 ms entre deux requêtes reste sous les limites publiques de
                // TVmaze / iTunes tout en gardant une interface réactive.
                try { Thread.Sleep(280); } catch { }
            }
        }

        static void SaveIfNeeded()
        {
            if ((DateTime.UtcNow - lastSave).TotalSeconds < 8) return;
            lastSave = DateTime.UtcNow;
            ArtworkStore.Save();
        }

        // ------------------------------------------------------------------ TVmaze

        static ArtworkRecord FetchSeries(string title, string year)
        {
            ArtworkRecord record = FetchTvmaze(title);
            if (record == null || !record.HasPoster)
            {
                // TVmaze ne couvre pas tout et n'a pas toujours d'affiche :
                // l'image IMDb complète la fiche.
                var imdb = FetchImdb(title, true, year);
                if (imdb != null)
                {
                    if (record == null) record = imdb;
                    else
                    {
                        if (String.IsNullOrWhiteSpace(record.Poster)) record.Poster = imdb.Poster;
                        if (String.IsNullOrWhiteSpace(record.Year)) record.Year = imdb.Year;
                        record.Source = "TVmaze/IMDb";
                    }
                }
                if (record != null && String.IsNullOrWhiteSpace(record.Overview)) record.Overview = FetchWikipedia(title, true);
            }
            return record;
        }

        static ArtworkRecord FetchTvmaze(string title)
        {
            string text;
            try { text = HttpUtil.GetString("https://api.tvmaze.com/singlesearch/shows?q=" + Uri.EscapeDataString(title), 15000, "LyaranTV/2.0"); }
            catch { return null; }
            var root = Json(text);
            if (root == null) return null;
            string name = Val(root, "name");
            if (!LooksSame(title, name)) return null;
            var record = new ArtworkRecord
            {
                Title = name,
                Source = "TVmaze",
                Overview = PlainText(Val(root, "summary")),
                Genres = JoinArray(Arr(root, "genres")),
                Year = FirstYear(Val(root, "premiered"))
            };
            var image = Child(root, "image");
            if (image != null) record.Poster = Val(image, "original");
            if (record.Poster.Length == 0 && image != null) record.Poster = Val(image, "medium");
            var rating = Child(root, "rating");
            if (rating != null) record.Rating = Num(rating, "average");
            int id = (int)Num(root, "id");
            if (id > 0) LoadEpisodes(record, id);
            return record.HasPoster || record.Overview.Length > 0 ? record : null;
        }

        static void LoadEpisodes(ArtworkRecord record, int showId)
        {
            string text;
            try { text = HttpUtil.GetString("https://api.tvmaze.com/shows/" + showId.ToString(CultureInfo.InvariantCulture) + "/episodes", 20000, "LyaranTV/2.0"); }
            catch { return; }
            foreach (object entry in RootArray(text))
            {
                var item = entry as Dictionary<string, object>;
                if (item == null) continue;
                var image = Child(item, "image");
                var episode = new ArtworkEpisode
                {
                    Season = Val(item, "season"),
                    Number = Val(item, "number"),
                    Name = Val(item, "name"),
                    Still = image == null ? "" : Val(image, "original"),
                    Overview = PlainText(Val(item, "summary")),
                    Runtime = Val(item, "runtime")
                };
                if (episode.Still.Length == 0 && image != null) episode.Still = Val(image, "medium");
                if (record.Episodes.Count >= 800) break;
                record.Episodes.Add(episode);
            }
        }

        // ------------------------------------------------------------------- IMDb

        static ArtworkRecord FetchMovie(string title, string year)
        {
            ArtworkRecord record = FetchImdb(title, false, year);
            if (record == null || !record.HasPoster)
            {
                // Le catalogue iTunes complète IMDb : il référence des films
                // absents des suggestions et fournit une affiche de bonne taille.
                var itunes = FetchItunes(title, year);
                if (itunes != null)
                {
                    if (record == null) record = itunes;
                    else if (String.IsNullOrWhiteSpace(record.Poster))
                    {
                        record.Poster = itunes.Poster;
                        if (String.IsNullOrWhiteSpace(record.Year)) record.Year = itunes.Year;
                        record.Source += "/iTunes";
                    }
                }
            }
            if (record == null) return null;
            if (String.IsNullOrWhiteSpace(record.Overview)) record.Overview = FetchWikipedia(title, false);
            return record;
        }

        /// <summary>Affiche et année via le catalogue public iTunes, sans clé.</summary>
        static ArtworkRecord FetchItunes(string title, string year)
        {
            string url = "https://itunes.apple.com/search?media=movie&entity=movie&limit=6&term=" + Uri.EscapeDataString(title);
            Dictionary<string, object> root;
            try { root = Json(HttpUtil.GetString(url, 12000, "LyaranTV/2.0")); }
            catch { return null; }
            if (root == null) return null;
            Dictionary<string, object> best = null;
            int bestScore = 0;
            foreach (object entry in Arr(root, "results"))
            {
                var item = entry as Dictionary<string, object>;
                if (item == null) continue;
                string name = Val(item, "trackName");
                if (!LooksSame(title, name)) continue;
                string artwork = Val(item, "artworkUrl100");
                if (String.IsNullOrWhiteSpace(artwork)) continue;
                int score = 60;
                string releaseYear = FirstYear(Val(item, "releaseDate"));
                if (year.Length > 0 && releaseYear.Length > 0 && year == releaseYear) score += 30;
                if (score > bestScore) { bestScore = score; best = item; }
            }
            if (best == null) return null;
            string poster = Val(best, "artworkUrl100").Replace("100x100bb", "600x600bb");
            if (String.IsNullOrWhiteSpace(poster)) return null;
            return new ArtworkRecord
            {
                Title = Val(best, "trackName"),
                Source = "iTunes",
                Poster = poster,
                Year = FirstYear(Val(best, "releaseDate"))
            };
        }

        /// <summary>
        /// API de suggestion d'IMDb (celle de la barre de recherche du site) :
        /// aucune clé, affiches pour films et séries. Le type renvoyé sert à
        /// distinguer un long métrage d'une série ou d'un court-métrage.
        /// </summary>
        static ArtworkRecord FetchImdb(string title, bool series, string year)
        {
            string query = Uri.EscapeDataString(TextUtil.NormalizeKey(title));
            if (query.Length == 0) return null;
            Dictionary<string, object> root;
            try { root = Json(HttpUtil.GetString("https://v3.sg.media-imdb.com/suggestion/x/" + query + ".json", 12000, "LyaranTV/2.0")); }
            catch { return null; }
            if (root == null) return null;
            Dictionary<string, object> best = null;
            int bestScore = 0;
            foreach (object entry in Arr(root, "d"))
            {
                var item = entry as Dictionary<string, object>;
                if (item == null) continue;
                string name = Val(item, "l");
                string type = Val(item, "q");
                bool isSeries = type.IndexOf("series", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isSeries != series) continue;
                int score = Similarity(title, name) * 10;
                if (score == 0) continue;
                // Un long métrage passe avant un court-métrage ou une vidéo, mais
                // ces derniers restent acceptés faute de mieux.
                if (!series)
                {
                    if (String.Equals(type, "short", StringComparison.OrdinalIgnoreCase)) score -= 9;
                    else if (String.Equals(type, "video", StringComparison.OrdinalIgnoreCase)) score -= 6;
                    else if (String.Equals(type, "tv movie", StringComparison.OrdinalIgnoreCase)) score -= 2;
                    else score += 2;
                    if (score <= 0) continue;
                }
                string foundYear = Val(item, "y");
                if (foundYear.Length >= 4 && !String.IsNullOrWhiteSpace(year) && foundYear.StartsWith(year.Substring(0, Math.Min(4, year.Length)), StringComparison.Ordinal)) score += 4;
                var image = Child(item, "i");
                if (image != null && !String.IsNullOrWhiteSpace(Val(image, "imageUrl"))) score += 3;
                if (score <= bestScore) continue;
                best = item;
                bestScore = score;
            }
            if (best == null) return null;
            var art = Child(best, "i");
            string poster = art == null ? "" : Val(art, "imageUrl");
            if (poster.Length == 0) return null;
            return new ArtworkRecord
            {
                Title = Val(best, "l"),
                Poster = poster,
                Year = Val(best, "y"),
                Source = "IMDb"
            };
        }

        /// <summary>Synopsis d'appoint (Wikipédia) quand aucune autre source n'en fournit.</summary>
        static string FetchWikipedia(string title, bool series)
        {
            string query = title + (series ? " série télévisée" : " film");
            string url = "https://fr.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch="
                + Uri.EscapeDataString(query) + "&gsrlimit=5&prop=extracts&exintro=1&explaintext=1&format=json&formatversion=2";
            Dictionary<string, object> root;
            try { root = Json(HttpUtil.GetString(url, 12000, "LyaranTV/2.0")); }
            catch { return ""; }
            var pages = Arr(Child(root, "query"), "pages");
            foreach (object entry in pages)
            {
                var page = entry as Dictionary<string, object>;
                if (page == null) continue;
                if (Similarity(title, Val(page, "title")) == 0) continue;
                string extract = Val(page, "extract");
                if (!String.IsNullOrWhiteSpace(extract)) return TextUtil.Ellipsis(extract.Replace("\n", " ").Trim(), 900);
            }
            return "";
        }

        // ------------------------------------------------------------------ outils

        /// <summary>Applique une fiche à un contenu, sans écraser les données TMDb.</summary>
        public static void Apply(Channel channel, ArtworkRecord record)
        {
            if (channel == null || record == null || !record.HasPoster) return;
            if (String.IsNullOrWhiteSpace(channel.PosterUrl)) channel.PosterUrl = record.Poster;
            if (String.IsNullOrWhiteSpace(channel.Overview)) channel.Overview = record.Overview;
            if (String.IsNullOrWhiteSpace(channel.Description)) channel.Description = record.Overview;
            if (String.IsNullOrWhiteSpace(channel.Genres)) channel.Genres = record.Genres;
            if (String.IsNullOrWhiteSpace(channel.Year)) channel.Year = record.Year;
            if (channel.TmdbRating <= 0 && record.Rating > 0) channel.TmdbRating = record.Rating;
            if (String.IsNullOrWhiteSpace(channel.Rating) && record.Rating > 0)
                channel.Rating = record.Rating.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static void ApplyEpisode(Channel episode, ArtworkRecord record)
        {
            if (episode == null || record == null) return;
            var still = record.Find(episode.Season, episode.Episode);
            if (still == null) return;
            if (String.IsNullOrWhiteSpace(episode.StillUrl)) episode.StillUrl = still.Still;
            if (String.IsNullOrWhiteSpace(episode.Description)) episode.Description = still.Overview;
            if (String.IsNullOrWhiteSpace(episode.Overview)) episode.Overview = still.Overview;
            if (String.IsNullOrWhiteSpace(episode.Runtime) && !String.IsNullOrWhiteSpace(still.Runtime))
            {
                int minutes;
                if (Int32.TryParse(still.Runtime, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes) && minutes > 0)
                    episode.Runtime = minutes.ToString(CultureInfo.InvariantCulture) + " min";
            }
        }

        /// <summary>Un titre de résultat est-il bien le même contenu ?</summary>
        static bool LooksSame(string wanted, string found)
        {
            return Similarity(wanted, found) >= 2;
        }

        /// <summary>2 = même titre, 1 = l'un contient l'autre, 0 = différent.</summary>
        static int Similarity(string wanted, string found)
        {
            string left = TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(wanted));
            string right = TextUtil.NormalizeKey(found ?? "");
            if (left.Length == 0 || right.Length == 0) return 0;
            if (left == right) return 2;
            if (right.StartsWith(left + " ", StringComparison.Ordinal) || left.StartsWith(right + " ", StringComparison.Ordinal)) return 1;
            return 0;
        }

        static string FirstYear(string value)
        {
            string text = (value ?? "").Trim();
            if (text.Length < 4) return "";
            for (int i = 0; i + 4 <= text.Length; i++)
            {
                if (!Char.IsDigit(text[i]) || !Char.IsDigit(text[i + 1]) || !Char.IsDigit(text[i + 2]) || !Char.IsDigit(text[i + 3])) continue;
                int year;
                if (Int32.TryParse(text.Substring(i, 4), out year) && year >= 1900 && year <= 2100) return year.ToString(CultureInfo.InvariantCulture);
            }
            return "";
        }

        static string PlainText(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            var builder = new StringBuilder(value.Length);
            bool inside = false;
            foreach (char c in value)
            {
                if (c == '<') { inside = true; continue; }
                if (c == '>') { inside = false; continue; }
                if (!inside) builder.Append(c);
            }
            string text = builder.ToString();
            try { text = WebUtility.HtmlDecode(text); } catch { }
            return text.Replace("  ", " ").Trim();
        }

        static string JoinArray(object[] values)
        {
            if (values == null || values.Length == 0) return "";
            var parts = new List<string>();
            foreach (object value in values)
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(text) && !parts.Contains(text)) parts.Add(text.Trim());
            }
            return String.Join(", ", parts.ToArray());
        }

        /// <summary>Décode un flux JSON quelconque (racine objet ou tableau).</summary>
        static Dictionary<string, object> Json(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                return serializer.DeserializeObject(text) as Dictionary<string, object>;
            }
            catch { return null; }
        }

        static object[] Arr(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value)) return new object[0];
            return value as object[] ?? new object[0];
        }

        /// <summary>Tableau racine d'une réponse JSON (liste d'épisodes TVmaze).</summary>
        static object[] RootArray(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return new object[0];
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                return serializer.DeserializeObject(text) as object[] ?? new object[0];
            }
            catch { return new object[0]; }
        }

        static Dictionary<string, object> Child(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value)) return null;
            return value as Dictionary<string, object>;
        }

        static string Val(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        static double Num(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return 0;
            double number;
            return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
        }
    }
}
