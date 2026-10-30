using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Moteur de lecture du lecteur intégré, avec deux implémentations :
    /// MediaElement (Windows Media Foundation) et libVLC. Windows ne décode pas
    /// l'AC-3, l'E-AC-3, le DTS ni le TrueHD : les films et épisodes qui les
    /// utilisent sortaient sans son. libVLC décode tout et sait changer de piste
    /// audio ; MediaElement reste le repli quand VLC n'est pas installé.
    ///
    /// L'objet se présente comme une surface WPF : il contient l'élément média et
    /// l'image qui reçoit les trames VLC, si bien que les contrôles, les
    /// sous-titres et les animations restent au-dessus de la vidéo.
    /// </summary>
    internal sealed class PlaybackEngine : Grid
    {
        readonly MediaElement element;
        readonly Image surface;
        VlcPlayback vlc;
        WriteableBitmap bitmap;
        DispatcherTimer stateTimer;
        DispatcherTimer frameTimer;

        Uri source;
        bool usingVlc;
        bool opened;
        bool endedRaised;
        bool bufferingRaised;
        bool switchingEngine;
        bool closed;
        bool loggedOpen;
        bool triedBothEngines;
        bool audioLanguageApplied;
        int audioRepairAttempts;
        DateTime lastAudioRepair = DateTime.MinValue;
        DateTime openedAt = DateTime.MinValue;
        DateTime startedAt = DateTime.MinValue;
        double volume = 1;
        double speed = 1;
        Stretch stretch = System.Windows.Media.Stretch.Uniform;

        public PlaybackEngine()
        {
            Background = Brushes.Transparent;
            element = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                ScrubbingEnabled = true,
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Visible
            };
            element.MediaOpened += delegate { RaiseOpened(); };
            element.MediaFailed += delegate(object sender, ExceptionRoutedEventArgs e) { OnMediaFailed(e == null ? null : e.ErrorException); };
            element.MediaEnded += delegate { RaiseEnded(); };
            element.BufferingStarted += delegate { OnBuffering(true); };
            element.BufferingEnded += delegate { OnBuffering(false); };
            Children.Add(element);

            surface = new Image
            {
                Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                SnapsToDevicePixels = true
            };
            Children.Add(surface);
        }

        // ------------------------------------------------------------ événements

        public event Action MediaOpened;
        public event Action<Exception> MediaFailed;
        public event Action MediaEnded;
        public event Action BufferingStarted;
        public event Action BufferingEnded;

        void RaiseOpened()
        {
            if (opened) return;
            opened = true;
            openedAt = DateTime.Now;
            if (usingVlc && vlc != null) vlc.ApplyPendingSeek();
            Action handler = MediaOpened;
            if (handler != null) handler();
        }

        void RaiseEnded()
        {
            if (endedRaised) return;
            endedRaised = true;
            Action handler = MediaEnded;
            if (handler != null) handler();
        }

        void OnBuffering(bool active)
        {
            if (active)
            {
                if (bufferingRaised) return;
                bufferingRaised = true;
                Action handler = BufferingStarted;
                if (handler != null) handler();
            }
            else
            {
                bufferingRaised = false;
                Action handler = BufferingEnded;
                if (handler != null) handler();
            }
        }

        void OnMediaFailed(Exception error)
        {
            if (switchingEngine) return;
            // Deuxième chance avec l'autre moteur : Windows refuse certains MKV,
            // VLC refuse parfois un flux que Windows lit très bien. Une seule
            // bascule par lecture, pour ne pas osciller entre les deux.
            bool preferVlcRemaining = !usingVlc && VlcRuntime.Available;
            bool preferWindowsRemaining = usingVlc && EngineMode != "vlc";
            if (source != null && !triedBothEngines && (preferVlcRemaining || preferWindowsRemaining))
            {
                switchingEngine = true;
                triedBothEngines = true;
                try
                {
                    HttpUtil.AppendLog("Lecteur : bascule vers " + (preferVlcRemaining ? "VLC" : "Media Foundation") + " après échec du premier moteur.");
                }
                catch { }
                bool ok = preferVlcRemaining ? StartVlc(source) : StartMediaElement(source);
                switchingEngine = false;
                if (ok) return;
            }
            Action<Exception> handler = MediaFailed;
            if (handler != null) handler(error);
        }

        // -------------------------------------------------------------- réglages

        /// <summary>« auto », « vlc » ou « windows » : moteur demandé par les réglages.</summary>
        string EngineMode
        {
            get
            {
                try
                {
                    AppSettings settings = SettingsStore.Load();
                    string mode = settings == null ? "" : (settings.PlaybackEngine ?? "");
                    if (mode == "vlc" || mode == "windows") return mode;
                }
                catch { }
                return "auto";
            }
        }

        public bool UsingVlc { get { return usingVlc; } }

        /// <summary>Vrai quand le flux contient une image ; faux pour un fichier audio seul.</summary>
        public bool HasVideo
        {
            get
            {
                if (usingVlc && vlc != null) return vlc.HasVideo;
                try { return element.NaturalVideoWidth > 0 && element.NaturalVideoHeight > 0; }
                catch { return false; }
            }
        }

        /// <summary>Le flux a du son mais VLC n'a pas pu ouvrir de sortie audio.</summary>
        public bool AudioOutputMissing
        {
            get { return usingVlc && vlc != null && vlc.AudioOutputMissing; }
        }

        /// <summary>
        /// Relance la sortie audio sans interrompre la lecture : c'est le recours
        /// quand un film sort muet parce que la carte refuse le format du flux.
        /// </summary>
        public bool RepairAudio()
        {
            if (!usingVlc || vlc == null) return false;
            if (!vlc.AudioOutputMissing)
            {
                try { vlc.Volume = volume; } catch { }
                return true;
            }
            try { HttpUtil.AppendLog("Son : relance demandée."); } catch { }
            string used = vlc.TryRecoverAudioOutput();
            if (String.IsNullOrWhiteSpace(used)) return false;
            audioRepairAttempts = 0;
            try { vlc.Volume = volume; } catch { }
            try { HttpUtil.AppendLog("Son : sortie audio relancée sur « " + used + " »."); } catch { }
            return !vlc.AudioOutputMissing;
        }

        public string EngineLabel
        {
            get
            {
                if (usingVlc && vlc != null) return "VLC " + vlc.VideoWidth + "×" + vlc.VideoHeight;
                return "Media Foundation";
            }
        }

        public double Volume
        {
            get { return volume; }
            set
            {
                volume = Math.Max(0, Math.Min(1, value));
                try { element.Volume = volume; } catch { }
                if (vlc != null) { try { vlc.Volume = volume; } catch { } }
            }
        }

        public double SpeedRatio
        {
            get { return speed; }
            set
            {
                speed = value <= 0 ? 1 : value;
                try { element.SpeedRatio = speed; } catch { }
                if (vlc != null) { try { vlc.Rate = speed; } catch { } }
            }
        }

        public Stretch Stretch
        {
            get { return stretch; }
            set
            {
                stretch = value;
                element.Stretch = value;
                surface.Stretch = value;
            }
        }

        public int NaturalVideoWidth
        {
            get
            {
                if (usingVlc && vlc != null) return vlc.VideoWidth;
                return element.NaturalVideoWidth;
            }
        }

        public int NaturalVideoHeight
        {
            get
            {
                if (usingVlc && vlc != null) return vlc.VideoHeight;
                return element.NaturalVideoHeight;
            }
        }

        public double BufferingProgress
        {
            get
            {
                if (usingVlc && vlc != null) return vlc.State == 2 ? 0.5 : 1;
                return element.BufferingProgress;
            }
        }

        public double DownloadProgress
        {
            get
            {
                if (usingVlc && vlc != null) return vlc.State == 2 ? 0.5 : 1;
                return element.DownloadProgress;
            }
        }

        public Duration NaturalDuration
        {
            get
            {
                if (usingVlc && vlc != null)
                {
                    long length = vlc.LengthMs;
                    return length > 0 ? new Duration(TimeSpan.FromMilliseconds(length)) : Duration.Automatic;
                }
                return element.NaturalDuration;
            }
        }

        public TimeSpan Position
        {
            get
            {
                if (usingVlc && vlc != null)
                {
                    long ms = vlc.TimeMs;
                    return ms > 0 ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
                }
                return element.Position;
            }
            set
            {
                if (usingVlc && vlc != null)
                {
                    try { vlc.SeekTo(value.TotalSeconds); } catch { }
                    return;
                }
                try { element.Position = value; } catch { }
            }
        }

        // ---------------------------------------------------------------- ouverture

        /// <summary>Ouvre le média et démarre la lecture avec le meilleur moteur.</summary>
        public void Open(Uri media)
        {
            if (media == null) return;
            StopEngines(true);
            source = media;
            opened = false;
            endedRaised = false;
            bufferingRaised = false;
            loggedOpen = false;
            triedBothEngines = false;
            audioLanguageApplied = false;
            audioRepairAttempts = 0;
            lastAudioRepair = DateTime.MinValue;
            openedAt = DateTime.MinValue;
            startedAt = DateTime.Now;
            closed = false;

            string mode = EngineMode;
            bool preferVlc = mode == "vlc" || (mode == "auto" && VlcRuntime.Available);
            if (preferVlc && StartVlc(media)) return;
            if (mode == "vlc" && !VlcRuntime.Available)
            {
                try { HttpUtil.AppendLog("VLC demandé mais indisponible : " + VlcRuntime.Failure); } catch { }
            }
            StartMediaElement(media);
        }

        bool StartVlc(Uri media)
        {
            try { if (vlc == null) vlc = new VlcPlayback(); }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("VLC indisponible : " + ex.GetBaseException().Message); } catch { }
                return false;
            }
            try
            {
                element.Stop();
                element.Source = null;
                element.Visibility = Visibility.Collapsed;
                surface.Visibility = Visibility.Visible;
                usingVlc = true;
                if (!vlc.Open(media.AbsoluteUri, null, null, null))
                {
                    usingVlc = false;
                    surface.Visibility = Visibility.Collapsed;
                    element.Visibility = Visibility.Visible;
                    return false;
                }
                vlc.Volume = volume;
                vlc.Rate = speed;
                EnsureTimers();
                stateTimer.Start();
                frameTimer.Start();
                return true;
            }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("VLC n'a pas pu ouvrir « " + media.AbsoluteUri + " » : " + ex.GetBaseException().Message); } catch { }
                usingVlc = false;
                surface.Visibility = Visibility.Collapsed;
                element.Visibility = Visibility.Visible;
                return false;
            }
        }

        bool StartMediaElement(Uri media)
        {
            try
            {
                if (vlc != null) { try { vlc.Stop(); } catch { } }
                StopTimers();
                usingVlc = false;
                surface.Visibility = Visibility.Collapsed;
                surface.Source = null;
                element.Visibility = Visibility.Visible;
                element.Stop();
                element.Source = media;
                try { element.Volume = volume; element.SpeedRatio = speed; element.Stretch = stretch; } catch { }
                element.Play();
                return true;
            }
            catch (Exception ex)
            {
                try { HttpUtil.AppendLog("MediaElement n'a pas pu ouvrir « " + media.AbsoluteUri + " » : " + ex.GetBaseException().Message); } catch { }
                return false;
            }
        }

        // ------------------------------------------------------------------ dédié

        /// <summary>Pistes audio réelles du flux (uniquement avec le moteur VLC).</summary>
        public List<KeyValuePair<int, string>> AudioTracks()
        {
            if (!usingVlc || vlc == null) return new List<KeyValuePair<int, string>>();
            try { return vlc.AudioTracks(); }
            catch { return new List<KeyValuePair<int, string>>(); }
        }

        /// <summary>Pistes audio enrichies (langue, codec, canaux, débit).</summary>
        public List<AudioStream> AudioStreams()
        {
            if (!usingVlc || vlc == null) return new List<AudioStream>();
            try { return vlc.AudioStreams(); }
            catch { return new List<AudioStream>(); }
        }

        public int AudioTrackCount
        {
            get { return usingVlc && vlc != null ? vlc.AudioStreams().Count : 0; }
        }

        public int CurrentAudioTrack
        {
            get { return usingVlc && vlc != null ? vlc.CurrentAudioTrack : -1; }
        }

        public bool SelectAudioTrack(int identifier)
        {
            if (!usingVlc || vlc == null) return false;
            try { return vlc.SelectAudioTrack(identifier); } catch { return false; }
        }

        /// <summary>Pistes de sous-titres intégrées au flux (moteur VLC uniquement).</summary>
        public List<SubtitleStream> SubtitleStreams()
        {
            if (!usingVlc || vlc == null) return new List<SubtitleStream>();
            try { return vlc.SubtitleStreams(); }
            catch { return new List<SubtitleStream>(); }
        }

        public int CurrentSubtitleTrack
        {
            get { return usingVlc && vlc != null ? vlc.CurrentSubtitle : -1; }
        }

        public bool SelectSubtitleTrack(int identifier)
        {
            if (!usingVlc || vlc == null) return false;
            try { return vlc.SelectSubtitle(identifier); } catch { return false; }
        }

        public void DisableSubtitles()
        {
            if (!usingVlc || vlc == null) return;
            try { vlc.DisableSubtitles(); } catch { }
        }

        // --------------------------------------------------------------- commandes

        public void Play()
        {
            if (usingVlc && vlc != null) { try { vlc.Play(); } catch { } return; }
            try { element.Play(); } catch { }
        }

        public void Pause()
        {
            if (usingVlc && vlc != null) { try { vlc.Pause(); } catch { } return; }
            try { element.Pause(); } catch { }
        }

        public void Stop()
        {
            StopEngines(false);
        }

        public void Close()
        {
            closed = true;
            StopEngines(true);
            try { element.Close(); } catch { }
        }

        void StopEngines(bool release)
        {
            StopTimers();
            try { element.Stop(); } catch { }
            if (vlc != null)
            {
                try { vlc.Stop(); } catch { }
            }
            if (release)
            {
                opened = false;
                usingVlc = false;
                surface.Visibility = Visibility.Collapsed;
                surface.Source = null;
                bitmap = null;
                element.Visibility = Visibility.Visible;
                if (vlc != null) { try { vlc.Dispose(); } catch { } vlc = null; }
                if (closed) { try { element.Source = null; } catch { } }
            }
        }

        // ----------------------------------------------------------- surveillance

        void EnsureTimers()
        {
            if (stateTimer == null)
            {
                stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
                stateTimer.Tick += delegate { PollVlc(); };
            }
            if (frameTimer == null)
            {
                frameTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                frameTimer.Tick += delegate { DrawVlcFrame(); };
            }
        }

        void StopTimers()
        {
            if (stateTimer != null) stateTimer.Stop();
            if (frameTimer != null) frameTimer.Stop();
        }

        void PollVlc()
        {
            if (!usingVlc || vlc == null || closed) return;
            int state = vlc.State;
            switch (state)
            {
                case 7: // Error
                    OnMediaFailed(new Exception("VLC n'a pas pu lire ce flux."));
                    return;
                case 6: // Ended
                    RaiseEnded();
                    return;
                case 2: // Buffering
                    OnBuffering(true);
                    break;
                case 3: // Playing
                case 4: // Paused
                    OnBuffering(false);
                    if (!opened)
                    {
                        LogVlcStart();
                        RaiseOpened();
                        SelectPreferredAudio();
                    }
                    else vlc.ApplyPendingSeek();
                    break;
                case 1: // Opening
                    break;
            }
            if (!opened && startedAt != DateTime.MinValue && (DateTime.Now - startedAt).TotalSeconds > 22)
            {
                OnMediaFailed(new Exception("Le flux ne répond pas (délai d'ouverture dépassé)."));
                return;
            }
            if (opened) CheckAudioHealth();
        }

        /// <summary>
        /// Surveille la sortie audio réelle. Un flux dont la piste est décodée mais
        /// dont le périphérique refuse le format joue sans son : on bascule alors
        /// sur une autre sortie (WASAPI → DirectSound → WaveOut).
        /// </summary>
        void CheckAudioHealth()
        {
            if (!usingVlc || vlc == null || closed) return;
            if (audioRepairAttempts >= 2) return;
            if ((DateTime.Now - openedAt).TotalSeconds < 1.5) return;
            if ((DateTime.Now - lastAudioRepair).TotalSeconds < 1.5) return;
            if (!vlc.AudioOutputMissing) return;
            lastAudioRepair = DateTime.Now;
            string used = vlc.TryRecoverAudioOutput();
            if (String.IsNullOrWhiteSpace(used)) return;
            audioRepairAttempts++;
            try { vlc.Volume = volume; } catch { }
            try { HttpUtil.AppendLog("Son : aucune sortie audio au démarrage, bascule sur « " + used + " »."); } catch { }
        }

        /// <summary>Trace le moteur et les pistes réellement décodées, pour le diagnostic.</summary>
        void LogVlcStart()
        {
            if (loggedOpen || vlc == null) return;
            loggedOpen = true;
            try
            {
                List<AudioStream> audio = vlc.AudioStreams();
                string tracks = "";
                foreach (AudioStream stream in audio)
                    tracks += (tracks.Length == 0 ? "" : " | ") + stream.Id + ":" + stream.Label;
                string outputs = "";
                foreach (string name in vlc.AudioOutputs()) outputs += (outputs.Length == 0 ? "" : "/") + name;
                HttpUtil.AppendLog("Lecture VLC · " + audio.Count + " piste(s) audio · "
                    + (vlc.HasVideo ? vlc.VideoWidth + "×" + vlc.VideoHeight : "audio seul") + " · volume " + vlc.Volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + (tracks.Length == 0 ? "" : " · " + tracks)
                    + (vlc.AudioOutputMissing ? " · SORTIE AUDIO INDISPONIBLE" : " · sortie audio active")
                    + (outputs.Length == 0 ? "" : " · sorties " + outputs));
            }
            catch { }
        }

        /// <summary>
        /// Sur un fichier à plusieurs pistes, démarre sur la langue choisie dans les
        /// réglages. VLC reçoit aussi l'option « audio-language » à l'ouverture :
        /// ceci rattrape les cas où le nom des pistes n'est pas normalisé.
        /// </summary>
        void SelectPreferredAudio()
        {
            if (!usingVlc || vlc == null || audioLanguageApplied) return;
            audioLanguageApplied = true;
            string code = "";
            try
            {
                AppSettings settings = SettingsStore.Load();
                code = settings == null ? "" : (settings.PreferredAudioLanguage ?? "");
            }
            catch { }
            code = (code ?? "").Trim().ToLowerInvariant();
            if (code.Length == 0 || code == "auto") return;
            List<AudioStream> audioTracks = vlc.AudioStreams();
            if (audioTracks.Count < 2) return;
            foreach (AudioStream audioTrack in audioTracks)
            {
                if (!audioTrack.MatchesLanguage(code)) continue;
                if (audioTrack.Id == vlc.CurrentAudioTrack) return;
                if (!vlc.SelectAudioTrack(audioTrack.Id)) return;
                try { HttpUtil.AppendLog("Son : piste « " + audioTrack.Label + " » retenue pour la langue préférée (" + code + ")."); } catch { }
                return;
            }
        }

        void DrawVlcFrame()
        {
            if (!usingVlc || vlc == null) return;
            vlc.CopyLatestFrame(delegate(IntPtr data, int width, int height, int pixelsStride)
            {
                try
                {
                    if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                    {
                        bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                        surface.Source = bitmap;
                    }
                    bitmap.WritePixels(new Int32Rect(0, 0, width, height), data, pixelsStride * height, pixelsStride);
                }
                catch { }
            });
        }
    }
}
