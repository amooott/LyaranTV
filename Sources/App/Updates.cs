using System;
using System.Collections;
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
        public string AssetApiUrl = "";
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

                object rawAssets;
                var assets = payload.TryGetValue("assets", out rawAssets) ? rawAssets as IEnumerable : null;
                Dictionary<string, object> sidecar = null;
                if (assets != null)
                {
                    foreach (object entry in assets)
                    {
                        var asset = AsMap(entry);
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
                            info.AssetApiUrl = Str(asset, "url");
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
                    info.Sha256 = ParseSidecar(FetchSidecar(Str(sidecar, "url"), Str(sidecar, "browser_download_url"), token));
                if (!IsSha256(info.Sha256))
                {
                    info.Error = "Empreinte SHA-256 introuvable dans cette Release; téléchargement annulé.";
                    return info;
                }
                info.Sha256 = info.Sha256.ToLowerInvariant();

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

        static string FetchSidecar(string apiUrl, string browserUrl, string token)
        {
            try
            {
                using (var response = OpenAssetResponse(apiUrl, browserUrl, token))
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch { return ""; }
        }

        static HttpWebResponse OpenAssetResponse(string apiUrl, string browserUrl, string token)
        {
            bool useApi = IsGitHubAssetApiUrl(apiUrl);
            string currentUrl = useApi ? apiUrl : browserUrl;
            if (String.IsNullOrWhiteSpace(currentUrl)) throw new InvalidOperationException("URL du fichier de mise à jour manquante.");

            for (int redirects = 0; redirects <= 5; redirects++)
            {
                Uri uri = new Uri(currentUrl, UriKind.Absolute);
                if (!String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("GitHub a renvoyé une URL de téléchargement non sécurisée.");

                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.UserAgent = UserAgent;
                request.Accept = "application/octet-stream";
                request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.AllowAutoRedirect = false;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                // Le jeton n'est envoyé qu'à l'API GitHub. Une redirection vers
                // l'hôte signé des assets est suivie sans transmettre le jeton.
                if (useApi && String.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
                    && !String.IsNullOrWhiteSpace(token))
                    request.Headers["Authorization"] = "Bearer " + token;

                HttpWebResponse response;
                try { response = (HttpWebResponse)request.GetResponse(); }
                catch (WebException ex)
                {
                    response = ex.Response as HttpWebResponse;
                    if (response == null) throw;
                }

                int status = (int)response.StatusCode;
                bool redirect = status == 301 || status == 302 || status == 303 || status == 307 || status == 308;
                if (redirect)
                {
                    string location = response.Headers[HttpResponseHeader.Location];
                    response.Close();
                    if (String.IsNullOrWhiteSpace(location)) throw new InvalidOperationException("Redirection GitHub sans destination.");
                    if (redirects == 5) throw new InvalidOperationException("Trop de redirections pendant le téléchargement.");
                    currentUrl = new Uri(uri, location).AbsoluteUri;
                    useApi = false;
                    continue;
                }
                if (status != 200)
                {
                    response.Close();
                    throw new WebException("GitHub a refusé le téléchargement (HTTP " + status.ToString(CultureInfo.InvariantCulture) + ").");
                }
                return response;
            }
            throw new InvalidOperationException("Le téléchargement GitHub n'a pas abouti.");
        }

        static bool IsGitHubAssetApiUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)
                || !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !String.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)) return false;
            string expectedPath = "/repos/" + AppInfo.Repository + "/releases/assets/";
            return uri.AbsolutePath.StartsWith(expectedPath, StringComparison.OrdinalIgnoreCase);
        }

        static string ParseSidecar(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string[] parts = value.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 && IsSha256(parts[0]) ? parts[0].ToLowerInvariant() : "";
        }

        static bool IsSha256(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }

        static string Str(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        static Dictionary<string, object> AsMap(object value)
        {
            var typed = value as Dictionary<string, object>;
            if (typed != null) return typed;
            var untyped = value as IDictionary;
            if (untyped == null) return null;
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in untyped)
            {
                string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(key)) result[key] = entry.Value;
            }
            return result;
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
            if (info == null || !info.Ok || (info.AssetApiUrl.Length == 0 && info.AssetUrl.Length == 0)) return "";
            if (!IsSha256(info.Sha256)) throw new InvalidOperationException("Empreinte SHA-256 manquante; installation annulée.");
            string directory = Path.Combine(Path.GetTempPath(), "LyaranTV-update");
            Directory.CreateDirectory(directory);
            string assetName = Path.GetFileName(info.AssetName);
            if (String.IsNullOrWhiteSpace(assetName)) assetName = "LyaranTV-Setup.exe";
            string path = Path.Combine(directory, assetName);

            bool completed = false;
            try
            {
                string token = Token(settings);
                using (var response = OpenAssetResponse(info.AssetApiUrl, info.AssetUrl, token))
                using (var input = response.GetResponseStream())
                using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (input == null) throw new InvalidOperationException("GitHub n'a renvoyé aucun fichier.");
                    byte[] buffer = new byte[65536];
                    long received = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        received += read;
                        if (progress != null) progress(received, response.ContentLength);
                    }
                }
                completed = true;
            }
            finally
            {
                if (!completed) { try { File.Delete(path); } catch { } }
            }

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
            // Installe exactement dans le dossier depuis lequel cette copie
            // tourne. Sans /DIR, le setup peut installer ailleurs (notamment
            // si l'application est lancée depuis un dossier portable).
            string installDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).FullName;
            string arguments = (silent ? "/SILENT " : "") + "/UPDATE /DIR=\"" + installDirectory + "\"";
            Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path)
            });
        }
    }
}
