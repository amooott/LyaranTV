using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace LyaranTV
{
    internal static class HttpUtil
    {
        static HttpUtil()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
            try { ServicePointManager.DefaultConnectionLimit = 16; } catch { }
        }

        public static string GetString(string url, int timeoutMs, string userAgent)
        {
            byte[] bytes = GetBytes(url, timeoutMs, userAgent);
            return DecodeText(bytes);
        }

        public static string GetString(string url) { return GetString(url, 20000, "LyaranTV/2.0"); }

        /// <summary>
        /// Requête d'API JSON : le moteur standard est essayé en premier, avec
        /// un délai court et sans keep-alive, puis le client IPv4 en repli.
        /// L'ordre inverse (IPv4 d'abord) faisait attendre chaque adresse
        /// injoignable du CDN, d'où des recherches qui semblaient ne jamais
        /// aboutir. Le délai total reste borné dans tous les cas.
        /// </summary>
        public static string ApiGet(string url, int timeoutMs)
        {
            return ApiRequest(url, "GET", null, timeoutMs, "LyaranTV/2.0", null);
        }

        public static string ApiRequest(string url, string method, string body, int timeoutMs, string userAgent,
            IDictionary<string, string> headers)
        {
            if (!TextUtil.IsHttp(url)) throw new InvalidDataException("Adresse HTTP/HTTPS invalide : " + (url ?? ""));
            int budget = Math.Max(3000, Math.Min(timeoutMs, 15000));
            Uri uri = null;
            bool https = Uri.TryCreate(url, UriKind.Absolute, out uri)
                && String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            Exception failure = null;
            try
            {
                return ApiStandard(url, method, body, budget, userAgent, headers);
            }
            catch (Exception ex)
            {
                var web = ex as WebException;
                // Une réponse HTTP réelle (401, 404, 429…) ne doit pas déclencher
                // une seconde requête : elle est traduite telle quelle.
                if (web != null && web.Response != null) throw new InvalidDataException(DescribeException(web, url), web);
                failure = ex;
                try { AppendLog("API standard · " + uri.Host + " · " + ex.GetBaseException().Message + " (repli IPv4)"); } catch { }
            }
            if (https && !HasProxy(uri))
            {
                try
                {
                    byte[] payload = body == null ? null : Encoding.UTF8.GetBytes(body);
                    return DecodeText(DownloadHttpsIpv4(uri, Math.Min(budget, 7000), userAgent, method, payload, headers));
                }
                catch (Exception ex) { failure = ex; }
            }
            throw new InvalidDataException(DescribeException(failure, url), failure);
        }

        static string ApiStandard(string url, string method, string body, int timeoutMs, string userAgent,
            IDictionary<string, string> headers)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = String.IsNullOrWhiteSpace(method) ? "GET" : method;
            request.UserAgent = String.IsNullOrWhiteSpace(userAgent) ? "LyaranTV/2.0" : userAgent;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.AllowAutoRedirect = true;
            request.KeepAlive = false;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Accept = "application/json";
            if (headers != null)
                foreach (var header in headers)
                    if (!String.IsNullOrWhiteSpace(header.Key)) request.Headers[header.Key] = header.Value ?? "";
            if (body != null)
            {
                byte[] payload = Encoding.UTF8.GetBytes(body);
                request.ContentType = "application/json";
                request.ContentLength = payload.Length;
                using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var memory = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) memory.Write(buffer, 0, read);
                return DecodeText(memory.ToArray());
            }
        }

        /// <summary>
        /// Requête HTTPS avec méthode, corps et en-têtes (API JSON), en IPv4
        /// d'abord. Utilisée par OpenSubtitles : le moteur standard attendait
        /// ~25 s sur l'IPv6 injoignable de certains réseaux avant d'échouer.
        /// Conservée pour les appels qui ont besoin du client IPv4 direct.
        /// </summary>
        public static string Request(string url, string method, string body, int timeoutMs, string userAgent,
            IDictionary<string, string> headers)
        {
            if (!TextUtil.IsHttp(url)) throw new InvalidDataException("Adresse HTTP/HTTPS invalide : " + (url ?? ""));
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri)
                && String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !HasProxy(uri))
            {
                try
                {
                    byte[] payload = body == null ? null : Encoding.UTF8.GetBytes(body);
                    return DecodeText(DownloadHttpsIpv4(uri, Math.Max(2000, timeoutMs), userAgent, method, payload, headers));
                }
                catch (InvalidDataException) { throw; }
                catch (Exception ipv4Error)
                {
                    try { AppendLog("HTTPS IPv4 · " + uri.Host + " · " + ipv4Error.GetBaseException().Message + " (repli)"); } catch { }
                }
            }
            return RequestStandard(url, method, body, timeoutMs, userAgent, headers);
        }

        static string RequestStandard(string url, string method, string body, int timeoutMs, string userAgent,
            IDictionary<string, string> headers)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = String.IsNullOrWhiteSpace(method) ? "GET" : method;
            request.UserAgent = String.IsNullOrWhiteSpace(userAgent) ? "LyaranTV/2.0" : userAgent;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Accept = "application/json";
            if (headers != null)
                foreach (var header in headers)
                    if (!String.IsNullOrWhiteSpace(header.Key)) request.Headers[header.Key] = header.Value ?? "";
            try
            {
                if (body != null)
                {
                    byte[] payload = Encoding.UTF8.GetBytes(body);
                    request.ContentType = "application/json";
                    request.ContentLength = payload.Length;
                    using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);
                }
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var memory = new MemoryStream())
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) memory.Write(buffer, 0, read);
                    return DecodeText(memory.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(DescribeException(ex, url), ex);
            }
        }

        public static byte[] GetBytes(string url, int timeoutMs, string userAgent)
        {
            if (!TextUtil.IsHttp(url)) throw new InvalidDataException("Adresse HTTP/HTTPS invalide : " + (url ?? ""));
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri)
                && String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !HasProxy(uri))
            {
                // L'IPv6 annoncé par certains réseaux est injoignable : le moteur
                // historique attend alors ~20 s avant de basculer en IPv4, bien
                // au-delà des délais des cartes. On télécharge donc directement
                // en IPv4, puis on retombe sur le moteur standard en cas d'échec
                // (proxy, certificat, serveur particulier).
                try { return DownloadHttpsIpv4(uri, timeoutMs, userAgent, "GET", null, null); }
                catch (InvalidDataException) { throw; }
                catch (Exception ipv4Error)
                {
                    try { AppendLog("HTTPS IPv4 · " + uri.Host + " · " + ipv4Error.GetBaseException().Message + " (repli)"); } catch { }
                }
            }
            return GetBytesStandard(url, timeoutMs, userAgent);
        }

        static byte[] GetBytesStandard(string url, int timeoutMs, string userAgent)
        {
            try { return GetBytesStandardRaw(url, timeoutMs, userAgent); }
            catch (Exception ex)
            {
                throw new InvalidDataException(DescribeException(ex, url), ex);
            }
        }

        static byte[] GetBytesStandardRaw(string url, int timeoutMs, string userAgent)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = String.IsNullOrWhiteSpace(userAgent) ? "LyaranTV/2.0" : userAgent;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Accept = "*/*";
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var memory = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) memory.Write(buffer, 0, read);
                return memory.ToArray();
            }
        }

        public static byte[] GetBytes(string url) { return GetBytes(url, 20000, "LyaranTV/2.0"); }

        /// <summary>
        /// Téléchargement d'image : le moteur standard passe en premier (délai
        /// court), le client IPv4 ne sert qu'en repli. Une affiche ne doit
        /// jamais immobiliser la file d'images derrière une adresse CDN morte.
        /// </summary>
        public static byte[] GetBytesStandardFirst(string url, int timeoutMs, string userAgent)
        {
            if (!TextUtil.IsHttp(url)) throw new InvalidDataException("Adresse HTTP/HTTPS invalide : " + (url ?? ""));
            int budget = Math.Max(2000, timeoutMs);
            Exception failure = null;
            try { return GetBytesStandardRaw(url, budget, userAgent); }
            catch (Exception ex)
            {
                var web = ex as WebException;
                if (web != null && web.Response != null) throw new InvalidDataException(DescribeException(web, url), web);
                failure = ex;
            }
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri)
                && String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !HasProxy(uri))
            {
                try { return DownloadHttpsIpv4(uri, Math.Min(budget, 5000), userAgent, "GET", null, null); }
                catch (Exception ex) { failure = ex; }
            }
            throw new InvalidDataException(DescribeException(failure, url), failure);
        }

        /// <summary>Vrai si une passerelle système relaie cette adresse.</summary>
        static bool HasProxy(Uri uri)
        {
            try
            {
                IWebProxy proxy = WebRequest.GetSystemWebProxy();
                if (proxy == null) return false;
                Uri relay = proxy.GetProxy(uri);
                return relay != null && !String.Equals(relay.Host, uri.Host, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        sealed class HttpsResponse
        {
            public int Status;
            public string Location = "";
            public byte[] Body = new byte[0];
        }

        // Les affiches arrivent par dizaines du même hôte : une résolution DNS
        // par image ajoutait plusieurs secondes à l'ouverture d'une rangée.
        static readonly object DnsGate = new object();
        static readonly Dictionary<string, List<IPAddress>> DnsCache = new Dictionary<string, List<IPAddress>>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, DateTime> DnsExpiry = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        static List<IPAddress> ResolveIpv4(string host)
        {
            lock (DnsGate)
            {
                DateTime expiry;
                List<IPAddress> cached;
                if (DnsCache.TryGetValue(host, out cached) && DnsExpiry.TryGetValue(host, out expiry) && expiry > DateTime.UtcNow) return cached;
            }
            var addresses = new List<IPAddress>();
            foreach (IPAddress address in Dns.GetHostAddresses(host))
                if (address.AddressFamily == AddressFamily.InterNetwork) addresses.Add(address);
            lock (DnsGate)
            {
                if (DnsCache.Count > 256) { DnsCache.Clear(); DnsExpiry.Clear(); }
                DnsCache[host] = addresses;
                DnsExpiry[host] = DateTime.UtcNow.AddMinutes(10);
            }
            return addresses;
        }

        /// <summary>
        /// Téléchargement HTTPS en IPv4 seulement : résolution A, connexion
        /// directe avec un délai borné, TLS 1.2 avec SNI, puis lecture de la
        /// réponse (Content-Length, chunked, gzip éventuel). Les redirections
        /// HTTPS sont suivies. Une erreur HTTP réelle est signalée par une
        /// InvalidDataException, sans nouvelle tentative inutile.
        /// </summary>
        static byte[] DownloadHttpsIpv4(Uri uri, int timeoutMs, string userAgent)
        {
            return DownloadHttpsIpv4(uri, timeoutMs, userAgent, "GET", null, null);
        }

        static byte[] DownloadHttpsIpv4(Uri uri, int timeoutMs, string userAgent, string method, byte[] body,
            IDictionary<string, string> headers)
        {
            int budget = Math.Max(2000, timeoutMs);
            for (int hop = 0; hop < 5; hop++)
            {
                HttpsResponse response = FetchHttpsIpv4(uri, budget, userAgent, method, body, headers);
                if (response.Status >= 300 && response.Status < 400 && response.Location.Length > 0)
                {
                    Uri next;
                    if (Uri.TryCreate(uri, response.Location, out next)
                        && String.Equals(next.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    {
                        uri = next;
                        continue;
                    }
                    throw new InvalidDataException("Redirection vers une adresse non sécurisée refusée : " + response.Location);
                }
                if (response.Status >= 400)
                {
                    if (response.Status == 401 || response.Status == 403) throw new InvalidDataException("Accès refusé par " + uri.Host + " (HTTP " + response.Status + ").");
                    if (response.Status == 404) throw new InvalidDataException("Adresse introuvable sur " + uri.Host + " (HTTP 404).");
                    throw new InvalidDataException("Erreur HTTP " + response.Status + " renvoyée par " + uri.Host + ".");
                }
                return response.Body;
            }
            throw new InvalidDataException("Trop de redirections pour " + uri.Host + ".");
        }

        static HttpsResponse FetchHttpsIpv4(Uri uri, int timeoutMs, string userAgent, string method, byte[] body,
            IDictionary<string, string> headers)
        {
            if (String.IsNullOrWhiteSpace(method)) method = "GET";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var addresses = ResolveIpv4(uri.Host);
            if (addresses.Count == 0) throw new IOException("Aucune adresse IPv4 pour " + uri.Host + ".");

            TcpClient client = ConnectIpv4(addresses, uri.Port, Math.Max(1500, timeoutMs));
            try
            {
                using (var ssl = new SslStream(client.GetStream(), false))
                {
                    int remaining = Math.Max(1500, timeoutMs - (int)watch.ElapsedMilliseconds);
                    ssl.ReadTimeout = remaining;
                    ssl.WriteTimeout = remaining;
                    ssl.AuthenticateAsClient(uri.Host, null, SslProtocols.Tls12, false);

                    string path = String.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
                    var request = new StringBuilder();
                    request.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\nHost: ").Append(uri.Host)
                        .Append("\r\nUser-Agent: ").Append(String.IsNullOrWhiteSpace(userAgent) ? "LyaranTV/4.0" : userAgent)
                        .Append("\r\nAccept: ").Append(headers != null && headers.ContainsKey("Accept") ? headers["Accept"] : "*/*")
                        .Append("\r\nAccept-Encoding: identity\r\nConnection: close\r\n");
                    if (headers != null)
                    {
                        foreach (var entry in headers)
                        {
                            // Le nom et la valeur sont validés : une en-tête
                            // forgée ne peut pas couper la requête en deux.
                            string name = (entry.Key ?? "").Trim();
                            string value = (entry.Value ?? "").Replace("\r", "").Replace("\n", "").Trim();
                            if (name.Length == 0 || name == "Accept" || name == "Host" || name == "Content-Length" || name == "Connection") continue;
                            request.Append(name).Append(": ").Append(value).Append("\r\n");
                        }
                    }
                    if (body != null && body.Length > 0)
                        request.Append("Content-Type: application/json\r\nContent-Length: ").Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                    request.Append("\r\n");
                    byte[] header = Encoding.ASCII.GetBytes(request.ToString());
                    ssl.Write(header, 0, header.Length);
                    if (body != null && body.Length > 0) ssl.Write(body, 0, body.Length);
                    ssl.Flush();

                    using (var memory = new MemoryStream())
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = ssl.Read(buffer, 0, buffer.Length)) > 0) memory.Write(buffer, 0, read);
                        return ParseHttpsResponse(memory.ToArray(), uri.Host);
                    }
                }
            }
            finally { try { client.Close(); } catch { } }
        }

        static TcpClient ConnectIpv4(List<IPAddress> addresses, int port, int timeoutMs)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Exception last = null;
            int attempts = 0;
            foreach (IPAddress address in addresses)
            {
                // Deux adresses suffisent : au-delà, un CDN qui répond mal
                // immobiliserait la requête. Chaque essai reste court pour que
                // le repli standard prenne le relais rapidement.
                if (++attempts > 3) break;
                int remaining = timeoutMs - (int)watch.ElapsedMilliseconds;
                if (remaining < 300) break;
                var client = new TcpClient(AddressFamily.InterNetwork);
                try
                {
                    var connect = client.ConnectAsync(address, port);
                    if (connect.Wait(Math.Min(remaining, 2500))) return client;
                    last = new TimeoutException("Connexion à " + address + " expirée.");
                }
                catch (Exception error) { last = error; }
                try { client.Close(); } catch { }
            }
            throw new IOException("Connexion impossible : " + (last == null ? "aucune adresse IPv4" : last.GetBaseException().Message), last);
        }

        static HttpsResponse ParseHttpsResponse(byte[] raw, string host)
        {
            int split = -1;
            for (int i = 0; i + 3 < raw.Length; i++)
                if (raw[i] == 13 && raw[i + 1] == 10 && raw[i + 2] == 13 && raw[i + 3] == 10) { split = i + 4; break; }
            if (split < 0) throw new IOException("Réponse incomplète de " + host + ".");
            string head = Encoding.ASCII.GetString(raw, 0, split);
            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) throw new IOException("Réponse vide de " + host + ".");
            string[] status = lines[0].Split(' ');
            int code;
            if (status.Length < 2 || !Int32.TryParse(status[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
                throw new IOException("Statut HTTP illisible renvoyé par " + host + ".");

            var response = new HttpsResponse { Status = code };
            bool chunked = false;
            string encoding = "";
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (String.Equals(name, "Location", StringComparison.OrdinalIgnoreCase)) response.Location = value;
                else if (String.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) chunked = value.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;
                else if (String.Equals(name, "Content-Encoding", StringComparison.OrdinalIgnoreCase)) encoding = value.ToLowerInvariant();
            }

            byte[] body = new byte[raw.Length - split];
            Buffer.BlockCopy(raw, split, body, 0, body.Length);
            if (chunked) body = DecodeChunked(body);
            if (encoding.Contains("gzip")) body = Inflate(body, true);
            else if (encoding.Contains("deflate")) body = Inflate(body, false);
            response.Body = body;
            return response;
        }

        static byte[] DecodeChunked(byte[] body)
        {
            using (var input = new MemoryStream(body))
            using (var output = new MemoryStream())
            {
                while (true)
                {
                    string line = ReadAsciiLine(input);
                    if (line == null) break;
                    int extension = line.IndexOf(';');
                    if (extension >= 0) line = line.Substring(0, extension);
                    int size;
                    if (!Int32.TryParse(line.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out size) || size <= 0) break;
                    byte[] chunk = new byte[size];
                    int read = 0;
                    while (read < size)
                    {
                        int count = input.Read(chunk, read, size - read);
                        if (count <= 0) break;
                        read += count;
                    }
                    output.Write(chunk, 0, read);
                    ReadAsciiLine(input);
                }
                return output.ToArray();
            }
        }

        static string ReadAsciiLine(Stream stream)
        {
            var text = new StringBuilder();
            while (true)
            {
                int value = stream.ReadByte();
                if (value < 0) return text.Length == 0 ? null : text.ToString();
                if (value == 10) return text.ToString();
                if (value != 13) text.Append((char)value);
            }
        }

        static byte[] Inflate(byte[] data, bool gzip)
        {
            try
            {
                using (var input = new MemoryStream(data))
                using (Stream decompressor = gzip ? (Stream)new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress)
                    : new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, read);
                    return output.ToArray();
                }
            }
            catch { return data; }
        }

        public static string DownloadToString(string location)
        {
            return DownloadToString(location, 20000);
        }

        public static string DownloadToString(string location, int timeoutMs)
        {
            if (String.IsNullOrWhiteSpace(location)) throw new InvalidDataException("Aucune source indiquée.");
            if (File.Exists(location)) return ReadTextFile(location);
            if (TextUtil.IsHttp(location)) return GetString(location, timeoutMs, "LyaranTV/2.0");
            throw new InvalidDataException("Source introuvable : " + location);
        }

        public static byte[] DownloadBytes(string location)
        {
            return DownloadBytes(location, 20000);
        }

        public static byte[] DownloadBytes(string location, int timeoutMs)
        {
            if (String.IsNullOrWhiteSpace(location)) throw new InvalidDataException("Aucune source indiquée.");
            if (File.Exists(location)) return File.ReadAllBytes(location);
            if (TextUtil.IsHttp(location)) return GetBytes(location, timeoutMs, "LyaranTV/2.0");
            throw new InvalidDataException("Source introuvable : " + location);
        }

        public static string ReadTextFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return DecodeText(bytes);
        }

        public static string DecodeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch { return Encoding.Default.GetString(bytes); }
        }

        public static string DescribeException(Exception ex, string url)
        {
            string host = "";
            try { host = new Uri(url).Host; } catch { }
            var web = ex as WebException;
            if (web != null)
            {
                if (web.Status == WebExceptionStatus.Timeout) return "Délai dépassé" + (host.Length > 0 ? " pour " + host : "") + ". Vérifiez votre connexion.";
                if (web.Status == WebExceptionStatus.NameResolutionFailure) return "Serveur introuvable : " + host + ". Vérifiez l’adresse.";
                if (web.Status == WebExceptionStatus.ConnectFailure) return "Connexion impossible à " + host + ". Le serveur est peut-être hors ligne ou le port fermé.";
                if (web.Status == WebExceptionStatus.TrustFailure) return "Le certificat SSL du serveur n’est pas valide.";
                if (web.Status == WebExceptionStatus.SecureChannelFailure) return "Connexion sécurisée impossible avec ce serveur (SSL/TLS).";
                var response = web.Response as HttpWebResponse;
                if (response != null)
                {
                    int code = (int)response.StatusCode;
                    if (code == 401 || code == 403) return "Accès refusé par le serveur (HTTP " + code + "). Vérifiez vos identifiants.";
                    if (code == 404) return "Adresse introuvable sur le serveur (HTTP 404).";
                    if (code >= 500) return "Le serveur a renvoyé une erreur (HTTP " + code + ").";
                    return "Erreur HTTP " + code + " (" + response.StatusDescription + ").";
                }
                return web.Message;
            }
            if (ex is InvalidDataException) return ex.Message;
            if (ex is IOException) return "Erreur de lecture : " + ex.Message;
            return ex.Message;
        }

        public static void AppendLog(string message)
        {
            try
            {
                AppPaths.EnsureDataDir();
                File.AppendAllText(AppPaths.LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
