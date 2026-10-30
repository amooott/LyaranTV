# LyaranTV 2.0

**LyaranTV** est un lecteur IPTV pour Windows, construit avec WPF et .NET Framework 4.x. Il reprend les usages essentiels d'un lecteur IPTV moderne — sources, bibliothèque, direct, guide EPG, recherche, favoris, reprise de lecture et fiches de contenus — avec une identité visuelle originale.

> LyaranTV est uniquement un lecteur. Le projet ne fournit aucun contenu, aucune playlist, aucun fournisseur, aucun identifiant et aucune URL de fournisseur réelle. Vous restez responsable des sources que vous ajoutez et du respect des droits applicables.

## Points clés

- Interface française, thèmes **Sombre**, **Clair**, **Noir AMOLED** et **Automatique**.
- Huit accents prédéfinis et accent personnalisé, avec migration automatique de l'ancien ambre vers le violet LyaranTV.
- Sources **M3U/M3U8**, **Xtream Codes**, **Stalker/MAC**, démonstration intégrée et fichiers locaux.
- Chargement des comptes Xtream **en parallèle** avec barre de progression, temps écoulé et **temps restant estimé** ; guide EPG chargé en arrière-plan. Une étape dédiée **« Chargement des affiches »** préchauffe en arrière-plan les couvertures des premiers films et séries, prêtes à l'ouverture.
- **Accueil « billboard »** : bannière plein cadre sur 486 px, rotation automatique entre les contenus à reprendre et les mieux notés, affiche portrait nette à droite quand seule une affiche existe, fond flou et fondu progressif vers la page, boutons d’action blancs, favori et pastilles cliquables. En dessous : salutation, raccourcis, **Top 10**, nouveautés, rangées **À voir · Letterboxd** et **Avis récents de vos profils** lorsqu’elles ont des données, films, séries, rangée **En direct** avec programme en cours et barre de progression, puis les playlists.
- Bibliothèque repensée en mode découverte : hero cinématique avec rotation automatique et aperçu au survol, étagères horizontales avec flèches au survol, **grandes cartes de 236 × 354 px** (2:3 strict) dont l'affiche remplit tout le cadre, filtres rapides, vue « Tout parcourir » en grille et **toutes les catégories de chaînes, films et séries** réunies en blocs verticaux.
- **Palette cinéma neutre** : charcoal `#07080B`, surfaces `#101218`, accent violet conservé comme seule couleur d’action ; halo animé très lent, vignette et dégradés. Thèmes clair et AMOLED alignés.
- Lecteur intégré Windows Media Foundation : sonde de connexion, proxy local, ordre des liens TS → MP4/MKV → HLS, reconnexion du direct, pause, volume, plein écran, reprise, raccourcis ±5 s, sous-titres SRT/WebVTT **et pistes texte embarquées MKV/MP4**, détection des pistes audio, lecteur externe. Les menus de pistes affichent toujours la **langue et le type technique** (par exemple `Français · 5.1 · AC-3`, `Japonais · stéréo · AAC`, `Français · VOSTFR · SubRip`). Les contrôles sont **superposés à la vidéo** (fenêtré comme plein écran), avec une **barre de progression interactive** (piste de 6 px qui s'épaissit à 10 px au survol, curseur lumineux de 15 px, infobulle et glissement) et un masquage automatique après 3,5 s en lecture ; le curseur disparaît aussi en plein écran.
- **Outils de lecteur classiques** : vitesse de 0,5× à 2× mémorisée, lecture en boucle, **séquence A-B** avec repères sur la timeline, **minuterie de veille** (15–90 min ou fin de l’épisode), avance **image par image**, sauts ±10 s, menu contextuel complet au clic droit et aide des raccourcis (`?`).
- **Sous-titres personnalisables** : taille de 14 à 28 px, fond sombre activable, langue préférée utilisée par la recherche OpenSubtitles et les fichiers voisins. L’enchaînement automatique de l’épisode suivant est réglable.
- **Bouton Retour du lecteur** : il quitte la lecture et retrouve l’interface, avec la page, les filtres et la position de défilement d’origine. Le retour au dernier point de saut reste disponible avec `Retour arrière` et dans le menu contextuel du lecteur.
- Guide EPG XMLTV avec navigation par jour et programmes en cours.
- Favoris, historique, positions de reprise, profils séparés et contrôle parental. Supprimer une playlist efface aussi son historique, ses reprises et ses épisodes en cours dans tous les profils, sans toucher aux autres playlists.
- Fiches de contenus enrichies avec titre original, casting, réalisation, durée, pays, classification et bande-annonce via **TMDb** lorsque vous fournissez votre propre clé.
- Activité Letterboxd facultative : import local de vos fichiers CSV d’export, notes et critiques personnelles, avis publics récents de profils choisis via RSS, liste à voir locale et liens vers Letterboxd. Les données sont séparées par profil.
- Affiches, vignettes et logos chargés **en ligne**, avec un cache disque borné (`%APPDATA%\LyaranTV\covers`) et un cache mémoire : aucun visuel n'est fourni avec le programme.
- Barres de défilement personnalisées, champs de saisie lisibles dans tous les thèmes, animations discrètes et option de réduction des mouvements.
- Sauvegarde complète `.lyaran`, export/import JSON portable et export M3U.
- Aucun compte LyaranTV, aucun serveur obligatoire, aucune télémétrie et aucun paywall.

## Démarrage rapide

Prérequis : Windows 10/11 avec .NET Framework 4.x (fourni avec Windows) et le compilateur `csc.exe` inclus.

```bat
Sources\Reconstruire.bat
Dist\LyaranTV.exe
```

La compilation place aussi une copie de `LyaranTV.exe` à la racine du dépôt.

Pour découvrir l'application sans compte ni fournisseur :

```bat
Dist\LyaranTV.exe --example
Dist\LyaranTV.exe --example --page "Guide EPG"
```

L'exemple fictif se trouve dans `Examples\` et utilise une vidéo locale. Il ne contient aucun contenu réel.

## Bibliothèque en mode découverte

La page **Bibliothèque** fonctionne comme une page d'accueil de streaming : un hero plein format présente le contenu sélectionné, les étagères horizontales regroupent les films, les séries et le direct, et les catégories sont accessibles en un survol.

- **Survol** d'une affiche : le hero change immédiatement (titre, synopsis, note, programme en cours, bouton de lecture et favori) sans clic.
- **Rotation automatique** du hero toutes les 8,5 secondes ; elle se met en pause dès que la souris entre dans le hero ou survole une carte.
- **Clavier** : `Tab` place le focus sur les cartes, `←` / `→` passent d'une carte à l'autre dans une étagère, `Entrée` ou `Espace` ouvre le contenu.
- **Molette** au-dessus d'une étagère : défilement horizontal ; le défilement vertical reprend aux extrémités.
- **Filtres** : type de contenu, catégorie, tri, et bouton `Tout parcourir` pour afficher la bibliothèque complète en grille paginée.
- **Favoris et historique** : les contenus déjà commencés apparaissent dans une étagère « Reprendre la lecture » avec leur position.

Les affiches et backdrops TMDb remplissent le hero et les cartes ; sans image, un dégradé, les initiales et un filigrane du titre conservent une composition lisible.

## Compilation

```bat
Sources\Reconstruire.bat
```

Le script compile tous les fichiers `.cs` de `Sources\` avec le `csc.exe` de .NET Framework 4.x, les assemblies WPF, le manifeste DPI et l'icône LyaranTV. Aucun SDK .NET, aucun NuGet et aucun outil externe ne sont nécessaires.

L'exécutable est produit dans `Dist\` :

```text
Dist\LyaranTV.exe
```

Pour construire l'installateur (application + libVLC embarqués) :

```bat
Installer\Construire-Installateur.bat
```

Le résultat est écrit dans `Dist\setup-LyaranTV-<version>.exe` : le nom reprend la
version exacte de `Sources\App\AppInfo.cs`. Pour changer de version partout
(application, installateur, assembly), une seule commande :

```bat
bash Tools\version.sh 1.2.0
```

Sous Git Bash, les deux
étapes sont aussi disponibles avec `Tools/build.sh` et `Tools/setup.sh`, qui
génèrent en plus les empreintes SHA-256 de la Release.

## Organisation du dossier

```text
appiptv\
├─ README.md               présentation et démarrage
├─ LyaranTV.exe            exécutable compilé de la dernière version
├─ Sources\                code C# 5, manifeste, icône, script de compilation
├─ Examples\               playlist M3U, guide XMLTV et vidéo fictifs
├─ Installer\              source de l'installateur et script de packaging
├─ Tools\                  compilation et packaging en Git Bash
├─ VLC\                    libVLC embarqué (non versionné, reconstitué au build)
├─ Dist\                   sorties : application, setup et empreintes SHA-256
├─ build\                  fichiers intermédiaires (ignorent le versionnement)
├─ Docs\
│  ├─ REFERENCE.md         CLI, raccourcis, formats, données
│  ├─ DESIGN.md            identité, thèmes, composants, accessibilité
│  ├─ BUGS.md              corrections, limites, diagnostic
│  ├─ FEATURES.md          état des fonctions P0/P1/P2
│  ├─ RELEASE-NOTES.md     notes publiées avec chaque Release
│  └─ Screenshots\         captures de référence
└─ Assets\
   └─ Logos\              pistes de logo (17 retenue) et esquisses SVG
```

Les dossiers `Docs\`, `Assets\`, `Installer\`, `Tools\`, `build\` et `VLC\` ne
sont pas nécessaires à l'exécution : seul `Dist\LyaranTV.exe` et, pour
l'exemple, `Examples\` sont utilisés au lancement. L'installateur copie lui-même
libVLC à côté de l'application installée.

## Tests

```bat
Dist\LyaranTV.exe --selftest
```

Le rapport est écrit dans `%TEMP%\LyaranTV-selftest.log`. Les **33 auto-tests** couvrent notamment le nettoyage et les correspondances TMDb prudentes, la recherche par titre original/casting/réalisation/genres/synopsis, l’isolation par profil et source, l’import CSV Letterboxd avec critiques à plusieurs lignes et l’analyse locale de flux RSS publics.

## Connexion et chargement

À l'ouverture d'un compte Xtream, LyaranTV vérifie d'abord le compte (`player_api.php`), puis charge **en parallèle** les trois catalogues — direct, films et séries. Sur un serveur lent, la bibliothèque s'ouvre donc en 6 à 8 secondes là où un chargement séquentiel demandait 15 à 20 secondes.

La fenêtre de chargement affiche une barre de progression lissée, le pourcentage, le **temps écoulé** et une **estimation du temps restant** recalculée à chaque étape. Cinq étapes sont suivies : connexion au compte, catégories et catalogues, guide des programmes, préparation de la bibliothèque, puis **chargement des affiches** — les couvertures des premiers films et séries sont demandées en arrière-plan et mises en cache pendant que la bibliothèque s'ouvre.

Le guide XMLTV est volumineux et indépendant des catalogues : il est téléchargé **après** l'ouverture de la bibliothèque, en arrière-plan. Un message discret signale son arrivée et le compteur du menu **Guide EPG** se met à jour sans bloquer l'interface.

Pour les sources M3U et les portails Stalker/MAC, le même principe s'applique : la playlist est analysée immédiatement, le guide EPG éventuel est chargé ensuite.

## Lecture intégrée

Avant de confier un flux au lecteur, LyaranTV effectue une **sonde de connexion** de quelques millisecondes : elle vérifie que le serveur répond, relève le type de contenu (MPEG-TS, MKV, MP4, HLS), la latence et le User-Agent accepté. Si le flux est refusé (401, 403, 404) ou injoignable, un message explicite s'affiche immédiatement au lieu d'une attente silencieuse.

Les liens sont essayés dans un ordre réfléchi : le MPEG-TS, le MKV et le MP4 progressifs d'abord, le HLS (`.m3u8`, non décodé par `MediaElement`) en dernier recours. Le lecteur indique le type détecté, la latence de connexion et le numéro du lien utilisé (`1/2`).

Les flux HTTP et HTTPS passent par un petit proxy local (`127.0.0.1`). WPF `MediaElement` envoie par défaut `User-Agent: NSPlayer/12…` ; certains fournisseurs refusent cette signature (HTTP 401/403), ce qui produisait l'erreur `0xC00D1198`. Le proxy rejoue la requête avec le User-Agent validé par la sonde, puis celui du navigateur, de VLC, et `NSPlayer` en dernier recours, en relayant les requêtes `Range` et les en-têtes utiles.

Les options de playlist `#EXTVLCOPT:http-user-agent`, `http-referrer`, `http-origin` et `http-cookie`, ainsi que les suffixes `|User-Agent=…&Referer=…`, sont transmises au proxy. Les fichiers locaux ne passent jamais par lui. L'ouverture d'un flux est limitée à 20 secondes, et un direct interrompu tente jusqu'à deux reconnexions automatiques.

## TMDb

Les affiches, notes et fiches TMDb sont optionnelles. Ajoutez votre propre clé dans **Paramètres → Métadonnées et sous-titres**, ou définissez la variable d'environnement :

```bat
set LYARANTV_TMDB_KEY=votre_cle
```

La clé n'est jamais incluse dans le projet, les exemples, les sauvegardes portables ou les journaux. Sans clé, LyaranTV reste pleinement utilisable et affiche des vignettes de remplacement.

Les images distantes sont lues à la demande depuis TMDb ou depuis les URL `tvg-logo` de votre playlist. Elles sont décodées à 420 px de large, gardées en mémoire (environ 220 images) et conservées dans un cache disque borné sous `%APPDATA%\LyaranTV\covers` ; les fichiers de plus de 45 jours sont purgés et le cache est ramené à environ 1 200 images. Ce cache se vide depuis **Paramètres → Métadonnées et sous-titres**.

## Affiches et synopsis de secours

TMDb reste la source principale des affiches et des synopsis. Quand il n'y a pas de clé ou pas de correspondance, LyaranTV complète automatiquement les fiches, sans compte ni clé :

- **Séries et épisodes** : [TVmaze](https://www.tvmaze.com/api) (affiche, synopsis, saisons, captures d'épisodes), puis IMDb pour l'affiche.
- **Films** : API de suggestion d'IMDb pour l'affiche, Wikipédia pour le synopsis.

Les résultats sont mis en cache dans `%APPDATA%\LyaranTV\artwork-cache.json` et les images dans `covers`. Les recherches partent en arrière-plan, à faible cadence, uniquement pour les contenus sans affiche. Si aucune source ne répond, une vignette dessinée (dégradé, initiales, titre) est affichée : une carte n'est jamais vide. Le cache se vide depuis **Paramètres → Métadonnées et sous-titres**.

## Letterboxd

Dans **Paramètres → API Letterboxd**, chaque profil local peut enregistrer son nom Letterboxd, indiquer jusqu’à huit profils publics à suivre (vous-même et/ou des amis), et importer ses fichiers CSV (`watchlist.csv`, `ratings.csv`, `diary.csv`, `reviews.csv`). Les avis RSS publics sont mis en cache six heures ; les notes et avis sont associés prudemment par titre et année et affichés dans les fiches de films, avec un lien vers l’avis original. La liste à voir Letterboxd et les avis récents alimentent aussi l’accueil lorsqu’ils correspondent à la playlist. L’activité importée et le cache RSS restent locaux et séparés par profil. Les actions « vu » et « liste à voir » de LyaranTV restent locales et ne modifient pas Letterboxd.

La section **API Letterboxd** indique l’état d’accès et ouvre la [documentation API](https://api-docs.letterboxd.com/) ou la [page de demande d’accès](https://letterboxd.com/api-beta/access/). LyaranTV ne demande aucun mot de passe Letterboxd et ne découvre pas automatiquement la liste d’amis : l’utilisateur saisit les noms publics à consulter. Les flux ne donnent accès qu’à l’activité récente rendue publique. Letterboxd indique que l’API est accordée sur demande et n’est actuellement pas accessible aux projets privés/personnels ; la connexion OAuth, les listes privées et l’écriture vers le compte restent donc désactivées.

## Sous-titres en ligne (OpenSubtitles)

La recherche en ligne est optionnelle. Créez une clé API gratuite sur opensubtitles.com, puis collez-la dans **Paramètres → Métadonnées et sous-titres**, ou définissez :

```bat
set LYARANTV_OPENSUBTITLES_KEY=votre_cle
```

Dans le lecteur, le bouton des sous-titres propose « Chercher sur OpenSubtitles… » : le titre du film — ou de la série, avec saison et épisode — est prérempli, les fichiers sont listés avec leur langue et leur nombre de téléchargements, puis le fichier choisi est téléchargé en SubRip dans `%APPDATA%\LyaranTV\sous-titres` et appliqué immédiatement. Aucune requête n'est envoyée sans action de votre part. Les fichiers restent soumis aux conditions d'OpenSubtitles.com.

## Données et confidentialité

Par défaut :

| Élément | Emplacement |
|---|---|
| Données, réglages, playlists, favoris | `%APPDATA%\LyaranTV` |
| Journal d'erreurs | `%APPDATA%\LyaranTV\error.log` |
| Cache d'affiches (disque) | `%APPDATA%\LyaranTV\covers` |
| Sous-titres téléchargés | `%APPDATA%\LyaranTV\sous-titres` |
| Caches de métadonnées | `%APPDATA%\LyaranTV\tmdb-cache.json`, `artwork-cache.json` |

Pour isoler complètement les données :

```bat
set LYARANTV_DATA_DIR=C:\temp\lyarantv-data
LyaranTV.exe
```

Les mots de passe Xtream et les adresses MAC sont protégés par DPAPI pour l'utilisateur Windows courant. L'export JSON portable omet volontairement les mots de passe, adresses MAC, codes PIN et clés TMDb/OpenSubtitles. La sauvegarde complète `.lyaran` conserve les identifiants chiffrés.

## Documentation

- [`Docs/REFERENCE.md`](Docs/REFERENCE.md) — options CLI, raccourcis, formats, fichiers et architecture.
- [`Docs/DESIGN.md`](Docs/DESIGN.md) — identité, palette, composants, mouvement et accessibilité.
- [`Docs/BUGS.md`](Docs/BUGS.md) — corrections, limites connues et diagnostic.
- [`Docs/FEATURES.md`](Docs/FEATURES.md) — état des fonctionnalités P0/P1/P2.
- [`Sources/README.md`](Sources/README.md) — organisation du code source.
- [`Docs/Screenshots/`](Docs/Screenshots/) — captures de référence.

## Limites principales

- Le changement de piste audio en cours de lecture et le décodage AC-3, E-AC-3, DTS/DTS-HD, TrueHD/MLP, AAC, MP3, Opus, FLAC, Vorbis, PCM/WAV et WMA nécessitent **libVLC** (installé avec VLC, détecté automatiquement). Sans lui, le moteur `MediaElement` de WPF reste utilisé et seule la première piste audio est lue ; LyaranTV propose alors d’ouvrir le fichier dans VLC. Les sous-titres texte embarqués (SubRip, ASS/SSA, WebVTT, `tx3g`) sont extraits et affichés dans l’application dans les deux cas. Les sous-titres image (PGS/VobSub) ne sont pas affichés.
- La lecture HTTPS intégrée peut encore échouer sur certaines installations Windows 11 à cause de Media Foundation ; utilisez alors VLC ou un autre lecteur externe. L'erreur `0xC00D1198` liée au User-Agent est traitée par le proxy local.
- Les fonctions dépendant d'API mobiles/TV (AirPlay, Cast, PiP, widgets, téléchargements hors ligne) ne sont pas disponibles nativement en WPF.
- Les logos de chaînes proviennent du champ `tvg-logo` de la playlist ; sans logo fourni, une vignette de remplacement est affichée.
- L'interface est en français ; l'anglais complet reste un travail futur.
- Les dates de l'exemple EPG sont fictives et centrées sur le 28 octobre 2026 ; après cette date, les programmes apparaissent comme passés.
