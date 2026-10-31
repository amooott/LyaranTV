using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace LyaranTV
{
    /// <summary>Résultat d'une recherche de mise à jour.</summary>
    internal sealed class UpdateInfo
    {
        public bool Ok;
        public string Error = "";
        public string Tag = "";
        public Version Version;
        public string Notes = "";
        public string AssetName = "";
        public string AssetUrl = "";
        public string Sha256 = "";
        public long Size;
        public string PageUrl = "";

        public bool HasUpdate
        {
            get { return Ok && Version != null && Version > AppInfo.Current; }
        }

        public string VersionText
        {
            get { return Version == null ? Tag : Version.ToString(3); }
        }
    }

    /// <summary>
    /// Mises à jour par GitHub Releases. Le dépôt est privé : la lecture exige un
    /// jeton personnel, saisi dans les réglages et conservé chiffré par
    /// CredentialVault (DPAPI, compte Windows courant). Aucun jeton n'est compilé
    /// dans le programme.
    /// </summary>
    internal static class Updates
    {
        const string ApiLatest = "https://api.github.com/repos/" + AppInfo.Repository + "/releases/latest";

        public static string Token(AppSettings settings)
        {
            if (settings == null || String.IsNullOrWhiteSpace(settings.UpdateTokenProtected)) return "";
            return CredentialVault.Unprotect(settings.UpdateTokenProtected) ?? "";
        }

        public static void SetToken(AppSettings settings, string token)
        {
            if (settings == null) return;
            string value = (token ?? "").Trim();
            settings.UpdateTokenProtected = value.Length == 0 ? "" : CredentialVault.Protect(value);
        }

        public static string UserAgent
        {
            get { return AppInfo.Name + "/" + AppInfo.Version + " (Windows NT 10.0; " + (Environment.Is64BitProcess ? "x64" : "x86") + ")"; }
        }

        /// <summary>Interroge la dernière Release publiée.</summary>
        public static UpdateInfo Check(AppSettings settings)
        {
            var info = new UpdateInfo();
            try
            {
                string token = Token(settings);
                if (token.Length == 0)
                {
                    info.Error = "Dépôt privé : jeton GitHub requis.";
                    return info;
                }

                var request = (HttpWebRequest)WebRequest.Create(ApiLatest);
                request.UserAgent = UserAgent;
                request.Accept = "application/vnd.github+json";
                request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
                request.Headers["Authorization"] = "Bearer " + token;
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                string json;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    json = reader.ReadToEnd();

                var serializer = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
                var payload = serializer.Deserialize<Dictionary<string, object>>(json);
                if (payload == null) { info.Error = "Réponse GitHub illisible."; return info; }

                info.Tag = Str(payload, "tag_name");
                info.Version = AppInfo.Parse(info.Tag);
                info.Notes = Str(payload, "body");
                info.PageUrl = Str(payload, "html_url");

                var assets = payload.ContainsKey("assets") ? payload["assets"] as object[] : null;
                Dictionary<string, object> sidecar = null;
                if (assets != null)
                {
                    foreach (object entry in assets)
                    {
                        var asset = entry as Dictionary<string, object>;
                        if (asset == null) continue;
                        string name = Str(asset, "name");
                        if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                        {
                            sidecar = asset;
                            continue;
                        }
                        bool isSetup = name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                        if (!isExe) continue;
                        // Le setup est préféré : il remplace l'exécutable en place
                        // et conserve les données de %AppData%.
                        if (info.AssetUrl.Length == 0 || isSetup)
                        {
                            info.AssetName = name;
                            info.AssetUrl = Str(asset, "browser_download_url");
                            info.Size = Long(asset, "size");
                            info.Sha256 = Digest(asset);
                            if (isSetup) break;
                        }
                    }
                }

                if (info.AssetUrl.Length == 0)
                {
                    info.Error = "Aucun fichier installable dans cette Release.";
                    return info;
                }

                if (info.Sha256.Length == 0 && sidecar != null)
                    info.Sha256 = FetchSidecar(Str(sidecar, "browser_download_url"), token).Trim().Split(' ')[0];

                info.Ok = true;
                return info;
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                int status = response == null ? 0 : (int)response.StatusCode;
                if (status == 401 || status == 403) info.Error = "Jeton GitHub refusé ou droits insuffisants. Pour un jeton à granularité fine, autorisez le dépôt "
                    + AppInfo.Repository + " avec Contents : Read; pour un jeton classic, utilisez la portée repo.";
                else if (status == 404) info.Error = "GitHub ne trouve aucune Release accessible. Vérifiez qu’une Release est publiée et, pour un jeton à granularité fine, autorisez le dépôt "
                    + AppInfo.Repository + " avec Contents : Read.";
                else info.Error = "Réseau indisponible : " + ex.GetBaseException().Message;
                return info;
            }
            catch (Exception ex)
            {
                info.Error = ex.GetBaseException().Message;
                return info;
            }
        }

        static string FetchSidecar(string url, string token)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(url)) return "";
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = UserAgent;
                request.Headers["Authorization"] = "Bearer " + token;
                request.Timeout = 15000;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch { return ""; }
        }

        static string Str(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        static long Long(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return 0;
            long parsed;
            return Int64.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : 0;
        }

        /// <summary>Empreinte publiée par GitHub (« sha256:… »), si elle existe.</summary>
        static string Digest(Dictionary<string, object> asset)
        {
            string digest = Str(asset, "digest");
            if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return digest.Substring(7).Trim().ToLowerInvariant();
            return "";
        }

        /// <summary>
        /// Télécharge le fichier de mise à jour dans %TEMP%, en vérifiant
        /// l'empreinte SHA-256 publiée. Un fichier corrompu ou tronqué est refusé
        /// avant d'être exécuté.
        /// </summary>
        public static string Download(UpdateInfo info, AppSettings settings, Action<long, long> progress)
        {
            if (info == null || !info.Ok || info.AssetUrl.Length == 0) return "";
            string directory = Path.Combine(Path.GetTempPath(), "LyaranTV-update");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, String.IsNullOrWhiteSpace(info.AssetName) ? "LyaranTV-Setup.exe" : info.AssetName);

            using (var client = new WebClient())
            {
                client.Headers["User-Agent"] = UserAgent;
                client.Headers["Accept"] = "application/octet-stream";
                string token = Token(settings);
                if (token.Length > 0) client.Headers["Authorization"] = "Bearer " + token;
                if (progress != null) client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e) { progress(e.BytesReceived, e.TotalBytesToReceive); };
                client.DownloadFile(info.AssetUrl, path);
            }

            if (String.IsNullOrEmpty(info.Sha256)) return path;
            string actual = Sha256(path);
            if (!String.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { }
                throw new InvalidOperationException("Empreinte SHA-256 incorrecte.");
            }
            return path;
        }

        public static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        /// <summary>Lance le setup téléchargé en mode silencieux, puis quitte.</summary>
        public static void Install(string path, bool silent)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException(path);
            string arguments = silent ? "/SILENT /UPDATE" : "/UPDATE";
            Process.Start(new ProcessStartInfo(path, arguments) { UseShellExecute = true });
        }
    }
}
