using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LyaranTV
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool selftest = HasArg(args, "--selftest");
            bool demo = HasArg(args, "--demo");
            bool example = HasArg(args, "--example");
            bool screenshot = HasArg(args, "--screenshot");
            string screenshotPath = ValueAfter(args, "--screenshot");
            string pageName = ValueAfter(args, "--page");
            string playTarget = ValueAfter(args, "--play");
            string detailTarget = ValueAfter(args, "--detail");
            string themeTarget = ValueAfter(args, "--theme");
            string scrollTarget = ValueAfter(args, "--scroll");
            bool fullscreenTarget = HasArg(args, "--fullscreen");
            double screenshotDelay = 2.2;
            Double.TryParse(ValueAfter(args, "--screenshot-delay"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out screenshotDelay);
            if (screenshotDelay < 0.5) screenshotDelay = 2.2;
            if (screenshotDelay > 60) screenshotDelay = 60;
            if (selftest)
            {
                RunSelfTest();
                return;
            }
            if (HasArg(args, "--help") || HasArg(args, "-h"))
            {
                MessageBox.Show(
                    AppInfo.Name + " " + AppInfo.Version + "\n\nUtilisation :\n  LyaranTV.exe                 Lancer l'application\n  LyaranTV.exe --demo          Lancer avec la démonstration\n  LyaranTV.exe --example       Charger l'exemple fictif Examples\\lyarantv-demo.m3u\n  LyaranTV.exe --play <url>    Lire un fichier ou un flux immédiatement\n  LyaranTV.exe --page <nom>    Ouvrir une page (Accueil, Bibliothèque, Guide EPG, Paramètres…)\n  LyaranTV.exe --theme <nom>   Forcer un thème (Sombre, Clair, Noir AMOLED, Automatique)\n  LyaranTV.exe --detail <titre> Ouvrir la fiche d'un contenu de la bibliothèque\n  LyaranTV.exe --screenshot <fichier.png> [--demo] [--example] [--page <nom>] [--theme <nom>]\n  LyaranTV.exe --fullscreen    Basculer le lecteur en plein écran (diagnostic)\n  LyaranTV.exe --selftest      Vérifier les composants sans interface\n\nDonnées : %APPDATA%\\LyaranTV\nSurcharge : variable LYARANTV_DATA_DIR",
                    "LyaranTV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TLS avant toute requête HTTPS (mises à jour, TMDb, sous-titres) :
            // sur Windows ancien, .NET Framework démarre sinon en TLS 1.0.
            SingleInstance.PrepareNetwork();
            I18n.Install();
            // Une seule instance : la seconde rameute la fenêtre déjà ouverte
            // au lieu de se disputer les fichiers de cache et de réglages.
            if (!SingleInstance.Claim(delegate
            {
                Window existing = Application.Current == null ? null : Application.Current.MainWindow;
                if (existing == null) return;
                try
                {
                    if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                    existing.Activate();
                    existing.Topmost = true;
                    existing.Topmost = false;
                }
                catch { }
            }))
            {
                return;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.Exit += delegate { SingleInstance.Release(); };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                HttpUtil.AppendLog("DispatcherUnhandledException: " + e.Exception);
                try { MessageBox.Show("Une erreur inattendue est survenue.\n\n" + e.Exception.GetBaseException().Message + "\n\nLe journal a été mis à jour.", "LyaranTV", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                HttpUtil.AppendLog("UnhandledException: " + e.ExceptionObject);
            };

            MainWindow window = null;
            try
            {
                window = new MainWindow();
                window.Loaded += delegate
                {
                    if (!String.IsNullOrWhiteSpace(themeTarget)) window.SetThemeForTest(themeTarget);
                    if (demo) window.StartDemoForTest();
                    else if (example) window.StartExampleForTest();
                    if (!String.IsNullOrWhiteSpace(pageName))
                    {
                        // Le chargement d'une source est asynchrone : on laisse la
                        // bibliothèque s'ouvrir avant de rejoindre la page demandée.
                        // Une source enregistrée peut demander plusieurs secondes
                        // (cache volumineux) : on vise après la restauration.
                        double navDelay = (demo || example) ? 2.5 : 4.8;
                        var navTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(navDelay) };
                        navTimer.Tick += delegate
                        {
                            navTimer.Stop();
                            window.NavigateForTest(pageName);
                        };
                        navTimer.Start();
                    }
                    if (!String.IsNullOrWhiteSpace(detailTarget))
                    {
                        if (demo || example)
                        {
                            var detailTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
                            detailTimer.Tick += delegate
                            {
                                detailTimer.Stop();
                                window.OpenDetailForTest(detailTarget);
                            };
                            detailTimer.Start();
                        }
                        else window.Dispatcher.BeginInvoke(new Action(delegate { window.OpenDetailForTest(detailTarget); }), DispatcherPriority.Background);
                    }
                    if (!String.IsNullOrWhiteSpace(playTarget)) window.PlayTargetForTest(playTarget);
                    if (!String.IsNullOrWhiteSpace(scrollTarget)) window.ScrollForTest(scrollTarget);
                    // Contrôle de mise en page : capture d'un lecteur en plein écran.
                    if (fullscreenTarget)
                    {
                        var fsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
                        fsTimer.Tick += delegate
                        {
                            fsTimer.Stop();
                            window.ToggleFullScreenForTest();
                        };
                        fsTimer.Start();
                    }
                    // Capture de la carte de volume ouverte sur le lecteur.
                    if (HasArg(args, "--volume-card"))
                    {
                        var volTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
                        volTimer.Tick += delegate
                        {
                            volTimer.Stop();
                            window.ShowVolumeCardForTest();
                        };
                        volTimer.Start();
                    }
                };
                window.Show();
                // Prépare libVLC pendant que l'accueil s'affiche : le premier film
                // s'ouvre sans attendre le chargement des greffons.
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Thread.Sleep(1200); } catch { }
                    try { VlcPlayback.WarmUp(); } catch { }
                });
            }
            catch (Exception ex)
            {
                HttpUtil.AppendLog("Startup: " + ex);
                MessageBox.Show("Impossible de démarrer LyaranTV.\n\n" + ex.GetBaseException().Message, "LyaranTV", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (screenshot)
            {
                string path = String.IsNullOrWhiteSpace(screenshotPath) ? AppPaths.File("lyarantv-screenshot.png") : screenshotPath;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(screenshotDelay) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    try
                    {
                        window.UpdateLayout();
                        int width = Math.Max(800, (int)Math.Ceiling(window.ActualWidth));
                        int height = Math.Max(600, (int)Math.Ceiling(window.ActualHeight));
                        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                        if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                        using (var stream = File.Create(path)) encoder.Save(stream);
                        HttpUtil.AppendLog("Screenshot: " + Path.GetFullPath(path));
                        window.Close();
                        app.Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        HttpUtil.AppendLog("Screenshot: " + ex);
                        try { window.Close(); } catch { }
                        app.Shutdown(1);
                    }
                };
                timer.Start();
            }
            app.Run();
        }

        static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            foreach (string arg in args) if (String.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string ValueAfter(string[] args, string name)
        {
            if (args == null) return "";
            for (int i = 0; i < args.Length - 1; i++) if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return "";
        }

        // Fichiers de test minimaux (MKV et MP4) avec pistes audio et sous-titres embarqués.
        const string MkvTrackFixture =
            "GkXfo5NCgohtYXRyb3NrYUKHgQRChYECGFOAZ0GZFUmpZocq17GDD0JAFlSua0EKrq3XgQFzxYEBg4ERhotTX1RFWFQvVVRGOCK1" +
            "nINmcmVTbolGcmFuw6dhaXOIgQGurteBAnPFgQKDgQKGhUFfQUFDIrWcg2VuZ1Nuh0VuZ2xpc2iIgQHhh5+BBrWCu4CuQKjXgQNz" +
            "xYEDg4ERhopTX1RFWFQvQVNTIrWcg2dlclNuh0RldXRzY2hjovtbU2NyaXB0IEluZm9dClNjcmlwdFR5cGU6IHY0LjAwKwoKW0V2" +
            "ZW50c10KRm9ybWF0OiBMYXllciwgU3RhcnQsIEVuZCwgU3R5bGUsIE5hbWUsIE1hcmdpbkwsIE1hcmdpblIsIE1hcmdpblYsIEVm" +
            "ZmVjdCwgVGV4dAqIgQEfQ7Z1nueBAKOLgQPoAEJvbmpvdXKjjIELuABMZSBtb25kZR9DtnXV54InEKCNoYeBA+gARmlum4IH0KPA" +
            "gwPoADAsMCxEZWZhdWx0LCwwLDAsMCwsLEJvbmpvdXIsIHRvdXQgbGUgbW9uZGVcTkRldXhpw6htZSBsaWduZQ==";
        const string Mp4TrackFixture =
            "AAAAHGZ0eXBpc29tAAAAAGlzb21pc28ybXA0MQAAAdhtb292AAAAbm12aGQAAAAAAAAAAAAAAAAAAAPoAAAXcAABAAABAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAFJ" +
            "dHJhawAAAFx0a2hkAAAAAwAAAAAAAAAAAAAAAQAAAAAAABdwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA5W1kaWEAAAAgbWRoZAAAAAAAAAAAAAAAAAAAA+gAABdwGkUAAAAAACloZGxyAAAAAAAA" +
            "AABzYnRsAAAAAAAAAAAAAAAAU3VidGl0bGUAAAAAlG1pbmYAAACMc3RibAAAACBzdHNkAAAAAAAAAAEAAAAQdHgzZwAAAAAAAAAB" +
            "AAAAGHN0dHMAAAAAAAAAAQAAAAIAAAfQAAAAHHN0c2MAAAAAAAAAAQAAAAEAAAACAAAAAQAAABxzdHN6AAAAAAAAAAAAAAACAAAA" +
            "CQAAAAoAAAAUc3RjbwAAAAAAAAABAAAB/AAAABl1ZHRhAAAAEW5hbWVGcmFuw6dhaXMAAAAbbWRhdAAHQm9uam91cgAITGUgbW9u" +
            "ZGU=";

        static void RunSelfTest()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "lyarantv-selftest-" + Guid.NewGuid().ToString("N"));
            string requestedLogPath = Environment.GetEnvironmentVariable("LYARANTV_SELFTEST_LOG");
            string logPath = String.IsNullOrWhiteSpace(requestedLogPath)
                ? Path.Combine(Path.GetTempPath(), "LyaranTV-selftest.log")
                : Path.GetFullPath(requestedLogPath);
            var log = new StringBuilder();
            int failures = 0;
            try
            {
                Directory.CreateDirectory(tempDir);
                Environment.SetEnvironmentVariable("LYARANTV_DATA_DIR", tempDir);
                AppPaths.EnsureDataDir();
                log.AppendLine("LyaranTV self-test · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                log.AppendLine("Data: " + AppPaths.DataDir);
                log.AppendLine();

                Action<string, Action> test = delegate(string name, Action action)
                {
                    try
                    {
                        action();
                        log.AppendLine("[OK]   " + name);
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        log.AppendLine("[FAIL] " + name + " · " + ex.GetBaseException().Message);
                    }
                };

                test("Chemins de données", delegate
                {
                    Assert(Directory.Exists(AppPaths.DataDir), "Le dossier de données n'existe pas.");
                    Assert(AppPaths.File("test.txt").StartsWith(AppPaths.DataDir, StringComparison.OrdinalIgnoreCase), "AppPaths.File invalide.");
                });

                test("Utilitaires texte", delegate
                {
                    Assert(TextUtil.NormalizeKey("Été à l'antenne") == "ete a l'antenne", "Normalisation des accents incorrecte.");
                    Assert(TextUtil.IsHttp("https://example.com/a.m3u"), "HTTP non reconnu.");
                    Assert(!TextUtil.IsHttp("C:\\temp\\a.m3u"), "Un chemin local a été pris pour HTTP.");
                    Assert(TextUtil.HumanCount(1234).Contains("1"), "Formatage des nombres incorrect.");
                    Assert(TextUtil.FormatEta(12) == "≈ 12 s", "Estimation de chargement courte incorrecte.");
                    Assert(TextUtil.FormatEta(1) == "Moins de 3 s", "Estimation de chargement imminente incorrecte.");
                    Assert(TextUtil.FormatEta(90).Contains("min"), "Estimation de chargement longue incorrecte.");
                });

                test("Priorité des flux", delegate
                {
                    var live = new Channel { Url = "http://example.com/live.m3u8", AlternateUrl = "http://example.com/live.ts" };
                    var ordered = StreamPlanner.Candidates(live);
                    Assert(ordered.Count == 2, "Les deux liens de secours devraient être conservés.");
                    Assert(ordered[0].EndsWith(".ts", StringComparison.OrdinalIgnoreCase), "Le flux MPEG-TS devrait être essayé avant le HLS.");
                    Assert(ordered[1].EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase), "Le HLS devrait rester en dernier recours.");
                    var movie = new Channel { Url = "http://example.com/film.mp4", AlternateUrl = "http://example.com/film.m3u8" };
                    Assert(StreamPlanner.Candidates(movie)[0] == movie.Url, "Un MP4 direct ne doit pas être déclassé.");
                    Assert(StreamPlanner.Candidates(null).Count == 0, "Une chaîne absente ne doit produire aucun candidat.");
                });

                test("Erreurs réseau lisibles", delegate
                {
                    string timeout = HttpUtil.DescribeException(new System.Net.WebException("timeout", System.Net.WebExceptionStatus.Timeout), "https://t365.in/list.m3u");
                    Assert(timeout.Contains("Délai dépassé") && timeout.Contains("t365.in"), "Le délai d’attente n’est pas expliqué clairement.");
                    string refused = HttpUtil.DescribeException(new System.Net.WebException("refused", System.Net.WebExceptionStatus.ConnectFailure), "http://example.com/a.m3u");
                    Assert(refused.Contains("Connexion impossible"), "Le refus de connexion n’est pas expliqué clairement.");
                    bool invalid = false;
                    try { HttpUtil.DownloadToString(""); }
                    catch (InvalidDataException) { invalid = true; }
                    Assert(invalid, "Une source vide n’est pas signalée comme invalide.");
                });

                test("Proxy de lecture et User-Agent", delegate
                {
                    var listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    string seenAgent = "";
                    string probeAgent = "";
                    var server = new Thread(delegate()
                    {
                        for (int attempt = 0; attempt < 2; attempt++)
                        {
                            try
                            {
                                using (var client = listener.AcceptTcpClient())
                                using (var stream = client.GetStream())
                                {
                                    var buffer = new byte[4096];
                                    var header = new StringBuilder();
                                    while (header.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                                    {
                                        int read = stream.Read(buffer, 0, buffer.Length);
                                        if (read <= 0) break;
                                        header.Append(Encoding.ASCII.GetString(buffer, 0, read));
                                    }
                                    string agent = "";
                                    foreach (string line in header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
                                    {
                                        if (line.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase)) agent = line.Substring(11).Trim();
                                    }
                                    if (attempt == 0) probeAgent = agent; else seenAgent = agent;
                                    byte[] body = new byte[] { 0x47, 0x00, 0x00, 0x00 };
                                    byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: video/mp2t\r\nContent-Length: 4\r\nConnection: close\r\n\r\n");
                                    stream.Write(head, 0, head.Length);
                                    stream.Write(body, 0, body.Length);
                                    stream.Flush();
                                }
                            }
                            catch { }
                        }
                    });
                    server.IsBackground = true;
                    server.Start();
                    try
                    {
                        var probe = StreamProxy.Probe("http://127.0.0.1:" + port + "/test.ts", null, null, null, null, 5000);
                        Assert(probe.Ok && probe.Status == 200, "La sonde de flux n'a pas validé un flux accessible.");
                        Assert(probeAgent.Length > 0 && probeAgent.IndexOf("NSPlayer", StringComparison.OrdinalIgnoreCase) < 0, "La sonde n'a pas remplacé le User-Agent du lecteur.");
                        Assert(probe.ContentType.IndexOf("mp2t", StringComparison.OrdinalIgnoreCase) >= 0, "La sonde n'a pas relevé le type de contenu.");

                        string proxied = StreamProxy.Create("http://127.0.0.1:" + port + "/test.ts", probe.UserAgent, null, null, null);
                        Assert(proxied.StartsWith("http://127.0.0.1:", StringComparison.Ordinal), "Un flux HTTP devrait passer par le proxy local.");
                        var request = (HttpWebRequest)WebRequest.Create(proxied);
                        request.Timeout = 10000;
                        request.ReadWriteTimeout = 10000;
                        using (var response = (HttpWebResponse)request.GetResponse())
                        {
                            Assert((int)response.StatusCode == 200, "Le proxy n'a pas relayé le flux.");
                        }
                        Assert(seenAgent.Length > 0, "Le proxy n'a envoyé aucun User-Agent.");
                        Assert(seenAgent.IndexOf("NSPlayer", StringComparison.OrdinalIgnoreCase) < 0, "Le proxy n'a pas remplacé le User-Agent du lecteur.");
                        Assert(StreamProxy.Create(@"C:\temp\a.ts", null, null, null, null) == @"C:\temp\a.ts", "Un fichier local ne doit pas passer par le proxy.");
                    }
                    finally
                    {
                        try { listener.Stop(); } catch { }
                        StreamProxy.Clear();
                    }
                });

                test("Chiffrement des identifiants", delegate
                {
                    string encrypted = CredentialVault.Protect("secret-iptv");
                    Assert(!String.IsNullOrWhiteSpace(encrypted) && encrypted != "secret-iptv", "Le chiffrement n'a pas produit de valeur protégée.");
                    Assert(CredentialVault.Unprotect(encrypted) == "secret-iptv", "Le déchiffrement a échoué.");
                });

                test("Réglages persistants", delegate
                {
                    var settings = new AppSettings { Theme = "Clair", Accent = "#60A5FA", Volume = 0.44, ResumePlayback = false };
                    SettingsStore.Save(settings);
                    var loaded = SettingsStore.Load();
                    Assert(loaded.Theme == "Clair", "Thème non conservé.");
                    Assert(loaded.Accent == "#60A5FA", "Accent non conservé.");
                    Assert(Math.Abs(loaded.Volume - 0.44) < 0.001, "Volume non conservé.");
                    var migrated = new AppSettings { Accent = "#F2A33C" };
                    migrated.Normalize();
                    Assert(migrated.Accent == "#8B5CF6", "L’ancien accent ambre n’a pas été migré vers le violet LyaranTV.");
                });

                test("Playlists persistantes", delegate
                {
                    var list = new List<PlaylistSource>
                    {
                        new PlaylistSource { Name = "Test M3U", Type = "M3U/M3U8", Url = "https://example.com/list.m3u", EpgUrl = "https://example.com/guide.xml" },
                        new PlaylistSource { Name = "Test Xtream", Type = "Xtream", Url = "https://example.com", Username = "demo", Password = "secret" }
                    };
                    SourceStore.Save(list);
                    var loaded = SourceStore.Load();
                    Assert(loaded.Count == 2, "Nombre de playlists incorrect.");
                    Assert(loaded[1].Password == "secret", "Mot de passe non restauré.");
                });

                test("Favoris et positions", delegate
                {
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b" };
                    StateStore.WriteSet("selftest-set.txt", set);
                    var read = StateStore.ReadSet("selftest-set.txt");
                    Assert(read.Contains("a") && read.Contains("b"), "Ensemble non conservé.");
                    var positions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "movie:1", 123.4 } };
                    StateStore.SavePositions(positions);
                    var loaded = StateStore.LoadPositions();
                    Assert(loaded.ContainsKey("movie:1") && Math.Abs(loaded["movie:1"] - 123.4) < 0.01, "Positions non conservées.");
                });

                test("Suppression de l'historique d'une playlist", delegate
                {
                    StateStore.WriteList("recent.txt", new[] { "a", "b", "c", "d" });
                    StateStore.SavePositions(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "a", 10 }, { "b", 20 }, { "c", 30 }, { "d", 40 }
                    });
                    StateStore.SaveDurations(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "a", 100 }, { "b", 200 }, { "c", 300 }, { "d", 400 }
                    });
                    StateStore.SaveEpisodes(new List<EpisodeRecord>
                    {
                        new EpisodeRecord { Id = "a", SeriesId = "s1", Name = "Épisode A" },
                        new EpisodeRecord { Id = "e", SeriesId = "c", Name = "Épisode C" },
                        new EpisodeRecord { Id = "b", SeriesId = "s2", Name = "Épisode B" }
                    });
                    File.WriteAllLines(AppPaths.File("recent.Junior.txt"), new[] { "a", "b" });
                    StateStore.RecordHistorySources(new[] { "a", "c" }, "Source A");
                    StateStore.RecordHistorySources(new[] { "b", "d" }, "Source B");

                    var purged = StateStore.PurgeHistoryForSource("Source A", new[] { "c" }, new[] { "Principal", "Junior" });
                    Assert(purged.Contains("a") && purged.Contains("c") && !purged.Contains("b"),
                        "Les contenus de la playlist supprimée ne sont pas tous identifiés.");
                    var recent = StateStore.ReadList("recent.txt");
                    Assert(recent.Count == 2 && recent.Contains("b") && recent.Contains("d"),
                        "L'historique des autres playlists a été modifié.");
                    var junior = new List<string>(File.ReadAllLines(AppPaths.File("recent.Junior.txt")));
                    Assert(junior.Count == 1 && junior[0] == "b", "L'historique d'un autre profil n'a pas été purgé.");
                    var positions = StateStore.LoadPositions();
                    Assert(!positions.ContainsKey("a") && !positions.ContainsKey("c") && positions.ContainsKey("b") && positions.ContainsKey("d"),
                        "Les positions de reprise ne sont pas purgées par playlist.");
                    var durations = StateStore.LoadDurations();
                    Assert(!durations.ContainsKey("a") && !durations.ContainsKey("c") && durations.ContainsKey("b"),
                        "Les durées de la playlist supprimée sont conservées.");
                    var episodes = StateStore.LoadEpisodes();
                    Assert(episodes.Count == 1 && episodes[0].Id == "b",
                        "Les épisodes de la playlist supprimée sont conservés.");
                    var map = StateStore.LoadHistorySources();
                    Assert(!map.ContainsKey("a") && !map.ContainsKey("c") && map.ContainsKey("b") && map.ContainsKey("d"),
                        "Le suivi de propriété de l'historique n'est pas nettoyé.");
                    StateStore.RenameHistorySource("Source B", "Source B2");
                    Assert(StateStore.LoadHistorySources()["b"] == "Source B2", "Le renommage d'une playlist ne suit pas l'historique.");

                    StateStore.WriteList("recent.txt", new string[0]);
                    StateStore.SavePositions(new Dictionary<string, double>());
                    StateStore.SaveDurations(new Dictionary<string, double>());
                    StateStore.SaveEpisodes(new List<EpisodeRecord>());
                    StateStore.SaveHistorySources(new Dictionary<string, string>());
                    try { File.Delete(AppPaths.File("recent.Junior.txt")); } catch { }
                });

                test("Bibliothèque de démonstration", delegate
                {
                    var demo = PlaylistLoader.DemoLibrary();
                    Assert(demo.Items.Count >= 8, "La démonstration ne contient pas assez d'éléments.");
                    Assert(demo.Items.Any(c => c.Kind == "Chaînes"), "Aucune chaîne de démonstration.");
                    Assert(demo.Items.Any(c => c.Kind == "Films"), "Aucun film de démonstration.");
                    Assert(demo.Guide.Count >= 2, "Aucun programme EPG de démonstration.");
                });

                test("Analyse M3U", delegate
                {
                    string playlist = "#EXTM3U\n#EXTINF:-1 tvg-id=\"fr1\" tvg-logo=\"http://logo\" group-title=\"Actualités\",France 1\nhttp://example.com/live.m3u8\n#EXTINF:-1 group-title=\"Films\",Un film\nhttp://example.com/movie.mp4\n#EXTINF:-1 group-title=\"Séries\",Série S01E01\nhttp://example.com/serie.mp4\n";
                    string epg;
                    var items = PlaylistLoader.ParseM3uData(playlist, "http://example.com/list.m3u", out epg);
                    Assert(items.Count == 3, "Nombre d'éléments M3U incorrect.");
                    Assert(items[0].Name == "France 1" && items[0].TvgId == "fr1", "Métadonnées M3U incorrectes.");
                    Assert(items[1].Kind == "Films", "Classification film incorrecte.");
                    Assert(items[2].Kind == "Séries", "Classification série incorrecte.");

                    // Une entrée relative contenant « | » ne doit pas lever une
                    // ArgumentException de Path.IsPathRooted pendant l'analyse.
                    string relativePlaylist = "#EXTM3U\n#EXTINF:-1,Relatif\nrelative/chemin|sans-entete\n";
                    var relative = PlaylistLoader.ParseM3uData(relativePlaylist, "C:\\temp\\liste.m3u", out epg);
                    Assert(relative.Count == 1 && relative[0].Url == "relative/chemin|sans-entete", "Une URL relative non conforme fait échouer l'analyse M3U.");
                    Assert(TextUtil.ResolveUrl("relative|entete", "C:\\temp\\liste.m3u") == "relative|entete", "Les caractères de chemin invalides ne sont pas tolérés.");
                });

                test("En-têtes de flux M3U", delegate
                {
                    string playlist = "#EXTM3U\n#EXTINF:-1 group-title=\"Direct\",Chaîne A\n#EXTVLCOPT:http-user-agent=AgentTest/1.0\n#EXTVLCOPT:http-referrer=http://ref.example/\nhttp://example.com/a.ts\n#EXTINF:-1,Chaîne B\nhttp://example.com/b.mkv|User-Agent=PipeAgent%2F2.0&Referer=http%3A%2F%2Fpipe.example%2F&Origin=http%3A%2F%2Forigin.example\n";
                    string epg;
                    var items = PlaylistLoader.ParseM3uData(playlist, "http://example.com/list.m3u", out epg);
                    Assert(items.Count == 2, "Les chaînes avec en-têtes n'ont pas été analysées.");
                    Assert(items[0].HttpUserAgent == "AgentTest/1.0", "Le User-Agent EXTVLCOPT n'a pas été lu.");
                    Assert(items[0].HttpReferer == "http://ref.example/", "Le Referer EXTVLCOPT n'a pas été lu.");
                    Assert(items[1].HttpUserAgent == "PipeAgent/2.0", "Le User-Agent du suffixe pipe n'a pas été décodé.");
                    Assert(items[1].HttpReferer == "http://pipe.example/" && items[1].HttpOrigin == "http://origin.example", "Les en-têtes du suffixe pipe n'ont pas été décodés.");
                    Assert(items[1].Url == "http://example.com/b.mkv", "Le suffixe pipe est resté dans l'URL.");
                });

                test("Analyse Xtream", delegate
                {
                    string categories = "[{\"category_id\":\"1\",\"category_name\":\"Actualités\"},{\"category_id\":\"10\",\"category_name\":\"Films\"}]";
                    string live = "[{\"num\":1,\"name\":\"Chaîne Test\",\"stream_id\":101,\"category_id\":\"1\",\"epg_channel_id\":\"test.fr\",\"stream_icon\":\"/logos/101.png\"}]";
                    var liveItems = PlaylistLoader.ParseXtreamData(live, categories, "http://host:8080/", "demo", "p@ss", "Chaînes");
                    Assert(liveItems.Count == 1, "La chaîne Xtream n'a pas été analysée.");
                    Assert(liveItems[0].Group == "Actualités" && liveItems[0].TvgId == "test.fr", "La catégorie ou l'identifiant EPG Xtream est incorrect.");
                    Assert(liveItems[0].Url == "http://host:8080/live/demo/p%40ss/101.m3u8", "L'URL HLS Xtream est incorrecte.");
                    Assert(liveItems[0].AlternateUrl == "http://host:8080/live/demo/p%40ss/101.ts", "L'URL de secours MPEG-TS est incorrecte.");
                    Assert(liveItems[0].Logo == "http://host:8080/logos/101.png", "Le logo relatif Xtream n'a pas été résolu.");
                    Assert(StreamPlanner.Candidates(liveItems[0])[0].EndsWith(".ts", StringComparison.OrdinalIgnoreCase), "Le flux TS devrait être prioritaire sur le HLS.");
                    string movies = "[{\"name\":\"Film Test\",\"stream_id\":201,\"category_id\":\"10\",\"container_extension\":\"mkv\",\"rating\":\"7.5\"}]";
                    var films = PlaylistLoader.ParseXtreamData(movies, categories, "http://host:8080", "demo", "p@ss", "Films");
                    Assert(films.Count == 1 && films[0].Url == "http://host:8080/movie/demo/p%40ss/201.mkv", "L'URL de film Xtream est incorrecte.");
                    Assert(films[0].Group == "Films" && films[0].Rating == "7.5", "Les métadonnées du film Xtream sont incomplètes.");
                    string series = "[{\"name\":\"Série Test\",\"series_id\":301,\"category_id\":\"10\",\"releaseDate\":\"2023-05-01\"}]";
                    var shows = PlaylistLoader.ParseXtreamData(series, categories, "http://host:8080", "demo", "p@ss", "Séries");
                    Assert(shows.Count == 1 && shows[0].IsSeries && shows[0].SeriesId == "301", "La série Xtream n'a pas été reconnue.");
                    Assert(shows[0].Year == "2023", "L'année de la série Xtream est incorrecte.");
                    Assert(shows[0].Url == "", "Une série ne doit pas exposer d'URL avant le choix de l'épisode.");
                });

                test("Conversion M3U vers Xtream", delegate
                {
                    var fromGet = PlaylistConverter.FromUrl("http://host:8080/get.php?username=demo&password=p%40ss&type=m3u_plus&output=ts");
                    Assert(fromGet != null, "L'URL get.php n'est pas reconnue comme Xtream.");
                    Assert(fromGet.BaseUrl == "http://host:8080" && fromGet.Username == "demo" && fromGet.Password == "p@ss",
                        "Les identifiants get.php sont mal décodés.");
                    Assert(fromGet.Reason.Contains("get.php"), "L'origine de la conversion get.php est absente.");

                    var fromLive = PlaylistConverter.FromUrl("https://cdn.example.tv/iptv/live/demo/p%40ss/101.ts");
                    Assert(fromLive != null && fromLive.BaseUrl == "https://cdn.example.tv/iptv"
                        && fromLive.Username == "demo" && fromLive.Password == "p@ss",
                        "Le flux /live/ n'est pas converti correctement.");
                    var fromMovie = PlaylistConverter.FromUrl("http://host:8080/movie/demo/p%40ss/201.mkv");
                    Assert(fromMovie != null && fromMovie.BaseUrl == "http://host:8080" && fromMovie.Reason.Contains("/movie/"),
                        "Le flux /movie/ n'est pas converti correctement.");
                    var fromSeries = PlaylistConverter.FromUrl("http://host:8080/series/demo/p%40ss/301.m3u8");
                    Assert(fromSeries != null && fromSeries.Reason.Contains("/series/"), "Le flux /series/ n'est pas converti correctement.");

                    var fromContent = PlaylistConverter.FromPlaylist(
                        "#EXTM3U\n#EXTINF:-1 group-title=\"Direct\",Chaîne\nhttp://host:8080/live/demo/p%40ss/101.ts\n",
                        "http://host:8080/list.m3u");
                    Assert(fromContent != null && fromContent.Username == "demo" && fromContent.Password == "p@ss",
                        "Les flux Xtream d'une playlist M3U ne sont pas détectés.");

                    var applied = fromGet.Apply(new PlaylistSource { Name = "Fournisseur", EpgUrl = "http://epg.example/guide.xml" });
                    Assert(applied.Type == "Xtream" && applied.Name == "Fournisseur" && applied.EpgUrl == "http://epg.example/guide.xml",
                        "La conversion ne conserve pas le nom ou l'EPG.");
                    Assert(applied.Url == "http://host:8080" && applied.Username == "demo" && applied.Password == "p@ss",
                        "La source Xtream convertie est incomplète.");

                    Assert(PlaylistConverter.FromUrl("http://example.com/list.m3u") == null, "Une playlist M3U classique est convertie à tort.");
                    Assert(PlaylistConverter.FromPlaylist("#EXTM3U\n#EXTINF:-1,Chaîne\nhttp://example.com/live.m3u8\n", "http://example.com/list.m3u") == null,
                        "Un flux HLS classique est converti à tort.");
                });

                test("Analyse XMLTV", delegate
                {
                    string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><tv><channel id=\"fr1\"><display-name>France 1</display-name></channel><programme start=\"20261028120000 +0200\" stop=\"20261028130000 +0200\" channel=\"fr1\"><title>Journal</title><desc>Actualités</desc></programme></tv>";
                    var guide = PlaylistLoader.ParseEpgBytes(Encoding.UTF8.GetBytes(xml));
                    Assert(guide.Count == 1, "Programme XMLTV non analysé.");
                    Assert(guide[0].Title == "Journal" && guide[0].ChannelName == "France 1", "Métadonnées XMLTV incorrectes.");
                    var parsed = PlaylistLoader.ParseXmlTvTime("20261028120000 +0200");
                    Assert(parsed.HasValue, "Heure XMLTV non analysée.");
                });

                test("Nettoyage des titres TMDb", delegate
                {
                    Assert(TmdbTitleCleaner.CleanTitle("Inception (2010) [1080p]") == "Inception", "Nettoyage des crochets/année incorrect.");
                    Assert(TmdbTitleCleaner.CleanTitle("Film.2023.VOSTFR.1080p.WEB-DL.x265") == "Film", "Nettoyage des tags de qualité incorrect.");
                    Assert(TmdbTitleCleaner.CleanTitle("FR | Le Fabuleux Destin d'Amélie Poulain") == "Le Fabuleux Destin d'Amélie Poulain", "Nettoyage du préfixe de langue incorrect.");
                    Assert(TmdbTitleCleaner.CleanTitle("Série S01E01") == "Série", "Nettoyage des numéros d’épisode incorrect.");
                    Assert(TmdbTitleCleaner.ExtractYear("Inception.2010.1080p") == 2010, "Année non extraite.");
                    Assert(TmdbTitleCleaner.LooksLikeSeries("Ma série S02E04", "Films"), "Détection de série incorrecte.");
                    var correct = new TmdbResult { TmdbId = "603", Title = "The Matrix", OriginalTitle = "The Matrix", Year = "1999" };
                    var wrongYear = new TmdbResult { TmdbId = "999", Title = "The Matrix", OriginalTitle = "The Matrix", Year = "2021" };
                    Assert(TmdbClient.MatchScore("The.Matrix.1999.1080p", 1999, correct) == 100, "Le score exact titre/année est incorrect.");
                    Assert(TmdbClient.PickBest(new List<TmdbResult> { wrongYear, correct }, "The Matrix", 1999).TmdbId == "603",
                        "La correspondance exacte n'est pas prioritaire sur une année différente.");
                    Assert(TmdbClient.PickBest(new List<TmdbResult> { correct, new TmdbResult { TmdbId = "604", Title = "The Matrix", OriginalTitle = "The Matrix", Year = "1999" } }, "The Matrix", 1999) == null,
                        "Une correspondance ambiguë a été choisie automatiquement.");
                });

                test("Pistes audio et sous-titres en ligne", delegate
                {
                    var french = new MediaTrackInfo { Kind = "Audio", Ordinal = 1, LanguageCode = "fr", CodecLabel = "AC-3", Channels = 6, Title = "VFF" };
                    Assert(french.Label.Contains("Français"), "La langue de la piste audio n'apparaît pas.");
                    Assert(french.Label.Contains("VF"), "La version (VF) de la piste audio n'apparaît pas.");
                    Assert(french.Label.Contains("5.1"), "La disposition des canaux n'apparaît pas.");
                    Assert(!french.Label.Contains("VFF  ·  VF"), "La version est répétée dans le libellé.");
                    var fromName = new MediaTrackInfo { Kind = "Audio", Ordinal = 1, LanguageCode = "fr", CodecLabel = "AAC", Channels = 2, NameHint = "Film.2023.TRUEFRENCH.1080p" };
                    Assert(fromName.Label.Contains("TRUEFRENCH"), "La version du nom de fichier n'est pas reprise.");
                    Assert(fromName.Label.Contains("stéréo"), "La disposition stéréo n'apparaît pas.");
                    Assert(OpenSubtitlesClient.CleanTitle("FR - Adventure Time - S01E03 -") == "Adventure Time", "Le titre de série n'est pas nettoyé.");
                    Assert(OpenSubtitlesClient.CleanTitle("FR - Le Film (2021) -") == "Le Film (2021)", "Le titre de film n'est pas nettoyé.");
                    Assert(!OpenSubtitlesClient.IsConfigured(new AppSettings()), "OpenSubtitles ne doit pas être actif sans clé.");
                    Assert(OpenSubtitlesClient.IsConfigured(new AppSettings { OpenSubtitlesKey = "abc" }), "La clé OpenSubtitles n'est pas prise en compte.");
                    string json = "{\"total_count\":2,\"data\":[" +
                        "{\"id\":\"1\",\"attributes\":{\"language\":\"fr\",\"download_count\":1200,\"hearing_impaired\":false,\"release\":\"Adventure.Time.S03E07.VFF.1080p\",\"files\":[{\"file_id\":111,\"file_name\":\"Adventure.Time.S03E07 VFF\"}]}}," +
                        "{\"id\":\"2\",\"attributes\":{\"language\":\"en\",\"download_count\":50,\"hearing_impaired\":true,\"release\":\"Adventure Time S03E07\",\"files\":[{\"file_id\":112,\"file_name\":\"Adventure.Time.S03E07.en.srt\"}]}}," +
                        "{\"id\":\"3\",\"attributes\":{\"language\":\"fr\",\"download_count\":10,\"release\":\"Sans fichier\",\"files\":[]}}]}";
                    var online = OpenSubtitlesClient.ParseResults(json);
                    Assert(online.Count == 2, "Les résultats OpenSubtitles ne sont pas filtrés correctement.");
                    Assert(online[0].FileId == 111 && online[0].LanguageName == "Français" && online[0].Downloads == 1200, "Le premier résultat OpenSubtitles est mal décodé.");
                    Assert(online[0].Label == "Adventure.Time.S03E07.VFF.1080p" && online[0].Detail.Contains("Français"), "Libellé de sous-titre en ligne incorrect.");
                    Assert(online[0].FileName == "Adventure.Time.S03E07 VFF.srt" && online[0].Format == "srt", "Un nom sans extension n'a pas été complété en SubRip.");
                    Assert(online[0].Detail.Contains("SRT"), "Le format n'apparaît pas dans la fiche du sous-titre.");
                    Assert(online[1].HearingImpaired, "L'indicateur « malentendants » n'est pas repris.");
                    byte[] plain = Encoding.UTF8.GetBytes("1\r\n00:00:01,000 --> 00:00:02,000\r\nBonjour\r\n");
                    byte[] compressed;
                    using (var memory = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(memory, CompressionMode.Compress, true)) gzip.Write(plain, 0, plain.Length);
                        compressed = memory.ToArray();
                    }
                    string saved = OpenSubtitlesClient.Save(compressed, "Inception Fr.srt.gz");
                    Assert(File.Exists(saved) && saved.EndsWith(".srt", StringComparison.OrdinalIgnoreCase), "Le sous-titre téléchargé n'est pas enregistré en .srt.");
                    var downloaded = SubtitleLoader.Load(saved);
                    Assert(downloaded != null && downloaded.Cues.Count == 1, "Le sous-titre téléchargé n'est pas lisible après décompression.");
                });

                test("Pistes audio universelles", delegate
                {
                    Assert(VlcPlayback.FourCC(0x332d6361) == "ac-3", "Fourcc AC-3 mal converti.");
                    Assert(VlcPlayback.FourCC(0x332d6365) == "ec-3", "Fourcc E-AC-3 mal converti.");
                    var french = new AudioStream { Id = 3, Index = 1, LanguageCode = "fra", Codec = "ac-3", Channels = 6, Bitrate = 640000, Name = "VFF" };
                    Assert(french.Label.Contains("Français"), "La langue VLC n'apparaît pas.");
                    Assert(french.Label.Contains("VF"), "La version VLC n'apparaît pas.");
                    Assert(french.Label.Contains("5.1") && french.Label.Contains("AC-3"), "Canaux ou codec VLC absents.");
                    Assert(french.Label.Contains("640 kb/s"), "Le débit audio n'apparaît pas.");
                    Assert(french.MatchesLanguage("fr"), "La correspondance de langue VLC est incorrecte.");
                    var english = new AudioStream { Id = 4, Index = 2, LanguageCode = "eng", Codec = "ec-3", Channels = 8, Name = "English 7.1" };
                    Assert(english.Label.Contains("Anglais") && english.Label.Contains("7.1") && english.Label.Contains("E-AC-3"), "Piste anglaise mal décrite.");
                    Assert(!english.Label.Contains("English 7.1"), "Le titre répète la langue et les canaux.");
                    Assert(english.MatchesLanguage("en") && !english.MatchesLanguage("fr"), "La sélection par langue est incorrecte.");
                    var unknown = new AudioStream { Id = 9, Index = 3, Codec = "dtsl", Channels = 6 };
                    Assert(unknown.Label.Contains("Piste audio 3") && unknown.Label.Contains("DTS"), "Piste sans métadonnées mal décrite.");
                    var bare = new AudioStream { Id = 10, Index = 1, Codec = "ac-3" };
                    bare.Enrich(new MediaTrackInfo { Kind = "Audio", Ordinal = 1, LanguageCode = "fr", CodecLabel = "AC-3", Channels = 6, Title = "VFF" });
                    Assert(bare.Label.Contains("Français") && bare.Label.Contains("5.1") && bare.Label.Contains("AC-3"),
                        "Les métadonnées locales ne complètent pas une piste VLC incomplète.");
                    var already = new AudioStream { Id = 11, Index = 1, LanguageCode = "eng", Codec = "aac", Channels = 2 };
                    already.Enrich(new MediaTrackInfo { Kind = "Audio", Ordinal = 1, LanguageCode = "fr", CodecLabel = "AC-3", Channels = 6 });
                    Assert(already.Label.Contains("Anglais") && already.Label.Contains("stéréo") && already.Label.Contains("AAC"),
                        "Les métadonnées VLC existantes sont écrasées par l’analyse locale.");
                    Assert(MediaTrackScanner.CodecName("mlpa") == "MLP", "Codec MLP non reconnu.");
                    Assert(MediaTrackScanner.CodecName("trhd") == "TrueHD", "Codec TrueHD non reconnu.");
                    Assert(MediaTrackScanner.CodecName("vorb") == "Vorbis", "Codec Vorbis non reconnu.");
                    Assert(MediaTrackScanner.CodecName("wvpk") == "WavPack", "Codec WavPack non reconnu.");
                    Assert(MediaTrackScanner.CodecName("opus") == "Opus", "Codec Opus non reconnu.");
                    Assert(MediaTrackScanner.ChannelName(6) == "5.1" && MediaTrackScanner.ChannelName(8) == "7.1", "Disposition des canaux incorrecte.");
                    Assert(TextUtil.IsAudioFile("musique.FLAC") && TextUtil.IsAudioFile("film.mka") && TextUtil.IsAudioFile("piste.ac3"), "Fichier audio non reconnu.");
                    Assert(TextUtil.IsMediaFile("film.mkv") && TextUtil.IsMediaFile("musique.opus"), "Média local non reconnu.");
                    Assert(!TextUtil.IsAudioFile("film.mkv") && !TextUtil.IsMediaFile("notes.txt"), "Un fichier non média est accepté.");
                });

                test("Affiches et aperçus de secours", delegate
                {
                    var cover = new ArtworkRecord
                    {
                        Title = "Série Test", Poster = "https://example.com/affiche.jpg", Overview = "Un synopsis de secours.",
                        Genres = "Drame", Year = "2019", Rating = 8.2, Source = "TVmaze",
                        Episodes = new List<ArtworkEpisode>
                        {
                            new ArtworkEpisode { Season = "1", Number = "2", Still = "https://example.com/apercu.jpg", Overview = "Épisode deux.", Runtime = "24" }
                        }
                    };
                    ArtworkStore.Put(ArtworkStore.Key("Série Test", true), cover);
                    var reloaded = ArtworkStore.Get(ArtworkStore.Key("Série Test", true));
                    Assert(reloaded != null && reloaded.HasPoster && reloaded.Find("1", "2") != null, "La fiche d'affiche de secours n'est pas conservée.");
                    Assert(reloaded.Find("1", "3") == null, "Un épisode absent a été trouvé.");

                    var series = new Channel { Name = "Série Test", Kind = "Séries" };
                    ArtworkResolver.Apply(series, reloaded);
                    Assert(series.PosterUrl == cover.Poster, "L'affiche de secours n'est pas appliquée.");
                    Assert(series.Overview == cover.Overview && series.Genres == "Drame" && series.Year == "2019", "Les informations de secours sont incomplètes.");

                    var episode = new Channel { Name = "Épisode", Kind = "Épisodes", Season = "1", Episode = "2" };
                    ArtworkResolver.ApplyEpisode(episode, reloaded);
                    Assert(episode.StillUrl == cover.Episodes[0].Still, "L'aperçu d'épisode n'est pas appliqué.");
                    Assert(episode.Runtime == "24 min" && episode.Description == "Épisode deux.", "Les informations de l'épisode sont incomplètes.");

                    var copied = episode.CopyWithUrl("http://example.com/ep.mp4");
                    Assert(copied.StillUrl == episode.StillUrl, "L'aperçu d'épisode est perdu au changement de lien.");
                    ArtworkStore.Clear();
                    Assert(ArtworkStore.Get(ArtworkStore.Key("Série Test", true)) == null, "Le cache des affiches n'a pas été vidé.");
                });

                test("Nettoyage des titres", delegate
                {
                    // Les playlists IPTV préfixent les titres par la langue ; ce
                    // préfixe empêchait la recherche d'affiche et de bannière.
                    Assert(TmdbTitleCleaner.CleanTitle("IT - Assassin - 2023") == "Assassin", "Préfixe de langue non retiré.");
                    Assert(TmdbTitleCleaner.CleanTitle("FR - Les Veilleurs [MULTI-SUB]") == "Les Veilleurs", "Crochets non retirés.");
                    Assert(TmdbTitleCleaner.CleanTitle("EN | The Winter King") == "The Winter King", "Séparateur vertical non retiré.");
                    Assert(TmdbTitleCleaner.ExtractYear("IT - Assassin - 2023") == 2023, "Année non extraite.");
                });

                test("Cache TMDb", delegate
                {
                    TmdbCacheStore.Clear();
                    string key = TmdbCacheStore.CacheKey("Inception", false);
                    Assert(key == "inception|movie", "Clé de cache TMDb incorrecte.");
                    var result = new TmdbResult
                    {
                        TmdbId = "27205", MediaType = "movie", Title = "Inception",
                        PosterUrl = TmdbClient.ImageBase + "w500/poster.jpg", BackdropUrl = TmdbClient.ImageBase + "w780/backdrop.jpg",
                        Overview = "Un voleur d’idées.", Genres = "Action, Science-fiction", Year = "2010", Rating = 8.4, VoteCount = 32000
                    };
                    TmdbCacheStore.Put(key, result);
                    var cached = TmdbCacheStore.Get(key);
                    Assert(cached != null && cached.TmdbId == "27205", "Fiche TMDb non mise en cache.");
                    Assert(cached.Rating > 8.3 && cached.Year == "2010", "Métadonnées TMDb non conservées.");
                    Assert(cached.PosterUrl.Contains("/w500/") && cached.BackdropUrl.Contains("/w780/"), "URLs d’images TMDb incorrectes.");
                    TmdbCacheStore.Save();
                    Assert(File.Exists(AppPaths.File("tmdb-cache.json")), "Le cache TMDb n’a pas été écrit.");
                    TmdbCacheStore.Clear();
                    Assert(TmdbCacheStore.Get(key) == null, "Le cache TMDb n’a pas été vidé.");
                });

                test("Correspondances TMDb par profil et source", delegate
                {
                    string previous = ProfileScope.Current;
                    try
                    {
                        ProfileScope.Current = "Principal";
                        var channel = new Channel { Name = "The Matrix (1999)", Kind = "Films", SourceName = "Playlist A", Id = "matrix-cut-a" };
                        var match = new TmdbResult { TmdbId = "603", MediaType = "movie", Title = "The Matrix", Year = "1999" };
                        TmdbClient.ApplyOverride(channel, match);
                        Assert(TmdbMatchStore.Get(channel) != null, "La correspondance manuelle n'est pas mémorisée.");
                        Assert(TmdbMatchStore.Get(new Channel { Name = channel.Name, Kind = channel.Kind, SourceName = "Playlist B" }) == null,
                            "Une correspondance est partagée entre deux sources.");
                        Assert(TmdbMatchStore.Get(new Channel { Name = channel.Name, Kind = channel.Kind, SourceName = channel.SourceName, Id = "matrix-cut-b" }) == null,
                            "Une correspondance est partagée entre deux entrées distinctes d'une même source.");
                        ProfileScope.Current = "AutreProfil";
                        Assert(TmdbMatchStore.Get(channel) == null, "Une correspondance est visible depuis un autre profil.");
                        ProfileScope.Current = "Principal";
                        TmdbMatchStore.Remove(channel);
                        Assert(TmdbMatchStore.Get(channel) == null, "La suppression de correspondance n'est pas appliquée.");
                    }
                    finally { ProfileScope.Current = previous; }
                });

                test("Recherche dans les métadonnées", delegate
                {
                    var item = new Channel
                    {
                        Name = "Film de test", OriginalTitle = "Original Title", Overview = "Synopsis de démonstration",
                        Genres = "Science-fiction", Cast = "Acteur Exemple", Director = "Réalisatrice Exemple"
                    };
                    Assert(item.MatchesSearch("original title"), "Le titre original n'est pas indexé dans la recherche.");
                    Assert(item.MatchesSearch("acteur exemple") && item.MatchesSearch("réalisatrice exemple"), "Casting ou réalisation introuvable dans la recherche.");
                    Assert(item.MatchesSearch("science-fiction") && item.MatchesSearch("démonstration"), "Genres ou synopsis introuvables dans la recherche.");
                });

                test("Profils et code PIN", delegate
                {
                    var list = new List<UserProfile>
                    {
                        new UserProfile { Name = "Principal", AvatarColor = "#8B5CF6", Created = DateTime.Now.ToString("s") },
                        new UserProfile { Name = "Enfant", AvatarColor = "#22D3EE", IsChild = true, PinProtected = CredentialVault.Protect("1234"), Created = DateTime.Now.ToString("s") }
                    };
                    ProfileStore.Save(list);
                    var loaded = ProfileStore.Load();
                    Assert(loaded.Count == 2, "Profils non conservés.");
                    var child = loaded.First(p => p.Name == "Enfant");
                    Assert(child.IsChild, "Profil enfant non conservé.");
                    Assert(ProfileStore.VerifyPin(child, "1234"), "Code PIN correct refusé.");
                    Assert(!ProfileStore.VerifyPin(child, "0000"), "Code PIN incorrect accepté.");
                });

                test("État séparé par profil", delegate
                {
                    ProfileScope.Current = "Enfant";
                    StateStore.WriteSet("favorites.txt", new[] { "enfant-1" });
                    Assert(File.Exists(AppPaths.File("favorites.Enfant.txt")), "Favoris du profil non séparés.");
                    ProfileScope.Current = "Principal";
                    StateStore.WriteSet("favorites.txt", new[] { "principal-1" });
                    Assert(File.Exists(AppPaths.File("favorites.txt")), "Favoris du profil Principal non enregistrés au format hérité.");
                    Assert(StateStore.ReadSet("favorites.txt").Contains("principal-1"), "Favoris Principal incorrects.");
                    StateStore.RenameProfile("Enfant", "Junior");
                    Assert(File.Exists(AppPaths.File("favorites.Junior.txt")) && !File.Exists(AppPaths.File("favorites.Enfant.txt")),
                        "Renommage des données de profil incorrect (enfant=" + File.Exists(AppPaths.File("favorites.Enfant.txt"))
                        + ", junior=" + File.Exists(AppPaths.File("favorites.Junior.txt")) + ", dossier=" + AppPaths.DataDir + ").");
                    StateStore.DeleteProfile("Junior");
                    Assert(!File.Exists(AppPaths.File("favorites.Junior.txt")), "Suppression des données de profil incorrecte.");
                });

                test("Export portable JSON", delegate
                {
                    var exportSettings = new AppSettings
                    {
                        Theme = "Clair", Accent = "#22D3EE", TmdbApiKey = "secret-tmdb", OpenSubtitlesKey = "secret-os",
                        ParentalPinProtected = CredentialVault.Protect("9999")
                    };
                    var source = new PlaylistSource
                    {
                        Name = "Test Xtream", Type = "Xtream", Url = "http://example.com", Username = "demo",
                        Password = "secret-password", MacAddress = "00:11:22:33:44:55"
                    };
                    var data = StateStore.BuildPortable(exportSettings, new[] { source }, new[] { "fav-1" }, new[] { "recent-1" },
                        new Dictionary<string, double> { { "movie:1", 42.5 } },
                        new[] { new UserProfile { Name = "Enfant", PinProtected = CredentialVault.Protect("1234") } },
                        new[] { "hidden-1" }, new[] { "group-1" });
                    Assert(data.Settings.TmdbApiKey == "" && data.Settings.OpenSubtitlesKey == "" && data.Settings.ParentalPinProtected == "", "Des secrets figurent dans l’export portable.");
                    Assert(data.Sources.Count == 1 && data.Sources[0].Username == "demo", "Source portable incorrecte.");
                    Assert(data.Profiles.Count == 1 && data.Profiles[0].PinProtected == "", "Code PIN exporté dans le JSON portable.");
                    string path = Path.Combine(tempDir, "portable.json");
                    StateStore.WritePortable(path, data);
                    var loaded = StateStore.ReadPortable(path);
                    Assert(loaded.Format == "lyarantv-portable" && loaded.Version == 1, "En-tête portable incorrect.");
                    Assert(loaded.Favorites.Contains("fav-1") && loaded.Recent.Contains("recent-1"), "Favoris/historique portables incorrects.");
                    Assert(loaded.Positions.ContainsKey("movie:1") && Math.Abs(loaded.Positions["movie:1"] - 42.5) < 0.01, "Positions portables incorrectes.");
                    Assert(loaded.HiddenChannels.Contains("hidden-1") && loaded.HiddenGroups.Contains("group-1"), "Masquages portables incorrects.");
                    string invalid = Path.Combine(tempDir, "invalid.json");
                    File.WriteAllText(invalid, "{\"Format\":\"autre\"}", new UTF8Encoding(false));
                    bool rejected = false;
                    try { StateStore.ReadPortable(invalid); } catch (InvalidDataException) { rejected = true; }
                    Assert(rejected, "Un fichier JSON étranger a été accepté.");
                });

                test("Sous-titres SRT et WebVTT", delegate
                {
                    string srt = "1\r\n00:00:01,500 --> 00:00:03,000\r\nBonjour <i>LyaranTV</i>\r\n\r\n2\r\n00:00:04,000 --> 00:00:06,250\r\nDeuxième ligne\r\navec suite\r\n";
                    var track = SubtitleParser.Parse(srt, "demo.srt", "demo.srt");
                    Assert(track.Cues.Count == 2, "Nombre de sous-titres SRT incorrect.");
                    Assert(track.Cues[0].Text == "Bonjour LyaranTV", "Nettoyage HTML SRT incorrect.");
                    Assert(Math.Abs(track.Cues[0].Start - 1.5) < 0.01, "Début SRT incorrect.");
                    Assert(track.At(2.0) != null && track.At(2.0).Text.StartsWith("Bonjour"), "Recherche de sous-titre SRT incorrecte.");
                    Assert(track.At(3.5) == null, "Un sous-titre a été trouvé hors de sa plage.");

                    string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:02.500 align:middle\n<v Narrateur>Bienvenue &amp; bonne soirée</v>\n\n00:00:03.000 --> 00:00:04.000\nDeuxième\n";
                    var web = SubtitleParser.Parse(vtt, "demo.vtt", "demo.vtt");
                    Assert(web.Format == "WebVTT" && web.Cues.Count == 2, "Analyse WebVTT incorrecte.");
                    Assert(web.Cues[0].Text == "Bienvenue & bonne soirée", "Entités WebVTT non décodées.");
                    Assert(Math.Abs(web.Cues[0].End - 2.5) < 0.01, "Fin WebVTT incorrecte.");

                    string media = Path.Combine(tempDir, "film.mp4");
                    File.WriteAllText(media, "demo", new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(tempDir, "film.fr.srt"), srt, new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(tempDir, "film.vtt"), vtt, new UTF8Encoding(false));
                    var sidecars = SubtitleLoader.FindSidecars(media);
                    Assert(sidecars.Count == 2, "Détection des sous-titres sidecar incorrecte.");
                    Assert(sidecars[0].EndsWith("film.vtt", StringComparison.OrdinalIgnoreCase), "Priorité du sidecar exact incorrecte.");
                    var loaded = SubtitleLoader.Load(Path.Combine(tempDir, "film.fr.srt"));
                    Assert(loaded != null && loaded.Cues.Count == 2, "Chargement d’un fichier de sous-titres incorrect.");
                    Assert(SubtitleLoader.SidecarLabel(Path.Combine(tempDir, "film.fr.srt")) == "Français  ·  SubRip",
                        "La langue ou le format d’un sous-titre externe est absent.");
                    File.WriteAllText(Path.Combine(tempDir, "film.VOSTFR.srt"), srt, new UTF8Encoding(false));
                    Assert(SubtitleLoader.SidecarLabel(Path.Combine(tempDir, "film.VOSTFR.srt")) == "Français  ·  VOSTFR  ·  SubRip",
                        "La version d’un sous-titre externe n’est pas affichée.");
                    Assert(SubtitleLoader.SidecarLabel(Path.Combine(tempDir, "film.vtt")).Contains("Langue non déclarée")
                        && SubtitleLoader.SidecarLabel(Path.Combine(tempDir, "film.vtt")).Contains("WebVTT"),
                        "Un sous-titre sans langue n’indique pas son format.");
                });

                test("Pistes embarquées MKV et MP4", delegate
                {
                    Assert(MediaTrackScanner.LanguageName("fre", "") == "Français", "Code ISO 639-2 non traduit.");
                    Assert(MediaTrackScanner.LanguageName("ger", "Deutsch") == "Allemand", "Libellé de langue incorrect.");
                    Assert(MediaTrackScanner.CodecName("S_TEXT/ASS") == "ASS", "Nom de codec ASS incorrect.");

                    string mkv = Path.Combine(tempDir, "tracks.mkv");
                    File.WriteAllBytes(mkv, Convert.FromBase64String(MkvTrackFixture));
                    var scan = MediaTrackScanner.Scan(mkv);
                    Assert(scan.Container == "MKV", "Conteneur MKV non reconnu.");
                    Assert(scan.Audio.Count == 1 && scan.Audio[0].LanguageCode == "eng", "Piste audio MKV non détectée.");
                    Assert(scan.Subtitles.Count == 2, "Pistes de sous-titres MKV non détectées.");
                    var ass = MediaTrackScanner.LoadEmbeddedSubtitle(scan, scan.Subtitles[1]);
                    Assert(ass != null && ass.Cues.Count == 1, "Extraction ASS depuis le MKV incorrecte.");
                    Assert(ass.Cues[0].Text.Contains("Bonjour"), "Texte ASS non extrait.");

                    string mp4 = Path.Combine(tempDir, "tracks.mp4");
                    File.WriteAllBytes(mp4, Convert.FromBase64String(Mp4TrackFixture));
                    var mp4Scan = MediaTrackScanner.Scan(mp4);
                    Assert(mp4Scan.Container == "MP4" && mp4Scan.Subtitles.Count == 1, "Piste tx3g MP4 non détectée.");
                    var tx3g = MediaTrackScanner.LoadEmbeddedSubtitle(mp4Scan, mp4Scan.Subtitles[0]);
                    Assert(tx3g != null && tx3g.Cues.Count == 2, "Extraction tx3g depuis le MP4 incorrecte.");
                    Assert(tx3g.Cues[0].Text == "Bonjour" && tx3g.Cues[1].Text == "Le monde", "Texte tx3g non extrait.");
                });

                test("Thèmes, champs de saisie et scrollbars", delegate
                {
                    string report = MainWindow.VerifyUiBasics();
                    Assert(report.Contains("champs visibles") && report.Contains("Scrollbars"), "Le rapport d'interface est incomplet.");
                });

                test("Images en ligne décodées en mémoire", delegate
                {
                    var pixels = new byte[20 * 20 * 4];
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        pixels[i] = 139; pixels[i + 1] = 92; pixels[i + 2] = 246; pixels[i + 3] = 255;
                    }
                    var source = BitmapSource.Create(20, 20, 96, 96, PixelFormats.Bgra32, null, pixels, 20 * 4);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    var buffer = new MemoryStream();
                    encoder.Save(buffer);
                    var decoded = ImageCache.Decode(buffer.ToArray());
                    Assert(decoded != null, "Le décodage d'une image PNG en mémoire a échoué.");
                    Assert(decoded.PixelWidth > 0 && decoded.PixelWidth <= 560 && decoded.PixelHeight > 0, "Les dimensions de l'image décodée sont incorrectes.");
                    Assert(decoded.IsFrozen, "L'image décodée devrait être figée pour être partagée entre threads.");
                    Assert(ImageCache.Get("https://example.com/affiche.jpg") == null, "Le cache mémoire ne devrait pas contenir d'URL non chargée.");
                    string coversDir = Path.Combine(AppPaths.DataDir, "covers");
                    Directory.CreateDirectory(coversDir);
                    string cachedCover = Path.Combine(coversDir, "selftest.img");
                    File.WriteAllBytes(cachedCover, buffer.ToArray());
                    var cachedCovers = ImageCache.CachedCovers(4);
                    Assert(cachedCovers.Count == 1 && cachedCovers[0].PixelWidth > 0,
                        "Les affiches du cache disque ne sont pas chargées pour l'écran de chargement.");
                    try { File.Delete(cachedCover); } catch { }
                });

                test("Sauvegarde ZIP", delegate
                {
                    string backup = Path.Combine(tempDir, "selftest.lyaran");
                    StateStore.CreateBackup(backup);
                    Assert(File.Exists(backup) && new FileInfo(backup).Length > 0, "La sauvegarde ZIP n'a pas été créée.");
                });
            }
            catch (Exception ex)
            {
                failures++;
                log.AppendLine("[FAIL] Exception globale · " + ex);
            }
            finally
            {
                log.AppendLine();
                log.AppendLine(failures == 0 ? "RÉSULTAT : SUCCÈS" : "RÉSULTAT : " + failures + " ÉCHEC(S)");
                try { File.WriteAllText(logPath, log.ToString(), new UTF8Encoding(true)); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
            Environment.Exit(failures == 0 ? 0 : 1);
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
