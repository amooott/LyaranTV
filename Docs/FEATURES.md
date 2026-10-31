# Fonctionnalités de LyaranTV

Statut : **P0 terminé**, **P1 terminé**, **P2 partiel / non réalisable nativement en WPF**. Aucune fonctionnalité payante, aucun compte obligatoire, aucune télémétrie.

## P0 — Fondations

| Fonction | État | Détails |
|---|---|---|
| Sources M3U/M3U8 | ✅ | Fichier local ou URL, attributs `tvg-*`, catégories, logos. |
| Xtream Codes | ✅ | Connexion, catégories live/VOD/séries, identifiants chiffrés. |
| Stalker / MAC | ✅ | Portail, adresse MAC, type d'appareil. |
| Démonstration | ✅ | Bibliothèque fictive intégrée. |
| Lecteur intégré | ✅ | Double moteur : libVLC (audio AC-3, E-AC-3, DTS, TrueHD, changement de piste à la volée, rendu vidéo en mémoire pour garder les contrôles au-dessus de l'image) et MediaElement en repli, avec bascule automatique d'un moteur à l'autre si le premier refuse le flux. Sonde de connexion, proxy local avec User-Agent/Referer/Origin/Cookie, ordre TS → MP4/MKV → HLS, reconnexion du direct, pause, volume, plein écran, format d'image, reprise, raccourcis ±5 s, menus pistes audio et sous-titres. |
| Chargement des comptes | ✅ | Xtream : compte puis catalogues live/VOD/séries **en parallèle** ; barre de progression lissée, temps écoulé, **temps restant estimé** ; guide EPG en arrière-plan. Fenêtre redessinée (charcoal, liseré accent, étapes colorées, note EPG encadrée) ; **cinquième étape « Chargement des affiches de films et séries »** avec état (en attente, mise en cache, ✓ prêtes), les visuels n'étant pas exposés comme un aperçu ; anneaux de chargement animés dans le lecteur et la page des épisodes. |
| En-têtes de flux | ✅ | `#EXTVLCOPT:http-*` et suffixes `|User-Agent=…&Referer=…` transmis au proxy de lecture. |
| Direct et EPG | ✅ | Chaînes, guide XMLTV, programmes en cours/à venir, navigation par jour. |
| Recherche | ✅ | Recherche temporisée et tolérante aux accents ; résultats regroupés par films, séries, épisodes, chaînes, personnes et activité Letterboxd ; inclut titres originaux, synopsis, genres, casting, réalisation, critiques et notes locales lorsqu’ils sont disponibles. |
| Catégories complètes | ✅ | Toutes les catégories de chaînes, films et séries sont affichées en trois blocs verticaux, sans limite de nombre ; chaque carte filtre le type et la catégorie correspondants. |
| Favoris | ✅ | Ajout/retrait, page dédiée, séparation par profil. |
| Thèmes | ✅ | Sombre, Clair, Noir AMOLED, Automatique. |
| Scrollbars | ✅ | 6/10 px, pouce arrondi, fade, clavier, molette, restauration, bulle de lettre. |
| Champs de saisie | ✅ | Texte visible dans tous les thèmes, focus accent, placeholder. |
| Réglages essentiels | ✅ | Volume, reprise, thème, accent, lecteur externe, démarrage. |
| Corrections de bugs | ✅ | Voir [`BUGS.md`](BUGS.md). |
| Identité LyaranTV | ✅ | Nom, namespace, exécutable, icône, logo, manifeste, données. |

## P1 — Confort et personnalisation

| Fonction | État | Détails |
|---|---|---|
| Accueil billboard | ✅ | Bannière plein cadre (486 px) avec rotation automatique **et flèches précédente / suivante** (souris et clavier) pour parcourir les recommandations sans attendre ; fond constitué de la vraie image du contenu (backdrop, capture d'épisode, sinon affiche recadrée), description, note, boutons Lecture / Plus d’infos / Épisodes / favori et pastilles cliquables. Rangée **En direct** avec programme en cours, Top 10, films, séries et surprise aléatoire. |
| Bibliothèque cinématique | ✅ | Mode découverte avec hero rotatif, aperçu au survol, étagères horizontales et **flèches de défilement au survol**, navigation clavier dans les cartes, filtres et vue « Tout parcourir » en grille. |
| Cartes bord à bord | ✅ | Affiches 2:3 strictement remplies, coins arrondis réellement découpés, badge FILM/SÉRIE, note, titre et méta en dégradé, barre de reprise, élévation et ombre au survol. |
| Retour à la page précédente | ✅ | Historique de navigation : page, filtres et position de défilement restaurés. Le **bouton Retour du lecteur** et `Échap` quittent la lecture vers l’interface ; `Retour arrière` et le menu contextuel ramènent au dernier point de saut. |
| Lecteur superposé façon Netflix | ✅ | Contrôles en bas de la vidéo (fenêtré et plein écran), masquage automatique après 3,5 s en lecture, retour au mouvement de souris, bouton central en pause et bandeau titre en plein écran. |
| Barre de progression interactive | ✅ | Piste de 6 px qui s'épaissit à 10 px au survol, remplissage accentué, curseur lumineux de 15/18 px, infobulle de temps, glissement pour avancer, temps écoulé et durée aux extrémités. |
| Affiches pleine cadre | ✅ | Grandes cartes de films et séries en 2:3 strict (236 × 354 px) : l'affiche ou le logo occupe toute la surface, sans marge ni bande vide ; un recadrage centré minimal s'applique si le ratio diffère. |
| Aperçus d'épisodes | ✅ | Chaque épisode porte un aperçu 16:9 : image du fournisseur Xtream (`movie_image`), sinon capture TVmaze, sinon affiche de la série, sinon vignette dessinée avec le titre. L'aperçu sert aussi de fond à la fiche de l'épisode et aux cartes « Reprendre la lecture ». |
| Affiches de secours | ✅ | Si TMDb n'a pas de correspondance (ou aucune clé n'est fournie), les affiches sont cherchées chez **IMDb** (films et séries) et **TVmaze** (séries), mises en cache dans `artwork-cache.json`. En dernier recours, une vignette dessinée (dégradé, initiales, titre) remplace le cadre vide : aucun contenu n'est sans visuel. |
| Synopsis et infos de secours | ✅ | Synopsis (TVmaze, puis Wikipédia), année, genres, note et durée d'épisode sont complétés quand TMDb ne fournit rien. |
| Affiches et notes TMDb | ✅ | Recherche, cache métadonnées, enrichissement, repli. |
| Fiches de contenus | ✅ | Affiche/hero, année, genres, synopsis, note, actions. |
| Fiches enrichies TMDb | ✅ | Chargement à la demande du titre original, casting, réalisation/création, durée, pays, classification et bande-annonce ; détails conservés dans le cache. |
| Correspondance TMDb | ✅ | Score titre/année, détection des résultats ambigus, correction et réinitialisation manuelles ; correspondances corrigées séparées par profil et source IPTV. |
| Activité Letterboxd locale | ✅ | Import des CSV personnels de liste à voir, notes, journal et critiques ; fiche intégrée avec note, statut vu/à voir et texte complet des critiques, boutons de suivi locaux et données isolées par profil. |
| Avis publics Letterboxd | ✅ | Le nom d’utilisateur du compte suffit; jusqu’à sept profils d’amis sont facultatifs. Notes et critiques RSS récentes rapprochées par titre/année, cache local de six heures et affichage complet dans les fiches sans quitter LyaranTV. |
| Rangées Letterboxd de l’accueil | ✅ | Liste à voir importée et films de la playlist correspondant aux avis RSS récents ; les rangées restent masquées si aucune donnée correspondante n’existe. |
| Section API Letterboxd | ✅ | Le nom public suffit pour lire le RSS récent; les amis sont facultatifs. Liens vers la documentation et l’accès développeur, import CSV et gestion/suppression des données locales. |
| Reprise de lecture | ✅ | Rangée « Reprendre la lecture » sur l'accueil (hero) et dans la bibliothèque : films, chaînes, fichiers locaux **et séries** (épisode en cours), avec affiche, saison/épisode, barre de progression et temps restant. Les épisodes en cours sont mémorisés dans `episodes.json`, donc retrouvés même après un redémarrage. |
| Suppression d’une playlist | ✅ | La suppression efface l’historique, les positions de reprise, les durées et les épisodes en cours rattachés à cette playlist, **dans tous les profils**, sans toucher aux autres playlists. Les favoris et réglages sont conservés. |
| Autres épisodes sous le lecteur | ✅ | Rangée d'épisodes sous le lecteur, ouverte par défaut quand on regarde un épisode : vignettes, progression, badges « EN COURS » / « À SUIVRE », sélecteur de saison, bouton « Épisode suivant », défilement qui se cale sur l'épisode courant et enchaînement automatique à la fin avec compte à rebours annulable. |
| Profils | ✅ | Profils séparés, couleur, profil enfant, changement protégé. |
| Contrôle parental | ✅ | Filtrage par catégories, PIN, déverrouillage temporaire. |
| Export/import | ✅ | JSON portable, sauvegarde `.lyaran`, export M3U. |
| Sous-titres | ✅ | SRT, WebVTT externes et sidecars ; pistes texte embarquées MKV (SubRip, ASS/SSA) et MP4 (`tx3g`, `text`, WebVTT) extraites en mémoire, sélection via `CC` ou `S`. Menu dédié : désactivation, pistes intégrées, fichiers voisins, fichier local, et **recherche OpenSubtitles** avec titre, saison et épisode préremplis. Chaque fichier voisin affiche sa **langue, sa version et son format** (`Français · VOSTFR · SubRip`, `Langue non déclarée · WebVTT`). **Taille (14–28 px), fond sombre et langue préférée** réglables ; la taille augmente automatiquement en plein écran. |
| Outils VLC | ✅ | Vitesse 0,5×–2× (bouton et `[` / `]`), boucle (`Ctrl+L`), **boucle A-B** avec segment sur la timeline (`Maj+A/B/X`), **minuterie de veille** (15–90 min ou fin d’épisode), avance image par image (`,` / `.`), sauts ±10 s (`J` / `L`), format d’image, menu contextuel au clic droit et aide des raccourcis (`?`). |
| Langues de lecture | ✅ | Langue des sous-titres (fr, en, es, de, it, pt, ar), langue des métadonnées, libellés audio complets (VF/VO, 5.1/7.1, codec) et pistes intégrées sélectionnables. |
| Pistes audio détectées | ✅ | Bouton audio dédié (`A`) : chaque piste est présentée avec sa langue, sa version (VF, VO, VOSTFR, TRUEFRENCH…), sa disposition (stéréo, 5.1, 7.1) et son codec, par exemple « Français · VF · 5.1 · AC-3 ». Avec libVLC, les métadonnées du flux sont **complétées par l’analyse locale** et la piste se change à la volée ; avec MediaElement, les pistes suivantes s’ouvrent dans VLC. |
| Sous-titres en ligne | ✅ | Recherche OpenSubtitles.com (clé personnelle facultative) : résultats triés par téléchargements, téléchargement SubRip dans `%APPDATA%\LyaranTV\sous-titres`, application immédiate. Aucune requête sans action de l'utilisateur. |
| Images en ligne et cache borné | ✅ | Affiches, vignettes et logos chargés à la demande ; cache mémoire (environ 220 images) et cache disque `%APPDATA%\LyaranTV\covers` (420 px, purge à 45 jours, environ 1 200 fichiers). |
| Accessibilité | ✅ | Focus clavier, contrastes principaux, réduction des animations. |
| Mouvement réduit | ✅ | Option dans les paramètres. |
| Accent personnalisé | ✅ | Saisie hexadécimale + huit accents. |

## P2 — Fonctions avancées

| Fonction | État | Commentaire |
|---|---|---|
| Changement de piste audio intégré | ✅ | Avec libVLC (installé avec VLC), les pistes se changent à la volée et tous les codecs courants sont décodés. Sans libVLC, `MediaElement`/Media Foundation n’expose pas la sélection de piste ; le menu `A` ouvre alors VLC avec `--audio-track` pour les pistes suivantes. |
| Connexion OAuth Letterboxd, découverte automatique des amis et listes privées | ❌ | L’API est accordée sur demande et Letterboxd exclut actuellement les projets privés/personnels. Les noms publics doivent être saisis manuellement ; les flux RSS ne donnent que l’activité publique récente et ne permettent aucune écriture. |
| Sous-titres image (PGS/VobSub) | ❌ | Formats bitmap sans texte exploitable ; le lecteur WPF ne peut pas les superposer. |
| Multi-écrans simultanés | ❌ | Nécessiterait plusieurs instances MediaElement et une gestion de fenêtres dédiée. |
| HLS (`.m3u8`) | ❌ | Non décodé nativement par `MediaElement` sur Windows desktop ; lecteur externe requis. |
| Téléchargements hors ligne | ❌ | Dépend des droits, du stockage et des DRM ; non implémenté. |
| OpenSubtitles | ✅ | Recherche et téléchargement depuis le lecteur avec la clé personnelle de l'utilisateur (compte gratuit). |
| Plex / SMB / WebDAV | ❌ | Intégrations réseau spécifiques non incluses. |
| Cast / AirPlay | ❌ | API plateforme absente de WPF ; alternatives : lecteur externe ou DLNA système. |
| Picture-in-Picture | ❌ | Pas de PiP natif WPF ; le plein écran et la fenêtre redimensionnable restent disponibles. |
| Widgets / Live Tiles | ❌ | Nécessite une application packagée UWP/WinUI. |
| Synchronisation cloud | ❌ | Aucun compte ni serveur LyaranTV ; export/import manuel disponible. |
| Applications mobiles/TV | ❌ | Travail futur ; l'interface actuelle cible Windows. |

## Garanties produit

- **Aucun contenu fourni** : pas de playlist, fournisseur, identifiant ou URL de fournisseur réelle dans le projet.
- **Aucun paywall** : toutes les fonctions présentes sont accessibles.
- **Aucune publicité**, aucun pop-up promotionnel, aucun filigrane.
- **Aucune télémétrie** : pas d'envoi de données d'usage.
- **Aucun compte obligatoire** : l'application fonctionne entièrement en local.
- **Clés personnelles** (TMDb, OpenSubtitles) : jamais incluses dans le code ou les exports.
- **Identifiants protégés** : DPAPI pour les mots de passe Xtream et les adresses MAC.
- **Export portable sans secrets** : mots de passe, MAC, PIN et clés TMDb/OpenSubtitles omis.

## Fichiers de référence

- [`README.md`](../README.md) — démarrage, compilation, confidentialité.
- [`REFERENCE.md`](REFERENCE.md) — CLI, raccourcis, formats, architecture.
- [`DESIGN.md`](DESIGN.md) — identité et composants.
- [`BUGS.md`](BUGS.md) — corrections et limites.
