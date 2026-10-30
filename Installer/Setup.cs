using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

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
        public const string Version = "1.0.0";
        public const string Publisher = "Amooott";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LyaranTV";

        public const string PayloadResource = "payload.zip";
        const string DotNet480Url = "https://go.microsoft.com/fwlink/?linkid=2088631";

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
                // /DIR permet une installation de test ou sur un autre disque.
                string custom = Value(args, "/DIR");
                return Install(String.IsNullOrWhiteSpace(custom) ? SettingsPath() : custom, true, update, false, null);
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

        static string SettingsPath()
        {
            return InstallDir;
        }

        public static int Install(string target, bool createShortcut, bool update, bool launch, Action<int, string> progress)
        {
            try
            {
                Directory.CreateDirectory(target);
                WaitForRunningInstance(update);

                string staging = Path.Combine(Path.GetTempPath(), "LyaranTV-setup-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                string archive = Path.Combine(staging, "payload.zip");

                if (progress != null) progress(5, Strings.Get(Strings.StatusUnpacking));
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
                {
                    if (resource == null) throw new InvalidOperationException("Charge utile absente de l'installateur.");
                    using (var file = File.Create(archive)) resource.CopyTo(file);
                }

                if (progress != null) progress(15, Strings.Get(Strings.StatusExtracting));
                string extracted = Path.Combine(staging, "files");
                ZipFile.ExtractToDirectory(archive, extracted);
                try { File.Delete(archive); } catch { }

                if (progress != null) progress(25, Strings.Get(Strings.StatusCopying));
                CopyTree(extracted, target, progress);

                if (progress != null) progress(90, Strings.Get(Strings.StatusShortcut));
                if (createShortcut) CreateShortcut(target);
                WriteUninstallEntry(target);

                if (progress != null) progress(96, Strings.Get(Strings.StatusCleanup));
                try { Directory.Delete(staging, true); } catch { }

                if (progress != null) progress(100, Strings.Get(Strings.StatusDone));
                if (launch) Launch(target);
                return 0;
            }
            catch (Exception ex)
            {
                Log("Install: " + ex);
                if (progress != null) progress(-1, ex.Message);
                // En mode silencieux (mise à jour automatique), aucune fenêtre ne
                // doit s'ouvrir : le code de sortie et le journal suffisent.
                else if (Environment.UserInteractive && !IsSilent) MessageBox.Show(ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static void WaitForRunningInstance(bool update)
        {
            // Une mise à jour ne doit pas échouer parce que le programme tourne :
            // on lui laisse quelques secondes pour se fermer, puis on l'arrête.
            Process[] running = Process.GetProcessesByName(ProductName);
            if (running.Length == 0) return;
            DateTime limit = DateTime.Now.AddSeconds(update ? 20 : 8);
            while (DateTime.Now < limit)
            {
                bool alive = false;
                foreach (Process process in running)
                {
                    try { if (!process.HasExited) alive = true; } catch { }
                }
                if (!alive) return;
                System.Threading.Thread.Sleep(500);
            }
            foreach (Process process in running)
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(700);
        }

        static void CopyTree(string source, string target, Action<int, string> progress)
        {
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(source.Length).TrimStart('\\');
                string destination = Path.Combine(target, relative);
                string folder = Path.GetDirectoryName(destination);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.Copy(files[i], destination, true);
                if (progress != null && i % 25 == 0)
                {
                    int percent = 25 + (int)(60.0 * i / Math.Max(1, files.Length));
                    progress(percent, Strings.Get(Strings.StatusCopying));
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
            catch { }
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
        public const string Folder = "Folder";
        public const string Shortcut = "Shortcut";
        public const string LaunchAfter = "LaunchAfter";
        public const string Install = "Install";
        public const string Cancel = "Cancel";
        public const string Close = "Close";
        public const string DataNote = "DataNote";
        public const string StatusUnpacking = "StatusUnpacking";
        public const string StatusExtracting = "StatusExtracting";
        public const string StatusCopying = "StatusCopying";
        public const string StatusShortcut = "StatusShortcut";
        public const string StatusCleanup = "StatusCleanup";
        public const string StatusDone = "StatusDone";
        public const string StatusUninstalled = "StatusUninstalled";
        public const string NotifyDotNetMissing = "NotifyDotNetMissing";
        public const string InfoDotNetLaunched = "InfoDotNetLaunched";
        public const string ErrorDotNet = "ErrorDotNet";
        public const string Language = "Language";
        public const string AlreadyInstalled = "AlreadyInstalled";

        public static string Current = Detect();

        public static string Detect()
        {
            string two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (two == "fr" || two == "es" || two == "ru" || two == "pt") return two;
            return "en";
        }

        public static string Get(string key)
        {
            Dictionary<string, string> table = Map(Current);
            string value;
            return table.TryGetValue(key, out value) ? value : Map("en")[key];
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
                    d[Subtitle] = "Lecteur IPTV et vidéo pour Windows.";
                    d[Folder] = "Dossier d'installation";
                    d[Shortcut] = "Créer un raccourci sur le Bureau";
                    d[LaunchAfter] = "Lancer LyaranTV après l'installation";
                    d[Install] = "Installer";
                    d[Cancel] = "Annuler";
                    d[Close] = "Terminer";
                    d[DataNote] = "Vos comptes, playlists, affiches et positions déjà présents dans %AppData%\\LyaranTV sont conservés : l'installation ne les modifie pas.";
                    d[StatusUnpacking] = "Préparation des fichiers…";
                    d[StatusExtracting] = "Extraction de l'application et du lecteur vidéo…";
                    d[StatusCopying] = "Copie des fichiers…";
                    d[StatusShortcut] = "Création du raccourci…";
                    d[StatusCleanup] = "Nettoyage…";
                    d[StatusDone] = "LyaranTV est installé.";
                    d[StatusUninstalled] = "LyaranTV a été désinstallé. Vos données personnelles ont été conservées dans :";
                    d[NotifyDotNetMissing] = "LyaranTV nécessite .NET Framework 4.7.2 ou plus récent, absent de cet ordinateur.\n\nVoulez-vous le télécharger et l'installer maintenant ? Windows demandera l'autorisation.";
                    d[InfoDotNetLaunched] = "L'installation de .NET Framework a démarré. Relancez l'installateur une fois celle-ci terminée.";
                    d[ErrorDotNet] = "Impossible de télécharger .NET Framework. Installez-le depuis le site de Microsoft, puis relancez cet installateur.";
                    d[Language] = "Langue";
                    d[AlreadyInstalled] = "Une version est déjà installée : elle sera remplacée et vos données conservées.";
                    return d;
                case "es":
                    d[Title] = "Instalación de LyaranTV";
                    d[Subtitle] = "Reproductor IPTV y de vídeo para Windows.";
                    d[Folder] = "Carpeta de instalación";
                    d[Shortcut] = "Crear un acceso directo en el escritorio";
                    d[LaunchAfter] = "Iniciar LyaranTV al terminar";
                    d[Install] = "Instalar";
                    d[Cancel] = "Cancelar";
                    d[Close] = "Finalizar";
                    d[DataNote] = "Tus cuentas, listas, carátulas y posiciones que ya están en %AppData%\\LyaranTV se conservan: la instalación no las modifica.";
                    d[StatusUnpacking] = "Preparando los archivos…";
                    d[StatusExtracting] = "Extrayendo la aplicación y el reproductor…";
                    d[StatusCopying] = "Copiando archivos…";
                    d[StatusShortcut] = "Creando el acceso directo…";
                    d[StatusCleanup] = "Limpiando…";
                    d[StatusDone] = "LyaranTV está instalado.";
                    d[StatusUninstalled] = "LyaranTV se ha desinstalado. Tus datos personales se han conservado en:";
                    d[NotifyDotNetMissing] = "LyaranTV necesita .NET Framework 4.7.2 o superior, que no está en este equipo.\n\n¿Quieres descargarlo e instalarlo ahora? Windows pedirá autorización.";
                    d[InfoDotNetLaunched] = "La instalación de .NET Framework ha comenzado. Vuelve a ejecutar este instalador cuando termine.";
                    d[ErrorDotNet] = "No se ha podido descargar .NET Framework. Instálalo desde el sitio de Microsoft y vuelve a ejecutar el instalador.";
                    d[Language] = "Idioma";
                    d[AlreadyInstalled] = "Ya hay una versión instalada: se reemplazará y tus datos se conservarán.";
                    return d;
                case "ru":
                    d[Title] = "Установка LyaranTV";
                    d[Subtitle] = "Плеер IPTV и видео для Windows.";
                    d[Folder] = "Папка установки";
                    d[Shortcut] = "Создать ярлык на рабочем столе";
                    d[LaunchAfter] = "Запустить LyaranTV после установки";
                    d[Install] = "Установить";
                    d[Cancel] = "Отмена";
                    d[Close] = "Готово";
                    d[DataNote] = "Ваши учётные записи, плейлисты, обложки и позиции в %AppData%\\LyaranTV сохраняются: установка их не меняет.";
                    d[StatusUnpacking] = "Подготовка файлов…";
                    d[StatusExtracting] = "Распаковка программы и видеоплеера…";
                    d[StatusCopying] = "Копирование файлов…";
                    d[StatusShortcut] = "Создание ярлыка…";
                    d[StatusCleanup] = "Очистка…";
                    d[StatusDone] = "LyaranTV установлен.";
                    d[StatusUninstalled] = "LyaranTV удалён. Ваши данные сохранены в:";
                    d[NotifyDotNetMissing] = "Для LyaranTV требуется .NET Framework 4.7.2 или новее, которого нет на этом компьютере.\n\nСкачать и установить его сейчас? Windows запросит разрешение.";
                    d[InfoDotNetLaunched] = "Установка .NET Framework запущена. После её завершения запустите этот установщик снова.";
                    d[ErrorDotNet] = "Не удалось скачать .NET Framework. Установите его с сайта Microsoft и запустите установщик снова.";
                    d[Language] = "Язык";
                    d[AlreadyInstalled] = "Программа уже установлена: версия будет заменена, данные сохранятся.";
                    return d;
                case "pt":
                    d[Title] = "Instalação do LyaranTV";
                    d[Subtitle] = "Leitor IPTV e de vídeo para Windows.";
                    d[Folder] = "Pasta de instalação";
                    d[Shortcut] = "Criar um atalho no ambiente de trabalho";
                    d[LaunchAfter] = "Iniciar o LyaranTV no fim da instalação";
                    d[Install] = "Instalar";
                    d[Cancel] = "Cancelar";
                    d[Close] = "Concluir";
                    d[DataNote] = "As suas contas, listas, capas e posições em %AppData%\\LyaranTV são preservadas: a instalação não as altera.";
                    d[StatusUnpacking] = "A preparar os ficheiros…";
                    d[StatusExtracting] = "A extrair a aplicação e o leitor de vídeo…";
                    d[StatusCopying] = "A copiar ficheiros…";
                    d[StatusShortcut] = "A criar o atalho…";
                    d[StatusCleanup] = "A limpar…";
                    d[StatusDone] = "O LyaranTV está instalado.";
                    d[StatusUninstalled] = "O LyaranTV foi desinstalado. Os seus dados foram preservados em:";
                    d[NotifyDotNetMissing] = "O LyaranTV requer o .NET Framework 4.7.2 ou superior, que não está neste computador.\n\nPretende transferi-lo e instalá-lo agora? O Windows pedirá autorização.";
                    d[InfoDotNetLaunched] = "A instalação do .NET Framework foi iniciada. Volte a executar este instalador quando terminar.";
                    d[ErrorDotNet] = "Não foi possível transferir o .NET Framework. Instale-o a partir do site da Microsoft e volte a executar o instalador.";
                    d[Language] = "Idioma";
                    d[AlreadyInstalled] = "Já existe uma versão instalada: será substituída e os seus dados serão preservados.";
                    return d;
                default:
                    d[Title] = "LyaranTV setup";
                    d[Subtitle] = "IPTV and video player for Windows.";
                    d[Folder] = "Installation folder";
                    d[Shortcut] = "Create a desktop shortcut";
                    d[LaunchAfter] = "Launch LyaranTV when finished";
                    d[Install] = "Install";
                    d[Cancel] = "Cancel";
                    d[Close] = "Finish";
                    d[DataNote] = "Your accounts, playlists, artwork and positions already in %AppData%\\LyaranTV are kept: this setup never modifies them.";
                    d[StatusUnpacking] = "Preparing files…";
                    d[StatusExtracting] = "Extracting the app and video engine…";
                    d[StatusCopying] = "Copying files…";
                    d[StatusShortcut] = "Creating the shortcut…";
                    d[StatusCleanup] = "Cleaning up…";
                    d[StatusDone] = "LyaranTV is installed.";
                    d[StatusUninstalled] = "LyaranTV has been uninstalled. Your personal data was kept in:";
                    d[NotifyDotNetMissing] = "LyaranTV needs .NET Framework 4.7.2 or later, which is missing from this computer.\n\nDownload and install it now? Windows will ask for permission.";
                    d[InfoDotNetLaunched] = "The .NET Framework installation has started. Run this setup again when it finishes.";
                    d[ErrorDotNet] = "Could not download .NET Framework. Install it from Microsoft's website, then run this setup again.";
                    d[Language] = "Language";
                    d[AlreadyInstalled] = "A version is already installed: it will be replaced and your data kept.";
                    return d;
            }
        }
    }

    internal sealed class SetupForm : Form
    {
        readonly bool update;
        readonly Label title = new Label();
        readonly Label subtitle = new Label();
        readonly Label folderLabel = new Label();
        readonly TextBox folder = new TextBox();
        readonly CheckBox shortcut = new CheckBox();
        readonly CheckBox launch = new CheckBox();
        readonly Label dataNote = new Label();
        readonly Label status = new Label();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button primary = new Button();
        readonly Button secondary = new Button();
        readonly Button languageButton = new Button();
        readonly ContextMenuStrip languageMenu = new ContextMenuStrip();
        readonly Label languageLabel = new Label();
        bool installing;
        bool finished;

        public int ExitCode;

        public SetupForm(bool update)
        {
            this.update = update;
            Text = Strings.Get(Strings.Title);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 430);
            BackColor = Color.FromArgb(16, 16, 22);
            ForeColor = Color.FromArgb(238, 238, 244);
            Font = new Font("Segoe UI", 9.5f);

            title.Font = new Font("Segoe UI Semibold", 18f);
            title.ForeColor = Color.White;
            title.SetBounds(24, 20, 420, 34);
            Controls.Add(title);

            subtitle.ForeColor = Color.FromArgb(160, 160, 178);
            subtitle.SetBounds(24, 54, 420, 22);
            Controls.Add(subtitle);

            languageLabel.ForeColor = Color.FromArgb(160, 160, 178);
            languageLabel.SetBounds(24, 92, 90, 22);
            Controls.Add(languageLabel);

            // Sélecteur de langue dessiné dans le thème sombre de l'installateur :
            // un menu déroulant natif resterait blanc et jurerait avec le reste.
            languageButton.FlatStyle = FlatStyle.Flat;
            languageButton.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 84);
            languageButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 44, 60);
            languageButton.BackColor = Color.FromArgb(28, 28, 38);
            languageButton.ForeColor = Color.White;
            languageButton.TextAlign = ContentAlignment.MiddleLeft;
            languageButton.SetBounds(110, 88, 200, 30);
            languageButton.Click += delegate
            {
                languageMenu.Show(languageButton, new Point(0, languageButton.Height));
            };
            Controls.Add(languageButton);

            languageMenu.Renderer = new DarkMenuRenderer();
            languageMenu.BackColor = Color.FromArgb(24, 24, 32);
            languageMenu.ForeColor = Color.FromArgb(238, 238, 244);
            languageMenu.ShowImageMargin = false;
            string[] codes = { "fr", "en", "es", "ru", "pt" };
            foreach (string code in codes)
            {
                string target = code;
                var item = new ToolStripMenuItem(Strings.LanguageName(code));
                item.ForeColor = Color.FromArgb(238, 238, 244);
                item.Checked = code == Strings.Current;
                item.Click += delegate
                {
                    Strings.Current = target;
                    foreach (ToolStripMenuItem other in languageMenu.Items) other.Checked = other.Text == Strings.LanguageName(target);
                    ApplyTexts();
                };
                languageMenu.Items.Add(item);
            }

            folderLabel.SetBounds(24, 136, 240, 22);
            Controls.Add(folderLabel);

            folder.SetBounds(24, 160, 380, 28);
            folder.BackColor = Color.FromArgb(28, 28, 38);
            folder.ForeColor = Color.White;
            folder.BorderStyle = BorderStyle.FixedSingle;
            folder.Text = SetupProgram.InstallDir;
            Controls.Add(folder);

            var browse = new Button();
            browse.Text = "…";
            browse.SetBounds(412, 159, 36, 29);
            browse.FlatStyle = FlatStyle.Flat;
            browse.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 78);
            browse.BackColor = Color.FromArgb(32, 32, 44);
            browse.ForeColor = Color.White;
            browse.Click += delegate
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    if (Directory.Exists(folder.Text)) dialog.SelectedPath = folder.Text;
                    if (dialog.ShowDialog() == DialogResult.OK) folder.Text = dialog.SelectedPath;
                }
            };
            Controls.Add(browse);

            shortcut.SetBounds(24, 202, 420, 24);
            shortcut.Checked = true;
            Controls.Add(shortcut);

            launch.SetBounds(24, 228, 420, 24);
            launch.Checked = true;
            Controls.Add(launch);

            dataNote.ForeColor = Color.FromArgb(150, 150, 168);
            dataNote.SetBounds(24, 262, 500, 54);
            Controls.Add(dataNote);

            status.ForeColor = Color.FromArgb(190, 190, 205);
            status.SetBounds(24, 322, 500, 22);
            Controls.Add(status);

            progress.SetBounds(24, 346, 500, 8);
            progress.Style = ProgressBarStyle.Continuous;
            Controls.Add(progress);

            secondary.SetBounds(348, 374, 100, 34);
            secondary.FlatStyle = FlatStyle.Flat;
            secondary.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 78);
            secondary.BackColor = Color.FromArgb(32, 32, 44);
            secondary.ForeColor = Color.White;
            secondary.Click += delegate
            {
                if (installing) return;
                if (progress.Value >= 100) { Close(); return; }
                Close();
            };
            Controls.Add(secondary);

            primary.SetBounds(456, 374, 100, 34);
            primary.FlatStyle = FlatStyle.Flat;
            primary.FlatAppearance.BorderSize = 0;
            primary.BackColor = Color.FromArgb(139, 92, 246);
            primary.ForeColor = Color.White;
            primary.Font = new Font("Segoe UI Semibold", 9.5f);
            primary.Click += delegate
            {
                if (finished) { Close(); return; }
                BeginInstall();
            };
            Controls.Add(primary);

            ApplyTexts();
        }

        void ApplyTexts()
        {
            title.Text = Strings.Get(Strings.Title);
            Text = Strings.Get(Strings.Title);
            subtitle.Text = Strings.Get(Strings.Subtitle);
            languageLabel.Text = Strings.Get(Strings.Language);
            folderLabel.Text = Strings.Get(Strings.Folder);
            shortcut.Text = Strings.Get(Strings.Shortcut);
            launch.Text = Strings.Get(Strings.LaunchAfter);
            dataNote.Text = (update || Directory.Exists(SetupProgram.InstallDir) && File.Exists(Path.Combine(SetupProgram.InstallDir, "LyaranTV.exe")))
                ? Strings.Get(Strings.AlreadyInstalled) + "\n" + Strings.Get(Strings.DataNote)
                : Strings.Get(Strings.DataNote);
            primary.Text = Strings.Get(Strings.Install);
            secondary.Text = progress.Value >= 100 ? Strings.Get(Strings.Close) : Strings.Get(Strings.Cancel);
            languageButton.Text = Strings.LanguageName(Strings.Current) + "    ▾";
        }

        void BeginInstall()
        {
            if (installing) return;
            string target = folder.Text.Trim();
            if (target.Length == 0) return;
            installing = true;
            primary.Enabled = false;
            secondary.Enabled = false;
            languageButton.Enabled = false;
            folder.Enabled = false;
            shortcut.Enabled = false;
            launch.Enabled = false;

            var worker = new System.ComponentModel.BackgroundWorker();
            worker.DoWork += delegate(object sender, System.ComponentModel.DoWorkEventArgs e)
            {
                int result = SetupProgram.Install(target, shortcut.Checked, update, launch.Checked,
                    delegate(int percent, string message)
                    {
                        try
                        {
                            BeginInvoke(new Action(delegate
                            {
                                if (percent >= 0) progress.Value = Math.Max(0, Math.Min(100, percent));
                                status.Text = message;
                                if (percent >= 100)
                                {
                                    primary.Text = Strings.Get(Strings.Close);
                                    primary.Enabled = true;
                                    secondary.Enabled = true;
                                }
                            }));
                        }
                        catch { }
                    });
                e.Result = result;
            };
            worker.RunWorkerCompleted += delegate(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
            {
                installing = false;
                ExitCode = e.Result is int ? (int)e.Result : 1;
                if (ExitCode == 0)
                {
                    finished = true;
                    status.Text = Strings.Get(Strings.StatusDone);
                    secondary.Enabled = true;
                    secondary.Text = Strings.Get(Strings.Close);
                    primary.Enabled = true;
                    primary.Text = Strings.Get(Strings.Close);
                }
                else
                {
                    primary.Enabled = true;
                    secondary.Enabled = true;
                }
            };            worker.RunWorkerAsync();
        }
    }

    /// <summary>Menu déroulant sombre, assorti au reste de l'installateur.</summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(238, 238, 244);
            base.OnRenderItemText(e);
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 24, 32); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 24, 32); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 24, 32); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 24, 32); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(56, 48, 86); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(139, 92, 246); } }
        public override Color MenuBorder { get { return Color.FromArgb(70, 70, 92); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(56, 48, 86); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(56, 48, 86); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(40, 40, 54); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(40, 40, 54); } }
    }
}
