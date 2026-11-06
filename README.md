# LyaranTV 1.6.10

**LyaranTV** est un lecteur IPTV pour Windows, construit avec WPF et .NET Framework 4.x. Il reprend les usages essentiels d'un lecteur IPTV moderne — sources, bibliothèque, direct, guide EPG, recherche, favoris, reprise de lecture et fiches de contenus — avec une identité visuelle originale.

> LyaranTV est uniquement un lecteur. Le projet ne fournit aucun contenu, aucune playlist, aucun fournisseur, aucun identifiant et aucune URL de fournisseur réelle. Vous restez responsable des sources que vous ajoutez et du respect des droits applicables.

## Points clés

- Interface française, thèmes **Sombre**, **Clair**, **Noir AMOLED** et **Automatique**.
- **Apparence personnalisable** : huit accents prédéfinis ou accent personnalisé, **seconde couleur de dégradé**, **ambiance du fond** (Discrète / Équilibrée / Vive) et option de **surfaces teintées** par l'accent. L'ancien ambre est migré automatiquement vers le violet LyaranTV.
- Sources **M3U/M3U8**, **Xtream Codes**, **Stalker/MAC**, démonstration intégrée et fichiers locaux.
- Chargement des comptes Xtream **en parallèle** avec barre de progression, temps écoulé et **temps restant estimé** ; guide EPG chargé en arrière-plan. Une étape dédiée **« Chargement des affiches »** affiche sa progression réelle : les jaquettes du premier écran sont téléchargées avant l'ouverture, et une **file de fond** complète ensuite l'accueil puis la bibliothèque en continu. Les images sont conservées sur disque, donc les lancements suivants s'ouvrent avec toutes leurs affiches. Les téléchargements passent d'abord par le moteur HTTPS standard (délai court), puis par un client **IPv4** en repli (TLS 1.2, redirections, réponses chunked) : une adresse CDN morte ne bloque plus la file.
- **Ouverture cinématique** : un générique de lancement animé (logo, lettres qui apparaissent une à une, barre de progression et messages d’étape), puis l’écran **« Qui regarde ? »** façon plateforme de streaming. Chaque profil a sa carte, sa couleur, son badge enfant ou cadenas ; le profil choisi ouvre l’application en fondu et en léger zoom, avec un message de bienvenue. L’écran reste optionnel depuis **Paramètres → Démarrage**.
- **Accueil « billboard »** : bannière plein cadre sur 486 px, rotation automatique entre les contenus à reprendre et les mieux notés, affiche portrait nette à droite quand seule une affiche existe, fond flou et fondu progressif vers la page, boutons d’action blancs, favori et pastilles cliquables. En dessous : salutation, raccourcis, **Top 10**, nouveautés, films, séries, rangée **En direct** avec programme en cours et barre de progression, puis les playlists.
- **Ouverture de la bibliothèque avec chargement visible** : sur un gros catalogue, la page s'affiche immédiatement avec un indicateur, pendant que les listes et les catégories sont préparées en arrière-plan — la fenêtre reste réactive.
- Bibliothèque repensée en mode découverte : hero cinématique avec rotation automatique et aperçu au survol, étagères horizontales avec flèches au survol, **grandes cartes de 236 × 354 px** (2:3 strict) dont l'affiche remplit tout le cadre, filtres en onglets texte, **bascule liste / grille** qui affiche toute la sélection dans une grille paginée, vue « Tout parcourir » en grille et **toutes les catégories de chaînes, films et séries** réunies en blocs verticaux.
- **Palette cinéma neutre** : charcoal `#07080B`, surfaces `#101218`, accent violet conservé comme seule couleur d’action ; halo animé très lent, vignette et dégradés. **Boutons réactifs** au survol (reflet diagonal et légère élévation), boutons principaux en dégradé accent → seconde teinte, bordure supérieure et barre latérale dégradées. Thèmes clair et AMOLED alignés.
- Lecteur intégré Windows Media Foundation : sonde de connexion, proxy local, ordre des liens TS → MP4/MKV → HLS, reconnexion du direct, pause, volume, plein écran, reprise, raccourcis ±5 s, sous-titres SRT/WebVTT **et pistes texte embarquées MKV/MP4**, détection des pistes audio, lecteur externe. Les menus de pistes affichent toujours la **langue et le type technique** (par exemple `Français · 5.1 · AC-3`, `Japonais · stéréo · AAC`, `Français · VOSTFR · SubRip`). Les contrôles sont **superposés à la vidéo** (fenêtré comme plein écran), avec une **barre de progression interactive** (piste de 6 px qui s'épaissit à 10 px au survol, curseur lumineux de 15 px, infobulle et glissement), une **partie tampon blanche** indiquant ce qui est déjà chargé, et un masquage automatique après 3,5 s en lecture ; le curseur disparaît aussi en plein écran. Pour le direct, un bouton **Retour au direct** recharge le flux au bord du live.
- **Tiroir des chaînes en direct** : depuis le lecteur, `C` ou le bouton de la barre supérieure ouvre une colonne à gauche avec filtre instantané, logos, programme EPG en cours et badge de la chaîne regardée. Les flèches ou la manette changent de chaîne sans quitter la lecture, et la vidéo reste visible derrière le tiroir.
- **Outils de lecteur classiques** : vitesse de 0,5× à 2× mémorisée, lecture en boucle, **séquence A-B** avec repères sur la timeline, **minuterie de veille** (15–90 min ou fin de l’épisode), avance **image par image**, sauts ±10 s, menu contextuel complet au clic droit et aide des raccourcis (`?`).
- **Sous-titres personnalisables depuis le lecteur** : taille, couleur du texte, couleur et opacité du fond, affichage du fond et contour, avec aperçu en direct. La langue préférée est utilisée par la recherche OpenSubtitles et les fichiers voisins. L’enchaînement automatique de l’épisode suivant est réglable.
- **Bouton Retour du lecteur** : il quitte la lecture et retrouve l’interface, avec la page, les filtres et la position de défilement d’origine. Le retour au dernier point de saut reste disponible avec `Retour arrière` et dans le menu contextuel du lecteur.
- Guide EPG XMLTV avec navigation par jour et programmes en cours.
- Favoris, historique, positions de reprise, profils séparés et contrôle parental. Supprimer une playlist efface aussi son historique, ses reprises et ses épisodes en cours dans tous les profils, sans toucher aux autres playlists.
- **Aperçu intégré à gauche** : la fiche d’un film ou d’une série glisse dans la colonne des fonctionnalités, sous le rail de catégories, plutôt que de recouvrir la page. En-tête collant, fond presque transparent, image d’ambiance, affiche, synopsis, métadonnées, programme EPG et actions ; `Échap`, la croix ou un clic à l’extérieur referment le panneau sans perdre la page. Titre original, casting, réalisation, durée, pays, classification et bande-annonce via **TMDb** lorsque vous fournissez votre propre clé. Les séries disposent d'un en-tête cinématographique et de cartes d'épisodes interactives.
- **Mode langue** facultatif : un code (FR, EN, AR, ES…) ne laisse visibles que les catégories qui le contiennent. Le choix reste actif en changeant de page et après un redémarrage.
- Affiches, vignettes et logos chargés **en ligne**, avec un cache disque borné (`%APPDATA%\LyaranTV\covers`) et un cache mémoire : aucun visuel n'est fourni avec le programme.
- **Navigation salon** : manette Xbox (XInput), télécommande multimédia et **clavier virtuel AZERTY** dans la recherche, avec focus spatial, raccourcis et aide contextuelle. Un **bouton hamburger** en haut à gauche fait glisser la barre latérale hors de l’écran (ou la ramène) avec une animation fluide ; `Ctrl+B` fait de même, et `F11` met toute l’application en plein écran.
- **Recherche mémorisée** : les dernières recherches validées apparaissent en pastilles cliquables, avec un bouton **Effacer l’historique**.
- Barres de défilement personnalisées, champs de saisie lisibles dans tous les thèmes, animations discrètes et option de réduction des mouvements.
- Sauvegarde complète `.lyaran`, export/import JSON portable et export M3U.
- Aucun compte LyaranTV, aucun serveur obligatoire, aucune télémétrie et aucun paywall.
- Mises à jour par GitHub Releases : une Release publique fonctionne sans compte ni jeton GitHub; l'installateur est contrôlé par SHA-256 avant son lancement.

## Démarrage rapide

Prérequis : Windows 10/11 avec .NET Framework 4.x (fourni avec Windows) et le compilateur `csc.exe` inclus.

```bat
Sources\Reconstruire.bat
Dist\LyaranTV.exe
```

La compilation produit `Dist\LyaranTV.exe` ; la racine ne contient que les sources et la documentation.

Pour découvrir l'application sans compte ni fournisseur :

```bat
Dist\LyaranTV.exe --example
Dist\LyaranTV.exe --example --page "Guide EPG"
```

L'exemple fictif se trouve dans `Examples\` et utilise une vidéo locale. Il ne contient aucun contenu réel.

## Ouverture et profils

Au lancement, LyaranTV prend le temps d’un générique : le logo apparaît, les lettres de la marque se posent une à une, une fine barre de progression accompagne cinq messages (« Initialisation de l’interface », « Lecture de vos réglages », « Préparation de votre bibliothèque », « Application de votre thème », « Presque prêt »), puis l’écran laisse place au choix du profil.

- **« Qui regarde ? »** présente chaque profil dans une carte de 148 px, avec sa photo ou son avatar prédéfini, sa couleur, sa mention « DERNIER PROFIL » pour le dernier utilisé, son badge **ENFANT** ou son cadenas.
- **Clavier, souris ou manette** : les flèches déplacent le focus, `Entrée` ou `A` entre dans le profil, le survol agrandit l’avatar et allume un anneau coloré. Un profil protégé demande son code PIN ; un code incorrect fait trembler la carte et affiche un message, sans quitter l’écran.
- **Ajouter un profil** est accessible directement depuis l’écran d’ouverture, avec la même vérification parentale que dans les réglages. La création demande désormais **un compte IPTV** (M3U, Xtream, portail MAC ou fichier local) dans une étape animée ; annuler cette étape annule le profil. Chaque profil ne voit que ses propres comptes, sans mélange de catalogues.
- **Transition** : l’avatar choisi zoome brièvement, le voile se dissout, l’accueil apparaît en fondu et un toast « Bienvenue, … » confirme l’entrée.
- **Réglage** : *Paramètres → Démarrage → Afficher « Qui regarde ? » au démarrage*. Désactivé, LyaranTV ouvre directement le dernier profil. L’option *Réduire les animations* raccourcit le générique et supprime les mouvements.

Le mode diagnostic `--profiles` force l’affichage de cet écran pour les captures ; les autres modes de capture et l’auto-test l’ignorent.

## Navigation salon

La manette Xbox est détectée automatiquement via XInput, sans pilote supplémentaire. La télécommande multimédia utilise les touches navigateur et média standard.

- **Croix directionnelle / stick gauche** : déplacement spatial du focus. **A** valide, **B** revient, **Y** ouvre la recherche et son clavier virtuel, **X** ouvre le tiroir des chaînes en lecture, **Start** bascule le plein écran.
- **Gâchettes LB / RB** : chaîne précédente / suivante en lecture. **Stick droit** : tiroir des chaînes.
- **Télécommande** : `BrowserBack` revient, `BrowserHome` ouvre l’accueil, `BrowserRefresh` actualise la source, `MediaPlayPause` lit ou met en pause, `MediaNextTrack` / `MediaPreviousTrack` changent de chaîne.
- **Clavier virtuel AZERTY** : ouvert par `Y` dans la recherche, il écrit dans le champ actif, avec aperçu, retour arrière et validation. La souris reprend la main dès qu’elle bouge.
- **Barre latérale escamotable** : le hamburger en haut à gauche, ou `Ctrl+B`, la fait glisser hors de l’écran ; elle réapparaît au même endroit, dans l’état mémorisé.

## Bibliothèque en mode découverte

La page **Bibliothèque** fonctionne comme une page d'accueil de streaming : un hero plein format présente le contenu sélectionné, les étagères horizontales regroupent les films, les séries et le direct, et les catégories sont accessibles en un survol.

- **Survol** d'une affiche : le hero change immédiatement (titre, synopsis, note, programme en cours, bouton de lecture et favori) sans clic.
- **Rotation automatique** du hero toutes les 8,5 secondes ; elle se met en pause dès que la souris entre dans le hero ou survole une carte.
- **Clavier** : `Tab` place le focus sur les cartes, `←` / `→` passent d'une carte à l'autre dans une étagère, `Entrée` ou `Espace` ouvre le contenu.
- **Molette** au-dessus d'une étagère : défilement horizontal ; le défilement vertical reprend aux extrémités.
- **Filtres** : type de contenu, catégorie, tri, **bascule liste / grille** (mémorisée) et bouton `Tout parcourir` pour afficher la bibliothèque complète en grille paginée.
- **Favoris et historique** : les contenus déjà commencés apparaissent dans une étagère « Reprendre la lecture » avec leur position.

Les affiches et backdrops TMDb remplissent le hero et les cartes ; sans image, un dégradé, les initiales et un filigrane du titre conservent une composition lisible.

## Interface épurée

L'interface privilégie le contenu et retire les informations répétées :

- **Barre latérale** : navigation, playlists, profil et langue, sans compteurs ni numéro de version. Le hamburger la fait glisser hors de l’écran, et `Ctrl+B` produit le même effet.
- **Catégories en rail** : Accueil, Chaînes, Films, Séries et Ma liste, avec libellés à 17 px et réactions au survol, au clavier et à la manette. Le Guide EPG reste dans la barre latérale.
- **Pages de type** : chaque entrée du rail ouvre sa page dédiée (Chaînes en direct, Films, Séries, Ma liste) avec un titre, le nombre d'éléments et la source active ; le contenu est rangé en étagères par catégorie du fournisseur.
- **Plein écran application** : `F11` ou le bouton de la barre supérieure masque le cadre Windows et occupe tout le moniteur ; `Échap` ou `F11` restaure la fenêtre et sa position.
- **Filtres intégrés** : `Tout`, `Direct`, `Films`, `Séries` et `Effacer les filtres` restent des onglets texte soulignés par l'accent dans la bibliothèque complète. Ils disparaissent des pages Chaînes, Films, Séries et Ma liste, où le type est déjà porté par le rail.
- **Titres lisibles** : les préfixes de playlist (`FR - `, `EN - `) et l'année finale sont retirés à l'affichage ; le nom d'origine reste utilisé pour la recherche et TMDb.
- **Cartes calmes** : note discrète, pas de badge de type, méta réduite à l'année, à la saison/épisode ou à la reprise.
- **Typographie** : toutes les petites informations sont remontées à **12,5 px minimum**.
- **Barre d'état** : page à gauche, source active à droite.

## Logo

Le logo actuel est la **proposition n° 24 · Voile**, retenue sans le triangle de lecture : deux voiles superposés, blanc et cyan, sur le carré arrondi violet-bleu historique. La planche de contrôle est [`Docs/logo-final.png`](Docs/logo-final.png). **Quarante variantes vectorielles** restent disponibles en deux planches : [`Docs/logo-proposals.png`](Docs/logo-proposals.png) pour les propositions **01 à 20**, [`Docs/logo-proposals-2.png`](Docs/logo-proposals-2.png) pour les propositions **21 à 40**. Elles se régénèrent avec :

```bat
Dist\LyaranTV.exe --logogallery "%TEMP%\logos.png"
Dist\LyaranTV.exe --logogallery2 "%TEMP%\logos-2.png"
Dist\LyaranTV.exe --logofinal "%TEMP%\logo-final.png"
Dist\LyaranTV.exe --makeicon Sources\LyaranTV.ico
```

Aucune variante ne remplace le logo sans un choix explicite.

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
   └─ Logos\              pistes de logo et planches des quarante propositions
```

Les dossiers `Docs\`, `Assets\`, `Installer\`, `Tools\`, `build\` et `VLC\` ne
sont pas nécessaires à l'exécution : seul `Dist\LyaranTV.exe` et, pour
l'exemple, `Examples\` sont utilisés au lancement. L'installateur copie lui-même
libVLC à côté de l'application installée.

## Tests

```bat
Dist\LyaranTV.exe --selftest
```

Le rapport est écrit dans `%TEMP%\LyaranTV-selftest.log`. Les **35 auto-tests** couvrent notamment le nettoyage et les correspondances TMDb prudentes, la recherche par titre original/casting/réalisation/genres/synopsis, l’isolation par profil et source, les pistes audio et sous-titres, l’apparence des sous-titres, l’extraction des notes de version, la sauvegarde et les photos de profil.

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

L'enrichissement part **en parallèle (4 requêtes)** et traite d'abord les contenus visibles sur l'accueil (hero, reprises, nouveautés, films et séries). Une correspondance exacte titre/année est préférée ; deux fiches de même titre sans année sont départagées par popularité. La correction manuelle (**clic droit → Corriger TMDb**) recherche le titre, affiche les résultats avec leur score de correspondance et mémorise le choix par profil et par source.

Les images distantes sont lues à la demande depuis TMDb ou depuis les URL `tvg-logo` de votre playlist. Elles sont décodées à 420 px de large, gardées en mémoire (environ 220 images) et conservées dans un cache disque borné sous `%APPDATA%\LyaranTV\covers` ; les fichiers de plus de 45 jours sont purgés et le cache est ramené à environ 1 200 images. Les affiches des contenus visibles sont **préchargées en priorité** pour éviter les cadres vides. Ce cache se vide depuis **Paramètres → Métadonnées et sous-titres**.

## Affiches et synopsis de secours

TMDb reste la source principale des affiches et des synopsis. Quand il n'y a pas de clé ou pas de correspondance, LyaranTV complète automatiquement les fiches, sans compte ni clé :

- **Séries et épisodes** : [TVmaze](https://www.tvmaze.com/api) (affiche, synopsis, saisons, captures d'épisodes), puis IMDb pour l'affiche.
- **Films** : API de suggestion d'IMDb pour l'affiche, iTunes pour les films, Wikipédia pour le synopsis.

Trois sources facultatives prennent le relais lorsqu'une affiche manque encore. Elles demandent une clé personnelle, saisie dans **Paramètres → Métadonnées et sous-titres → Sources d'affiches** ou fournie par variable d'environnement :

| Source | Variable | Apport |
|---|---|---|
| [OMDb](https://www.omdbapi.com/apikey.aspx) | `LYARANTV_OMDB_KEY` | Affiches et notes IMDb de secours. |
| [Fanart.tv](https://fanart.tv/get-an-api-key/) | `LYARANTV_FANART_KEY` | Affiches et arrière-plans haute définition. |
| [TheTVDB](https://thetvdb.com/api-information) | `LYARANTV_TVDB_KEY` | Séries anciennes ou peu référencées. |

ThePosterDB, MoviePosterDB, IMP Awards et Cinematerial restent des sources de recherche manuelle : aucune clé n'est requise, mais aucun scraping fragile n'est effectué. Les résultats sont mis en cache dans `%APPDATA%\LyaranTV\artwork-cache.json` et les images dans `covers`. Les recherches partent en arrière-plan, à faible cadence, uniquement pour les contenus sans affiche. Si aucune source ne répond, une vignette dessinée (dégradé, initiales, titre) est affichée : une carte n'est jamais vide. Le cache se vide depuis **Paramètres → Métadonnées et sous-titres**.

## Mode langue

Dans **Paramètres → Mode langue**, activez l'option puis saisissez un code (`FR`, `EN`, `AR`, `ES`…). Seules les catégories dont le nom contient ce code restent visibles, ce qui guide un compte IPTV multilingue vers les groupes de la langue voulue. Le réglage est enregistré : il reste actif en revenant à l'accueil, à la bibliothèque ou après un redémarrage. « Tout afficher » le désactive.

## Sous-titres en ligne (OpenSubtitles)

La recherche en ligne est optionnelle. Créez une clé API gratuite sur opensubtitles.com, puis collez-la dans **Paramètres → Métadonnées et sous-titres**, ou définissez :

```bat
set LYARANTV_OPENSUBTITLES_KEY=votre_cle
```

Dans le lecteur, le bouton des sous-titres propose « Chercher sur OpenSubtitles… » : le titre du film — ou de la série, avec saison et épisode — est prérempli, les fichiers sont listés avec leur langue et leur nombre de téléchargements, puis le fichier choisi est téléchargé en SubRip dans `%APPDATA%\LyaranTV\sous-titres` et appliqué immédiatement. Aucune requête n'est envoyée sans action de votre part. Les délais réseau sont bornés et, si l'API ne répond pas, un message explicite invite à réessayer au lieu de laisser « Recherche… » indéfiniment. Les fichiers restent soumis aux conditions d'OpenSubtitles.com.

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

Les mots de passe Xtream et les adresses MAC sont protégés par DPAPI pour l'utilisateur Windows courant. L'export JSON portable omet volontairement les mots de passe, adresses MAC, codes PIN et clés TMDb/OpenSubtitles/OMDb/Fanart.tv/TheTVDB. La sauvegarde complète `.lyaran` conserve les identifiants chiffrés.

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
