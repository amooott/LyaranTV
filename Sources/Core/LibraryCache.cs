using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LyaranTV
{
    /// <summary>
    /// Copie locale du dernier catalogue chargé. Au démarrage, la bibliothèque
    /// est relue depuis ce fichier en quelques dixièmes de seconde au lieu de
    /// refaire tout le chargement réseau ; l'actualisation n'a lieu que si le
    /// cache est absent, périmé, ou si la source a changé (adresse, compte,
    /// fichier M3U modifié).
    ///
    /// Format binaire compact : un gros compte Xtream (200 000 éléments) tient
    /// dans quelques dizaines de mégaoctets, là où un JSON serait plusieurs fois
    /// plus volumineux et bien plus lent à relire.
    /// </summary>
    internal static class LibraryCache
    {
        const int Magic = 0x4C594331; // "LYC1"
        const int Version = 3;
        const int MaxItems = 5000000;

        /// <summary>Durée au-delà de laquelle une actualisation est proposée.</summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

        public static string FileFor(PlaylistSource source)
        {
            if (source == null) return "";
            // Clé stable sur le nom : modifier l'adresse d'une playlist remplace
            // le même fichier au lieu d'en laisser un orphelin à chaque édition.
            return AppPaths.File("library-" + TextUtil.Hash("cache|" + (source.Name ?? "")) + ".bin");
        }

        /// <summary>
        /// Empreinte de la source : type, adresse, compte et état du fichier M3U.
        /// Toute modification rend le cache inutilisable et déclenche un vrai
        /// chargement.
        /// </summary>
        public static string Fingerprint(PlaylistSource source)
        {
            if (source == null) return "";
            var builder = new StringBuilder();
            builder.Append(source.Type ?? "").Append('|').Append(source.Location ?? "")
                .Append('|').Append(source.Username ?? "").Append('|').Append(source.MacAddress ?? "");
            string file = source.FilePath;
            if (!String.IsNullOrWhiteSpace(file))
            {
                try
                {
                    var info = new FileInfo(file);
                    builder.Append('|').Append(info.Exists ? info.Length : -1)
                        .Append('|').Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
                }
                catch { }
            }
            return TextUtil.Hash(builder.ToString());
        }

        public static bool Exists(PlaylistSource source)
        {
            string path = FileFor(source);
            return path.Length > 0 && File.Exists(path);
        }

        /// <summary>Date d'enregistrement du cache, ou DateTime.MinValue.</summary>
        public static DateTime SavedUtc(PlaylistSource source)
        {
            try
            {
                string path = FileFor(source);
                if (path.Length == 0 || !File.Exists(path)) return DateTime.MinValue;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadInt32() != Magic) return DateTime.MinValue;
                    if (reader.ReadInt32() != Version) return DateTime.MinValue;
                    long ticks = reader.ReadInt64();
                    if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return DateTime.MinValue;
                    return new DateTime(ticks, DateTimeKind.Utc);
                }
            }
            catch { return DateTime.MinValue; }
        }

        /// <summary>Enregistre le catalogue. Appelé hors du fil d'interface.</summary>
        public static void Save(PlaylistSource source, IList<Channel> items, DateTime savedUtc)
        {
            Save(source, items, null, savedUtc);
        }

        /// <summary>Enregistre le catalogue et le guide EPG déjà téléchargé.</summary>
        public static void Save(PlaylistSource source, IList<Channel> items, IList<EpgProgram> guide, DateTime savedUtc)
        {
            if (source == null || items == null) return;
            string path = FileFor(source);
            if (path.Length == 0) return;
            string temp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    writer.Write(savedUtc.Ticks);
                    writer.Write(Fingerprint(source));
                    writer.Write(items.Count);
                    for (int i = 0; i < items.Count; i++) WriteChannel(writer, items[i]);
                    int guideCount = guide == null ? 0 : guide.Count;
                    writer.Write(guideCount);
                    for (int i = 0; i < guideCount; i++) WriteProgram(writer, guide[i]);
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("Cache bibliothèque (" + source.Name + ") : " + ex.GetBaseException().Message); } catch { }
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        /// <summary>Relit le catalogue si l'empreinte correspond toujours.</summary>
        public static bool TryLoad(PlaylistSource source, out List<Channel> items, out DateTime savedUtc)
        {
            List<EpgProgram> guide;
            return TryLoad(source, out items, out guide, out savedUtc);
        }

        /// <summary>Relit le catalogue et le guide EPG mis en cache.</summary>
        public static bool TryLoad(PlaylistSource source, out List<Channel> items, out List<EpgProgram> guide, out DateTime savedUtc)
        {
            guide = null;
            items = null;
            savedUtc = DateTime.MinValue;
            string path = FileFor(source);
            if (path.Length == 0 || !File.Exists(path)) return false;
            string fingerprint = Fingerprint(source);
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadInt32() != Magic) return false;
                    if (reader.ReadInt32() != Version) return false;
                    long ticks = reader.ReadInt64();
                    if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return false;
                    string stored = reader.ReadString();
                    if (!String.Equals(stored, fingerprint, StringComparison.Ordinal)) return false;
                    int count = reader.ReadInt32();
                    if (count <= 0 || count > MaxItems) return false;
                    var list = new List<Channel>(count);
                    for (int i = 0; i < count; i++) list.Add(ReadChannel(reader));
                    var programs = new List<EpgProgram>();
                    if (stream.Position < stream.Length)
                    {
                        int guideCount = reader.ReadInt32();
                        if (guideCount < 0 || guideCount > MaxItems) return false;
                        for (int i = 0; i < guideCount; i++) programs.Add(ReadProgram(reader));
                    }
                    items = list;
                    guide = programs;
                    savedUtc = new DateTime(ticks, DateTimeKind.Utc);
                    return true;
                }
            }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("Cache bibliothèque illisible : " + ex.GetBaseException().Message); } catch { }
                try { File.Delete(path); } catch { }
                return false;
            }
        }

        /// <summary>Supprime toutes les copies locales (réinitialisation, restauration).</summary>
        public static void ClearAll()
        {
            try
            {
                foreach (string file in Directory.GetFiles(AppPaths.DataDir, "library-*.bin"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        public static void Delete(PlaylistSource source)
        {
            try
            {
                string path = FileFor(source);
                if (path.Length > 0 && File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        static void WriteChannel(BinaryWriter writer, Channel channel)
        {
            var value = channel ?? new Channel();
            WriteOpt(writer, value.Name);
            WriteOpt(writer, value.Group);
            WriteOpt(writer, value.Url);
            WriteOpt(writer, value.AlternateUrl);
            WriteOpt(writer, value.Logo);
            WriteOpt(writer, value.Id);
            WriteOpt(writer, value.TvgId);
            WriteOpt(writer, value.Kind);
            WriteOpt(writer, value.StreamId);
            WriteOpt(writer, value.Number);
            WriteOpt(writer, value.Description);
            WriteOpt(writer, value.Rating);
            WriteOpt(writer, value.Year);
            WriteOpt(writer, value.SeriesId);
            WriteOpt(writer, value.Season);
            WriteOpt(writer, value.Episode);
            WriteOpt(writer, value.TmdbId);
            WriteOpt(writer, value.PosterUrl);
            WriteOpt(writer, value.BackdropUrl);
            WriteOpt(writer, value.Overview);
            WriteOpt(writer, value.Genres);
            WriteOpt(writer, value.Runtime);
            WriteOpt(writer, value.Certification);
            WriteOpt(writer, value.TrailerUrl);
            WriteOpt(writer, value.Cast);
            WriteOpt(writer, value.StillUrl);
            WriteOpt(writer, value.HttpUserAgent);
            WriteOpt(writer, value.HttpReferer);
            WriteOpt(writer, value.HttpOrigin);
            WriteOpt(writer, value.HttpCookie);
            writer.Write(value.TmdbRating);
            writer.Write(value.VoteCount);
            writer.Write(value.TmdbChecked);
            writer.Write(value.ArtworkRequested);
            writer.Write(value.IsSeries);
            writer.Write(value.Order);
        }

        static Channel ReadChannel(BinaryReader reader)
        {
            var value = new Channel();
            value.Name = ReadOpt(reader);
            value.Group = ReadOpt(reader);
            value.Url = ReadOpt(reader);
            value.AlternateUrl = ReadOpt(reader);
            value.Logo = ReadOpt(reader);
            value.Id = ReadOpt(reader);
            value.TvgId = ReadOpt(reader);
            value.Kind = ReadOpt(reader);
            value.StreamId = ReadOpt(reader);
            value.Number = ReadOpt(reader);
            value.Description = ReadOpt(reader);
            value.Rating = ReadOpt(reader);
            value.Year = ReadOpt(reader);
            value.SeriesId = ReadOpt(reader);
            value.Season = ReadOpt(reader);
            value.Episode = ReadOpt(reader);
            value.TmdbId = ReadOpt(reader);
            value.PosterUrl = ReadOpt(reader);
            value.BackdropUrl = ReadOpt(reader);
            value.Overview = ReadOpt(reader);
            value.Genres = ReadOpt(reader);
            value.Runtime = ReadOpt(reader);
            value.Certification = ReadOpt(reader);
            value.TrailerUrl = ReadOpt(reader);
            value.Cast = ReadOpt(reader);
            value.StillUrl = ReadOpt(reader);
            value.HttpUserAgent = ReadOpt(reader);
            value.HttpReferer = ReadOpt(reader);
            value.HttpOrigin = ReadOpt(reader);
            value.HttpCookie = ReadOpt(reader);
            value.TmdbRating = reader.ReadDouble();
            value.VoteCount = reader.ReadInt32();
            value.TmdbChecked = reader.ReadBoolean();
            value.ArtworkRequested = reader.ReadBoolean();
            value.IsSeries = reader.ReadBoolean();
            value.Order = reader.ReadInt32();
            return value;
        }

        static void WriteProgram(BinaryWriter writer, EpgProgram program)
        {
            var value = program ?? new EpgProgram();
            WriteOpt(writer, value.ChannelId);
            WriteOpt(writer, value.ChannelName);
            WriteOpt(writer, value.Title);
            WriteOpt(writer, value.Description);
            WriteOpt(writer, value.Category);
            WriteOpt(writer, value.Episode);
            WriteOpt(writer, value.Icon);
            writer.Write(value.Start.Ticks);
            writer.Write(value.Start.Offset.Ticks);
            writer.Write(value.Stop.Ticks);
            writer.Write(value.Stop.Offset.Ticks);
        }

        static EpgProgram ReadProgram(BinaryReader reader)
        {
            var value = new EpgProgram();
            value.ChannelId = ReadOpt(reader);
            value.ChannelName = ReadOpt(reader);
            value.Title = ReadOpt(reader);
            value.Description = ReadOpt(reader);
            value.Category = ReadOpt(reader);
            value.Episode = ReadOpt(reader);
            value.Icon = ReadOpt(reader);
            long startTicks = reader.ReadInt64();
            long startOffset = reader.ReadInt64();
            long stopTicks = reader.ReadInt64();
            long stopOffset = reader.ReadInt64();
            value.Start = new DateTimeOffset(startTicks, new TimeSpan(startOffset));
            value.Stop = new DateTimeOffset(stopTicks, new TimeSpan(stopOffset));
            return value;
        }

        static void WriteOpt(BinaryWriter writer, string value)
        {
            if (String.IsNullOrEmpty(value)) { writer.Write((byte)0); return; }
            writer.Write((byte)1);
            writer.Write(value);
        }

        static string ReadOpt(BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? "" : reader.ReadString();
        }
    }
}
