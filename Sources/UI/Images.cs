using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LyaranTV
{
    internal static class ImageCache
    {
        sealed class Request
        {
            public string Url;
        }

        static readonly object Gate = new object();
        static readonly Dictionary<string, BitmapSource> Memory = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        static readonly LinkedList<string> Order = new LinkedList<string>();
        static readonly Dictionary<string, List<Action<ImageSource>>> Pending = new Dictionary<string, List<Action<ImageSource>>>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Request> Requests = new Dictionary<string, Request>(StringComparer.OrdinalIgnoreCase);
        // Pile LIFO : les images de la vue affichée passent avant les demandes
        // devenues hors écran, ce qui accélère nettement les grandes bibliothèques.
        static readonly List<Request> Queue = new List<Request>();
        static int activeWorkers;
        const int MaxWorkers = 6;
        const int MaxMemory = 220;
        const int DecodeWidth = 420;
        // Les affiches sont aussi conservées sur le disque : l'accueil s'affiche
        // immédiatement aux lancements suivants, même si le fournisseur d'images
        // est lent ou momentanément indisponible.
        static bool cacheReady;

        public static ImageSource Get(string url)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;
            lock (Gate)
            {
                BitmapSource image;
                if (Memory.TryGetValue(url, out image))
                {
                    Order.Remove(url); Order.AddFirst(url);
                    return image;
                }
                return null;
            }
        }

        public static void GetAsync(string url, Action<ImageSource> callback)
        {
            if (String.IsNullOrWhiteSpace(url)) { if (callback != null) callback(null); return; }
            ImageSource cached = Get(url);
            if (cached != null) { if (callback != null) callback(cached); return; }
            lock (Gate)
            {
                List<Action<ImageSource>> waiters;
                if (Pending.TryGetValue(url, out waiters))
                {
                    if (callback != null) waiters.Add(callback);
                    Request existing;
                    if (Requests.TryGetValue(url, out existing))
                    {
                        Queue.Remove(existing);
                        Queue.Add(existing);
                    }
                    return;
                }
                waiters = new List<Action<ImageSource>>();
                if (callback != null) waiters.Add(callback);
                Pending[url] = waiters;
                var request = new Request { Url = url };
                Requests[url] = request;
                Queue.Add(request);
                if (activeWorkers < MaxWorkers)
                {
                    activeWorkers++;
                    ThreadPool.QueueUserWorkItem(Worker);
                }
            }
        }

        static void Worker(object state)
        {
            while (true)
            {
                Request request;
                lock (Gate)
                {
                    if (Queue.Count == 0) { activeWorkers--; return; }
                    int last = Queue.Count - 1;
                    request = Queue[last];
                    Queue.RemoveAt(last);
                }
                // Un autre worker a pu charger la même image entre-temps.
                BitmapSource image = Get(request.Url) as BitmapSource;
                if (image == null)
                {
                    try { image = Load(request.Url); }
                    catch { }
                }
                List<Action<ImageSource>> waiters = null;
                lock (Gate)
                {
                    Pending.TryGetValue(request.Url, out waiters);
                    Pending.Remove(request.Url);
                    Requests.Remove(request.Url);
                    if (image != null)
                    {
                        if (Memory.ContainsKey(request.Url)) Order.Remove(request.Url);
                        Memory[request.Url] = image;
                        Order.AddFirst(request.Url);
                        while (Memory.Count > MaxMemory && Order.Last != null)
                        {
                            Memory.Remove(Order.Last.Value);
                            Order.RemoveLast();
                        }
                    }
                }
                if (waiters != null && waiters.Count > 0)
                {
                    var dispatcher = Application.Current == null ? null : Application.Current.Dispatcher;
                    if (dispatcher != null)
                    {
                        dispatcher.BeginInvoke(new Action(delegate
                        {
                            foreach (var callback in waiters) { try { callback(image); } catch { } }
                        }));
                    }
                }
            }
        }

        static BitmapSource Load(string url)
        {
            byte[] bytes = null;
            if (File.Exists(url))
            {
                bytes = File.ReadAllBytes(url);
            }
            else if (TextUtil.IsHttp(url))
            {
                // Le cache disque évite un écran vide au démarrage et garde les
                // affiches disponibles quand le réseau est capricieux.
                bytes = ReadDisk(url);
                if (bytes == null)
                {
                    bytes = HttpUtil.GetBytes(url, 15000, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) LyaranTV/3.0");
                    if (bytes != null && bytes.Length > 0) WriteDisk(url, bytes);
                }
            }
            else if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                string path = new Uri(url).LocalPath;
                if (File.Exists(path)) bytes = File.ReadAllBytes(path);
            }
            if (bytes == null || bytes.Length == 0) return null;
            try { return Decode(bytes); }
            catch
            {
                // Une réponse HTML ou un fichier corrompu ne doit pas rester en cache.
                if (TextUtil.IsHttp(url)) DeleteDisk(url);
                return null;
            }
        }

        static string CoverCacheDir { get { return Path.Combine(AppPaths.DataDir, "covers"); } }

        static void EnsureCache()
        {
            if (cacheReady) return;
            cacheReady = true;
            try { Directory.CreateDirectory(CoverCacheDir); } catch { }
        }

        static string CachePath(string url)
        {
            try
            {
                using (var sha = SHA1.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
                    var builder = new StringBuilder(hash.Length * 2);
                    foreach (byte value in hash) builder.Append(value.ToString("x2"));
                    return Path.Combine(CoverCacheDir, builder.ToString() + ".img");
                }
            }
            catch { return ""; }
        }

        static byte[] ReadDisk(string url)
        {
            EnsureCache();
            string path = CachePath(url);
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                // Un fichier vide ou vieux de plus de 45 jours est ignoré.
                var info = new FileInfo(path);
                if (info.Length <= 0) return null;
                if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalDays > 45) return null;
                return File.ReadAllBytes(path);
            }
            catch { return null; }
        }

        static void WriteDisk(string url, byte[] bytes)
        {
            if (bytes == null || bytes.Length < 128) return;
            EnsureCache();
            string path = CachePath(url);
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                string temporary = path + ".tmp";
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                PruneDisk(false);
            }
            catch { }
        }

        static void DeleteDisk(string url)
        {
            try
            {
                string path = CachePath(url);
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        static int writesSincePrune;

        static void PruneDisk(bool force)
        {
            writesSincePrune++;
            if (!force && writesSincePrune % 60 != 0) return;
            try
            {
                if (!Directory.Exists(CoverCacheDir)) return;
                var files = new List<FileInfo>(new DirectoryInfo(CoverCacheDir).GetFiles("*.img"));
                DateTime cutoff = DateTime.UtcNow.AddDays(-45);
                foreach (var file in files)
                {
                    try { if (file.LastWriteTimeUtc < cutoff) file.Delete(); } catch { }
                }
                files = new List<FileInfo>(new DirectoryInfo(CoverCacheDir).GetFiles("*.img"));
                if (files.Count <= 1400) return;
                files.Sort(delegate(FileInfo first, FileInfo second) { return first.LastWriteTimeUtc.CompareTo(second.LastWriteTimeUtc); });
                int remove = files.Count - 1200;
                for (int i = 0; i < remove && i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        internal static BitmapSource Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Affiches déjà présentes dans le cache disque, de la plus récente à la
        /// plus ancienne. Elles sont chargées avant l'ouverture de la fenêtre de
        /// chargement d'un compte pour que celle-ci ne s'ouvre jamais sur un
        /// espace vide.
        /// </summary>
        public static List<BitmapSource> CachedCovers(int max)
        {
            var result = new List<BitmapSource>();
            if (max <= 0) return result;
            EnsureCache();
            try
            {
                if (!Directory.Exists(CoverCacheDir)) return result;
                var files = new List<FileInfo>(new DirectoryInfo(CoverCacheDir).GetFiles("*.img"));
                files.Sort(delegate(FileInfo first, FileInfo second)
                {
                    return second.LastWriteTimeUtc.CompareTo(first.LastWriteTimeUtc);
                });
                foreach (FileInfo file in files)
                {
                    if (result.Count >= max) break;
                    try
                    {
                        if (file.Length < 128) continue;
                        var image = Decode(File.ReadAllBytes(file.FullName));
                        if (image != null) result.Add(image);
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        public static void ClearMemory()
        {
            lock (Gate)
            {
                Memory.Clear();
                Order.Clear();
            }
        }

        /// <summary>Vide le cache disque des affiches et vignettes.</summary>
        public static void ClearDisk()
        {
            try
            {
                if (Directory.Exists(CoverCacheDir))
                {
                    foreach (string file in Directory.GetFiles(CoverCacheDir, "*.img")) { try { File.Delete(file); } catch { } }
                }
            }
            catch { }
        }

        public static void ClearAll()
        {
            // Nettoie aussi les anciens fichiers .img créés par les versions précédentes.
            try { if (Directory.Exists(AppPaths.ImageCacheDir)) Directory.Delete(AppPaths.ImageCacheDir, true); } catch { }
            ClearDisk();
            ClearMemory();
        }
    }

    internal sealed class AsyncImage : Image
    {
        public static readonly DependencyProperty UrlProperty = DependencyProperty.Register("Url", typeof(string), typeof(AsyncImage), new PropertyMetadata(null, OnUrlChanged));
        public static readonly DependencyProperty FadeInProperty = DependencyProperty.Register("FadeIn", typeof(bool), typeof(AsyncImage), new PropertyMetadata(false));

        public string Url
        {
            get { return (string)GetValue(UrlProperty); }
            set { SetValue(UrlProperty, value); }
        }

        /// <summary>Fondu très court quand l'image arrive, comme un chargement de cover.</summary>
        public bool FadeIn
        {
            get { return (bool)GetValue(FadeInProperty); }
            set { SetValue(FadeInProperty, value); }
        }

        /// <summary>Déclenché sur le fil d'interface quand l'image n'a pas pu être chargée.</summary>
        public event Action<string> Failed;

        public AsyncImage()
        {
            Stretch = Stretch.UniformToFill;
            SnapsToDevicePixels = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = (AsyncImage)d;
            image.Source = null;
            string url = e.NewValue as string;
            if (String.IsNullOrWhiteSpace(url)) return;
            ImageSource cached = ImageCache.Get(url);
            if (cached != null) { image.ApplyImage(cached); return; }
            ImageCache.GetAsync(url, delegate(ImageSource source)
            {
                if (!String.Equals(image.Url, url, StringComparison.OrdinalIgnoreCase)) return;
                if (source == null)
                {
                    if (image.Failed != null) image.Failed(url);
                    return;
                }
                image.ApplyImage(source);
            });
        }

        void ApplyImage(ImageSource source)
        {
            Source = source;
            if (!FadeIn || UiMotion.Reduced)
            {
                if (Opacity <= 0) Opacity = 1;
                return;
            }
            double target = Opacity <= 0 ? 1 : Opacity;
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }
}
