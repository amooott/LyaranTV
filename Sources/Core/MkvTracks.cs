using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LyaranTV
{
    internal sealed class MkvTrackData
    {
        public long Number;
        public int TrackType;
        public string Codec = "";
        public string Language = "";
        public string Title = "";
        public bool IsDefault = true;
        public bool IsForced;
        public int Channels;
        public byte[] CodecPrivate;
        public bool IsText;
        public bool IsSelectable;
        internal int AssTextIndex = -1;
        internal int AssFieldCount = -1;
    }

    /// <summary>
    /// Lecture minimale du format EBML/Matroska : pistes, langue et extraction
    /// des sous-titres textuels. Les sous-titres image (PGS, VobSub) sont
    /// signalés mais ne peuvent pas être affichés par la superposition WPF.
    /// </summary>
    internal static class MkvTrackReader
    {
        const long SegmentId = 0x18538067;
        const long InfoId = 0x1549A966;
        const long TimestampScaleId = 0x2AD7B1;
        const long TracksId = 0x1654AE6B;
        const long TrackEntryId = 0xAE;
        const long TrackNumberId = 0xD7;
        const long TrackTypeId = 0x83;
        const long CodecId = 0x86;
        const long CodecPrivateId = 0x63A2;
        const long TrackNameId = 0x536E;
        const long TrackLanguageId = 0x22B59C;
        const long TrackLanguageIetfId = 0x22B59D;
        const long FlagDefaultId = 0x88;
        const long FlagForcedId = 0x55AA;
        const long AudioId = 0xE1;
        const long ChannelsId = 0x9F;
        const long ClusterId = 0x1F43B675;
        const long TimestampId = 0xE7;
        const long SimpleBlockId = 0xA3;
        const long BlockGroupId = 0xA0;
        const long BlockId = 0xA1;
        const long BlockDurationId = 0x9B;
        const int MaxTracksBytes = 8 * 1024 * 1024;

        public static void Probe(MediaTrackScan scan)
        {
            using (var stream = File.OpenRead(scan.Path))
            using (var reader = new EbmlReader(stream))
            {
                long segmentStart, segmentEnd;
                if (!FindSegment(reader, out segmentStart, out segmentEnd)) return;
                reader.Seek(segmentStart);
                while (reader.Position < segmentEnd)
                {
                    long id, size, dataStart;
                    if (!reader.ReadElement(segmentEnd, out id, out size, out dataStart)) break;
                    long end = dataStart + size;
                    if (id == TracksId)
                    {
                        long length = size;
                        if (length <= 0 || length > MaxTracksBytes) return;
                        byte[] payload = reader.ReadBytes(length);
                        ParseTracks(payload, scan);
                        return;
                    }
                    reader.Seek(end);
                }
            }
        }

        public static SubtitleTrack ExtractSubtitle(string path, MediaTrackInfo info)
        {
            var target = info.ContainerData as MkvTrackData;
            if (target == null) return null;
            PrepareAssText(target);
            var track = new SubtitleTrack { Path = path, Name = info.Label, Format = info.CodecLabel };
            using (var stream = File.OpenRead(path))
            using (var reader = new EbmlReader(stream))
            {
                long segmentStart, segmentEnd;
                if (!FindSegment(reader, out segmentStart, out segmentEnd)) return null;
                long timestampScale = 1000000;
                var cues = new List<SubtitleCue>();
                reader.Seek(segmentStart);
                while (reader.Position < segmentEnd)
                {
                    long id, size, dataStart;
                    if (!reader.ReadElement(segmentEnd, out id, out size, out dataStart)) break;
                    long end = dataStart + size;
                    if (id == InfoId) ParseInfo(reader, dataStart, end, ref timestampScale);
                    else if (id == ClusterId) ParseCluster(reader, dataStart, end, target, timestampScale, cues);
                    reader.Seek(end);
                }
                track.Cues = cues;
            }
            MediaTrackScanner.NormalizeCues(track);
            return track;
        }

        static void ParseTracks(byte[] payload, MediaTrackScan scan)
        {
            using (var stream = new MemoryStream(payload))
            using (var reader = new EbmlReader(stream))
            {
                long end = payload.Length;
                while (reader.Position < end)
                {
                    long id, size, dataStart;
                    if (!reader.ReadElement(end, out id, out size, out dataStart)) break;
                    long itemEnd = dataStart + size;
                    if (id == TrackEntryId)
                    {
                        MkvTrackData track = ParseTrackEntry(reader, dataStart, itemEnd);
                        if (track != null) AddTrack(scan, track);
                    }
                    reader.Seek(itemEnd);
                }
            }
        }

        static MkvTrackData ParseTrackEntry(EbmlReader reader, long start, long end)
        {
            reader.Seek(start);
            var track = new MkvTrackData();
            bool flagDefaultSeen = false;
            while (reader.Position < end)
            {
                long id, size, dataStart;
                if (!reader.ReadElement(end, out id, out size, out dataStart)) break;
                long itemEnd = dataStart + size;
                switch (id)
                {
                    case TrackNumberId: track.Number = reader.ReadUInt(size); break;
                    case TrackTypeId: track.TrackType = (int)reader.ReadUInt(size); break;
                    case CodecId: track.Codec = reader.ReadString(size); break;
                    case CodecPrivateId: track.CodecPrivate = reader.ReadBytes(size); break;
                    case TrackNameId: track.Title = reader.ReadString(size); break;
                    case TrackLanguageId: track.Language = reader.ReadString(size); break;
                    case TrackLanguageIetfId:
                        string ietf = reader.ReadString(size);
                        if (!String.IsNullOrWhiteSpace(ietf)) track.Language = ietf;
                        break;
                    case FlagDefaultId:
                        track.IsDefault = reader.ReadUInt(size) != 0;
                        flagDefaultSeen = true;
                        break;
                    case FlagForcedId: track.IsForced = reader.ReadUInt(size) != 0; break;
                    case AudioId: ParseAudio(reader, dataStart, itemEnd, track); break;
                }
                reader.Seek(itemEnd);
            }
            if (!flagDefaultSeen) track.IsDefault = true;
            if (track.TrackType != 2 && track.TrackType != 17) return null;
            track.IsText = IsTextCodec(track.Codec);
            track.IsSelectable = track.TrackType == 2 || track.IsText;
            return track;
        }

        static void ParseAudio(EbmlReader reader, long start, long end, MkvTrackData track)
        {
            reader.Seek(start);
            while (reader.Position < end)
            {
                long id, size, dataStart;
                if (!reader.ReadElement(end, out id, out size, out dataStart)) break;
                if (id == ChannelsId) track.Channels = (int)reader.ReadUInt(size);
                reader.Seek(dataStart + size);
            }
        }

        static void AddTrack(MediaTrackScan scan, MkvTrackData track)
        {
            var info = new MediaTrackInfo
            {
                Kind = track.TrackType == 2 ? "Audio" : "Sous-titres",
                Container = "MKV",
                TrackNumber = track.Number,
                Codec = track.Codec,
                CodecLabel = MediaTrackScanner.CodecName(track.Codec),
                LanguageCode = track.Language,
                Title = track.Title,
                IsDefault = track.IsDefault,
                IsForced = track.IsForced,
                IsText = track.IsText,
                IsSelectable = track.IsSelectable,
                Channels = track.Channels,
                ContainerData = track
            };
            if (track.TrackType == 2) scan.Audio.Add(info);
            else scan.Subtitles.Add(info);
        }

        static void ParseInfo(EbmlReader reader, long start, long end, ref long timestampScale)
        {
            reader.Seek(start);
            while (reader.Position < end)
            {
                long id, size, dataStart;
                if (!reader.ReadElement(end, out id, out size, out dataStart)) break;
                if (id == TimestampScaleId)
                {
                    long value = reader.ReadUInt(size);
                    if (value > 0) timestampScale = value;
                }
                reader.Seek(dataStart + size);
            }
        }

        static void ParseCluster(EbmlReader reader, long start, long end, MkvTrackData target, long timestampScale, List<SubtitleCue> cues)
        {
            reader.Seek(start);
            long clusterTimestamp = 0;
            while (reader.Position < end)
            {
                long id, size, dataStart;
                if (!reader.ReadElement(end, out id, out size, out dataStart)) break;
                long itemEnd = dataStart + size;
                if (id == TimestampId)
                {
                    clusterTimestamp = reader.ReadUInt(size);
                }
                else if (id == SimpleBlockId)
                {
                    ParseBlock(reader, dataStart, itemEnd, target, clusterTimestamp, timestampScale, cues);
                }
                else if (id == BlockGroupId)
                {
                    ParseBlockGroup(reader, dataStart, itemEnd, target, clusterTimestamp, timestampScale, cues);
                }
                reader.Seek(itemEnd);
            }
        }

        static void ParseBlockGroup(EbmlReader reader, long start, long end, MkvTrackData target, long clusterTimestamp, long timestampScale, List<SubtitleCue> cues)
        {
            reader.Seek(start);
            long duration = 0;
            int firstCue = cues.Count;
            while (reader.Position < end)
            {
                long id, size, dataStart;
                if (!reader.ReadElement(end, out id, out size, out dataStart)) break;
                long itemEnd = dataStart + size;
                if (id == BlockId) ParseBlock(reader, dataStart, itemEnd, target, clusterTimestamp, timestampScale, cues);
                else if (id == BlockDurationId) duration = reader.ReadUInt(size);
                reader.Seek(itemEnd);
            }
            if (duration > 0)
            {
                double seconds = duration * timestampScale / 1000000000.0;
                for (int i = firstCue; i < cues.Count; i++)
                {
                    if (cues[i].End <= cues[i].Start) cues[i].End = cues[i].Start + seconds;
                }
            }
        }

        static void ParseBlock(EbmlReader reader, long dataStart, long end, MkvTrackData target, long clusterTimestamp, long timestampScale, List<SubtitleCue> cues)
        {
            reader.Seek(dataStart);
            long trackNumber;
            int length;
            if (!reader.TryReadVintValue(out trackNumber, out length)) return;
            if (trackNumber != target.Number) return;
            if (reader.Position + 3 > end) return;
            int relative = reader.ReadInt16();
            int flags = reader.ReadByte();
            long remaining = end - reader.Position;
            if (remaining < 0 || remaining > 16 * 1024 * 1024) return;
            byte[] payload = reader.ReadBytes(remaining);
            ParseFrames(payload, flags, target, clusterTimestamp + relative, timestampScale, cues);
        }

        static void ParseFrames(byte[] payload, int flags, MkvTrackData target, long timestamp, long timestampScale, List<SubtitleCue> cues)
        {
            int lacing = flags & 0x06;
            if (lacing == 0)
            {
                AddCue(payload, 0, payload.Length, target, timestamp, timestampScale, cues, 0);
                return;
            }
            if (payload.Length == 0) return;
            int frames = payload[0] + 1;
            int position = 1;
            var sizes = new int[frames];
            if (lacing == 0x02)
            {
                for (int i = 0; i < frames - 1; i++)
                {
                    int size = 0;
                    int value;
                    do
                    {
                        if (position >= payload.Length) return;
                        value = payload[position++];
                        size += value;
                    } while (value == 255);
                    sizes[i] = size;
                }
                int used = Sum(sizes, frames - 1);
                sizes[frames - 1] = payload.Length - position - used;
            }
            else if (lacing == 0x04)
            {
                int remaining = payload.Length - position;
                int size = frames > 0 ? remaining / frames : 0;
                for (int i = 0; i < frames; i++) sizes[i] = size;
                if (frames > 0) sizes[frames - 1] = remaining - size * (frames - 1);
            }
            else if (lacing == 0x06)
            {
                long firstSize;
                int firstLength;
                if (!ReadVint(payload, ref position, false, out firstSize, out firstLength)) return;
                sizes[0] = (int)Math.Min(Int32.MaxValue, Math.Max(0, firstSize));
                for (int i = 1; i < frames - 1; i++)
                {
                    long raw;
                    int deltaLength;
                    if (!ReadVint(payload, ref position, false, out raw, out deltaLength)) return;
                    long bias = (1L << (7 * deltaLength - 1)) - 1;
                    long delta = raw - bias;
                    sizes[i] = Math.Max(0, sizes[i - 1] + (int)delta);
                }
                int used = Sum(sizes, frames - 1);
                sizes[frames - 1] = payload.Length - position - used;
            }
            else return;

            for (int i = 0; i < frames; i++)
            {
                int size = sizes[i];
                if (size < 0 || position + size > payload.Length) return;
                AddCue(payload, position, size, target, timestamp, timestampScale, cues, 0);
                position += size;
            }
        }

        static void AddCue(byte[] payload, int offset, int count, MkvTrackData target, long timestamp, long timestampScale, List<SubtitleCue> cues, long explicitDuration)
        {
            if (count <= 0) return;
            string text = DecodeText(payload, offset, count, target);
            if (String.IsNullOrWhiteSpace(text)) return;
            double start = Math.Max(0, timestamp) * timestampScale / 1000000000.0;
            cues.Add(new SubtitleCue { Start = start, End = explicitDuration > 0 ? start + explicitDuration * timestampScale / 1000000000.0 : 0, Text = text });
        }

        static string DecodeText(byte[] payload, int offset, int count, MkvTrackData target)
        {
            string raw = Encoding.UTF8.GetString(payload, offset, count);
            if (IsAssCodec(target.Codec))
            {
                if (target.AssFieldCount > 0 && CountCommas(raw) >= target.AssFieldCount - 1)
                {
                    string[] parts = raw.Split(new[] { ',' }, target.AssFieldCount);
                    if (target.AssTextIndex >= 0 && target.AssTextIndex < parts.Length) raw = parts[target.AssTextIndex];
                    else if (parts.Length > 0) raw = parts[parts.Length - 1];
                }
                raw = raw.Replace("\\N", "\n").Replace("\\n", "\n").Replace("\\h", " ");
            }
            return SubtitleParser.CleanText(raw);
        }

        static void PrepareAssText(MkvTrackData track)
        {
            if (track == null || !IsAssCodec(track.Codec) || track.CodecPrivate == null) return;
            string header;
            try { header = Encoding.UTF8.GetString(track.CodecPrivate); }
            catch { return; }
            foreach (string line in header.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase)) continue;
                string[] fields = trimmed.Substring(7).Split(',');
                for (int i = 0; i < fields.Length; i++)
                {
                    if (String.Equals(fields[i].Trim(), "Text", StringComparison.OrdinalIgnoreCase))
                    {
                        track.AssTextIndex = i;
                        track.AssFieldCount = fields.Length;
                        return;
                    }
                }
            }
        }

        static bool IsAssCodec(string codec)
        {
            return !String.IsNullOrEmpty(codec) &&
                (codec.IndexOf("ASS", StringComparison.OrdinalIgnoreCase) >= 0 || codec.IndexOf("SSA", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static bool IsTextCodec(string codec)
        {
            if (String.IsNullOrWhiteSpace(codec)) return false;
            if (codec.StartsWith("S_TEXT/", StringComparison.OrdinalIgnoreCase)) return true;
            if (codec.StartsWith("D_WEBVTT/", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static int CountCommas(string value)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++) if (value[i] == ',') count++;
            return count;
        }

        static int Sum(int[] values, int count)
        {
            int total = 0;
            for (int i = 0; i < count; i++) total += values[i];
            return total;
        }

        static bool ReadVint(byte[] data, ref int position, bool keepMarker, out long value, out int length)
        {
            value = 0;
            length = 0;
            if (position >= data.Length) return false;
            int first = data[position];
            if (first == 0) return false;
            length = 1;
            int mask = 0x80;
            while (length <= 8 && (first & mask) == 0) { length++; mask >>= 1; }
            if (length > 8 || position + length > data.Length) return false;
            long result = keepMarker ? first : first & (mask - 1);
            for (int i = 1; i < length; i++) result = (result << 8) | data[position + i];
            position += length;
            value = result;
            return true;
        }

        static bool FindSegment(EbmlReader reader, out long segmentStart, out long segmentEnd)
        {
            segmentStart = 0;
            segmentEnd = 0;
            reader.Seek(0);
            while (reader.Position < reader.Length)
            {
                long id, size, dataStart;
                if (!reader.ReadElement(reader.Length, out id, out size, out dataStart)) break;
                long end = dataStart + size;
                if (id == SegmentId)
                {
                    segmentStart = dataStart;
                    segmentEnd = end;
                    return true;
                }
                reader.Seek(end);
            }
            return false;
        }
    }

    /// <summary>
    /// Lecteur EBML séquentiel avec tailles variables et sauts.
    /// </summary>
    internal sealed class EbmlReader : IDisposable
    {
        readonly Stream _stream;

        public EbmlReader(Stream stream) { _stream = stream; }

        public long Position { get { return _stream.Position; } }
        public long Length { get { return _stream.Length; } }

        public void Seek(long position)
        {
            if (position < 0) position = 0;
            if (position > Length) position = Length;
            _stream.Position = position;
        }

        public bool ReadElement(long parentEnd, out long id, out long size, out long dataStart)
        {
            id = 0;
            size = 0;
            dataStart = 0;
            if (Position >= parentEnd || Position >= Length) return false;
            int length;
            if (!ReadVint(true, out id, out length)) return false;
            ulong rawSize;
            if (!ReadVint(false, out rawSize, out length)) return false;
            dataStart = Position;
            if (rawSize == UInt64.MaxValue || rawSize > Int64.MaxValue) size = parentEnd - dataStart;
            else size = (long)rawSize;
            if (size < 0 || dataStart + size > parentEnd) size = Math.Max(0, parentEnd - dataStart);
            return true;
        }

        public bool TryReadVintValue(out long value, out int length)
        {
            ulong raw;
            bool ok = ReadVint(false, out raw, out length);
            value = raw > Int64.MaxValue ? -1 : (long)raw;
            return ok;
        }

        public int ReadByte()
        {
            int value = _stream.ReadByte();
            if (value < 0) throw new EndOfStreamException();
            return value;
        }

        public int ReadInt16()
        {
            int high = ReadByte();
            int low = ReadByte();
            return (short)((high << 8) | low);
        }

        public byte[] ReadBytes(long size)
        {
            if (size <= 0) return new byte[0];
            if (size > Int32.MaxValue) throw new InvalidDataException("Bloc EBML trop volumineux.");
            var buffer = new byte[(int)size];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = _stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0) break;
                total += read;
            }
            if (total < buffer.Length) Array.Resize(ref buffer, total);
            return buffer;
        }

        public long ReadUInt(long size)
        {
            if (size <= 0) return 0;
            long value = 0;
            long limit = Math.Min(size, 8);
            for (long i = 0; i < limit; i++)
            {
                int current = ReadByte();
                value = (value << 8) | (uint)current;
            }
            if (size > 8) Seek(Position + (size - 8));
            return value;
        }

        public string ReadString(long size)
        {
            byte[] bytes = ReadBytes(size);
            return Encoding.UTF8.GetString(bytes).Trim('\0', ' ', '\r', '\n');
        }

        bool ReadVint(bool keepMarker, out long value, out int length)
        {
            ulong raw;
            bool ok = ReadVint(false, out raw, out length);
            value = raw > Int64.MaxValue ? -1 : (long)raw;
            if (!ok) { value = 0; return false; }
            if (keepMarker)
            {
                // Relit l'identifiant complet : le marqueur fait partie de la valeur.
                Seek(Position - length);
                int first = ReadByte();
                long full = first;
                for (int i = 1; i < length; i++) full = (full << 8) | (uint)ReadByte();
                value = full;
            }
            return true;
        }

        bool ReadVint(bool keepMarkerUnused, out ulong value, out int length)
        {
            value = 0;
            length = 0;
            int first = _stream.ReadByte();
            if (first < 0 || first == 0) return false;
            length = 1;
            int mask = 0x80;
            while (length <= 8 && (first & mask) == 0) { length++; mask >>= 1; }
            if (length > 8) return false;
            ulong result = (ulong)(first & (mask - 1));
            for (int i = 1; i < length; i++)
            {
                int current = _stream.ReadByte();
                if (current < 0) return false;
                result = (result << 8) | (uint)current;
            }
            // Taille inconnue : tous les bits de données valent 1.
            if (result == (1UL << (7 * length)) - 1) value = UInt64.MaxValue;
            else value = result;
            return true;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
