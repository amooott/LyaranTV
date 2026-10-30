# Référence LyaranTV

Cette référence décrit les commandes, raccourcis, formats, fichiers de données et points d'architecture de LyaranTV 2.0.

## Ligne de commande

| Commande | Effet |
|---|---|
| `LyaranTV.exe` | Lance l'application avec les dernières préférences. |
| `LyaranTV.exe --help` | Affiche l'aide dans une boîte de dialogue. |
| `LyaranTV.exe --demo` | Charge la bibliothèque de démonstration intégrée. |
| `LyaranTV.exe --example` | Charge `Examples\lyarantv-demo.m3u` et son guide fictif. |
| `LyaranTV.exe --play <url\|fichier\|id>` | Ouvre immédiatement un flux, un fichier local ou un contenu de la bibliothèque par son identifiant. |
| `LyaranTV.exe --page <nom>` | Ouvre une page après le chargement éventuel d'une source. |
| `LyaranTV.exe --theme <nom>` | Force un thème : `Sombre`, `Clair`, `Noir AMOLED`, `Automatique`. |
| `LyaranTV.exe --detail <titre>` | Ouvre la fiche du premier contenu dont le titre contient la chaîne. |
| `LyaranTV.exe --screenshot <fichier.png>` | Capture la fenêtre principale après 2,2 s, puis quitte. |
| `LyaranTV.exe --selftest` | Exécute les auto-tests sans interface et écrit un journal. |

Les options peuvent être combinées, par exemple :

```bat
LyaranTV.exe --example --page "Paramètres" --theme "Clair"
LyaranTV.exe --example --detail "Carnet de Voyage"
LyaranTV.exe --demo --screenshot "%TEMP%\lyarantv.png"
```

Noms de pages reconnus : `Accueil`, `Bibliothèque`, `Favoris`, `Récents`, `Guide EPG`, `Médias locaux`, `Catégories`, `Chaînes masquées`, `Paramètres`, `Guide rapide`.

`--detail` attend que la bibliothèque soit chargée ; avec `--example`, la fiche s'ouvre après l'indexation. `--screenshot` ne capture que la fenêtre principale, pas les dialogues modaux.

## Raccourcis clavier

### Fenêtre principale

| Raccourci | Action |
|---|---|
| `Ctrl+F` | Placer le focus dans la recherche. |
| `Ctrl+R` | Actualiser la source active. |
| `Ctrl+N` | Ajouter une playlist. |
| `Ctrl+D` | Ajouter ou retirer le contenu en cours des favoris. |
| `F11` | Basculer en plein écran. |
| `Échap` | Quitter le plein écran ou la recherche ; dans la lecture, quitter le lecteur et revenir à l'interface ; dans les épisodes, à la page précédente. |
| `Page préc.` / `Page suiv.` | Faire défiler la page active. |
| `Home` / `End` | Aller au début ou à la fin de la page active. |

Le bouton **Retour** de la barre supérieure (à gauche de la marque) revient à la page précédente avec ses filtres et sa position de défilement ; il est inactif quand l'historique est vide. Le bouton **Retour** des épisodes se comporte de la même façon. Chaque changement de page, y compris l'entrée en lecture, mémorise l'état courant ; l'historique conserve 24 points.

Dans le lecteur, le bouton **Retour** et `Échap` quittent la lecture et retrouvent l'interface : la **page des épisodes de la série** pour un épisode, ou la **catégorie exacte** d'où la lecture a été lancée pour un film ou une chaîne (type et catégorie conservés, position de défilement restaurée). Le retour au dernier point de retour — la position occupée juste avant le dernier saut — reste disponible avec `Retour arrière` et dans le menu contextuel du lecteur.

### Bibliothèque

| Interaction | Action |
|---|---|
| Survol d'une carte | Change l'aperçu du hero sans clic. |
| Molette sur une étagère | Défilement horizontal ; le défilement vertical reprend aux extrémités. |
| `Tab` | Parcourt les cartes et les contrôles. |
| `←` / `→` | Carte précédente / suivante dans l'étagère. |
| `Entrée` / `Espace` | Ouvre le contenu sélectionné. |

Le hero tourne automatiquement toutes les 8,5 s et se met en pause au survol (14 s après une carte, 30 s dans le hero). La rotation est désactivée avec l'option de réduction des animations.

La rangée **Reprendre la lecture** de l'accueil utilise la même source : elle reprend les films, les chaînes, les fichiers locaux et les **séries** — ces dernières apparaissent sous le nom de la série, avec saison, épisode, barre de progression et temps restant, et rouvrent directement l'épisode en cours. Le hero du haut affiche le contenu le plus récent de cette liste.

Sous les étagères, **Toutes les catégories** présente l'intégralité des catégories de chaînes, de films et de séries en trois blocs qui s'empilent verticalement. Chaque carte filtre le type et la catégorie ; les boutons **Tout voir** ouvrent la vue complète du bloc.

### Lecteur

| Raccourci | Action |
|---|---|
| `Espace` | Lecture / pause. |
| `←` / `→` | Reculer / avancer de 5 secondes. |
| `J` / `L` | Reculer / avancer de 10 secondes. |
| `↑` / `↓` | Augmenter / baisser le volume. |
| `[` / `]` | Ralentir / accélérer la lecture (0,5× à 2×). |
| `Ctrl+L` | Activer ou désactiver la lecture en boucle. |
| `Maj+A` / `Maj+B` | Placer les points A et B d’une séquence répétée. |
| `Maj+X` | Effacer la séquence A-B. |
| `,` / `.` | Image précédente / suivante (met la lecture en pause). |
| `?` | Afficher tous les raccourcis du lecteur. |
| `M` | Couper ou rétablir le son. |
| `S` | Ouvrir le menu des sous-titres (pistes embarquées, sidecars, fichier externe). |
| `A` | Ouvrir le menu des pistes audio détectées. |
| `Retour arrière` | Revenir au dernier point de retour (position quittée lors du dernier saut). |
| `E` | Faire défiler jusqu'aux autres épisodes, sous le lecteur. |
| `Page préc.` / `Page suiv.` | Chaîne précédente / suivante. |
| `+` / `-` | Chaîne suivante / précédente. |
| `Échap` | Quitter le plein écran, puis quitter le lecteur et revenir à l'interface. |

La barre de progression se pilote à la souris : le survol épaissit la piste et affiche l'infobulle du temps visé, le glissement déplace la lecture. Les contrôles sont superposés à la vidéo et s'effacent après 3,5 s en lecture ; un mouvement de souris, une touche ou l'ouverture d'un menu les ramènent. En plein écran, le curseur disparaît avec eux. Deux boutons **− 5 s** / **+ 5 s** encadrent la lecture, comme les boutons `←` / `→` du clavier.

Un **clic droit** sur l'image ouvre le menu complet du lecteur : lecture, sauts, pistes audio et sous-titres, vitesse, boucle, séquence A-B, minuterie de veille, format d'image, raccourcis, fiche du contenu et lecteur externe. Le bouton de vitesse affiche la valeur courante et ouvre les vitesses prédéfinies ; le bouton de veille propose 15 à 90 minutes ou la fin de l'épisode ; le bouton de boucle reste accentué quand il est actif. La séquence A-B se matérialise par un segment coloré sur la barre de progression et ramène automatiquement au point A.

Pour les séries, la rangée **Autres épisodes** s'ouvre automatiquement sous le lecteur (bouton ou `E` pour la replier) : vignettes, avancement, badges **EN COURS** / **À SUIVRE**, sélecteur de saison, bouton **Épisode suivant** et défilement calé sur l'épisode en cours. La lecture enchaîne l'épisode suivant à la fin, avec un compte à rebours annulable.

## Formats pris en charge

### Sources

- **M3U / M3U8** : fichier local, URL HTTP(S), attributs `tvg-id`, `tvg-name`, `tvg-logo`, `group-title`, `tvg-chno`.
- **Xtream Codes** : serveur, nom d'utilisateur, mot de passe ; catégories live, VOD et séries.
- **Stalker / MAC** : portail, adresse MAC, type d'appareil.
- **Démonstration** : bibliothèque fictive intégrée.
- **Médias locaux** : fichiers vidéo ouverts depuis le disque.

### Lecture

- Fichiers locaux : lus directement par `MediaElement`.
- Flux HTTP/HTTPS : **sonde de connexion** (statut, type de contenu, latence, User-Agent accepté) puis proxy local (`127.0.0.1`) qui relaie le flux avec le User-Agent validé, un Referer, une Origin, un Cookie et les requêtes `Range`.
- Ordre des liens : MPEG-TS, MKV, MP4 progressifs, puis HLS en dernier recours. Le lecteur affiche le type détecté, la latence et l'indice du lien (`1/2`).
- En-têtes de playlist : `#EXTVLCOPT:http-user-agent`, `http-referrer`, `http-origin`, `http-cookie`, ou suffixe `|User-Agent=…&Referer=…&Origin=…&Cookie=…`.
- Délai d'ouverture d'un flux : 20 s. Reconnexion automatique d'un direct interrompu : 2 tentatives espacées de 2 s.
- HLS (`.m3u8`) et DRM ne sont pas pris en charge nativement ; le lecteur externe reste disponible.

### Chargement d'un compte

| Étape | Contenu |
|---|---|
| 0 | Connexion au compte (`player_api.php` sans action). |
| 1 | Catégories et catalogues live, VOD et séries, chargés **en parallèle**. |
| 2 | Guide XMLTV, chargé **après** l'ouverture de la bibliothèque (arrière-plan). |
| 3 | Préparation et affichage de la bibliothèque. |
| 4 | Affiches : demande en arrière-plan des couvertures des premiers films et séries (`PrefetchArtwork`), état suivi dans la fenêtre. |

La fenêtre affiche le pourcentage lissé, le temps écoulé et une estimation du temps restant (`elapsed × (100 − progression) / max(30, progression)`, lissée).

### Guide

- XMLTV `.xml`.
- XMLTV compressé `.xml.gz`.
- Téléchargement HTTP(S) d'un guide.
- Guide fictif fourni : `Examples\lyarantv-guide.xml`.

### Sous-titres et pistes audio

- **SRT** (`.srt`).
- **WebVTT** (`.vtt`).
- Détection automatique des fichiers sidecar à côté du média : `film.srt`, `film.vtt`, `film.fr.srt`, etc.
- **Pistes texte embarquées** des fichiers locaux : MKV (`S_TEXT/UTF8`, `S_TEXT/ASS`, `S_TEXT/SSA`, `S_TEXT/WEBVTT`) et MP4/MOV (`tx3g`, `text`, `wvtt`). Elles sont extraites en mémoire et proposées dans le menu `CC` ou par la touche `S`.
- Les pistes image (PGS, VobSub) sont listées mais désactivées : le lecteur ne peut pas les superposer.
- **Pistes audio** : détection des pistes MKV/MP4 et de leur langue, canaux et codec dans le menu `A`, par exemple `Français · 5.1 · AC-3`. Avec libVLC, les métadonnées du flux sont complétées par l’analyse locale et la piste se change à la volée ; avec `MediaElement`, les pistes suivantes s’ouvrent dans VLC avec `--audio-track` et reprennent à la position courante.
- **Sous-titres voisins** : chaque fichier est libellé avec sa langue, sa version et son format (`Français · VOSTFR · SubRip`, `Langue non déclarée · WebVTT`).
- Sélection manuelle via le bouton `CC` / `S` pour les sous-titres, et le bouton audio / `A` pour les pistes.

### Images

- Affiches TMDb (`image.tmdb.org`), vignettes et logos de chaînes (`tvg-logo` de la playlist).
- Les images sont chargées à la demande, décodées à 420 px de large, puis gardées dans un cache mémoire (environ 220 images) et un cache disque borné sous `%APPDATA%\LyaranTV\covers`. Les fichiers de plus de 45 jours sont purgés et le cache est ramené à environ 1 200 images. Le cache se vide depuis les paramètres.
- En cas d'échec réseau, une vignette de remplacement avec icône de type est affichée.

## Fichiers de données

Répertoire par défaut : `%APPDATA%\LyaranTV`. Surcharge : variable `LYARANTV_DATA_DIR`.

| Fichier | Rôle |
|---|---|
| `settings.json` | Thème, accent, volume, reprise, TMDb, contrôle parental, lecteur externe, etc. |
| `playlists.json` | Sources enregistrées ; identifiants chiffrés par DPAPI. |
| `profiles.json` | Profils, couleurs, indicateur enfant, PIN chiffré. |
| `favorites.txt` | Favoris du profil Principal. |
| `recent.txt` | Historique du profil Principal. |
| `positions.json` | Positions de reprise du profil Principal. |
| `durations.json` | Durées connues des contenus (tous profils). |
| `episodes.json` | Épisodes en cours (tous profils). |
| `history-sources.json` | Propriétaire (playlist) de chaque contenu de l’historique ; sert à purger une playlist sans toucher aux autres. |
| `favorites.<profil>.txt` | Favoris d'un autre profil. |
| `recent.<profil>.txt` | Historique d'un autre profil. |
| `positions.<profil>.json` | Positions d'un autre profil. |
| `hidden-channels.txt` | Contenus masqués. |
| `hidden-groups.txt` | Catégories masquées. |
| `local-files.txt` | Médias locaux récents. |
| `tmdb-cache.json` | Métadonnées TMDb en cache (pas les images). |
| `artwork-cache.json` | Affiches et synopsis de secours (TVmaze, IMDb, Wikipédia). |
| `covers\` | Cache disque des affiches et vignettes (`.img`, purge automatique). |
| `sous-titres\` | Sous-titres téléchargés depuis OpenSubtitles. |
| `error.log` | Journal local des erreurs. |

Les fichiers du profil `Principal` conservent leurs noms historiques sans suffixe. Les autres profils utilisent un suffixe pour isoler favoris, historique et reprise. `durations.json`, `episodes.json` et `history-sources.json` restent globaux : supprimer une playlist efface son historique, ses reprises, ses durées et ses épisodes en cours dans **tous les profils**, sans toucher aux autres playlists ; les favoris et les réglages sont conservés.

## Sauvegarde et export

- **Sauvegarde complète** `.lyaran` : archive ZIP de tous les fichiers du répertoire de données, identifiants chiffrés inclus.
- **Restauration** : vérifie la présence de `settings.json` ou `playlists.json` avant de copier.
- **Export JSON portable** : contient les playlists (sans mots de passe ni MAC), favoris, historique, positions, profils (sans PIN), masquages et réglages (sans clé TMDb ni PIN parental).
- **Import JSON portable** : refuse les fichiers dont le marqueur `"lyarantv-portable"` ou la version sont absents.
- **Export M3U** : exporte la playlist active.

## TMDb

Ordre de résolution de la clé :

1. `AppSettings.TmdbApiKey` dans **Paramètres → Métadonnées TMDb**.
2. Variable d'environnement `LYARANTV_TMDB_KEY`.

La clé n'est jamais incluse dans le code, les exemples, les exports portables, les sauvegardes `.lyaran` ou les journaux. Le cache TMDb ne stocke que les métadonnées textuelles et les URL d'images.

Attribution affichée dans l'application :

> Ce produit utilise l'API TMDb mais n'est ni approuvé ni certifié par TMDb.

## Architecture

| Fichier | Responsabilité |
|---|---|
| `Sources/LyaranTV.cs` | Point d'entrée, options CLI, auto-tests. |
| `Sources/Core/Models.cs` | Modèles, réglages, profils, magasins JSON, export portable, sauvegarde. |
| `Sources/Core/Network.cs` | Requêtes HTTP, décodage de texte, journal. |
| `Sources/Core/StreamProxy.cs` | Proxy HTTP local de lecture : User-Agent, Referer, Origin, Cookie, requêtes `Range`, sonde de flux. |
| `Sources/Core/StreamPlanner.cs` | Ordre des liens de lecture : TS/MKV/MP4 avant HLS. |
| `Sources/Core/PlaylistLoader.cs` | M3U, Xtream, XMLTV, démonstration, médias locaux. |
| `Sources/Core/StalkerClient.cs` | Portails MAC / Stalker. |
| `Sources/Core/Tmdb.cs` | Nettoyage des titres, client TMDb, cache, enrichissement. |
| `Sources/Core/Subtitles.cs` | Analyse SRT/WebVTT, sidecars, encodages. |
| `Sources/Core/MediaTracks.cs` | Modèles de pistes, détection du conteneur, libellés de langue/codec, normalisation des cues. |
| `Sources/Core/Mp4Tracks.cs` | Lecture ISO BMFF : pistes audio/sous-titres, extraction `tx3g`/`text`/`wvtt`. |
| `Sources/Core/MkvTracks.cs` | Lecture EBML/Matroska : pistes audio/sous-titres, extraction SubRip/ASS/SSA/WebVTT. |
| `Sources/Core/CredentialVault.cs` | Chiffrement DPAPI. |
| `Sources/UI/MainWindow.cs` | Fenêtre principale, profils, filtrage parental, navigation, état. |
| `Sources/UI/PagesHome.cs` | Accueil, recherche, cartes de l'accueil, catégories et chaînes masquées. |
| `Sources/UI/PagesLibrary.cs` | Bibliothèque découverte : hero rotatif, étagères, filtres, cartes et vue « Tout parcourir ». |
| `Sources/UI/PagesPlayer.cs` | Lecteur, reprise, sous-titres, épisodes, lecture locale. |
| `Sources/UI/PagesGuide.cs` | Guide EPG. |
| `Sources/UI/PagesSettings.cs` | Paramètres, profils, TMDb, données, à propos. |
| `Sources/UI/Dialogs.cs` | Sources, fiches, TMDb manuel, chargement, dialogues. |
| `Sources/UI/UiKit.cs` | Thèmes, styles, champs, scrollbars, logo, toasts, composants. |
| `Sources/UI/Images.cs` | Chargement d'images en ligne, cache mémoire, cache disque et affiches de l'écran de chargement. |

## Compilation et environnement

```bat
Sources\Reconstruire.bat
```

- Sortie : `Dist\LyaranTV.exe`.
- Compilateur : `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
- Cible : `winexe`, .NET Framework 4.x, WPF.
- Langage : C# 5 (pas d'interpolation, `?.`, `nameof`, tuples, correspondance de motifs).
- Aucun SDK .NET, NuGet, MSBuild ou ffmpeg requis.
- Manifeste DPI `LyaranTV.manifest`, icône `LyaranTV.ico`.
- Installateur : `Installer\Construire-Installateur.bat` → `Dist\setup-LyaranTV-<version>.exe` (version lue dans `AppInfo.cs`).
- Équivalents Git Bash : `Tools/build.sh` et `Tools/setup.sh`.

## Auto-tests

```bat
Dist\LyaranTV.exe --selftest
```

Journal : `%TEMP%\LyaranTV-selftest.log`.

La suite compte **30 auto-tests** et couvre : chemins, utilitaires texte, priorité des flux, erreurs réseau lisibles, chiffrement, réglages et migration d'accent, playlists, favoris/positions, **suppression de l'historique d'une playlist**, démonstration, M3U, en-têtes de flux M3U, Xtream, **conversion M3U vers Xtream**, XMLTV, **nettoyage des titres** (préfixes `IT -`, `FR |`, crochets), nettoyage TMDb, **pistes audio et sous-titres en ligne**, **pistes audio universelles**, affiches et aperçus de secours, cache TMDb, profils/PIN, état par profil, export portable, sous-titres SRT/WebVTT, **pistes embarquées MKV et MP4**, thèmes/champs/scrollbars, décodage des images en mémoire et lecture du cache disque d'affiches, et sauvegarde ZIP.
