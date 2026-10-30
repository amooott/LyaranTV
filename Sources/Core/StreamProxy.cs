using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Petit proxy HTTP local pour la lecture intégrée.
    /// WPF MediaElement envoie « NSPlayer/12… » et certains fournisseurs refusent
    /// cette signature (HTTP 401/403), ce qui se traduit par 0xC00D1198. Le proxy
    /// rejoue la requête avec un User-Agent de navigateur, puis VLC, puis NSPlayer
    /// en dernier recours, transmet Referer/Origin/Cookie, relaie les requêtes
    /// Range et ne diffuse que sur la boucle locale.
    /// </summary>
    internal static class StreamProxy
    {
        const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        const string VlcUserAgent = "VLC/3.0.20 LibVLC/3.0.20";
        const string MediaUserAgent = "NSPlayer/12.00.26100.9711 WMFSDK/12.00.26100.9711";
        static readonly object Gate = new object();
        static readonly Dictionary<string, Target> Targets = new Dictionary<string, Target>(StringComparer.Ordinal);
        static readonly List<string> Order = new List<string>();
        static TcpListener listener;
        static int port;

        sealed class Target
        {
            public string Url, UserAgent, Referer, Origin, Cookie;
            public DateTime Created;
        }

        static StreamProxy()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        public static string Create(string url, string userAgent, string referer, string origin, string cookie)
        {
            if (!TextUtil.IsHttp(url)) return url;
            try
            {
                EnsureStarted();
                string token = Guid.NewGuid().ToString("N");
                var target = new Target
                {
                    Url = url,
                    UserAgent = userAgent,
                    Referer = referer,
                    Origin = origin,
                    Cookie = cookie,
                    Created = DateTime.UtcNow
                };
                lock (Gate)
                {
                    Targets[token] = target;
                    Order.Add(token);
                    while (Order.Count > 32)
                    {
                        Targets.Remove(Order[0]);
                        Order.RemoveAt(0);
                    }
                }
                return "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/s/" + token;
            }
            catch
            {
                return url;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Targets.Clear();
                Order.Clear();
            }
        }

        /// <summary>
        /// Teste rapidement un flux avant de confier son URL à MediaElement.
        /// Renvoie le User-Agent qui a été accepté et la latence de connexion.
        /// </summary>
        public static StreamProbeResult Probe(string url, string preferredUserAgent, string referer, string origin, string cookie, int timeoutMs)
        {
            var result = new StreamProbeResult();
            if (!TextUtil.IsHttp(url))
            {
                result.Ok = true;
                result.ContentType = GuessContentType(url);
                return result;
            }
            string host = HostOf(url);
            string[] userAgents = BuildUserAgents(preferredUserAgent);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < userAgents.Length; i++)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.UserAgent = userAgents[i];
                    request.AllowAutoRedirect = true;
                    request.AutomaticDecompression = DecompressionMethods.None;
                    request.Accept = "*/*";
                    request.Timeout = timeoutMs;
                    request.ReadWriteTimeout = timeoutMs;
                    request.KeepAlive = false;
                    if (!String.IsNullOrWhiteSpace(referer)) request.Referer = referer;
                    if (!String.IsNullOrWhiteSpace(origin)) request.Headers["Origin"] = origin;
                    if (!String.IsNullOrWhiteSpace(cookie)) request.Headers["Cookie"] = cookie;
                    request.AddRange(0, 0);
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        result.Status = (int)response.StatusCode;
                        result.ContentType = response.ContentType ?? "";
                        result.UserAgent = userAgents[i];
                        result.ElapsedMs = watch.ElapsedMilliseconds;
                        result.Ok = result.Status == 200 || result.Status == 206;
                        if (!result.Ok) result.Error = DescribeProbeStatus(result.Status, host);
                    }
                    if (result.Ok) return result;
                    if ((result.Status == 401 || result.Status == 403) && i + 1 < userAgents.Length) continue;
                    return result;
                }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    result.ElapsedMs = watch.ElapsedMilliseconds;
                    if (response != null)
                    {
                        result.Status = (int)response.StatusCode;
                        response.Close();
                        result.Error = DescribeProbeStatus(result.Status, host);
                        if ((result.Status == 401 || result.Status == 403) && i + 1 < userAgents.Length) continue;
                        return result;
                    }
                    result.Error = DescribeProbeException(ex, host);
                    return result;
                }
                catch (Exception ex)
                {
                    result.ElapsedMs = watch.ElapsedMilliseconds;
                    result.Error = ex.Message;
                    return result;
                }
            }
            return result;
        }

        static string DescribeProbeStatus(int status, string host)
        {
            if (status == 401 || status == 403)
                return "Le serveur du flux refuse la connexion (HTTP " + status.ToString(CultureInfo.InvariantCulture) + "). Le lien est peut-être protégé, expiré ou lié à une session.";
            if (status == 404) return "Flux introuvable sur " + host + " (HTTP 404).";
            if (status >= 500) return "Le serveur du flux rencontre une erreur (HTTP " + status.ToString(CultureInfo.InvariantCulture) + ").";
            return "Le serveur du flux a répondu HTTP " + status.ToString(CultureInfo.InvariantCulture) + ".";
        }

        static string DescribeProbeException(WebException ex, string host)
        {
            if (ex.Status == WebExceptionStatus.Timeout) return "Le serveur du flux ne répond pas (délai dépassé).";
            if (ex.Status == WebExceptionStatus.NameResolutionFailure) return "Serveur du flux introuvable : " + host + ".";
            if (ex.Status == WebExceptionStatus.ConnectFailure) return "Connexion au flux impossible : " + host + " est peut-être hors ligne.";
            if (ex.Status == WebExceptionStatus.TrustFailure) return "Le certificat SSL du flux n’est pas valide.";
            if (ex.Status == WebExceptionStatus.SecureChannelFailure) return "Connexion sécurisée impossible avec le flux (SSL/TLS).";
            return ex.Message;
        }

        static void EnsureStarted()
        {
            lock (Gate)
            {
                if (listener != null) return;
                var started = new TcpListener(IPAddress.Loopback, 0);
                started.Start();
                listener = started;
                port = ((IPEndPoint)started.LocalEndpoint).Port;
                var thread = new Thread(AcceptLoop) { IsBackground = true, Name = "LyaranTV stream proxy" };
                thread.Start();
            }
        }

        static void AcceptLoop()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                ThreadPool.QueueUserWorkItem(delegate { Handle(client); });
            }
        }

        static void Handle(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = 15000;
                client.SendTimeout = 30000;
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    string header = ReadHeader(stream);
                    if (header == null) return;
                    string[] lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;
                    string[] requestLine = lines[0].Split(' ');
                    if (requestLine.Length < 2) return;
                    string method = requestLine[0];
                    string path = requestLine[1];
                    if (!path.StartsWith("/s/", StringComparison.Ordinal)) { WriteError(stream, 404, "Not Found", "Flux inconnu."); return; }

                    string token = path.Substring(3);
                    int query = token.IndexOf('?');
                    if (query >= 0) token = token.Substring(0, query);
                    Target target;
                    lock (Gate)
                    {
                        if (!Targets.TryGetValue(token, out target)) target = null;
                    }
                    if (target == null) { WriteError(stream, 404, "Not Found", "Flux expiré."); return; }

                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        int colon = lines[i].IndexOf(':');
                        if (colon <= 0) continue;
                        headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
                    }
                    Proxy(stream, method, headers, target);
                }
            }
            catch
            {
                try { client.Close(); } catch { }
            }
        }

        static string ReadHeader(NetworkStream stream)
        {
            var memory = new MemoryStream();
            byte[] buffer = new byte[4096];
            while (memory.Length < 32768)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return null;
                memory.Write(buffer, 0, read);
                string text = Encoding.ASCII.GetString(memory.GetBuffer(), 0, (int)memory.Length);
                int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end >= 0) return text.Substring(0, end);
            }
            return null;
        }

        static void Proxy(NetworkStream client, string method, Dictionary<string, string> clientHeaders, Target target)
        {
            string range;
            clientHeaders.TryGetValue("Range", out range);
            string[] userAgents = BuildUserAgents(target.UserAgent);
            HttpWebResponse response = null;
            string lastError = "";
            int lastStatus = 0;
            for (int i = 0; i < userAgents.Length; i++)
            {
                try
                {
                    response = OpenUpstream(target, method, range, userAgents[i]);
                    int code = (int)response.StatusCode;
                    if ((code == 401 || code == 403) && i + 1 < userAgents.Length)
                    {
                        response.Close();
                        response = null;
                        continue;
                    }
                    break;
                }
                catch (WebException ex)
                {
                    var errorResponse = ex.Response as HttpWebResponse;
                    if (errorResponse != null)
                    {
                        int code = (int)errorResponse.StatusCode;
                        lastStatus = code;
                        errorResponse.Close();
                        if ((code == 401 || code == 403) && i + 1 < userAgents.Length) continue;
                    }
                    lastError = ex.Message;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    break;
                }
            }

            if (response == null)
            {
                if (lastStatus == 401 || lastStatus == 403)
                {
                    HttpUtil.AppendLog("Proxy de lecture : " + HostOf(target.Url) + " a refusé la connexion (HTTP " + lastStatus.ToString(CultureInfo.InvariantCulture) + ")");
                    WriteError(client, lastStatus, lastStatus == 401 ? "Unauthorized" : "Forbidden", "Le fournisseur refuse la connexion.");
                }
                else
                {
                    HttpUtil.AppendLog("Proxy de lecture : " + HostOf(target.Url) + " injoignable (" + lastError + ")");
                    WriteError(client, 502, "Bad Gateway", "Le flux est injoignable.");
                }
                return;
            }

            try
            {
                int status = (int)response.StatusCode;
                if (status >= 400)
                {
                    HttpUtil.AppendLog("Proxy de lecture : " + HostOf(target.Url) + " a répondu HTTP " + status.ToString(CultureInfo.InvariantCulture));
                }
                string contentType = response.ContentType;
                if (String.IsNullOrWhiteSpace(contentType) || contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                    contentType = GuessContentType(target.Url);
                long length = response.ContentLength;
                var builder = new StringBuilder();
                builder.Append("HTTP/1.1 ").Append(status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(response.StatusDescription).Append("\r\n");
                builder.Append("Content-Type: ").Append(contentType).Append("\r\n");
                if (length >= 0) builder.Append("Content-Length: ").Append(length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                builder.Append("Accept-Ranges: bytes\r\n");
                string contentRange = response.Headers["Content-Range"];
                if (!String.IsNullOrWhiteSpace(contentRange)) builder.Append("Content-Range: ").Append(contentRange).Append("\r\n");
                builder.Append("Cache-Control: no-cache\r\nConnection: close\r\n\r\n");
                byte[] header = Encoding.ASCII.GetBytes(builder.ToString());
                client.Write(header, 0, header.Length);

                if (String.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) || status == 204 || status == 304) return;
                using (Stream input = response.GetResponseStream())
                {
                    if (input == null) return;
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        client.Write(buffer, 0, read);
                        client.Flush();
                    }
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (WebException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        static string[] BuildUserAgents(string preferred)
        {
            var list = new List<string>();
            if (!String.IsNullOrWhiteSpace(preferred)) list.Add(preferred.Trim());
            if (!list.Contains(BrowserUserAgent)) list.Add(BrowserUserAgent);
            if (!list.Contains(VlcUserAgent)) list.Add(VlcUserAgent);
            // Dernier recours : certains serveurs Windows Media n'acceptent que cette signature.
            if (!list.Contains(MediaUserAgent)) list.Add(MediaUserAgent);
            return list.ToArray();
        }

        static HttpWebResponse OpenUpstream(Target target, string method, string range, string userAgent)
        {
            var request = (HttpWebRequest)WebRequest.Create(target.Url);
            request.Method = String.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ? "HEAD" : "GET";
            request.UserAgent = userAgent;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.None;
            request.Accept = "*/*";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 30000;
            request.KeepAlive = false;
            if (!String.IsNullOrWhiteSpace(target.Referer)) request.Referer = target.Referer;
            if (!String.IsNullOrWhiteSpace(target.Origin)) request.Headers["Origin"] = target.Origin;
            if (!String.IsNullOrWhiteSpace(target.Cookie)) request.Headers["Cookie"] = target.Cookie;

            long start, end;
            if (!String.IsNullOrWhiteSpace(range) && TryParseRange(range, out start, out end))
            {
                if (end >= start) request.AddRange(start, end);
                else request.AddRange(start);
            }
            return (HttpWebResponse)request.GetResponse();
        }

        static bool TryParseRange(string value, out long start, out long end)
        {
            start = 0;
            end = -1;
            if (String.IsNullOrWhiteSpace(value) || !value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;
            value = value.Substring(6).Trim();
            int dash = value.IndexOf('-');
            if (dash <= 0) return false;
            if (!Int64.TryParse(value.Substring(0, dash).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            string tail = value.Substring(dash + 1).Trim();
            if (tail.Length > 0 && !Int64.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) end = -1;
            return true;
        }

        static string GuessContentType(string url)
        {
            string path = "";
            try { path = new Uri(url).AbsolutePath.ToLowerInvariant(); } catch { }
            if (path.EndsWith(".ts") || path.EndsWith(".m2ts") || path.EndsWith(".mts")) return "video/mp2t";
            if (path.EndsWith(".mkv")) return "video/x-matroska";
            if (path.EndsWith(".mp4") || path.EndsWith(".m4v")) return "video/mp4";
            if (path.EndsWith(".webm")) return "video/webm";
            if (path.EndsWith(".avi")) return "video/x-msvideo";
            if (path.EndsWith(".mov")) return "video/quicktime";
            if (path.EndsWith(".m3u8")) return "application/vnd.apple.mpegurl";
            return "application/octet-stream";
        }

        static string HostOf(string url)
        {
            try { return new Uri(url).Host; } catch { return "source"; }
        }

        static void WriteError(NetworkStream stream, int code, string reason, string message)
        {
            try
            {
                byte[] body = Encoding.UTF8.GetBytes(message ?? "");
                string header = "HTTP/1.1 " + code.ToString(CultureInfo.InvariantCulture) + " " + reason + "\r\n"
                    + "Content-Type: text/plain; charset=utf-8\r\n"
                    + "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
                    + "Connection: close\r\n\r\n";
                byte[] bytes = Encoding.ASCII.GetBytes(header);
                stream.Write(bytes, 0, bytes.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush();
            }
            catch { }
        }
    }

    internal sealed class StreamProbeResult
    {
        public bool Ok;
        public int Status;
        public string ContentType = "";
        public string UserAgent = "";
        public long ElapsedMs;
        public string Error = "";
    }
}
