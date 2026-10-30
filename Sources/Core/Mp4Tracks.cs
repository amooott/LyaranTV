using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LyaranTV
{
    internal sealed class Mp4TrackData
    {
        public int TrackId;
        public uint Flags;
        public uint Timescale = 1;
        public long DurationTicks;
        public string Handler = "";
        public string Format = "";
        public string Language = "";
        public string Title = "";
        public int Channels;
        public bool IsAudio;
        public bool IsText;
        public bool IsSelectable = true;
        public List<long> Offsets = new List<long>();
        public List<int> Sizes = new List<int>();
        public List<double> Starts = new List<double>();
        public List<double> Durations = new List<double>();
        internal readonly List<uint[]> TimeToSample = new List<uint[]>();
        internal readonly List<int[]> SampleToChunk = new List<int[]>();
        internal readonly List<long> ChunkOffsets = new List<long>();
        internal int FixedSampleSize;
        internal int SampleCount;
    }

    /// <summary>
    /// Lecture minimale des boîtes ISO BMFF (MP4, M4V, MOV) pour lister les
    /// pistes et extraire les sous-titres textuels (tx3g, text, WebVTT).
    /// </summary>
    internal static class Mp4TrackReader
    {
        const int MaxMoovBytes = 128 * 1024 * 1024;
        const int MaxSamples = 300000;
        const int MaxSampleBytes = 4 * 1024 * 1024;

        public static void Probe(MediaTrackScan scan)
        {
            byte[] moov = ReadMoov(scan.Path);
            if (moov == null || moov.Length == 0) return;
            var tracks = new List<Mp4TrackData>();
            var reader = new Mp4ByteReader(moov);
            ParseMoov(reader, 0, moov.Length, tracks);
            foreach (var track in tracks)
            {
                if (track.IsAudio || track.Handler == "soun")
                {
                    scan.Audio.Add(CreateInfo(track, "Audio"));
                }
                else if (IsSubtitleHandler(track.Handler) || IsSubtitleFormat(track.Format))
                {
                    scan.Subtitles.Add(CreateInfo(track, "Sous-titres"));
                }
            }
        }

        public static SubtitleTrack ExtractSubtitle(string path, MediaTrackInfo info)
        {
            var data = info.ContainerData as Mp4TrackData;
            if (data == null || data.Offsets.Count == 0) return null;
            var track = new SubtitleTrack { Path = path, Name = info.Label, Format = info.CodecLabel };
            using (var stream = File.OpenRead(path))
            {
                for (int i = 0; i < data.Offsets.Count; i++)
                {
                    if (i >= data.Sizes.Count) break;
                    long offset = data.Offsets[i];
                    int size = data.Sizes[i];
                    if (offset < 0 || size <= 0 || size > MaxSampleBytes || offset + size > stream.Length) continue;
                    var payload = new byte[size];
                    stream.Position = offset;
                    if (!ReadExactly(stream, payload, 0, payload.Length)) continue;
                    string text = DecodeSample(data.Format, payload);
                    if (String.IsNullOrWhiteSpace(text)) continue;
                    double start = i < data.Starts.Count ? data.Starts[i] : 0;
                    double duration = i < data.Durations.Count ? data.Durations[i] : 0;
                    track.Cues.Add(new SubtitleCue { Start = start, End = start + Math.Max(0, duration), Text = text });
                }
            }
            MediaTrackScanner.NormalizeCues(track);
            return track;
        }

        static MediaTrackInfo CreateInfo(Mp4TrackData data, string kind)
        {
            var info = new MediaTrackInfo
            {
                Kind = kind,
                Container = "MP4",
                TrackNumber = data.TrackId,
                Codec = data.Format,
                CodecLabel = MediaTrackScanner.CodecName(data.Format),
                LanguageCode = data.Language,
                Title = data.Title,
                // Dans un MP4, le bit 0 de tkhd signifie « piste activée », pas
                // « piste par défaut » : ne pas l'afficher comme telle.
                IsDefault = false,
                IsText = data.IsText,
                IsSelectable = data.IsSelectable,
                Channels = data.Channels,
                ContainerData = data
            };
            return info;
        }

        static void ParseMoov(Mp4ByteReader reader, int start, int end, List<Mp4TrackData> tracks)
        {
            int position = start;
            while (position + 8 <= end)
            {
                reader.Position = position;
                uint size = reader.UInt32();
                string type = reader.FourCC();
                int header = 8;
                long boxSize = size;
                if (size == 1)
                {
                    if (position + 16 > end) break;
                    boxSize = (long)reader.UInt64();
                    header = 16;
                }
                else if (size == 0)
                {
                    boxSize = end - position;
                }
                if (boxSize < header) break;
                long boxEnd = position + boxSize;
                if (boxEnd > end) boxEnd = end;
                int payloadStart = position + header;
                if (type == "trak")
                {
                    Mp4TrackData track = ParseTrak(reader, payloadStart, (int)boxEnd);
                    if (track != null) tracks.Add(track);
                }
                if (boxEnd <= position) break;
                position = (int)boxEnd;
            }
        }

        static Mp4TrackData ParseTrak(Mp4ByteReader reader, int start, int end)
        {
            var track = new Mp4TrackData();
            bool hasTkhd = false;
            int position = start;
            while (position + 8 <= end)
            {
                reader.Position = position;
                uint size = reader.UInt32();
                string type = reader.FourCC();
                int header = 8;
                long boxSize = size;
                if (size == 1)
                {
                    if (position + 16 > end) break;
                    boxSize = (long)reader.UInt64();
                    header = 16;
                }
                else if (size == 0)
                {
                    boxSize = end - position;
                }
                if (boxSize < header) break;
                long boxEnd = position + boxSize;
                if (boxEnd > end) boxEnd = end;
                int payloadStart = position + header;
                if (type == "tkhd")
                {
                    ParseTkhd(reader, payloadStart, (int)boxEnd, track);
                    hasTkhd = true;
                }
                else if (type == "mdia")
                {
                    ParseMdia(reader, payloadStart, (int)boxEnd, track);
                }
                else if (type == "udta")
                {
                    ParseUdta(reader, payloadStart, (int)boxEnd, track);
                }
                if (boxEnd <= position) break;
                position = (int)boxEnd;
            }
            if (!hasTkhd && String.IsNullOrWhiteSpace(track.Handler)) return null;
            if (track.IsText) BuildSampleTables(track);
            return track;
        }

        static void ParseTkhd(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start;
            int version = reader.Byte();
            track.Flags = reader.UInt24();
            if (version == 1)
            {
                reader.Skip(16);
                track.TrackId = (int)reader.UInt32();
                reader.Skip(4);
                if (reader.Position + 8 <= end) track.DurationTicks = (long)reader.UInt64();
            }
            else
            {
                reader.Skip(8);
                track.TrackId = (int)reader.UInt32();
                reader.Skip(4);
                if (reader.Position + 4 <= end) track.DurationTicks = reader.UInt32();
            }
        }

        static void ParseMdhd(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start;
            int version = reader.Byte();
            reader.Skip(3);
            if (version == 1)
            {
                reader.Skip(16);
                if (reader.Position + 4 > end) return;
                track.Timescale = reader.UInt32();
                if (reader.Position + 8 <= end) track.DurationTicks = (long)reader.UInt64();
                if (reader.Position + 2 <= end) track.Language = DecodeLanguage(reader.UInt16());
            }
            else
            {
                reader.Skip(8);
                if (reader.Position + 4 > end) return;
                track.Timescale = reader.UInt32();
                if (reader.Position + 4 <= end) track.DurationTicks = reader.UInt32();
                if (reader.Position + 2 <= end) track.Language = DecodeLanguage(reader.UInt16());
            }
            if (track.Timescale == 0) track.Timescale = 1;
        }

        static void ParseMdia(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            int position = start;
            while (position + 8 <= end)
            {
                reader.Position = position;
                uint size = reader.UInt32();
                string type = reader.FourCC();
                int header = 8;
                long boxSize = size;
                if (size == 1)
                {
                    if (position + 16 > end) break;
                    boxSize = (long)reader.UInt64();
                    header = 16;
                }
                else if (size == 0)
                {
                    boxSize = end - position;
                }
                if (boxSize < header) break;
                long boxEnd = position + boxSize;
                if (boxEnd > end) boxEnd = end;
                int payloadStart = position + header;
                if (type == "mdhd") ParseMdhd(reader, payloadStart, (int)boxEnd, track);
                else if (type == "hdlr") ParseHdlr(reader, payloadStart, (int)boxEnd, track);
                else if (type == "minf") ParseMinf(reader, payloadStart, (int)boxEnd, track);
                if (boxEnd <= position) break;
                position = (int)boxEnd;
            }
        }

        static void ParseHdlr(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start + 8;
            if (reader.Position + 4 > end) return;
            track.Handler = reader.FourCC();
        }

        static void ParseMinf(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            int position = start;
            while (position + 8 <= end)
            {
                reader.Position = position;
                uint size = reader.UInt32();
                string type = reader.FourCC();
                int header = 8;
                long boxSize = size;
                if (size == 1)
                {
                    if (position + 16 > end) break;
                    boxSize = (long)reader.UInt64();
                    header = 16;
                }
                else if (size == 0)
                {
                    boxSize = end - position;
                }
                if (boxSize < header) break;
                long boxEnd = position + boxSize;
                if (boxEnd > end) boxEnd = end;
                if (type == "stbl") ParseStbl(reader, position + header, (int)boxEnd, track);
                if (boxEnd <= position) break;
                position = (int)boxEnd;
            }
        }

        static void ParseStbl(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            int position = start;
            while (position + 8 <= end)
            {
                reader.Position = position;
                uint size = reader.UInt32();
                string type = reader.FourCC();
                int header = 8;
                long boxSize = size;
                if (size == 1)
                {
                    if (position + 16 > end) break;
                    boxSize = (long)reader.UInt64();
                    header = 16;
                }
                else if (size == 0)
                {
                    boxSize = end - position;
                }
                if (boxSize < header) break;
                long boxEnd = position + boxSize;
                if (boxEnd > end) boxEnd = end;
                int payloadStart = position + header;
                if (type == "stsd") ParseStsd(reader, payloadStart, (int)boxEnd, track);
                else if (type == "stts") ParseStts(reader, payloadStart, (int)boxEnd, track);
                else if (type == "stsc") ParseStsc(reader, payloadStart, (int)boxEnd, track);
                else if (type == "stsz") ParseStsz(reader, payloadStart, (int)boxEnd, track);
                else if (type == "stco") ParseStco(reader, payloadStart, (int)boxEnd, track, false);
                else if (type == "co64") ParseStco(reader, payloadStart, (int)boxEnd, track, true);
                if (boxEnd <= position) break;
                position = (int)boxEnd;
            }
        }

        static void ParseStsd(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start + 4;
            if (reader.Position + 4 > end) return;
            uint count = reader.UInt32();
            if (count == 0) return;
            int position = reader.Position;
            reader.Position = position;
            uint entrySize = reader.UInt32();
            if (entrySize < 8) return;
            string format = reader.FourCC();
            track.Format = format;
            long entryEndLong = position + (long)Math.Min(entrySize, (uint)Int32.MaxValue);
            int entryEnd = (int)Math.Min((long)end, entryEndLong);
            if (IsAudioFormat(format) || track.Handler == "soun")
            {
                track.IsAudio = true;
                if (position + 26 <= entryEnd) track.Channels = (int)(ushort)reader.UInt16At(position + 24);
            }
            else if (IsSubtitleFormat(format) || IsSubtitleHandler(track.Handler))
            {
                track.IsText = true;
                track.IsSelectable = format == "tx3g" || format == "text" || format == "wvtt";
            }
        }

        static void ParseStts(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start + 4;
            if (reader.Position + 4 > end) return;
            uint count = reader.UInt32();
            for (uint i = 0; i < count; i++)
            {
                if (reader.Position + 8 > end) break;
                uint sampleCount = reader.UInt32();
                uint delta = reader.UInt32();
                if (track.TimeToSample.Count < MaxSamples) track.TimeToSample.Add(new[] { sampleCount, delta });
            }
        }

        static void ParseStsc(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start + 4;
            if (reader.Position + 4 > end) return;
            uint count = reader.UInt32();
            for (uint i = 0; i < count; i++)
            {
                if (reader.Position + 12 > end) break;
                int firstChunk = (int)reader.UInt32();
                int samplesPerChunk = (int)reader.UInt32();
                int description = (int)reader.UInt32();
                track.SampleToChunk.Add(new[] { firstChunk, samplesPerChunk, description });
            }
        }

        static void ParseStsz(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            reader.Position = start + 4;
            if (reader.Position + 8 > end) return;
            track.FixedSampleSize = (int)reader.UInt32();
            track.SampleCount = (int)Math.Min((uint)MaxSamples, reader.UInt32());
            if (track.FixedSampleSize == 0)
            {
                for (int i = 0; i < track.SampleCount; i++)
                {
                    if (reader.Position + 4 > end) break;
                    track.Sizes.Add((int)reader.UInt32());
                }
                track.SampleCount = track.Sizes.Count;
            }
        }

        static void ParseStco(Mp4ByteReader reader, int start, int end, Mp4TrackData track, bool wide)
        {
            reader.Position = start + 4;
            if (reader.Position + 4 > end) return;
            uint count = reader.UInt32();
            for (uint i = 0; i < count; i++)
            {
                if (wide)
                {
                    if (reader.Position + 8 > end) break;
                    track.ChunkOffsets.Add((long)reader.UInt64());
                }
                else
                {
                    if (reader.Position + 4 > end) break;
                    track.ChunkOffsets.Add(reader.UInt32());
                }
            }
        }

        static void ParseUdta(Mp4ByteReader reader, int start, int end, Mp4TrackData track)
        {
            int position = start;
            while (position + 8 <= end)
            {
                reader.Position = position;
                uint size = reader.UInt32();
                string type = reader.FourCC();
                int header = 8;
                long boxSize = size;
                if (size == 1)
                {
                    if (position + 16 > end) break;
                    boxSize = (long)reader.UInt64();
                    header = 16;
                }
                else if (size == 0) boxSize = end - position;
                if (boxSize < header) break;
                long boxEnd = position + boxSize;
                if (boxEnd > end) boxEnd = end;
                if (type == "name")
                {
                    int length = (int)Math.Max(0, boxEnd - (position + header));
                    if (length > 0 && length < 4096)
                    {
                        track.Title = reader.Utf8(position + header, length).Trim('\0', ' ', '\r', '\n');
                    }
                }
                if (boxEnd <= position) break;
                position = (int)boxEnd;
            }
        }

        static void BuildSampleTables(Mp4TrackData track)
        {
            int count = track.SampleCount;
            if (count <= 0 && track.Sizes.Count > 0) count = track.Sizes.Count;
            if (count <= 0) return;
            if (track.Sizes.Count == 0 && track.FixedSampleSize > 0)
                for (int i = 0; i < count; i++) track.Sizes.Add(track.FixedSampleSize);
            count = Math.Min(count, track.Sizes.Count);

            double time = 0;
            int sample = 0;
            for (int i = 0; i < track.TimeToSample.Count && sample < count; i++)
            {
                uint repeats = track.TimeToSample[i][0];
                uint delta = track.TimeToSample[i][1];
                double duration = track.Timescale > 0 ? delta / (double)track.Timescale : 0;
                for (uint r = 0; r < repeats && sample < count; r++, sample++)
                {
                    track.Starts.Add(time);
                    track.Durations.Add(duration);
                    time += duration;
                }
            }
            while (track.Starts.Count < count)
            {
                track.Starts.Add(time);
                track.Durations.Add(0);
            }

            if (track.ChunkOffsets.Count == 0) return;
            if (track.SampleToChunk.Count == 0) track.SampleToChunk.Add(new[] { 1, 1, 1 });
            int sampleIndex = 0;
            for (int e = 0; e < track.SampleToChunk.Count && sampleIndex < count; e++)
            {
                int firstChunk = Math.Max(1, track.SampleToChunk[e][0]);
                int samplesPerChunk = Math.Max(0, track.SampleToChunk[e][1]);
                int nextFirst = e + 1 < track.SampleToChunk.Count ? Math.Max(1, track.SampleToChunk[e + 1][0]) : track.ChunkOffsets.Count + 1;
                for (int chunk = firstChunk; chunk < nextFirst && chunk <= track.ChunkOffsets.Count && sampleIndex < count; chunk++)
                {
                    long offset = track.ChunkOffsets[chunk - 1];
                    for (int s = 0; s < samplesPerChunk && sampleIndex < count; s++)
                    {
                        track.Offsets.Add(offset);
                        offset += track.Sizes[sampleIndex];
                        sampleIndex++;
                    }
                }
            }
            while (track.Offsets.Count < count) track.Offsets.Add(-1);
        }

        static string DecodeSample(string format, byte[] payload)
        {
            if (format == "tx3g") return DecodeTimedText(payload);
            if (format == "text") return DecodeQuickTimeText(payload);
            if (format == "wvtt") return DecodeWebVttSample(payload);
            return "";
        }

        static string DecodeTimedText(byte[] payload)
        {
            if (payload == null || payload.Length < 2) return "";
            int length = (payload[0] << 8) | payload[1];
            int end = Math.Min(payload.Length, 2 + Math.Max(0, length));
            if (end <= 2) return "";
            string text;
            if (length >= 2 && payload.Length >= 4 && payload[2] == 0xFE && payload[3] == 0xFF)
                text = Encoding.BigEndianUnicode.GetString(payload, 4, Math.Max(0, end - 4));
            else if (length >= 2 && payload.Length >= 4 && payload[2] == 0xFF && payload[3] == 0xFE)
                text = Encoding.Unicode.GetString(payload, 4, Math.Max(0, end - 4));
            else
                text = Encoding.UTF8.GetString(payload, 2, end - 2);
            return SubtitleParser.CleanText(text);
        }

        static string DecodeQuickTimeText(byte[] payload)
        {
            if (payload == null || payload.Length < 2) return "";
            int length = (payload[0] << 8) | payload[1];
            int end = Math.Min(payload.Length, 2 + Math.Max(0, length));
            if (end <= 2) return "";
            Encoding encoding;
            try { encoding = Encoding.GetEncoding(10000); }
            catch { encoding = Encoding.UTF8; }
            return SubtitleParser.CleanText(encoding.GetString(payload, 2, end - 2));
        }

        static string DecodeWebVttSample(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return "";
            var text = new StringBuilder();
            int position = 0;
            bool foundPayload = false;
            while (position + 8 <= payload.Length)
            {
                uint size = ((uint)payload[position] << 24) | ((uint)payload[position + 1] << 16) | ((uint)payload[position + 2] << 8) | payload[position + 3];
                string type = Encoding.ASCII.GetString(payload, position + 4, 4);
                if (size < 8 || position + (long)size > payload.Length) break;
                if (type == "vttc") ParseVttc(payload, position + 8, position + (int)size, text, ref foundPayload);
                else if (type == "payl")
                {
                    text.Append(Encoding.UTF8.GetString(payload, position + 8, (int)size - 8));
                    foundPayload = true;
                }
                position += (int)size;
            }
            if (!foundPayload) text.Append(Encoding.UTF8.GetString(payload));
            return SubtitleParser.CleanText(text.ToString());
        }

        static void ParseVttc(byte[] payload, int start, int end, StringBuilder text, ref bool found)
        {
            int position = start;
            while (position + 8 <= end)
            {
                uint size = ((uint)payload[position] << 24) | ((uint)payload[position + 1] << 16) | ((uint)payload[position + 2] << 8) | payload[position + 3];
                string type = Encoding.ASCII.GetString(payload, position + 4, 4);
                if (size < 8 || position + (long)size > end) break;
                if (type == "payl")
                {
                    text.Append(Encoding.UTF8.GetString(payload, position + 8, (int)size - 8));
                    found = true;
                }
                position += (int)size;
            }
        }

        static bool IsAudioFormat(string format)
        {
            switch (format)
            {
                case "mp4a": case "ac-3": case "ec-3": case "alac": case "fLaC": case "Opus":
                case "dtsc": case "dtsh": case "dtsl": case "dtse": case ".mp3": case "samr":
                case "sowt": case "twos": case "lpcm": case "raw ": case "ulaw": case "alaw":
                    return true;
                default: return false;
            }
        }

        static bool IsSubtitleFormat(string format)
        {
            switch (format)
            {
                case "tx3g": case "text": case "wvtt": case "stpp": case "c608": case "c708":
                    return true;
                default: return false;
            }
        }

        static bool IsSubtitleHandler(string handler)
        {
            return handler == "sbtl" || handler == "subt" || handler == "text" || handler == "clcp";
        }

        static string DecodeLanguage(int code)
        {
            if (code == 0) return "";
            char a = (char)(((code >> 10) & 0x1F) + 0x60);
            char b = (char)(((code >> 5) & 0x1F) + 0x60);
            char c = (char)((code & 0x1F) + 0x60);
            string value = new string(new[] { a, b, c });
            return value == "und" ? "" : value;
        }

        static byte[] ReadMoov(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                long length = stream.Length;
                long position = 0;
                var header = new byte[16];
                while (position + 8 <= length)
                {
                    stream.Position = position;
                    if (!ReadExactly(stream, header, 0, 8)) break;
                    uint size = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
                    string type = Encoding.ASCII.GetString(header, 4, 4);
                    int headerSize = 8;
                    ulong boxSize = size;
                    if (size == 1)
                    {
                        if (!ReadExactly(stream, header, 0, 8)) break;
                        boxSize = ((ulong)header[0] << 56) | ((ulong)header[1] << 48) | ((ulong)header[2] << 40) | ((ulong)header[3] << 32)
                            | ((ulong)header[4] << 24) | ((ulong)header[5] << 16) | ((ulong)header[6] << 8) | header[7];
                        headerSize = 16;
                    }
                    else if (size == 0)
                    {
                        boxSize = (ulong)(length - position);
                    }
                    if (boxSize < (ulong)headerSize || boxSize > Int64.MaxValue) break;
                    long boxEnd = position + (long)boxSize;
                    if (boxEnd > length) boxEnd = length;
                    if (type == "moov")
                    {
                        long payload = boxEnd - position - headerSize;
                        if (payload <= 0 || payload > MaxMoovBytes) return null;
                        var buffer = new byte[payload];
                        stream.Position = position + headerSize;
                        if (!ReadExactly(stream, buffer, 0, buffer.Length)) return null;
                        return buffer;
                    }
                    if (boxEnd <= position) break;
                    position = boxEnd;
                }
            }
            return null;
        }

        static bool ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0) return false;
                total += read;
            }
            return true;
        }
    }

    /// <summary>
    /// Petit lecteur big-endian borné, utilisé sur le contenu de la boîte moov.
    /// </summary>
    internal sealed class Mp4ByteReader
    {
        readonly byte[] _data;
        public int Position;

        public Mp4ByteReader(byte[] data) { _data = data; }

        public byte Byte()
        {
            if (Position < 0 || Position >= _data.Length) throw new EndOfStreamException();
            return _data[Position++];
        }

        public ushort UInt16()
        {
            if (Position + 2 > _data.Length) throw new EndOfStreamException();
            int value = (_data[Position] << 8) | _data[Position + 1];
            Position += 2;
            return (ushort)value;
        }

        public ushort UInt16At(int offset)
        {
            if (offset < 0 || offset + 2 > _data.Length) return 0;
            return (ushort)((_data[offset] << 8) | _data[offset + 1]);
        }

        public uint UInt24()
        {
            if (Position + 3 > _data.Length) throw new EndOfStreamException();
            uint value = ((uint)_data[Position] << 16) | ((uint)_data[Position + 1] << 8) | _data[Position + 2];
            Position += 3;
            return value;
        }

        public uint UInt32()
        {
            if (Position + 4 > _data.Length) throw new EndOfStreamException();
            uint value = ((uint)_data[Position] << 24) | ((uint)_data[Position + 1] << 16) | ((uint)_data[Position + 2] << 8) | _data[Position + 3];
            Position += 4;
            return value;
        }

        public ulong UInt64()
        {
            if (Position + 8 > _data.Length) throw new EndOfStreamException();
            ulong value = 0;
            for (int i = 0; i < 8; i++) value = (value << 8) | _data[Position + i];
            Position += 8;
            return value;
        }

        public string FourCC()
        {
            if (Position + 4 > _data.Length) throw new EndOfStreamException();
            string value = Encoding.ASCII.GetString(_data, Position, 4);
            Position += 4;
            return value;
        }

        public string Utf8(int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset + length > _data.Length) return "";
            return Encoding.UTF8.GetString(_data, offset, length);
        }

        public void Skip(int count)
        {
            Position += Math.Max(0, count);
        }
    }
}
