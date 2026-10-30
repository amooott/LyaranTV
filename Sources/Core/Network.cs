using System;
using System.IO;
using System.Net;
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

        public static byte[] GetBytes(string url, int timeoutMs, string userAgent)
        {
            if (!TextUtil.IsHttp(url)) throw new InvalidDataException("Adresse HTTP/HTTPS invalide : " + (url ?? ""));
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = String.IsNullOrWhiteSpace(userAgent) ? "LyaranTV/2.0" : userAgent;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Accept = "*/*";
            try
            {
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
            catch (Exception ex)
            {
                throw new InvalidDataException(DescribeException(ex, url), ex);
            }
        }

        public static byte[] GetBytes(string url) { return GetBytes(url, 20000, "LyaranTV/2.0"); }

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
