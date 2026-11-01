using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("LyaranTV Setup")]
[assembly: AssemblyDescription("Installateur de LyaranTV")]
[assembly: AssemblyProduct("LyaranTV")]
[assembly: AssemblyCompany("Amooott")]
[assembly: AssemblyVersion(LyaranTV.Setup.SetupProgram.Version + ".0")]
[assembly: AssemblyFileVersion(LyaranTV.Setup.SetupProgram.Version + ".0")]

namespace LyaranTV.Setup
{
    /// <summary>
    /// Installateur de LyaranTV. Installation par utilisateur dans
    /// %LocalAppData%\LyaranTV : aucun droit administrateur n'est demandé, sauf
    /// si .NET Framework 4.x doit être installé. Les données existantes
    /// (%AppData%\LyaranTV : comptes, affiches, positions) ne sont jamais
    /// touchées.
    /// </summary>
    internal static class SetupProgram
    {
        public const string ProductName = "LyaranTV";
        public const string Version = "1.3.7";
        public const string Publisher = "Amooott";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LyaranTV";

        public const string PayloadResource = "payload.zip";
        const string DotNet480Url = "https://go.microsoft.com/fwlink/?linkid=2088631";

        // Même entropie que CredentialVault dans l'application : le jeton
        // enregistré ici est relu tel quel par Updates.Token().
        static readonly byte[] TokenEntropy = Encoding.UTF8.GetBytes("LyaranTV.credentials.v1");

        /// <summary>Vrai pendant une installation silencieuse : aucune fenêtre.</summary>
        public static bool IsSilent;

        [STAThread]
        static int Main(string[] args)
        {
            bool silent = Has(args, "/SILENT") || Has(args, "/VERYSILENT");
            IsSilent = silent;
            Log("Démarrage · silent=" + silent + " · args=" + String.Join(" ", args));
            bool update = Has(args, "/UPDATE");
            bool uninstall = Has(args, "/UNINSTALL");
            // Capture le chemin de l'application dès le démarrage du setup :
            // l'ancien programme peut se fermer juste après Process.Start.
            string silentTarget = silent && !uninstall ? ResolveInstallDirectory(Value(args, "/DIR"), update) : "";

            if (!HasDotNet())
            {
                if (silent)
                {
                    // Mise à jour silencieuse : on tente l'installation du
                    // prérequis, sinon on échoue proprement.
                    if (!InstallDotNet(silent)) return 5;
                }
                else
                {
                    string message = Strings.Get(Strings.NotifyDotNetMissing);
                    string title = ProductName + " " + Version;
                    if (MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return 5;
                    if (!InstallDotNet(false)) return 5;
                }
            }

            if (!HasDotNet()) Log("Prérequis .NET Framework absent ou ancien.");

            if (uninstall) return Uninstall(silent);

            if (silent)
            {
                string target = String.IsNullOrWhiteSpace(silentTarget) ? InstallDir : silentTarget;
                int result = Install(target, true, update, null);
                if (update)
                {
                    if (result == 0) Launch(target);
                    else if (Environment.UserInteractive)
                        MessageBox.Show(Strings.Get(Strings.ErrorBody) + "\n\nJournal : "
                            + Path.Combine(Path.GetTempPath(), "lyarantv-setup.log"), Strings.Get(Strings.ErrorTitle),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return result;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new SetupForm(update))
            {
                Application.Run(form);
                return form.ExitCode;
            }
        }

        // ------------------------------------------------------------ installation

        /// <summary>
        /// Dossier du programme. Le sous-dossier « app » est volontaire :
        /// l'application range son cache d'images dans %LocalAppData%\LyaranTV\images
        /// et une désinstallation ne doit jamais l'emporter.
        /// </summary>
        public static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName, "app"); }
        }

        static string ResolveInstallDirectory(string custom, bool update)
        {
            if (!String.IsNullOrWhiteSpace(custom)) return custom;
            if (update)
            {
                // Les anciennes versions ne transmettaient pas /DIR. Tant que
                // l'application se ferme, récupère son chemin depuis le
                // processus actif pour mettre à jour la copie réellement lancée.
                string runningDirectory = "";
                Process[] running = Process.GetProcessesByName(ProductName);
                try
                {
                    foreach (Process process in running)
                    {
                        try
                        {
                            string executable = process.MainModule == null ? "" : process.MainModule.FileName;
                            if (String.Equals(Path.GetFileNameWithoutExtension(executable), ProductName, StringComparison.OrdinalIgnoreCase))
                                runningDirectory = Path.GetDirectoryName(executable) ?? "";
                        }
                        catch { }
                        if (!String.IsNullOrWhiteSpace(runningDirectory)) break;
                    }
                }
                finally
                {
                    foreach (Process process in running)
                        try { process.Dispose(); } catch { }
                }
                if (!String.IsNullOrWhiteSpace(runningDirectory)) return runningDirectory;

                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    {
                        string registered = key == null ? "" : key.GetValue("InstallLocation") as string;
                        if (!String.IsNullOrWhiteSpace(registered) && File.Exists(Path.Combine(registered, ProductName + ".exe")))
                            return registered;
                    }
                }
                catch { }
            }
            return InstallDir;
        }

        /// <summary>Taille décompressée de la charge utile, pour l'espace requis.</summary>
        public static long PayloadSize()
        {
            try
            {
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
                {
                    if (resource == null) return 0;
                    using (var zip = new ZipArchive(resource, ZipArchiveMode.Read))
                    {
                        long total = 0;
                        foreach (ZipArchiveEntry entry in zip.Entries) total += entry.Length;
                        return total;
                    }
                }
            }
            catch { return 0; }
        }

        /// <summary>
        /// Installe l'application. La progression remonte trois valeurs : le
        /// pourcentage, le texte de l'étape en cours et son numéro (0 à 5), que
        /// l'assistant utilise pour cocher les étapes.
        /// </summary>
        public static int Install(string target, bool createShortcut, bool update, Action<int, string, int> progress)
        {
            try
            {
                Directory.CreateDirectory(target);
                WaitForRunningInstance(update);

                string staging = Path.Combine(Path.GetTempPath(), "LyaranTV-setup-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                string archive = Path.Combine(staging, "payload.zip");

                if (progress != null) progress(6, Strings.Get(Strings.StepUnpacking), 0);
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
                {
                    if (resource == null) throw new InvalidOperationException("Charge utile absente de l'installateur.");
                    using (var file = File.Create(archive)) resource.CopyTo(file);
                }

                if (progress != null) progress(18, Strings.Get(Strings.StepExtracting), 1);
                string extracted = Path.Combine(staging, "files");
                ZipFile.ExtractToDirectory(archive, extracted);
                try { File.Delete(archive); } catch { }

                if (progress != null) progress(30, Strings.Get(Strings.StepCopying), 2);
                CopyTree(extracted, target, progress);

                if (progress != null) progress(90, Strings.Get(Strings.StepShortcut), 3);
                if (createShortcut) CreateShortcut(target);
                WriteUninstallEntry(target);
                SeedUpdateToken();

                if (progress != null) progress(96, Strings.Get(Strings.StepCleanup), 4);
                try { Directory.Delete(staging, true); } catch { }

                if (progress != null) progress(100, Strings.Get(Strings.StepCleanup), 5);
                return 0;
            }
            catch (Exception ex)
            {
                Log("Install: " + ex);
                if (progress != null) progress(-1, ex.Message, -1);
                // En mode silencieux (mise à jour automatique), aucune fenêtre ne
                // doit s'ouvrir : le code de sortie et le journal suffisent.
                else if (Environment.UserInteractive && !IsSilent) MessageBox.Show(ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static void WaitForRunningInstance(bool update)
        {
            // Une mise à jour ne doit pas échouer parce que le programme tourne :
            // on lui laisse le temps de se fermer, puis on attend réellement
            // la fin du processus avant de remplacer ses fichiers.
            Process[] running = Process.GetProcessesByName(ProductName);
            if (running.Length == 0) return;
            try
            {
                DateTime limit = DateTime.Now.AddSeconds(update ? 20 : 8);
                while (DateTime.Now < limit && AnyRunning(running))
                    System.Threading.Thread.Sleep(250);
                if (!AnyRunning(running)) return;

                foreach (Process process in running)
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                }
                DateTime killLimit = DateTime.Now.AddSeconds(8);
                while (DateTime.Now < killLimit && AnyRunning(running))
                    System.Threading.Thread.Sleep(200);
                if (AnyRunning(running))
                    throw new InvalidOperationException("LyaranTV est toujours ouvert; ses fichiers ne peuvent pas être remplacés.");
            }
            finally
            {
                foreach (Process process in running)
                    try { process.Dispose(); } catch { }
            }
        }

        static bool AnyRunning(IEnumerable<Process> processes)
        {
            foreach (Process process in processes)
            {
                try { if (!process.HasExited) return true; } catch { }
            }
            return false;
        }

        static void CopyTree(string source, string target, Action<int, string, int> progress)
        {
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(source.Length).TrimStart('\\');
                string destination = Path.Combine(target, relative);
                string folder = Path.GetDirectoryName(destination);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.Copy(files[i], destination, true);
                if (progress != null && i % 20 == 0)
                {
                    int percent = 30 + (int)(58.0 * i / Math.Max(1, files.Length));
                    progress(percent, Strings.Get(Strings.StepCopying), 2);
                }
            }
        }

        static void CreateShortcut(string target)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string link = Path.Combine(desktop, ProductName + ".lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(target, ProductName + ".exe") });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(target, ProductName + ".exe") + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { ProductName + " · lecteur IPTV et vidéo" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        public static void RemoveShortcut()
        {
            try
            {
                string link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk");
                if (File.Exists(link)) File.Delete(link);
            }
            catch { }
        }

        static void WriteUninstallEntry(string target)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    if (key == null) return;
                    string exe = Path.Combine(target, ProductName + ".exe");
                    key.SetValue("DisplayName", ProductName);
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", Publisher);
                    key.SetValue("DisplayIcon", exe + ",0");
                    key.SetValue("InstallLocation", target);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("EstimatedSize", EstimateSize(target), RegistryValueKind.DWord);
                    key.SetValue("UninstallString", "\"" + Application.ExecutablePath + "\" /UNINSTALL");
                    key.SetValue("QuietUninstallString", "\"" + Application.ExecutablePath + "\" /UNINSTALL /SILENT");
                }
            }
            catch { }
        }

        /// <summary>
        /// Enregistre un jeton facultatif dans les réglages de l'application,
        /// chiffré par DPAPI comme le fait CredentialVault. Les installateurs
        /// publiés ne contiennent aucun jeton; un jeton existant n'est jamais remplacé.
        /// </summary>
        static void SeedUpdateToken()
        {
            try
            {
                string token = UpdateToken.Value;
                if (String.IsNullOrWhiteSpace(token)) return;

                string overridden = Environment.GetEnvironmentVariable("LYARANTV_DATA_DIR");
                string dataDir = String.IsNullOrWhiteSpace(overridden)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName)
                    : overridden;
                Directory.CreateDirectory(dataDir);
                string file = Path.Combine(dataDir, "settings.json");

                var serializer = new JavaScriptSerializer();
                var settings = new Dictionary<string, object>();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!String.IsNullOrWhiteSpace(json))
                    {
                        var parsed = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (parsed != null) settings = parsed;
                    }
                }

                object existing;
                if (settings.TryGetValue("UpdateTokenProtected", out existing) && existing != null
                    && Convert.ToString(existing).Length > 0)
                    return;

                byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), TokenEntropy, DataProtectionScope.CurrentUser);
                settings["UpdateTokenProtected"] = Convert.ToBase64String(encrypted);
                File.WriteAllText(file, serializer.Serialize(settings), new UTF8Encoding(false));
                Log("Jeton de mise à jour enregistré dans " + file);
            }
            catch (Exception ex)
            {
                // Un jeton absent n'empêche jamais l'installation.
                Log("SeedUpdateToken: " + ex.Message);
            }
        }

        static int EstimateSize(string target)
        {
            try
            {
                long total = 0;
                foreach (string file in Directory.GetFiles(target, "*", SearchOption.AllDirectories)) total += new FileInfo(file).Length;
                return (int)Math.Min(Int32.MaxValue, total / 1024);
            }
            catch { return 0; }
        }

        public static void Launch(string target)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(target, ProductName + ".exe")) { WorkingDirectory = target }); }
            catch (Exception ex) { Log("Launch: " + ex.Message); }
        }

        // ------------------------------------------------------------ désinstallation

        static int Uninstall(bool silent)
        {
            try
            {
                WaitForRunningInstance(true);
                RemoveShortcut();

                // On supprime uniquement le dossier réellement installé (relu
                // dans le registre), jamais un dossier voisin qui contiendrait
                // les données de l'utilisateur.
                string target = InstallDir;
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    {
                        string location = key == null ? null : key.GetValue("InstallLocation") as string;
                        if (!String.IsNullOrWhiteSpace(location)) target = location;
                    }
                }
                catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
                if (Directory.Exists(target)) Directory.Delete(target, true);
                if (!silent)
                    MessageBox.Show(Strings.Get(Strings.StatusUninstalled) + "\n\n" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName),
                        ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                Log("Uninstall: " + ex);
                if (!silent && Environment.UserInteractive) MessageBox.Show(ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        /// <summary>Journal de l'installateur, utile quand aucune fenêtre ne s'ouvre.</summary>
        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "lyarantv-setup.log"),
                    DateTime.Now.ToString("s") + "  " + message + Environment.NewLine);
            }
            catch { }
        }

        // ------------------------------------------------------------ prérequis

        static bool HasDotNet()
        {
            // .NET Framework 4.8 (release 528040) ou 4.7.2 (461808) minimum :
            // l'application est compilée pour .NET Framework 4.x.
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (key == null) return false;
                    object value = key.GetValue("Release");
                    if (value == null) return false;
                    return Convert.ToInt32(value) >= 461808;
                }
            }
            catch { return false; }
        }

        static bool InstallDotNet(bool silent)
        {
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "ndp48-web.exe");
                if (!File.Exists(file))
                {
                    using (var client = new WebClient())
                    {
                        client.Headers["User-Agent"] = ProductName + " " + Version;
                        client.DownloadFile(DotNet480Url, file);
                    }
                }
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "runas" });
                if (silent) return true;
                MessageBox.Show(Strings.Get(Strings.InfoDotNetLaunched), ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch
            {
                if (!silent) MessageBox.Show(Strings.Get(Strings.ErrorDotNet), ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        static string Value(string[] args, string name)
        {
            if (args == null) return "";
            foreach (string arg in args)
            {
                if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return arg.Substring(name.Length + 1).Trim('"');
            }
            return "";
        }

        static bool Has(string[] args, string name)
        {
            if (args == null) return false;
            foreach (string arg in args) if (String.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>Textes de l'installateur, dans la langue de l'ordinateur.</summary>
    internal static class Strings
    {
        public const string Title = "Title";
        public const string Subtitle = "Subtitle";
        public const string StepWelcome = "StepWelcome";
        public const string StepFolder = "StepFolder";
        public const string StepInstall = "StepInstall";
        public const string StepFinish = "StepFinish";
        public const string WelcomeTitle = "WelcomeTitle";
        public const string WelcomeBody = "WelcomeBody";
        public const string Feature1Title = "Feature1Title";
        public const string Feature1Body = "Feature1Body";
        public const string Feature2Title = "Feature2Title";
        public const string Feature2Body = "Feature2Body";
        public const string Feature3Title = "Feature3Title";
        public const string Feature3Body = "Feature3Body";
        public const string Feature4Title = "Feature4Title";
        public const string Feature4Body = "Feature4Body";
        public const string NoteTitle = "NoteTitle";
        public const string NoteBody = "NoteBody";
        public const string FolderTitle = "FolderTitle";
        public const string FolderBody = "FolderBody";
        public const string FolderLabel = "FolderLabel";
        public const string Browse = "Browse";
        public const string Shortcut = "Shortcut";
        public const string SpaceRequired = "SpaceRequired";
        public const string SpaceAvailable = "SpaceAvailable";
        public const string SpaceLow = "SpaceLow";
        public const string DataNote = "DataNote";
        public const string AlreadyInstalled = "AlreadyInstalled";
        public const string InstallTitle = "InstallTitle";
        public const string InstallBody = "InstallBody";
        public const string StepUnpacking = "StepUnpacking";
        public const string StepExtracting = "StepExtracting";
        public const string StepCopying = "StepCopying";
        public const string StepShortcut = "StepShortcut";
        public const string StepCleanup = "StepCleanup";
        public const string Elapsed = "Elapsed";
        public const string Remaining = "Remaining";
        public const string FinishTitle = "FinishTitle";
        public const string FinishBody = "FinishBody";
        public const string LaunchNow = "LaunchNow";
        public const string FinishFolder = "FinishFolder";
        public const string Back = "Back";
        public const string Next = "Next";
        public const string Install = "Install";
        public const string Cancel = "Cancel";
        public const string Close = "Close";
        public const string Language = "Language";
        public const string ConfirmCancelTitle = "ConfirmCancelTitle";
        public const string ConfirmCancelBody = "ConfirmCancelBody";
        public const string ErrorTitle = "ErrorTitle";
        public const string ErrorBody = "ErrorBody";
        public const string Retry = "Retry";
        public const string StatusUninstalled = "StatusUninstalled";
        public const string NotifyDotNetMissing = "NotifyDotNetMissing";
        public const string InfoDotNetLaunched = "InfoDotNetLaunched";
        public const string ErrorDotNet = "ErrorDotNet";

        public static string Current = Detect();

        public static string Detect()
        {
            string two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (two == "fr" || two == "es" || two == "ru" || two == "pt") return two;
            return "en";
        }

        public static string Get(string key)
        {
            string value;
            if (Map(Current).TryGetValue(key, out value)) return value;
            if (Map("en").TryGetValue(key, out value)) return value;
            return key;
        }

        public static string LanguageName(string code)
        {
            switch (code)
            {
                case "fr": return "Français";
                case "es": return "Español";
                case "ru": return "Русский";
                case "pt": return "Português";
                default: return "English";
            }
        }

        static Dictionary<string, string> Map(string code)
        {
            var d = new Dictionary<string, string>();
            switch (code)
            {
                case "fr":
                    d[Title] = "Installation de LyaranTV";
                    d[Subtitle] = "Lecteur IPTV et vidéo pour Windows";
                    d[StepWelcome] = "Bienvenue";
                    d[StepFolder] = "Dossier";
                    d[StepInstall] = "Installation";
                    d[StepFinish] = "Terminé";
                    d[WelcomeTitle] = "Installer LyaranTV";
                    d[WelcomeBody] = "Trois étapes suffisent : choisir un dossier, installer, terminer. Aucun compte n'est requis.";
                    d[Feature1Title] = "Chaînes, films et séries";
                    d[Feature1Body] = "M3U, Xtream et portails MAC réunis dans une seule bibliothèque.";
                    d[Feature2Title] = "Lecture complète";
                    d[Feature2Body] = "Pistes audio, sous-titres, guide EPG et reprise de lecture.";
                    d[Feature3Title] = "Vos données restent les vôtres";
                    d[Feature3Body] = "Comptes et positions chiffrés, conservés sur cet ordinateur.";
                    d[Feature4Title] = "Mises à jour";
                    d[Feature4Body] = "Les nouvelles versions sont proposées depuis l'application.";
                    d[NoteTitle] = "Installation par utilisateur";
                    d[NoteBody] = "Aucun droit administrateur n'est demandé. Comptes, playlists et affiches déjà présents ne sont jamais modifiés.";
                    d[FolderTitle] = "Dossier d'installation";
                    d[FolderBody] = "LyaranTV s'installe dans votre profil Windows. Vous pouvez choisir un autre dossier.";
                    d[FolderLabel] = "Installer dans";
                    d[Browse] = "Parcourir…";
                    d[Shortcut] = "Créer un raccourci sur le Bureau";
                    d[SpaceRequired] = "Espace requis";
                    d[SpaceAvailable] = "Espace disponible";
                    d[SpaceLow] = "Espace insuffisant sur ce disque.";
                    d[DataNote] = "Vos comptes, playlists, affiches et positions déjà présents dans %AppData%\\LyaranTV sont conservés : l'installation ne les modifie pas.";
                    d[AlreadyInstalled] = "Une version est déjà installée : elle sera remplacée et vos données conservées.";
                    d[InstallTitle] = "Installation en cours";
                    d[InstallBody] = "Merci de patienter pendant la copie des fichiers.";
                    d[StepUnpacking] = "Préparation des fichiers";
                    d[StepExtracting] = "Extraction de l'application et du lecteur vidéo";
                    d[StepCopying] = "Copie des fichiers";
                    d[StepShortcut] = "Raccourci et entrée de désinstallation";
                    d[StepCleanup] = "Nettoyage";
                    d[Elapsed] = "Temps écoulé";
                    d[Remaining] = "Temps restant";
                    d[FinishTitle] = "Installation terminée";
                    d[FinishBody] = "LyaranTV est prêt. Vous pouvez lancer l'application dès maintenant.";
                    d[LaunchNow] = "Lancer LyaranTV";
                    d[FinishFolder] = "Dossier d'installation";
                    d[Back] = "Retour";
                    d[Next] = "Suivant";
                    d[Install] = "Installer";
                    d[Cancel] = "Annuler";
                    d[Close] = "Terminer";
                    d[Language] = "Langue";
                    d[ConfirmCancelTitle] = "Annuler l'installation ?";
                    d[ConfirmCancelBody] = "Les fichiers déjà copiés seront conservés. Vous pourrez relancer l'installateur plus tard.";
                    d[ErrorTitle] = "L'installation a échoué";
                    d[ErrorBody] = "Aucun fichier n'a pu être installé. Vérifiez l'espace disque, puis réessayez.";
                    d[Retry] = "Réessayer";
                    d[StatusUninstalled] = "LyaranTV a été désinstallé. Vos données personnelles ont été conservées dans :";
                    d[NotifyDotNetMissing] = "LyaranTV nécessite .NET Framework 4.7.2 ou plus récent, absent de cet ordinateur.\n\nVoulez-vous le télécharger et l'installer maintenant ? Windows demandera l'autorisation.";
                    d[InfoDotNetLaunched] = "L'installation de .NET Framework a démarré. Relancez l'installateur une fois celle-ci terminée.";
                    d[ErrorDotNet] = "Impossible de télécharger .NET Framework. Installez-le depuis le site de Microsoft, puis relancez cet installateur.";
                    return d;
                case "es":
                    d[Title] = "Instalación de LyaranTV";
                    d[Subtitle] = "Reproductor IPTV y de vídeo para Windows";
                    d[StepWelcome] = "Bienvenida";
                    d[StepFolder] = "Carpeta";
                    d[StepInstall] = "Instalación";
                    d[StepFinish] = "Finalizado";
                    d[WelcomeTitle] = "Instalar LyaranTV";
                    d[WelcomeBody] = "Solo tres pasos: elegir una carpeta, instalar y terminar. No se necesita cuenta.";
                    d[Feature1Title] = "Canales, películas y series";
                    d[Feature1Body] = "M3U, Xtream y portales MAC en una sola biblioteca.";
                    d[Feature2Title] = "Reproducción completa";
                    d[Feature2Body] = "Pistas de audio, subtítulos, guía EPG y reanudación.";
                    d[Feature3Title] = "Tus datos siguen siendo tuyos";
                    d[Feature3Body] = "Cuentas y posiciones cifradas, guardadas en este equipo.";
                    d[Feature4Title] = "Actualizaciones";
                    d[Feature4Body] = "Las nuevas versiones se ofrecen desde la aplicación.";
                    d[NoteTitle] = "Instalación por usuario";
                    d[NoteBody] = "No se piden permisos de administrador. Las cuentas, listas y carátulas existentes nunca se modifican.";
                    d[FolderTitle] = "Carpeta de instalación";
                    d[FolderBody] = "LyaranTV se instala en tu perfil de Windows. Puedes elegir otra carpeta.";
                    d[FolderLabel] = "Instalar en";
                    d[Browse] = "Examinar…";
                    d[Shortcut] = "Crear un acceso directo en el escritorio";
                    d[SpaceRequired] = "Espacio necesario";
                    d[SpaceAvailable] = "Espacio disponible";
                    d[SpaceLow] = "No hay espacio suficiente en este disco.";
                    d[DataNote] = "Tus cuentas, listas, carátulas y posiciones que ya están en %AppData%\\LyaranTV se conservan: la instalación no las modifica.";
                    d[AlreadyInstalled] = "Ya hay una versión instalada: se reemplazará y tus datos se conservarán.";
                    d[InstallTitle] = "Instalando";
                    d[InstallBody] = "Espera mientras se copian los archivos.";
                    d[StepUnpacking] = "Preparando los archivos";
                    d[StepExtracting] = "Extrayendo la aplicación y el reproductor";
                    d[StepCopying] = "Copiando archivos";
                    d[StepShortcut] = "Acceso directo y desinstalación";
                    d[StepCleanup] = "Limpiando";
                    d[Elapsed] = "Tiempo transcurrido";
                    d[Remaining] = "Tiempo restante";
                    d[FinishTitle] = "Instalación completada";
                    d[FinishBody] = "LyaranTV está listo. Puedes iniciarlo ahora mismo.";
                    d[LaunchNow] = "Iniciar LyaranTV";
                    d[FinishFolder] = "Carpeta de instalación";
                    d[Back] = "Atrás";
                    d[Next] = "Siguiente";
                    d[Install] = "Instalar";
                    d[Cancel] = "Cancelar";
                    d[Close] = "Finalizar";
                    d[Language] = "Idioma";
                    d[ConfirmCancelTitle] = "¿Cancelar la instalación?";
                    d[ConfirmCancelBody] = "Los archivos ya copiados se conservan. Puedes volver a ejecutar el instalador más tarde.";
                    d[ErrorTitle] = "La instalación ha fallado";
                    d[ErrorBody] = "No se ha podido instalar ningún archivo. Comprueba el espacio libre e inténtalo de nuevo.";
                    d[Retry] = "Reintentar";
                    d[StatusUninstalled] = "LyaranTV se ha desinstalado. Tus datos personales se han conservado en:";
                    d[NotifyDotNetMissing] = "LyaranTV necesita .NET Framework 4.7.2 o superior, que no está en este equipo.\n\n¿Quieres descargarlo e instalarlo ahora? Windows pedirá autorización.";
                    d[InfoDotNetLaunched] = "La instalación de .NET Framework ha comenzado. Vuelve a ejecutar este instalador cuando termine.";
                    d[ErrorDotNet] = "No se ha podido descargar .NET Framework. Instálalo desde el sitio de Microsoft y vuelve a ejecutar el instalador.";
                    return d;
                case "ru":
                    d[Title] = "Установка LyaranTV";
                    d[Subtitle] = "Плеер IPTV и видео для Windows";
                    d[StepWelcome] = "Приветствие";
                    d[StepFolder] = "Папка";
                    d[StepInstall] = "Установка";
                    d[StepFinish] = "Готово";
                    d[WelcomeTitle] = "Установка LyaranTV";
                    d[WelcomeBody] = "Всего три шага: выбрать папку, установить, завершить. Учётная запись не нужна.";
                    d[Feature1Title] = "Каналы, фильмы и сериалы";
                    d[Feature1Body] = "M3U, Xtream и порталы MAC в одной библиотеке.";
                    d[Feature2Title] = "Полное воспроизведение";
                    d[Feature2Body] = "Аудиодорожки, субтитры, программа передач и продолжение просмотра.";
                    d[Feature3Title] = "Ваши данные остаются вашими";
                    d[Feature3Body] = "Учётные записи и позиции шифруются на этом компьютере.";
                    d[Feature4Title] = "Обновления";
                    d[Feature4Body] = "Новые версии предлагаются из самого приложения.";
                    d[NoteTitle] = "Установка для пользователя";
                    d[NoteBody] = "Права администратора не требуются. Существующие учётные записи, плейлисты и обложки не изменяются.";
                    d[FolderTitle] = "Папка установки";
                    d[FolderBody] = "LyaranTV устанавливается в ваш профиль Windows. Можно выбрать другую папку.";
                    d[FolderLabel] = "Установить в";
                    d[Browse] = "Обзор…";
                    d[Shortcut] = "Создать ярлык на рабочем столе";
                    d[SpaceRequired] = "Требуется места";
                    d[SpaceAvailable] = "Доступно";
                    d[SpaceLow] = "На этом диске недостаточно места.";
                    d[DataNote] = "Ваши учётные записи, плейлисты, обложки и позиции в %AppData%\\LyaranTV сохраняются: установка их не меняет.";
                    d[AlreadyInstalled] = "Программа уже установлена: версия будет заменена, данные сохранятся.";
                    d[InstallTitle] = "Идёт установка";
                    d[InstallBody] = "Подождите, файлы копируются.";
                    d[StepUnpacking] = "Подготовка файлов";
                    d[StepExtracting] = "Распаковка программы и видеоплеера";
                    d[StepCopying] = "Копирование файлов";
                    d[StepShortcut] = "Ярлык и запись об удалении";
                    d[StepCleanup] = "Очистка";
                    d[Elapsed] = "Прошло";
                    d[Remaining] = "Осталось";
                    d[FinishTitle] = "Установка завершена";
                    d[FinishBody] = "LyaranTV готов. Можно запускать приложение.";
                    d[LaunchNow] = "Запустить LyaranTV";
                    d[FinishFolder] = "Папка установки";
                    d[Back] = "Назад";
                    d[Next] = "Далее";
                    d[Install] = "Установить";
                    d[Cancel] = "Отмена";
                    d[Close] = "Готово";
                    d[Language] = "Язык";
                    d[ConfirmCancelTitle] = "Отменить установку?";
                    d[ConfirmCancelBody] = "Уже скопированные файлы сохранятся. Установщик можно запустить позже.";
                    d[ErrorTitle] = "Установка не удалась";
                    d[ErrorBody] = "Не удалось установить файлы. Проверьте свободное место и повторите попытку.";
                    d[Retry] = "Повторить";
                    d[StatusUninstalled] = "LyaranTV удалён. Ваши данные сохранены в:";
                    d[NotifyDotNetMissing] = "Для LyaranTV требуется .NET Framework 4.7.2 или новее, которого нет на этом компьютере.\n\nСкачать и установить его сейчас? Windows запросит разрешение.";
                    d[InfoDotNetLaunched] = "Установка .NET Framework запущена. После её завершения запустите этот установщик снова.";
                    d[ErrorDotNet] = "Не удалось скачать .NET Framework. Установите его с сайта Microsoft и запустите установщик снова.";
                    return d;
                case "pt":
                    d[Title] = "Instalação do LyaranTV";
                    d[Subtitle] = "Leitor IPTV e de vídeo para Windows";
                    d[StepWelcome] = "Bem-vindo";
                    d[StepFolder] = "Pasta";
                    d[StepInstall] = "Instalação";
                    d[StepFinish] = "Concluído";
                    d[WelcomeTitle] = "Instalar o LyaranTV";
                    d[WelcomeBody] = "Bastam três passos: escolher uma pasta, instalar e concluir. Não é precisa conta.";
                    d[Feature1Title] = "Canais, filmes e séries";
                    d[Feature1Body] = "M3U, Xtream e portais MAC numa única biblioteca.";
                    d[Feature2Title] = "Reprodução completa";
                    d[Feature2Body] = "Faixas de áudio, subtítulos, guia EPG e retoma.";
                    d[Feature3Title] = "Os seus dados ficam consigo";
                    d[Feature3Body] = "Contas e posições cifradas, guardadas neste computador.";
                    d[Feature4Title] = "Atualizações";
                    d[Feature4Body] = "As novas versões são propostas pela aplicação.";
                    d[NoteTitle] = "Instalação por utilizador";
                    d[NoteBody] = "Não são pedidos direitos de administrador. As contas, listas e capas existentes nunca são alteradas.";
                    d[FolderTitle] = "Pasta de instalação";
                    d[FolderBody] = "O LyaranTV instala-se no seu perfil Windows. Pode escolher outra pasta.";
                    d[FolderLabel] = "Instalar em";
                    d[Browse] = "Procurar…";
                    d[Shortcut] = "Criar um atalho no ambiente de trabalho";
                    d[SpaceRequired] = "Espaço necessário";
                    d[SpaceAvailable] = "Espaço disponível";
                    d[SpaceLow] = "Não há espaço suficiente neste disco.";
                    d[DataNote] = "As suas contas, listas, capas e posições em %AppData%\\LyaranTV são preservadas: a instalação não as altera.";
                    d[AlreadyInstalled] = "Já existe uma versão instalada: será substituída e os seus dados serão preservados.";
                    d[InstallTitle] = "A instalar";
                    d[InstallBody] = "Aguarde enquanto os ficheiros são copiados.";
                    d[StepUnpacking] = "A preparar os ficheiros";
                    d[StepExtracting] = "A extrair a aplicação e o leitor de vídeo";
                    d[StepCopying] = "A copiar ficheiros";
                    d[StepShortcut] = "Atalho e entrada de desinstalação";
                    d[StepCleanup] = "A limpar";
                    d[Elapsed] = "Tempo decorrido";
                    d[Remaining] = "Tempo restante";
                    d[FinishTitle] = "Instalação concluída";
                    d[FinishBody] = "O LyaranTV está pronto. Pode iniciá-lo agora.";
                    d[LaunchNow] = "Iniciar o LyaranTV";
                    d[FinishFolder] = "Pasta de instalação";
                    d[Back] = "Voltar";
                    d[Next] = "Seguinte";
                    d[Install] = "Instalar";
                    d[Cancel] = "Cancelar";
                    d[Close] = "Concluir";
                    d[Language] = "Idioma";
                    d[ConfirmCancelTitle] = "Cancelar a instalação?";
                    d[ConfirmCancelBody] = "Os ficheiros já copiados são conservados. Pode executar o instalador mais tarde.";
                    d[ErrorTitle] = "A instalação falhou";
                    d[ErrorBody] = "Não foi possível instalar os ficheiros. Verifique o espaço livre e tente novamente.";
                    d[Retry] = "Tentar novamente";
                    d[StatusUninstalled] = "O LyaranTV foi desinstalado. Os seus dados foram preservados em:";
                    d[NotifyDotNetMissing] = "O LyaranTV requer o .NET Framework 4.7.2 ou superior, que não está neste computador.\n\nPretende transferi-lo e instalá-lo agora? O Windows pedirá autorização.";
                    d[InfoDotNetLaunched] = "A instalação do .NET Framework foi iniciada. Volte a executar este instalador quando terminar.";
                    d[ErrorDotNet] = "Não foi possível transferir o .NET Framework. Instale-o a partir do site da Microsoft e volte a executar o instalador.";
                    return d;
                default:
                    d[Title] = "LyaranTV setup";
                    d[Subtitle] = "IPTV and video player for Windows";
                    d[StepWelcome] = "Welcome";
                    d[StepFolder] = "Folder";
                    d[StepInstall] = "Install";
                    d[StepFinish] = "Finish";
                    d[WelcomeTitle] = "Install LyaranTV";
                    d[WelcomeBody] = "Three steps: choose a folder, install, finish. No account required.";
                    d[Feature1Title] = "Channels, movies and series";
                    d[Feature1Body] = "M3U, Xtream and MAC portals in one library.";
                    d[Feature2Title] = "Complete playback";
                    d[Feature2Body] = "Audio tracks, subtitles, EPG guide and resume.";
                    d[Feature3Title] = "Your data stays yours";
                    d[Feature3Body] = "Accounts and positions encrypted, kept on this PC.";
                    d[Feature4Title] = "Updates";
                    d[Feature4Body] = "New versions are offered from the app.";
                    d[NoteTitle] = "Per-user installation";
                    d[NoteBody] = "No administrator rights needed. Existing accounts, playlists and artwork are never modified.";
                    d[FolderTitle] = "Installation folder";
                    d[FolderBody] = "LyaranTV installs in your Windows profile. You can choose another folder.";
                    d[FolderLabel] = "Install in";
                    d[Browse] = "Browse…";
                    d[Shortcut] = "Create a desktop shortcut";
                    d[SpaceRequired] = "Required space";
                    d[SpaceAvailable] = "Available space";
                    d[SpaceLow] = "Not enough space on this drive.";
                    d[DataNote] = "Your accounts, playlists, artwork and positions already in %AppData%\\LyaranTV are kept: this setup never modifies them.";
                    d[AlreadyInstalled] = "A version is already installed: it will be replaced and your data kept.";
                    d[InstallTitle] = "Installing";
                    d[InstallBody] = "Please wait while the files are copied.";
                    d[StepUnpacking] = "Preparing files";
                    d[StepExtracting] = "Extracting the app and video engine";
                    d[StepCopying] = "Copying files";
                    d[StepShortcut] = "Shortcut and uninstall entry";
                    d[StepCleanup] = "Cleaning up";
                    d[Elapsed] = "Elapsed";
                    d[Remaining] = "Remaining";
                    d[FinishTitle] = "Installation complete";
                    d[FinishBody] = "LyaranTV is ready. You can start it now.";
                    d[LaunchNow] = "Launch LyaranTV";
                    d[FinishFolder] = "Installation folder";
                    d[Back] = "Back";
                    d[Next] = "Next";
                    d[Install] = "Install";
                    d[Cancel] = "Cancel";
                    d[Close] = "Finish";
                    d[Language] = "Language";
                    d[ConfirmCancelTitle] = "Cancel the installation?";
                    d[ConfirmCancelBody] = "Files already copied are kept. You can run the setup again later.";
                    d[ErrorTitle] = "Installation failed";
                    d[ErrorBody] = "No file could be installed. Check free disk space and try again.";
                    d[Retry] = "Retry";
                    d[StatusUninstalled] = "LyaranTV has been uninstalled. Your personal data was kept in:";
                    d[NotifyDotNetMissing] = "LyaranTV needs .NET Framework 4.7.2 or later, which is missing from this computer.\n\nDownload and install it now? Windows will ask for permission.";
                    d[InfoDotNetLaunched] = "The .NET Framework installation has started. Run this setup again when it finishes.";
                    d[ErrorDotNet] = "Could not download .NET Framework. Install it from Microsoft's website, then run this setup again.";
                    return d;
            }
        }
    }

    // ---------------------------------------------------------------- thème

    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(10, 11, 16);
        public static readonly Color SidebarTop = Color.FromArgb(17, 18, 26);
        public static readonly Color SidebarBottom = Color.FromArgb(7, 8, 12);
        public static readonly Color Surface = Color.FromArgb(18, 19, 27);
        public static readonly Color SurfaceHover = Color.FromArgb(23, 25, 35);
        public static readonly Color Field = Color.FromArgb(12, 13, 19);
        public static readonly Color Border = Color.FromArgb(38, 40, 53);
        public static readonly Color BorderSoft = Color.FromArgb(27, 29, 39);
        public static readonly Color Text = Color.FromArgb(242, 243, 247);
        public static readonly Color Muted = Color.FromArgb(156, 161, 179);
        public static readonly Color Dim = Color.FromArgb(106, 111, 129);
        public static readonly Color Accent = Color.FromArgb(139, 92, 246);
        public static readonly Color AccentHover = Color.FromArgb(158, 118, 255);
        public static readonly Color AccentPressed = Color.FromArgb(118, 74, 214);
        public static readonly Color AccentSoft = Color.FromArgb(52, 38, 88);
        public static readonly Color Success = Color.FromArgb(52, 211, 153);
        public static readonly Color Danger = Color.FromArgb(248, 113, 113);

        public static Font Body(float size) { return new Font("Segoe UI", size); }
        public static Font Strong(float size) { return new Font("Segoe UI Semibold", size); }
    }

    internal static class Draw
    {
        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;
            int diameter = Math.Min(Math.Max(1, radius * 2), Math.Min(bounds.Width, bounds.Height));
            int arc = diameter / 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Signature LyaranTV : cinq barres d'égaliseur (piste 17).</summary>
        public static void Logo(Graphics g, Rectangle area, Color color)
        {
            float unit = area.Width / 5f;
            float thickness = Math.Max(2f, unit * 0.52f);
            float[] heights = { 0.46f, 0.78f, 1f, 0.62f, 0.84f };
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, thickness))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                for (int i = 0; i < heights.Length; i++)
                {
                    float x = area.X + unit * (i + 0.5f);
                    float height = area.Height * heights[i];
                    float top = area.Y + (area.Height - height) / 2f;
                    g.DrawLine(pen, x, top, x, top + height);
                }
            }
            g.SmoothingMode = old;
        }

        public static void Check(Graphics g, Rectangle area, Color color, float width)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                PointF[] points =
                {
                    new PointF(area.X + area.Width * 0.20f, area.Y + area.Height * 0.54f),
                    new PointF(area.X + area.Width * 0.42f, area.Y + area.Height * 0.74f),
                    new PointF(area.X + area.Width * 0.80f, area.Y + area.Height * 0.28f)
                };
                g.DrawLines(pen, points);
            }
            g.SmoothingMode = old;
        }

        public static void Cross(Graphics g, Rectangle area, Color color, float width)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, area.X + area.Width * 0.3f, area.Y + area.Height * 0.3f, area.X + area.Width * 0.7f, area.Y + area.Height * 0.7f);
                g.DrawLine(pen, area.X + area.Width * 0.7f, area.Y + area.Height * 0.3f, area.X + area.Width * 0.3f, area.Y + area.Height * 0.7f);
            }
            g.SmoothingMode = old;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "—";
            double mo = bytes / (1024.0 * 1024.0);
            if (mo >= 1024) return (mo / 1024.0).ToString("0.0") + " Go";
            return Math.Round(mo).ToString("0") + " Mo";
        }
    }

    // ---------------------------------------------------------------- boutons

    /// <summary>Bouton arrondi dessiné : accent plein ou contour discret.</summary>
    internal sealed class FlatButton : Button
    {
        public bool Accent;
        public int Radius = 10;
        bool hover;
        bool down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
            Font = Theme.Strong(9.4f);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Draw.Rounded(bounds, Radius))
            {
                if (Accent)
                {
                    Color top = Theme.Accent;
                    Color bottom = Theme.AccentPressed;
                    if (!Enabled) { top = Color.FromArgb(58, 52, 78); bottom = Color.FromArgb(48, 43, 64); }
                    else if (down) { top = Theme.AccentPressed; bottom = Theme.AccentPressed; }
                    else if (hover) { top = Theme.AccentHover; bottom = Theme.Accent; }
                    using (var brush = new LinearGradientBrush(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height + 1), top, bottom, 90f))
                        g.FillPath(brush, path);
                    // reflet supérieur très léger
                    using (var gloss = new LinearGradientBrush(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2 + 1), Color.FromArgb(38, Color.White), Color.FromArgb(0, Color.White), 90f))
                        g.FillPath(gloss, path);
                }
                else
                {
                    Color fill = !Enabled ? Theme.Surface : (hover ? Theme.SurfaceHover : Theme.Surface);
                    using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                    using (var pen = new Pen(hover && Enabled ? Color.FromArgb(72, 66, 104) : Theme.Border))
                        g.DrawPath(pen, path);
                }
            }
            if (Focused && ShowFocusCues)
            {
                var ring = new Rectangle(3, 3, Width - 7, Height - 7);
                using (GraphicsPath path = Draw.Rounded(ring, Math.Max(4, Radius - 3)))
                using (var pen = new Pen(Color.FromArgb(90, Color.White))) g.DrawPath(pen, path);
            }
            Color text = Enabled ? (Accent ? Color.White : (hover ? Theme.Text : Theme.Muted)) : Theme.Dim;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(2, 0, Width - 4, Height - 1), text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Case à cocher dessinée, assortie au thème.</summary>
    internal sealed class OptionToggle : Control
    {
        bool hover;
        bool down;

        public bool Checked { get; set; }

        public OptionToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body(9.4f);
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = 26;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnClick(EventArgs e) { Toggle(); base.OnClick(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        void Toggle()
        {
            Checked = !Checked;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Space || base.IsInputKey(keyData); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Toggle(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(1, (Height - 20) / 2, 20, 20);
            using (GraphicsPath path = Draw.Rounded(box, 6))
            {
                if (Checked)
                {
                    using (var brush = new LinearGradientBrush(box, Theme.AccentHover, Theme.Accent, 90f)) g.FillPath(brush, path);
                }
                else
                {
                    using (var brush = new SolidBrush(down ? Theme.SurfaceHover : (hover ? Theme.SurfaceHover : Theme.Surface))) g.FillPath(brush, path);
                    using (var pen = new Pen(hover ? Color.FromArgb(96, 88, 138) : Theme.Border)) g.DrawPath(pen, path);
                }
            }
            if (Checked) Draw.Check(g, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8), Color.White, 2.2f);
            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath path = Draw.Rounded(new Rectangle(-2, box.Y - 3, box.Width + 5, box.Height + 6), 8))
                using (var pen = new Pen(Color.FromArgb(80, Theme.Accent))) g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(32, 0, Width - 33, Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // ---------------------------------------------------------------- progression

    /// <summary>Barre de progression fine, arrondie, animée.</summary>
    internal sealed class SlimBar : Control
    {
        readonly Timer timer;
        int value;
        float shown;

        public SlimBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 10;
            timer = new Timer();
            timer.Interval = 16;
            timer.Tick += delegate
            {
                float delta = value - shown;
                if (Math.Abs(delta) < 0.4f) { shown = value; timer.Stop(); }
                else shown += delta * 0.28f;
                Invalidate();
            };
        }

        public int Value
        {
            get { return value; }
            set
            {
                this.value = Math.Max(0, Math.Min(100, value));
                if (!timer.Enabled) timer.Start();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = Height / 2;
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Draw.Rounded(track, radius))
            {
                using (var brush = new SolidBrush(Theme.BorderSoft)) g.FillPath(brush, path);
                using (var pen = new Pen(Color.FromArgb(34, 36, 48))) g.DrawPath(pen, path);
            }
            float ratio = Math.Max(0f, Math.Min(1f, shown / 100f));
            int fillWidth = (int)Math.Round(track.Width * ratio);
            if (fillWidth > 4)
            {
                var fill = new Rectangle(track.X, track.Y, fillWidth, track.Height);
                using (GraphicsPath path = Draw.Rounded(fill, radius))
                {
                    g.SetClip(path, CombineMode.Intersect);
                    using (var brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, track.Width), track.Height), Theme.Accent, Color.FromArgb(186, 148, 255), 0f))
                        g.FillRectangle(brush, fill);
                    using (var glow = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, track.Width), track.Height), Color.FromArgb(70, Color.White), Color.FromArgb(0, Color.White), 90f))
                        g.FillRectangle(glow, new Rectangle(fill.X, fill.Y, fill.Width, Math.Max(1, fill.Height / 2)));
                    g.ResetClip();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Liste des étapes d'installation avec coches.</summary>
    internal sealed class InstallSteps : Control
    {
        public string[] Labels = new string[0];
        int current;

        public InstallSteps()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 190;
        }

        public int Current
        {
            get { return current; }
            set { current = value; Invalidate(); }
        }

        public void Reset() { current = 0; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rowHeight = 36;
            for (int i = 0; i < Labels.Length; i++)
            {
                int y = i * rowHeight;
                bool done = i < current;
                bool active = i == current;
                var circle = new Rectangle(2, y + (rowHeight - 20) / 2, 20, 20);
                if (done)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(28, Theme.Success))) g.FillEllipse(brush, circle);
                    using (var pen = new Pen(Color.FromArgb(90, Theme.Success))) g.DrawEllipse(pen, circle);
                    Draw.Check(g, new Rectangle(circle.X + 5, circle.Y + 5, circle.Width - 10, circle.Height - 10), Theme.Success, 2f);
                }
                else if (active)
                {
                    using (var halo = new SolidBrush(Color.FromArgb(46, Theme.Accent))) g.FillEllipse(halo, new Rectangle(circle.X - 5, circle.Y - 5, 30, 30));
                    using (var brush = new SolidBrush(Theme.Accent)) g.FillEllipse(brush, circle);
                    TextRenderer.DrawText(g, (i + 1).ToString(), Theme.Strong(8f), circle, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    using (var pen = new Pen(Theme.Border)) g.DrawEllipse(pen, circle);
                    TextRenderer.DrawText(g, (i + 1).ToString(), Theme.Body(8f), circle, Theme.Dim,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                Color text = active ? Theme.Text : (done ? Theme.Muted : Theme.Dim);
                Font font = active ? Theme.Strong(9.2f) : Theme.Body(9.2f);
                TextRenderer.DrawText(g, Labels[i], font, new Rectangle(36, y, Width - 38, rowHeight), text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Barre latérale : identité, étapes, sélecteur de langue.</summary>
    internal sealed class Sidebar : Control
    {
        int current;
        string[] steps = new string[0];

        public Sidebar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public int Current
        {
            get { return current; }
            set { current = value; Invalidate(); }
        }

        public string[] Steps
        {
            get { return steps; }
            set { steps = value ?? new string[0]; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Theme.SidebarTop, Theme.SidebarBottom, 90f))
                g.FillRectangle(brush, new Rectangle(0, 0, Width, Height));

            // halo violet très discret en haut
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-120, -190, 420, 380);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(30, Theme.Accent);
                    glow.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };
                    g.FillPath(glow, path);
                }
            }

            using (var pen = new Pen(Color.FromArgb(24, 26, 35))) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

            Draw.Logo(g, new Rectangle(24, 30, 30, 30), Theme.Accent);
            TextRenderer.DrawText(g, SetupProgram.ProductName, Theme.Strong(14f), new Rectangle(66, 28, Width - 80, 24), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "v" + SetupProgram.Version, Theme.Body(8.4f), new Rectangle(66, 50, Width - 80, 18), Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            using (var pen = new Pen(Color.FromArgb(30, 32, 42))) g.DrawLine(pen, 24, 96, Width - 24, 96);
            TextRenderer.DrawText(g, Strings.Get(Strings.Title), Theme.Body(8.2f), new Rectangle(24, 104, Width - 40, 18), Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            int top = 146;
            for (int i = 0; i < steps.Length; i++)
            {
                int y = top + i * 48;
                bool done = i < current;
                bool active = i == current;
                var circle = new Rectangle(24, y, 24, 24);
                if (active)
                {
                    using (var brush = new SolidBrush(Theme.Accent)) g.FillEllipse(brush, circle);
                    TextRenderer.DrawText(g, (i + 1).ToString(), Theme.Strong(8.5f), circle, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else if (done)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(30, Theme.Success))) g.FillEllipse(brush, circle);
                    Draw.Check(g, new Rectangle(circle.X + 6, circle.Y + 6, 12, 12), Theme.Success, 2f);
                }
                else
                {
                    using (var pen = new Pen(Color.FromArgb(52, 55, 70))) g.DrawEllipse(pen, circle);
                    TextRenderer.DrawText(g, (i + 1).ToString(), Theme.Body(8.5f), circle, Theme.Dim,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                Color color = active ? Theme.Text : (done ? Theme.Muted : Theme.Dim);
                Font font = active ? Theme.Strong(9.2f) : Theme.Body(9.2f);
                TextRenderer.DrawText(g, steps[i], font, new Rectangle(60, y, Width - 72, 24), color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            TextRenderer.DrawText(g, SetupProgram.Publisher, Theme.Body(8.2f), new Rectangle(24, Height - 92, Width - 40, 18), Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Carte arrondie avec bordure, pour regrouper un réglage.</summary>
    internal sealed class SurfaceCard : Panel
    {
        public SurfaceCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Draw.Rounded(bounds, 12))
            {
                using (var brush = new SolidBrush(Theme.Surface)) g.FillPath(brush, path);
                using (var pen = new Pen(Theme.BorderSoft)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Champ de saisie arrondi, avec bordure qui s'éclaire au focus.</summary>
    internal sealed class FieldPanel : Panel
    {
        public TextBox Editor = new TextBox();

        public FieldPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            Height = 42;
            Editor.BorderStyle = BorderStyle.None;
            Editor.BackColor = Theme.Field;
            Editor.ForeColor = Theme.Text;
            Editor.Font = Theme.Body(9.4f);
            Editor.SetBounds(14, 12, 300, 20);
            Editor.Enter += delegate { Invalidate(); };
            Editor.Leave += delegate { Invalidate(); };
            Controls.Add(Editor);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Editor.SetBounds(14, (Height - Editor.Height) / 2, Math.Max(40, Width - 28), Editor.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Draw.Rounded(bounds, 10))
            {
                using (var brush = new SolidBrush(Theme.Field)) g.FillPath(brush, path);
                using (var pen = new Pen(Editor.Focused ? Color.FromArgb(150, Theme.Accent) : Theme.Border)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Ligne de présentation : pastille, titre, détail.</summary>
    internal sealed class FeatureRow : Control
    {
        public string Detail = "";

        public FeatureRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            Height = 50;
            Font = Theme.Strong(9.6f);
            ForeColor = Theme.Text;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var dot = new Rectangle(2, 8, 8, 8);
            using (var brush = new SolidBrush(Color.FromArgb(40, Theme.Accent))) g.FillEllipse(brush, new Rectangle(dot.X - 4, dot.Y - 4, 16, 16));
            using (var brush = new SolidBrush(Theme.Accent)) g.FillEllipse(brush, dot);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(24, 2, Width - 26, 20), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Detail, Theme.Body(8.8f), new Rectangle(24, 22, Width - 26, 22), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Encadré d'information avec barre d'accent.</summary>
    internal sealed class NoticeCard : Control
    {
        public string Heading = "";
        public string Body = "";

        public NoticeCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Draw.Rounded(bounds, 12))
            {
                using (var brush = new SolidBrush(Theme.Surface)) g.FillPath(brush, path);
                using (var pen = new Pen(Theme.BorderSoft)) g.DrawPath(pen, path);
            }
            using (GraphicsPath bar = Draw.Rounded(new Rectangle(0, 14, 3, Height - 28), 2))
            using (var brush = new SolidBrush(Theme.Accent)) g.FillPath(brush, bar);
            TextRenderer.DrawText(g, Heading, Theme.Strong(9.4f), new Rectangle(20, 14, Width - 36, 20), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Body, Theme.Body(8.8f), new Rectangle(20, 34, Width - 36, Height - 44), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>Grande coche ou croix de fin d'installation.</summary>
    internal sealed class StatusArt : Control
    {
        public bool Failure;

        public StatusArt()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = Failure ? Theme.Danger : Theme.Success;
            var circle = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Draw.Rounded(circle, Width / 2))
            {
                using (var brush = new SolidBrush(Color.FromArgb(26, accent))) g.FillPath(brush, path);
                using (var pen = new Pen(Color.FromArgb(110, accent), 1.4f)) g.DrawPath(pen, path);
            }
            if (Failure) Draw.Cross(g, new Rectangle(Width / 4, Height / 4, Width / 2, Height / 2), accent, 3.4f);
            else Draw.Check(g, new Rectangle(Width / 4, Height / 4, Width / 2, Height / 2), accent, 3.4f);
        }

    }

    // ---------------------------------------------------------------- assistant

    internal sealed class SetupForm : Form
    {
        const int SidebarWidth = 208;
        const int ContentLeft = SidebarWidth + 24;
        const int ContentWidth = 760 - ContentLeft - 24;

        readonly bool update;
        readonly long requiredBytes;

        readonly Sidebar sidebar = new Sidebar();
        readonly Panel[] pages = new Panel[4];
        readonly Label[] pageTitles = new Label[4];
        readonly Label[] pageBodies = new Label[4];
        readonly List<KeyValuePair<Control, string>> bindings = new List<KeyValuePair<Control, string>>();

        readonly FlatButton next = new FlatButton();
        readonly FlatButton back = new FlatButton();
        readonly FlatButton cancelButton = new FlatButton();
        readonly FlatButton languageButton = new FlatButton();
        readonly ContextMenuStrip languageMenu = new ContextMenuStrip();

        readonly FieldPanel field = new FieldPanel();
        readonly FlatButton browse = new FlatButton();
        readonly OptionToggle shortcut = new OptionToggle();
        readonly Label spaceInfo = new Label();
        readonly Label upgradeNote = new Label();

        readonly FeatureRow[] features = new FeatureRow[4];
        readonly NoticeCard notice = new NoticeCard();

        readonly InstallSteps steps = new InstallSteps();
        readonly SlimBar bar = new SlimBar();
        readonly Label percent = new Label();
        readonly Label timing = new Label();
        string currentStatus = "";
        readonly Label finishVersion = new Label();

        readonly StatusArt art = new StatusArt();
        readonly Label finishBody = new Label();
        readonly OptionToggle launch = new OptionToggle();
        readonly Label finishPathLabel = new Label();
        readonly Label finishPathValue = new Label();

        int page;
        bool installing;
        bool confirmedClose;
        bool success;
        string failureText = "";
        string progressError = "";
        string target;
        readonly Stopwatch clock = new Stopwatch();

        public int ExitCode;

        public SetupForm(bool update)
        {
            this.update = update;
            requiredBytes = SetupProgram.PayloadSize();

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.Body(9f);
            Text = Strings.Get(Strings.Title);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(760, 540);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            sidebar.SetBounds(0, 0, SidebarWidth, ClientSize.Height);
            Controls.Add(sidebar);

            BuildWelcomePage();
            BuildFolderPage();
            BuildInstallPage();
            BuildFinishPage();

            sidebar.Steps = new[] { Strings.Get(Strings.StepWelcome), Strings.Get(Strings.StepFolder), Strings.Get(Strings.StepInstall), Strings.Get(Strings.StepFinish) };

            // --- barre inférieure
            back.SetBounds(ContentLeft + ContentWidth - 136 - 12 - 112, 492, 112, 40);
            back.Click += delegate { if (installing) return; ShowPage(Math.Max(0, page - 1)); };
            Controls.Add(back);

            next.Accent = true;
            next.SetBounds(ContentLeft + ContentWidth - 136, 492, 136, 40);
            next.Click += delegate { Advance(); };
            Controls.Add(next);

            cancelButton.SetBounds(ContentLeft + ContentWidth - 136, 492, 136, 40);
            cancelButton.Click += delegate { RequestClose(); };
            Controls.Add(cancelButton);

            // --- sélecteur de langue, en bas de la barre latérale
            languageButton.SetBounds(20, 486, SidebarWidth - 40, 38);
            languageButton.Font = Theme.Body(9f);
            languageButton.Click += delegate { languageMenu.Show(languageButton, new Point(0, -languageMenu.PreferredSize.Height)); };
            sidebar.Controls.Add(languageButton);

            languageMenu.Renderer = new DarkMenuRenderer();
            languageMenu.BackColor = Color.FromArgb(20, 21, 29);
            languageMenu.ForeColor = Theme.Text;
            languageMenu.ShowImageMargin = false;
            string[] codes = { "fr", "en", "es", "ru", "pt" };
            foreach (string code in codes)
            {
                string chosen = code;
                var item = new ToolStripMenuItem(Strings.LanguageName(code));
                item.ForeColor = Theme.Text;
                item.Checked = code == Strings.Current;
                item.Click += delegate
                {
                    Strings.Current = chosen;
                    foreach (ToolStripMenuItem other in languageMenu.Items) other.Checked = other.Text == Strings.LanguageName(chosen);
                    ApplyTexts();
                };
                languageMenu.Items.Add(item);
            }

            AcceptButton = next;
            ResumeLayout(false);
            ApplyTexts();
            ShowPage(0);
        }

        // ------------------------------------------------------------ pages

        void BuildWelcomePage()
        {
            var panel = NewPage(Strings.WelcomeTitle, Strings.WelcomeBody);
            int y = 86;
            string[] titles = { Strings.Feature1Title, Strings.Feature2Title, Strings.Feature3Title, Strings.Feature4Title };
            string[] details = { Strings.Feature1Body, Strings.Feature2Body, Strings.Feature3Body, Strings.Feature4Body };
            for (int i = 0; i < features.Length; i++)
            {
                var row = new FeatureRow();
                row.SetBounds(0, y, ContentWidth, 50);
                panel.Controls.Add(row);
                Bind(row, titles[i], details[i]);
                features[i] = row;
                y += 52;
            }
            notice.SetBounds(0, y + 14, ContentWidth, 96);
            panel.Controls.Add(notice);
            pages[0] = panel;
        }

        void BuildFolderPage()
        {
            var panel = NewPage(Strings.FolderTitle, Strings.FolderBody);

            var card = new SurfaceCard();
            card.SetBounds(0, 86, ContentWidth, 104);
            panel.Controls.Add(card);

            var label = new Label();
            label.AutoSize = false;
            label.SetBounds(20, 16, 260, 18);
            label.Font = Theme.Strong(9f);
            label.ForeColor = Theme.Muted;
            card.Controls.Add(label);
            Bind(label, Strings.FolderLabel);

            field.SetBounds(20, 42, ContentWidth - 40 - 110, 42);
            card.Controls.Add(field);
            field.Editor.Text = SetupProgram.InstallDir;
            field.Editor.TextChanged += delegate { RefreshSpace(); };

            browse.SetBounds(card.Width - 20 - 96, 47, 96, 32);
            browse.Font = Theme.Body(8.8f);
            browse.Click += delegate
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    if (Directory.Exists(field.Editor.Text)) dialog.SelectedPath = field.Editor.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) field.Editor.Text = dialog.SelectedPath;
                }
            };
            card.Controls.Add(browse);
            Bind(browse, Strings.Browse);

            shortcut.SetBounds(0, 206, ContentWidth, 26);
            shortcut.Checked = true;
            panel.Controls.Add(shortcut);
            Bind(shortcut, Strings.Shortcut);

            spaceInfo.SetBounds(0, 240, ContentWidth, 20);
            spaceInfo.Font = Theme.Body(8.8f);
            spaceInfo.ForeColor = Theme.Muted;
            spaceInfo.BackColor = Theme.Background;
            panel.Controls.Add(spaceInfo);

            upgradeNote.SetBounds(0, 266, ContentWidth, 22);
            upgradeNote.Font = Theme.Body(8.8f);
            upgradeNote.ForeColor = Theme.Accent;
            upgradeNote.BackColor = Theme.Background;
            panel.Controls.Add(upgradeNote);
            Bind(upgradeNote, Strings.AlreadyInstalled);

            var note = new Label();
            note.SetBounds(0, 300, ContentWidth, 46);
            note.Font = Theme.Body(8.6f);
            note.ForeColor = Theme.Dim;
            note.BackColor = Theme.Background;
            panel.Controls.Add(note);
            Bind(note, Strings.DataNote);

            pages[1] = panel;
        }

        void BuildInstallPage()
        {
            var panel = NewPage(Strings.InstallTitle, Strings.InstallBody);

            steps.SetBounds(0, 88, ContentWidth, 190);
            panel.Controls.Add(steps);

            bar.SetBounds(0, 288, ContentWidth, 10);
            panel.Controls.Add(bar);

            percent.SetBounds(0, 306, 110, 22);
            percent.Font = Theme.Strong(9f);
            percent.ForeColor = Theme.Text;
            percent.BackColor = Theme.Background;
            percent.Text = "0 %";
            panel.Controls.Add(percent);

            timing.SetBounds(120, 306, ContentWidth - 120, 22);
            timing.Font = Theme.Body(8.6f);
            timing.ForeColor = Theme.Dim;
            timing.BackColor = Theme.Background;
            timing.TextAlign = ContentAlignment.TopRight;
            panel.Controls.Add(timing);

            pages[2] = panel;
        }

        void BuildFinishPage()
        {
            var panel = NewPage(Strings.FinishTitle, Strings.FinishBody);

            art.SetBounds((ContentWidth - 64) / 2, 86, 64, 64);
            panel.Controls.Add(art);

            finishBody.SetBounds(0, 162, ContentWidth, 46);
            finishBody.Font = Theme.Body(8.8f);
            finishBody.ForeColor = Theme.Danger;
            finishBody.BackColor = Theme.Background;
            finishBody.TextAlign = ContentAlignment.TopCenter;
            panel.Controls.Add(finishBody);

            launch.SetBounds((ContentWidth - 200) / 2, 218, 200, 26);
            launch.Checked = true;
            panel.Controls.Add(launch);
            Bind(launch, Strings.LaunchNow);

            finishPathLabel.SetBounds(0, 266, ContentWidth, 18);
            finishPathLabel.Font = Theme.Strong(8.4f);
            finishPathLabel.ForeColor = Theme.Dim;
            finishPathLabel.BackColor = Theme.Background;
            finishPathLabel.TextAlign = ContentAlignment.TopCenter;
            panel.Controls.Add(finishPathLabel);
            Bind(finishPathLabel, Strings.FinishFolder);

            finishPathValue.SetBounds(0, 286, ContentWidth, 20);
            finishPathValue.Font = Theme.Body(8.6f);
            finishPathValue.ForeColor = Theme.Muted;
            finishPathValue.BackColor = Theme.Background;
            finishPathValue.TextAlign = ContentAlignment.TopCenter;
            panel.Controls.Add(finishPathValue);

            finishVersion.SetBounds(0, 322, ContentWidth, 20);
            finishVersion.Font = Theme.Body(8.2f);
            finishVersion.ForeColor = Theme.Dim;
            finishVersion.BackColor = Theme.Background;
            finishVersion.TextAlign = ContentAlignment.TopCenter;
            panel.Controls.Add(finishVersion);

            pages[3] = panel;
        }

        int panelCount;

        Panel NewPage(string titleKey, string bodyKey)
        {
            var panel = new Panel();
            panel.SetBounds(ContentLeft, 48, ContentWidth, 424);
            panel.BackColor = Theme.Background;
            panel.Visible = false;
            Controls.Add(panel);

            var title = new Label();
            title.SetBounds(0, 0, ContentWidth, 30);
            title.Font = Theme.Strong(17.5f);
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Background;
            panel.Controls.Add(title);
            Bind(title, titleKey);

            var body = new Label();
            body.SetBounds(0, 34, ContentWidth, 40);
            body.Font = Theme.Body(9.2f);
            body.ForeColor = Theme.Muted;
            body.BackColor = Theme.Background;
            panel.Controls.Add(body);
            Bind(body, bodyKey);

            pageTitles[panelCount] = title;
            pageBodies[panelCount] = body;
            panelCount++;
            return panel;
        }

        void Bind(Control control, string key)
        {
            bindings.Add(new KeyValuePair<Control, string>(control, key));
        }

        void Bind(FeatureRow row, string titleKey, string detailKey)
        {
            bindings.Add(new KeyValuePair<Control, string>(row, titleKey));
            bindings.Add(new KeyValuePair<Control, string>(row, "detail:" + detailKey));
        }

        // ------------------------------------------------------------ textes

        void ApplyTexts()
        {
            foreach (KeyValuePair<Control, string> entry in bindings)
            {
                if (entry.Key == null) continue;
                if (entry.Value.StartsWith("detail:", StringComparison.Ordinal))
                {
                    var row = entry.Key as FeatureRow;
                    if (row != null) row.Detail = Strings.Get(entry.Value.Substring(7));
                    continue;
                }
                entry.Key.Text = Strings.Get(entry.Value);
            }

            var titles = new[] { Strings.Get(Strings.StepWelcome), Strings.Get(Strings.StepFolder), Strings.Get(Strings.StepInstall), Strings.Get(Strings.StepFinish) };
            sidebar.Steps = titles;

            steps.Labels = new[]
            {
                Strings.Get(Strings.StepUnpacking),
                Strings.Get(Strings.StepExtracting),
                Strings.Get(Strings.StepCopying),
                Strings.Get(Strings.StepShortcut),
                Strings.Get(Strings.StepCleanup)
            };
            steps.Invalidate();

            notice.Heading = Strings.Get(Strings.NoteTitle);
            notice.Body = Strings.Get(Strings.NoteBody);
            notice.Invalidate();

            Text = Strings.Get(Strings.Title) + " · " + SetupProgram.Version;
            languageButton.Text = Strings.LanguageName(Strings.Current) + "   ▾";
            finishVersion.Text = SetupProgram.ProductName + " " + SetupProgram.Version + "   ·   " + SetupProgram.Publisher;
            RefreshSpace();
            UpdateFinishState();
            UpdateFooter();
        }

        void UpdateFooter()
        {
            bool installPage = page == 2;
            back.Visible = !installing && (page == 1);
            next.Visible = !installing && page != 2;
            cancelButton.Visible = installing;

            if (page == 0) { next.Text = Strings.Get(Strings.Next); next.Enabled = true; }
            else if (page == 1) { next.Text = Strings.Get(Strings.Install); next.Enabled = CanInstall(); }
            else if (page == 3)
            {
                next.Text = success ? Strings.Get(Strings.Close) : Strings.Get(Strings.Retry);
                next.Enabled = true;
            }

            if (installing)
            {
                cancelButton.Text = Strings.Get(Strings.Cancel);
                cancelButton.Enabled = true;
            }
            else if (installPage)
            {
                next.Visible = false;
            }

            back.Text = Strings.Get(Strings.Back);
        }

        void ShowPage(int index)
        {
            page = Math.Max(0, Math.Min(3, index));
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].Visible = i == page;
            sidebar.Current = page;
            UpdateFooter();
        }

        void Advance()
        {
            if (installing) return;
            if (page == 3)
            {
                if (success) Close();
                else { ResetForRetry(); }
                return;
            }
            if (page == 1) { StartInstall(); return; }
            ShowPage(page + 1);
        }

        void ResetForRetry()
        {
            failureText = "";
            success = false;
            steps.Reset();
            bar.Value = 0;
            percent.Text = "0 %";
            currentStatus = "";
            timing.Text = "";
            ShowPage(1);
        }

        // ------------------------------------------------------------ espace disque

        bool CanInstall()
        {
            string path = field.Editor.Text.Trim();
            if (path.Length == 0 || !Path.IsPathRooted(path)) return false;
            long free = FreeSpace(path);
            return requiredBytes <= 0 || free <= 0 || free > requiredBytes;
        }

        static long FreeSpace(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (String.IsNullOrEmpty(root)) return 0;
                return new DriveInfo(root).AvailableFreeSpace;
            }
            catch { return 0; }
        }

        void RefreshSpace()
        {
            string path = field.Editor.Text.Trim();
            string required = SetupProgram.PayloadSize() > 0 ? Draw.FormatSize(requiredBytes) : "—";
            long free = path.Length > 0 && Path.IsPathRooted(path) ? FreeSpace(path) : 0;
            string line = Strings.Get(Strings.SpaceRequired) + " : " + required + "   ·   " + Strings.Get(Strings.SpaceAvailable) + " : " + Draw.FormatSize(free);
            bool low = requiredBytes > 0 && free > 0 && free < requiredBytes;
            if (low) line += "   ·   " + Strings.Get(Strings.SpaceLow);
            spaceInfo.Text = line;
            spaceInfo.ForeColor = low ? Theme.Danger : Theme.Muted;

            bool installed = Directory.Exists(SetupProgram.InstallDir) && File.Exists(Path.Combine(SetupProgram.InstallDir, "LyaranTV.exe"));
            upgradeNote.Visible = update || installed;
            UpdateFooter();
        }

        // ------------------------------------------------------------ installation

        void StartInstall()
        {
            target = field.Editor.Text.Trim();
            if (target.Length == 0) return;

            installing = true;
            success = false;
            failureText = "";
            progressError = "";
            steps.Reset();
            bar.Value = 0;
            percent.Text = "0 %";
            timing.Text = "";
            currentStatus = "";
            clock.Reset();
            clock.Start();
            art.Failure = false;

            ShowPage(2);
            UpdateFooter();

            var worker = new System.ComponentModel.BackgroundWorker();
            worker.DoWork += delegate(object sender, System.ComponentModel.DoWorkEventArgs e)
            {
                int result = SetupProgram.Install(target, shortcut.Checked, update,
                    delegate(int value, string message, int step)
                    {
                        try
                        {
                            if (value < 0) { progressError = message; return; }
                            BeginInvoke(new Action(delegate
                            {
                                bar.Value = value;
                                percent.Text = value + " %";
                                if (step >= 0) steps.Current = step;
                                currentStatus = message;
                                UpdateTiming(value);
                            }));
                        }
                        catch { }
                    });
                e.Result = result;
            };
            worker.RunWorkerCompleted += delegate(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
            {
                installing = false;
                clock.Stop();
                ExitCode = e.Result is int ? (int)e.Result : 1;
                success = ExitCode == 0;
                if (success)
                {
                    steps.Current = steps.Labels.Length;
                    bar.Value = 100;
                    percent.Text = "100 %";
                }
                else
                {
                    failureText = progressError.Length > 0 ? progressError : currentStatus;
                }
                ShowFinish();
            };
            worker.RunWorkerAsync();
        }

        void UpdateTiming(int value)
        {
            TimeSpan elapsed = clock.Elapsed;
            timing.Text = Strings.Get(Strings.Elapsed) + " " + Format( elapsed) + "   ·   " + Strings.Get(Strings.Remaining) + " " + Estimate(elapsed, value);
        }

        static string Format(TimeSpan span)
        {
            return ((int)span.TotalMinutes).ToString("0") + ":" + span.Seconds.ToString("00");
        }

        string Estimate(TimeSpan elapsed, int percent)
        {
            if (percent <= 0 || elapsed.TotalSeconds < 1.2) return "—";
            double total = elapsed.TotalSeconds / (percent / 100.0);
            double left = Math.Max(0, total - elapsed.TotalSeconds);
            return Format(TimeSpan.FromSeconds(left));
        }

        void ShowFinish()
        {
            art.Failure = !success;
            finishPathValue.Text = success ? target : "";
            ShowPage(3);
            UpdateFinishState();
            UpdateFooter();
        }

        void UpdateFinishState()
        {
            // Titre, sous-titre et visuels de la dernière page suivent l'état.
            if (pageTitles[3] == null) return;
            bool failed = !success && failureText.Length > 0;
            pageTitles[3].Text = Strings.Get(failed ? Strings.ErrorTitle : Strings.FinishTitle);
            pageBodies[3].Text = Strings.Get(failed ? Strings.ErrorBody : Strings.FinishBody);
            finishBody.Text = failed ? failureText : "";
            finishBody.Visible = failed;
            launch.Visible = success;
            finishPathLabel.Visible = success;
            finishPathValue.Visible = success;
            finishVersion.Visible = success;
            CenterToggle(launch);
        }

        /// <summary>Centre le libellé et sa case sous le visuel de fin.</summary>
        void CenterToggle(OptionToggle toggle)
        {
            Size measured = TextRenderer.MeasureText(toggle.Text, toggle.Font);
            int width = Math.Min(ContentWidth, 38 + measured.Width);
            toggle.SetBounds((ContentWidth - width) / 2, toggle.Top, width, toggle.Height);
        }

        // ------------------------------------------------------------ fermeture

        void RequestClose()
        {
            if (installing)
            {
                DialogResult answer = MessageBox.Show(this, Strings.Get(Strings.ConfirmCancelBody), Strings.Get(Strings.ConfirmCancelTitle),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return;
                ExitCode = ExitCode == 0 ? 1 : ExitCode;
                confirmedClose = true;
                Close();
                return;
            }
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (installing && !confirmedClose)
            {
                e.Cancel = true;
                RequestClose();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (success && launch.Checked && target != null && target.Length > 0) SetupProgram.Launch(target);
            base.OnFormClosed(e);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Barre de titre sombre, pour rester dans le thème du programme.</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            }
            catch { }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (installing) RequestClose();
                else if (page == 1) ShowPage(0);
                else if (page == 0) RequestClose();
                else if (page == 3) Advance();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(26, 28, 38)))
                e.Graphics.DrawLine(pen, ContentLeft, 480, ContentLeft + ContentWidth, 480);
        }
    }

    /// <summary>Menu déroulant sombre, assorti au reste de l'installateur.</summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.Text;
            base.OnRenderItemText(e);
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(20, 21, 29); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(20, 21, 29); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(20, 21, 29); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(20, 21, 29); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(48, 42, 74); } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuBorder { get { return Color.FromArgb(44, 46, 60); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(48, 42, 74); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(48, 42, 74); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(34, 36, 48); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(34, 36, 48); } }
    }
}
