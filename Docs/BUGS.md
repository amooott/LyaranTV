# Bugs, corrections et limites

## Corrections effectuées

| Problème | Correction |
|---|---|
| Texte saisi invisible dans certains champs et thèmes | Suppression du double rembourrage du `PART_ContentHost`, marges nulles et couleurs de texte explicites. Vérifié par l'auto-test « Thèmes, champs de saisie et scrollbars ». |
| Barres de défilement natives incohérentes | Scrollbars personnalisées de 6 px (10 px au survol), pouce arrondi, sans flèches, avec fade, molette, clavier et restauration de position. |
| Statut de la barre inférieure obsolète après navigation | Ajout de `SetPageStatus()` et mise à jour dans `Navigate`, `ShowHome`, `ShowLibrary` et `ShowSearch`. |
| `NullReferenceException` dans le lecteur lors d'une reconstruction de l'interface | Gardes sur `player`, `playerScrubber`/`scrubFill` et `totalText` dans `PlayerOpened()`. |
| Journaux de débogage temporaires dans les fiches et le hook `--detail` | Supprimés. Seuls les journaux d'erreurs utiles restent. |
| `--example --page …` pouvait être écrasé par la fin du chargement asynchrone | Navigation différée via `pendingPageAfterLoad`, appliquée après l'indexation. |
| Cache d'affiches et de logos écrit sur le disque | Les images distantes sont lues à la demande, conservées en mémoire et dans un cache disque borné sous `%APPDATA%\LyaranTV\covers` (420 px, purge à 45 jours) ; le cache se vide depuis les paramètres. Aucun visuel n'est fourni avec le programme. |
| Affiches et logos distants jamais affichés malgré le téléchargement | Le décodeur utilisait `BitmapCreateOptions.IgnoreImageCache` avec `StreamSource`, ce qui levait une `ArgumentNullException` interne à WPF (`ImagingCache.RemoveFromCache`). L'option a été retirée : les images sont décodées depuis un flux mémoire puis figées. Vérifié en conditions réelles (TMDb et logo distant). |
| Absence de test de régression sur les images | Auto-test « Images en ligne décodées en mémoire » : encodage PNG, décodage mémoire, image figée, cache mémoire vide au départ. |
| Logos longs à apparaître dans une très grande bibliothèque | Les demandes d'images étaient traitées dans l'ordre de création, y compris celles des vues déjà quittées. La file est désormais une pile LIFO : les images de la vue courante passent en premier, avec vérification du cache avant téléchargement. Vérifié sur une playlist de 99 012 éléments. |
| Favoris, historique et reprise mélangés entre profils | Fichiers suffixés par profil ; le profil Principal conserve les noms historiques. |
| Ancien accent ambre après mise à jour | Migration automatique de `#F2A33C` vers `#8B5CF6` dans `AppSettings.Normalize()`. |
| Fiches TMDb sans affiche ou sans correspondance | Repli graphique, recherche manuelle TMDb et cache de métadonnées. |
| Sous-titres absents ou illisibles | Analyse SRT/WebVTT, détection des sidecars, bouton `CC`, touche `S`, nettoyage HTML/ASS. |
| Pistes audio et sous-titres invisibles dans le lecteur | Détection des pistes MKV/MP4 (`MediaTracks`, `MkvTracks`, `Mp4Tracks`), menu audio (`A`) et menu sous-titres (`S`) ; extraction en mémoire des pistes texte et affichage superposé. Vérifié sur des fichiers MKV et MP4 synthétiques par l'auto-test « Pistes embarquées MKV et MP4 » et en conditions réelles avec une piste `tx3g`. |
| Films et épisodes sans son | Cause : Windows Media Foundation ne décode pas l'AC-3, l'E-AC-3, le DTS ni le TrueHD, fréquents dans les MKV VOD ; le film s'affichait muet (`0xC00D109B` dans le journal). Le lecteur utilise désormais **libVLC** (installé avec VLC) : décodage audio universel, changement de piste à la volée, rendu vidéo en mémoire pour garder les contrôles et les sous-titres au-dessus de l'image. MediaElement reste le repli, et le lecteur bascule seul sur l'autre moteur si le premier refuse le flux. Réglage : Paramètres → Lecture → Moteur de lecture. Vérifié sur un MKV AC-3 5.1 en local et en HTTP via le proxy (image, position, reprise, pistes listées). |
| Déplacement de 10 s au lieu de 5 s | `MainWindow.SeekStepSeconds` fixé à 5 ; flèches gauche/droite et outil de timeline alignés. |
| Saut de 5 s ignoré lorsque la lecture est en pause | `MediaElement` ramenait la position à 0 après un `Position` défini en pause. `SeekRelative` relance brièvement la lecture, applique le saut puis restaure la pause 120 ms plus tard. Vérifié en pause et en lecture sur une vidéo de 39 s. |
| Plein écran incomplet, avec barre des tâches et chrome de l'application | Fenêtre sans bordure ni barre de titre, positionnée sur le moniteur courant avec `MonitorFromWindow`/`GetMonitorInfo` et placée au-dessus de la barre des tâches. Le chrome est masqué, les contrôles se superposent à la vidéo puis s'effacent après 3,5 s ; `F11`, `Échap` ou l'icône de retour restaurent la taille et l'état précédents. Vérifié en 1920 × 1080 puis après restauration. |
| Sous-titres recouverts par la barre de contrôle | Le sous-titre est remonté à 100 px du bas en fenêtré et 118 px en plein écran tant que les contrôles sont visibles, puis revient à 18 px lorsqu'ils disparaissent. Vérifié avec une piste SRT affichée pendant et après l'auto-masquage. |
| Affiches de films et de séries qui ne remplissaient pas leur cadre | Deux causes : le cadre d'image mesurait 150 × 224 px (ratio 0,67 approximatif) et le repli affichait le logo avec une marge de 24 px, laissant des bandes vides. Le cadre est désormais 170 × 258 px (2:3 strict) dans une carte de 186 px et l'image occupe toute la surface sur les cartes portrait. Vérifié par la mesure du layout WPF (carte 186 × 316, cadre 170 × 258, image 170 × 255) et par capture de la bibliothèque. |
| Le bouton Retour ne quittait pas le lecteur | Le bouton Retour, `Échap` et le bouton de la barre supérieure du lecteur quittent désormais la lecture et retrouvent l’interface avec la page, les filtres et la position de défilement d’origine. Le retour au dernier point de saut reste disponible avec `Retour arrière` et dans le menu contextuel (`ReturnToPreviousPoint`). Vérifié en lançant une lecture depuis l’accueil puis en appuyant sur `Échap`. |
| Contrôles du lecteur séparés sous la vidéo, jamais masqués en fenêtré | Les contrôles sont superposés à la vidéo en permanence (fenêtré comme plein écran), avec un dégradé en bas de l'image. Ils s'effacent après 3,5 s en lecture, restent affichés en pause et reviennent au mouvement de souris, à une touche ou à l'ouverture d'un menu. Vérifié par captures successives : visibles, absents 4,3 s après le dernier mouvement, puis revenus. |
| Menu `S` ou `A` ouvert alors que les contrôles étaient masqués | Les contrôles sont réaffichés avant l'ouverture du menu, y compris en plein écran, pour que le menu soit ancré à un bouton visible. |
| Toast ou fiche ouverte après changement de page | Gardes et vérification du contexte avant affichage. |
| Liste déroulante du type de playlist : seul le trait de la flèche réagissait au clic | Le bouton d'ouverture couvre désormais tout le champ (fond transparent pour la zone de test) et la liste s'ouvre sur toute la largeur, sous le champ. Vérifié sur les quatre types et par l'auto-test « Thèmes, champs de saisie et scrollbars ». |
| Échec réseau au chargement d'une playlist affiché comme « erreur inattendue » | Le gestionnaire de fin de chargement lisait `e.Result` avant de tester `e.Error`, ce qui levait une `TargetInvocationException`. `e.Error` est désormais traité en premier : l'erreur est journalisée, affichée dans la fenêtre de chargement puis en notification lisible. Les délais sont portés à 60 s pour une playlist M3U, 45 s pour l'EPG et Xtream, 30 s pour Stalker. |
| `Lecture impossible : … 0xC00D1198` sur des flux HTTP/HTTPS | WPF `MediaElement` envoie `User-Agent: NSPlayer/12…`, refusé par certains fournisseurs (HTTP 401/403). Un proxy local (`Sources/Core/StreamProxy.cs`) rejoue la requête avec un User-Agent de navigateur, puis VLC, puis `NSPlayer` en dernier recours, et relaie les requêtes `Range`. Les en-têtes `#EXTVLCOPT:http-user-agent`, `http-referrer`, `http-origin`, `http-cookie` et les suffixes `|User-Agent=…&Referer=…` sont lus par l'analyseur M3U. Vérifié avec un serveur local qui refuse `NSPlayer` et par les auto-tests « Proxy de lecture et User-Agent » et « En-têtes de flux M3U ». |
| Attente indéfinie sur « Connexion au flux… » | Sonde de flux préalable (8 s) puis délai d'ouverture de 20 s, message explicite et journalisation `Lecture échouée (hôte.ext) : …` sans URL complète ni jeton. |
| Message d'erreur renvoyant systématiquement vers un lecteur externe | Messages réécrits : vérifier la source ou réessayer. Le bouton de lecteur externe reste disponible mais n'est plus présenté comme la solution obligatoire. |
| Flux `.m3u8` essayé en premier alors que `MediaElement` ne le décode pas | `Sources/Core/StreamPlanner.cs` ordonne les liens : MPEG-TS, MKV et MP4 progressifs d'abord, HLS en dernier. Vérifié par l'auto-test « Priorité des flux ». |
| Barre de progression invisible dans la fenêtre de chargement | Le `DockPanel` donnait une largeur nulle à la piste (dernier enfant en remplissage). Remplacé par une `Grid` deux colonnes (piste étoilée + pourcentage auto). |
| Estimation du temps restant démesurée au démarrage | La progression étant pondérée par étapes, le pourcentage initial est faible. Le calcul utilise un plancher de 30 % et un lissage 0,72/0,28 ; l'affichage reste masqué sous 1,2 s. |
| Bibliothèque longue à s'ouvrir sans retour visuel sur un gros compte | Un écran de chargement animé s'affiche après 150 ms si la préparation n'est pas terminée ; le filtrage et le tri sont calculés hors du fil d'interface. La barre d'état est restaurée à la fin. Mesuré sur 239 662 éléments : 429 ms au total, dont 232 ms de rendu, au lieu de 1 085 ms bloquantes. Vérifié par captures de la page de chargement et de la bibliothèque finale. |
| Barre d'état figée sur « Guide EPG · chargement en arrière-plan… » | Après le chargement du guide, la barre latérale est reconstruite (compteur EPG), `SetPageStatus()` restaure le libellé de la page et `SetPageStatus` gère désormais la page « Lecture ». |
| Une lecture lancée pendant le chargement était remplacée par la bibliothèque | `ApplyLoadResult` conserve la page « Lecture » si un contenu est déjà en cours (`keepPlayer`). |
| Bibliothèque dense, peu lisible et dépendante du clic | Refonte en mode découverte : hero cinématique avec rotation automatique et aperçu au survol, étagères horizontales, cartes animées, filtres rapides, vue « Tout parcourir » et navigation clavier (`Tab`, `←` / `→`, `Entrée` / `Espace`). Les contenus en cours apparaissent dans une étagère « Reprendre la lecture ». Vérifié avec la démonstration, le survol d'une affiche, la rotation automatique et le passage à la grille complète. |
| Seules les neuf premières catégories étaient visibles | `BuildLibraryCategories` limitait la liste avec `Take(9)` et la rangeait dans un défilement horizontal. L'exploration est désormais divisée en trois blocs verticaux (chaînes, films, séries), sans limite, avec des cartes qui s'enroulent sur plusieurs lignes au fil du défilement. Vérifié avec 15 catégories de chaînes, 12 de films et 12 de séries. |
| « Caractères non conformes dans le chemin d'accès » à l'ajout d'une playlist | `TextUtil.ResolveUrl` appelait `Path.IsPathRooted` sur des URL relatives : une entrée contenant un `|` (en-tête M3U mal formé) levait une `ArgumentException` et faisait échouer tout le chargement. L'appel est désormais protégé et l'entrée est conservée telle quelle. Régression vérifiée par l'auto-test « Analyse M3U ». |
| Historique conservé après la suppression d'une playlist | Les contenus étaient suivis par identifiant sans propriétaire. `history-sources.json` associe désormais chaque contenu à sa playlist ; la suppression purge `recent.txt` et `positions.json` de **tous les profils**, ainsi que `durations.json` et `episodes.json`, sans toucher aux autres playlists. Les identifiants connus de la bibliothèque complètent le suivi pour les entrées antérieures. Vérifié par l'auto-test « Suppression de l'historique d'une playlist ». |
| Pistes audio ou sous-titres sans langue ni type | Les flux distants publient souvent une langue `und`, un nom vide ou aucun canal. Les métadonnées libVLC sont complétées par l'analyse locale (`AudioStream.Enrich`), et le nombre de canaux est déduit du nom de piste lorsqu'il manque (`MediaTrackScanner.InferChannels` : « 5.1 », « 7.1 », « 3/2 », « 6ch », « stéréo »…). Chaque piste affiche donc sa langue et son format — `Anglais · 5.1 · E-AC-3` — et chaque fichier voisin de sous-titres sa langue, sa version et son format (`Français · VOSTFR · SubRip`). Vérifié par les auto-tests « Pistes audio et sous-titres en ligne » et « Sous-titres SRT et WebVTT », et par capture du menu `A` sur un MKV E-AC-3 5.1. |
| Sous-titres intégrés d'un flux ou d'un MKV impossibles à choisir | Le menu `S` liste désormais les pistes de sous-titres réelles fournies par libVLC (`libvlc_video_get_spu_description` + métadonnées du média), avec langue et codec (`Français · PGS`), et les active à la volée sans relancer la lecture. L'affichage texte interne est coupé pour éviter les doublons. Repli sur l'analyse locale (MKV/MP4) quand VLC n'est pas disponible ; toute erreur de la couche VLC est contenue et le menu reste utilisable. |
| Fenêtre de chargement ouverte sur un cadre vide | Les affiches ne sont plus exposées dans la fenêtre de chargement : une étape dédiée « Chargement des affiches de films et séries » affiche son état (en attente, mise en cache, puis ✓ affiches prêtes) pendant que les visuels sont demandés en arrière-plan. La fenêtre est redimensionnée en conséquence (5 étapes). Vérifié par compilation et par capture de l'écran de démarrage. |
| Bande noire sous la vidéo en plein écran | Deux causes. La hauteur de l'image était calculée à partir du viewport du défileur, encore périmé juste après le masquage de l'en-tête et du pied de page ; elle est désormais mesurée sur la grille racine (`rootGrid.ActualHeight`), réappliquée après le passage de disposition (`DispatcherPriority.Loaded` puis `Render`) et à chaque redimensionnement. Quand la fenêtre était **maximisée** avant le passage en plein écran, Windows pouvait aussi conserver la taille de l'espace de travail (barre des tâches exclue) : `EnsureFullScreenBounds()` réimpose les bornes du moniteur après le premier passage de disposition. La barre de défilement est masquée pendant la lecture plein cadre. Vérifié avec un profil « fenêtre maximisée » : capture de la fenêtre 1920 × 1080, image et contrôles jusqu'au bord inférieur. |
| « Cette piste de sous-titres n'a pas pu être activée » à la sélection | Le code retour de `libvlc_video_set_spu` n'est pas fiable et certaines pistes listées venaient des métadonnées du média avec un identifiant qui n'était pas un identifiant SPU. Seuls les identifiants issus de `libvlc_video_get_spu_description` sont proposés ; la sélection est confirmée en relisant `libvlc_video_get_spu` sur deux secondes, et pour les fichiers locaux texte c'est le rendu interne (avec décalage réglable) qui prend le relais. Un échec réel est journalisé (`Sous-titres VLC : piste … non activable`). |
| Liste des épisodes d'une série impossible à faire défiler | `SetContent` désactivait la barre de défilement verticale sur la page « Épisodes » (`ScrollBarVisibility.Disabled`), ce qui bloquait aussi la molette. La barre est de nouveau automatique sur toutes les pages : les longues saisons défilent à la molette, au glisser et au clavier. |
| Carrousel d'accueil sans navigation manuelle | La rotation automatique obligeait à attendre pour voir les autres recommandations. Deux flèches rondes (précédente / suivante) sont posées sur la bannière, au-dessus du contenu, et les flèches du clavier parcourent aussi le défilé depuis l'accueil. |
| « Préparation de la playlist » imposée à chaque lancement | Le catalogue et le guide EPG sont désormais enregistrés dans une copie locale binaire (`library-<empreinte>.bin`, version 3). Au démarrage, la bibliothèque est relue en quelques dixièmes de seconde (`TryRestoreLibrary`) ; le chargement réseau n'a lieu que si le cache est absent, si la source a changé (adresse, compte, identifiants, taille ou date du fichier M3U) ou si la copie dépasse 24 h. Dans ce dernier cas, la page s'ouvre immédiatement sur la copie existante, une notification « Bibliothèque du … · actualisation en arrière-plan… » apparaît, et l'actualisation se fait sans fenêtre modale ni changement de page ; en cas d'échec, la bibliothèque en cache reste utilisée. `RefreshActiveSource` (bouton d'actualisation) force toujours un vrai chargement, et la suppression/restauration/réinitialisation efface les copies locales. Vérifié avec `LYARANTV_DATA_DIR` : premier lancement (création du cache), lancement suivant à 1,5 s (bibliothèque, affiches et 336 programmes EPG affichés, aucune fenêtre de chargement), puis cache antidaté de 30 h (restauration immédiate + actualisation silencieuse). |
| « Préparation de la playlist » à chaque clic sur une playlist | Seul le démarrage automatique consultait la copie locale ; un clic dans la barre latérale relançait donc la préparation complète. `Activate(source)` tente désormais `TryRestoreLibrary` en premier : la bibliothèque s'ouvre instantanément, et le chargement réseau n'a lieu que si le cache est absent, périmé (24 h) ou si la source a changé — le cas échéant en arrière-plan, sans fenêtre modale. Vérifié avec `LYARANTV_DATA_DIR` : premier passage (cache écrit), second passage à 1,6 s (bibliothèque complète, notification « Bibliothèque restaurée instantanément »). |
| Bande d'image claire sous la barre de commandes en plein écran | La barre flottait à 14 px du bord (`Margin` bas) : le dégradé s'arrêtait au-dessus du bord et laissait apparaître une bande d'image non assombrie, qui donnait l'impression d'une vidéo mal dimensionnée. La barre est désormais collée au bord (marge nulle, rembourrage interne de 30/64/30/18) et le dégradé devient **opaque** sur son dernier pixel. Vérifié par capture écran 1920 × 1080 en plein écran : les derniers pixels (y = 1078, 1079) sont noirs, plus aucune bande claire. |
| Volume peu lisible et encombrant | Le rail permanent du volume a été remplacé par une carte flottante qui se déploie au survol du haut-parleur : intitulé « VOLUME », pourcentage courant, et un curseur dans le même langage que la barre de lecture — rail fin blanc translucide (la carte reste sombre dans tous les thèmes), remplissage plein dans la couleur d'accent, pastille blanche lumineuse qui grandit au survol et anneau accent pendant le glissement. Aucun halo superflu sur la piste : lignes nettes, un seul accent de couleur. La carte est posée dans la surface vidéo (et non dans la barre, dont la hauteur fixe l'écrasait) et se repositionne au-dessus du bouton en plein écran comme en fenêtre. Vérifié par capture du lecteur : carte 192 × 73, rail continu, pastille centrée sur la fraction exacte. |
| Défilé sans bannière pour les films et séries | Les étagères de l'accueil affichent maintenant des cartes au format bannière (280 × 158) dès qu'un visuel existe : image panoramique TMDb en priorité (`BackdropUrl`, puis capture d'épisode), sinon l'affiche réelle posée sur sa propre version floutée, avec le titre à gauche. Les rangées sans aucun visuel restent en affiches verticales. |
| Aucune correspondance TMDb pour les titres préfixés | Les playlists IPTV préfixent les titres par la langue (`IT - `, `FR - `, `EN | `), ce qui empêchait toute recherche et laissait les contenus sans affiche ni bannière. `TmdbTitleCleaner.LeadingTagRegex` accepte désormais un tiret ou un point médian après le code de langue. Vérifié par un nouvel auto-test « Nettoyage des titres ». |
| Bannière d'accueil en simple dégradé | Le défilé utilisait un fond flou ou un dégradé quand aucun backdrop n'existait. Il affiche maintenant la vraie image du contenu — backdrop, sinon capture d'épisode, sinon affiche recadrée en bandeau — derrière le titre ; la jaquette dessinée reste le repli quand aucun visuel n'est connu. |
| Gel de plusieurs secondes au lancement d'un film ou d'une série | Trois causes cumulées, mesurées sur un compte de 239 662 éléments. 1) `StateStore.RecordHistorySources` relisait puis réécrivait `history-sources.json` (9,7 Mo) à chaque lecture, sur le fil d'interface : 471 ms de lecture + 168 ms d'écriture. La carte est désormais gardée en mémoire et l'écriture décalée de 1,5 s en arrière-plan (vidée à la fermeture). 2) La file de lecture recopiait tout le catalogue ; elle est limitée à une fenêtre autour du contenu lancé. 3) `FindChannel` et `SeriesForEpisode` parcouraient linéairement les 240 000 entrées, à chaque carte de l'accueil : un index `Dictionary` est construit une fois par chargement. En complément, l'instance libVLC est partagée et préchauffée, et l'inventaire des sorties audio ne bloque plus le fil d'interface. Mesure après correction : ouverture du lecteur en **371 ms** (`Perf lecture`) et aucun gel de plus de 350 ms au lancement ; accueil de **2 227 ms à 928 ms**. Vérifié par `LYARANTV_UIPROBE=1` et par une lecture réelle avec capture du lecteur. |
| Affiches qui restent vides malgré le cache disque | Deux causes cumulées. 1) Le démarrage activait un drapeau **TLS 1.3** (`12288`) que .NET Framework ne sait pas négocier : ajouté à `ServicePointManager.SecurityProtocol`, il rendait muettes les connexions HTTPS internes. Il est retiré. 2) Sur un réseau qui annonce une IPv6 sans la router, `HttpWebRequest` attendait ~20 s avant de basculer en IPv4, alors que les jaquettes abandonnaient à 8 s : **aucune** image ne se téléchargeait. `HttpUtil.GetBytes` utilise désormais un client **HTTPS IPv4 d'abord** (TLS 1.2 avec SNI, redirections, chunked, gzip, cache DNS), avec repli sur le moteur historique derrière un proxy. Une jaquette se charge en **~250 ms**. S'y ajoute une file de préchargement de fond (6 images/120 ms) alimentée par l'accueil puis par la bibliothèque, prioritaire sur les cartes visibles, et une attente réelle des jaquettes du premier écran dans la fenêtre de chargement avec progression (`mise en cache · 36/40`). Vérifié à cache vide : 36/40 jaquettes en 2 s (4 URL TMDb supprimées, HTTP 404), 98 images en cache après 8 s, interface en 1,5 s ; accueil et bibliothèque capturés avec les affiches en place. |

## Limites connues

### Lecture intégrée

- L'erreur `0xC00D1198` liée au refus du User-Agent `NSPlayer` est traitée par le proxy local. Sur certaines installations Windows 11, un flux **HTTPS** peut néanmoins provoquer une `NullReferenceException` interne dans `System.Windows.Media.MediaPlayerState.OpenMedia`. Les fichiers locaux et les flux HTTP fonctionnent. Utilisez un lecteur externe (VLC, PotPlayer) pour ces flux.
- Les flux HLS (`.m3u8`) ne sont pas décodés nativement par `MediaElement` sur Windows desktop ; avec le moteur VLC ils sont lus normalement.
- Le moteur VLC utilise un décodage et une conversion logiciels (rendu mémoire, nécessaire pour superposer les contrôles WPF) : environ 20 % d'un cœur et 300 Mo pour un film 1080p. Sans VLC installé, le lecteur revient à Media Foundation et les pistes AC-3/DTS restent muettes.
- Certains fournisseurs lient le flux à une session, une adresse IP ou un jeton à durée limitée et renvoient HTTP 401/403 malgré un User-Agent valide. Le proxy ne contourne pas ces protections.
- Les codecs disponibles dépendent de Windows Media Foundation et des composants installés. Les formats exotiques peuvent nécessiter VLC.
- Le lecteur intégré ne gère pas les DRM ni les menus Blu-ray. Avec le moteur VLC, la piste audio se change directement dans le lecteur ; avec Media Foundation, les pistes suivantes s'ouvrent dans VLC. Les sous-titres image (PGS/VobSub) sont listés comme non pris en charge.
- Le plein écran et le PIP dépendent de la fenêtre WPF ; le PIP natif Windows n'est pas exposé.

### Images et métadonnées

- Les affiches TMDb nécessitent une clé personnelle. Sans clé, les vignettes de remplacement sont utilisées.
- Les logos de chaînes proviennent du champ `tvg-logo` de la playlist. LyaranTV n'invente pas de logo pour une chaîne qui n'en fournit pas.
- Les images sont chargées en ligne : sans connexion, les replis graphiques s'affichent. Un cache disque borné (`%APPDATA%\LyaranTV\covers`, 420 px, purge à 45 jours) accélère les affichages suivants ; il ne contient aucune image fournie avec le programme.
- Le cache TMDb contient uniquement des métadonnées textuelles et des URL ; il ne contient pas les images.
- TMDb peut renvoyer une correspondance approximative pour un titre mal nommé ; la recherche manuelle permet de corriger.

### EPG

- Le guide fictif `Examples/lyarantv-guide.xml` est daté autour du 28 octobre 2026. Après cette date, les programmes apparaissent comme passés.
- Les guides XMLTV très volumineux peuvent ralentir l'indexation ; ils sont désormais téléchargés **après** l'ouverture de la bibliothèque, en arrière-plan, et le compteur du menu se met à jour à la fin.
- Les fuseaux horaires XMLTV sont interprétés selon les informations du fichier ; certains fournisseurs produisent des guides incohérents.

### Plateformes et fonctions P2

Les fonctions suivantes ne sont pas réalisables nativement en WPF/.NET Framework sans API ou composants supplémentaires :

- AirPlay, Google Cast, Miracast intégré.
- Picture-in-Picture natif, widgets de bureau, Live Tiles.
- Téléchargements hors ligne avec gestion de DRM.
- Applications mobiles Android/iOS et versions TV.
- Synchronisation cloud multi-appareils avec compte.
- OpenSubtitles, Plex, SMB, WebDAV, DLNA.
- Multi-écrans simultanés.

Ces éléments sont documentés comme travail futur ou nécessitent un lecteur externe / une intégration système.

### Interface et accessibilité

- L'interface est en français. Une traduction anglaise complète n'est pas encore disponible.
- Les accents personnalisés peuvent produire des contrastes variables ; le texte des boutons à fond accent est choisi automatiquement (encre sombre ou blanche), mais les textes accent sur fond sombre restent à vérifier selon la couleur choisie.
- Les animations sont réduites via une option, mais certaines transitions système de Windows restent visibles.

### Tests et captures

- `--screenshot` ne capture que la fenêtre principale, pas les dialogues modaux.
- Les dialogues WPF transparents peuvent ne pas apparaître dans l'énumération UIAutomation standard ; les tests visuels utilisent une capture d'écran.
- Les tests automatiques n'accèdent pas à un fournisseur IPTV réel ; ils utilisent des données fictives et des fichiers locaux.
- L'auto-test « Pistes embarquées MKV et MP4 » utilise deux fichiers minimaux en base64 ; il vérifie la détection des langues/codecs et l'extraction des cues, pas le rendu graphique ni la sélection dans VLC.
- La lecture HTTPS dépend de l'environnement et n'est pas couverte par les auto-tests hors ligne.

## Diagnostic

Journal local :

```text
%APPDATA%\LyaranTV\error.log
```

Auto-test :

```bat
LyaranTV.exe --selftest
```

Journal d'auto-test :

```text
%TEMP%\LyaranTV-selftest.log
```

En cas d'échec de lecture, `error.log` contient `Lecture échouée (hôte.ext) : …` et, le cas échéant, `Proxy de lecture : hôte a refusé la connexion (HTTP 401/403)` ou `hôte injoignable`. Aucune URL complète ni aucun jeton n'est journalisé.

Pour isoler les données et reproduire un problème :

```bat
set LYARANTV_DATA_DIR=%TEMP%\lyarantv-debug
LyaranTV.exe
```

Ne joignez jamais de mots de passe, d'adresses MAC, de clés TMDb ou de playlist privée à un rapport de bug.
