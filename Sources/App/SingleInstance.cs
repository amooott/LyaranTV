using System;
using System.Net;
using System.Threading;
using System.Windows;

namespace LyaranTV
{
    /// <summary>
    /// Garanties de démarrage : une seule instance (deux copies ouvertes se
    /// disputent les fichiers de cache et de réglages), et un TLS moderne avant
    /// la première requête — .NET Framework laisse sinon TLS 1.0 par défaut sur
    /// les Windows anciens, ce qui fait échouer tous les serveurs HTTPS récents
    /// (GitHub, TMDb, OpenSubtitles).
    /// </summary>
    internal static class SingleInstance
    {
        const string MutexName = @"Local\LyaranTV.SingleInstance";
        const string EventName = @"Local\LyaranTV.Activate";

        static Mutex mutex;
        static EventWaitHandle activation;
        static RegisteredWaitHandle registration;

        public static void PrepareNetwork()
        {
            try
            {
                // TLS 1.2 suffit à tous les serveurs utilisés et reste négociable
                // par .NET Framework. Le drapeau TLS 1.3 (12288) n'existe pas dans
                // cette version du framework : ajouté à l'énumération, il rendait
                // toute connexion HTTPS interne muette jusqu'au délai d'attente —
                // les affiches ne se téléchargeaient donc jamais.
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 16;
            }
            catch { }
        }

        /// <summary>
        /// Renvoie true si cette instance est la première. Sinon, elle demande à
        /// la fenêtre existante de revenir au premier plan et renvoie false.
        /// </summary>
        public static bool Claim(Action onActivate)
        {
            // Les captures de contrôle peuvent tourner à côté de l'application
            // installée, dans un dossier de données séparé.
            if (Environment.GetEnvironmentVariable("LYARANTV_ALLOW_MULTI") == "1") return true;
            try
            {
                bool created;
                mutex = new Mutex(true, MutexName, out created);
                if (!created)
                {
                    try
                    {
                        EventWaitHandle.OpenExisting(EventName).Set();
                    }
                    catch { }
                    try { mutex.Dispose(); } catch { }
                    mutex = null;
                    return false;
                }

                activation = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                if (onActivate != null)
                {
                    registration = ThreadPool.RegisterWaitForSingleObject(activation,
                        delegate
                        {
                            try { Application.Current.Dispatcher.BeginInvoke(new Action(onActivate)); }
                            catch { }
                        }, null, Timeout.Infinite, false);
                }
                return true;
            }
            catch
            {
                // Pas de verrou disponible (session restreinte) : on laisse
                // démarrer plutôt que d'empêcher l'utilisation du programme.
                return true;
            }
        }

        public static void Release()
        {
            try { if (registration != null) { registration.Unregister(null); registration = null; } } catch { }
            try { if (activation != null) { activation.Close(); activation = null; } } catch { }
            try { if (mutex != null) { mutex.ReleaseMutex(); mutex.Dispose(); mutex = null; } } catch { }
        }
    }
}
