# Design LyaranTV

LyaranTV possède une identité visuelle originale, pensée pour un lecteur IPTV de bureau : sombre, cinématographique, dense mais respirante. Aucun élément graphique, texte, icône ou mise en page d'IPTVX n'est copié.

## Principes

1. **Le contenu d'abord** — affiches, titres et programmes guident l'œil ; les commandes restent discrètes.
2. **Sombre par défaut** — l'interface convient à une utilisation prolongée et met les images en valeur.
3. **Une seule couleur d'action** — le violet LyaranTV indique les actions, la sélection et le focus.
4. **Mouvement utile** — transitions courtes, jamais décoratives au point de gêner la lecture.
5. **Lisibilité** — textes contrastés, champs toujours visibles, scrollbars accessibles, réduction des animations.

## Palette

### Thème Sombre (par défaut)

| Rôle | Couleur | Usage |
|---|---|---|
| Fond | `#07080B` | Fenêtre, fond général. |
| Panneau | `#101218` | Cartes, barres, dialogues. |
| Surface élevée | `#181B23` | Boutons secondaires, champs, survols. |
| Texte principal | `#F5F7FB` | Titres et corps de texte. |
| Texte secondaire | `#9BA3B4` | Métadonnées, aides, légendes. |
| Accent | `#8B5CF6` | Actions, sélection, focus, indicateurs. |
| Accent secondaire | `#22D3EE` | Informations, accents complémentaires. |
| Bordure | `#242833` | Séparateurs et contours. |
| Succès | `#4ADE80` | Confirmations. |
| Avertissement | `#FBBF24` | Notes et alertes douces. |
| Erreur | `#F87171` | Erreurs et actions destructrices. |

Le thème sombre utilise un **charcoal neutre** plutôt qu’un noir violacé : l’accent reste la seule couleur forte et les affiches ne sont pas teintées. Un halo d’accent très lent (16 s, désactivé avec la réduction des animations), une vignette et un dégradé de fond donnent la profondeur cinématographique.

### Thème Clair

| Rôle | Couleur |
|---|---|
| Fond | `#F4F5F8` |
| Panneau | `#FFFFFF` |
| Surface élevée | `#ECEEF4` |
| Texte principal | `#101218` |
| Texte secondaire | `#5E6472` |
| Bordure | `#DADDE6` |

### Thème Noir AMOLED

| Rôle | Couleur |
|---|---|
| Fond | `#000000` |
| Panneau | `#090A0E` |
| Surface élevée | `#14161D` |
| Texte principal | `#F5F7FB` |
| Texte secondaire | `#9AA0AD` |
| Bordure | `#1F232C` |

Le thème **Automatique** suit le réglage Windows `AppsUseLightTheme` et se met à jour lorsque Windows change de thème.

### Accents

Huit accents sont proposés : violet `#8B5CF6` (défaut), cyan `#22D3EE`, bleu `#3B82F6`, vert `#10B981`, ambre `#F59E0B`, rouge `#EF4444`, rose `#EC4899`, gris `#94A3B8`. Un accent personnalisé peut être saisi en hexadécimal.

L'ancien accent ambre `#F2A33C` des versions précédentes est migré automatiquement vers le violet LyaranTV.

### Seconde teinte et ambiance

Chaque thème combine l'accent avec une **seconde couleur de dégradé** (cyan `#22D3EE` par défaut), réglable indépendamment. Elle alimente les boutons principaux, la bordure supérieure, les halos et les accents secondaires : proche de l'accent, le dégradé reste subtil ; opposée, il devient plus vibrant.

L'**ambiance** règle l'intensité des halos de fond en trois niveaux : **Discrète** (34 %), **Équilibrée** (58 %) et **Vive** (82 %). L'option **surfaces teintées** mélange une faible part d'accent aux panneaux et aux cartes, au lieu du gris neutre, pour réchauffer l'interface sans nuire au contraste.

Le fond de fenêtre reste volontairement composé de teintes sombres proches. Un dégradé clair vers sombre produirait un voile gris au milieu de l'interpolation ; la lumière est donc confiée aux halos radiaux, dont seule l'opacité varie.

## Typographie

- Famille : **Inter**, avec repli sur **Segoe UI**.
- Titres de page : 24–28 px, semi-gras.
- Titres de section : 17–20 px, semi-gras.
- Corps : 13–15 px.
- Métadonnées : **12,5 px minimum**, couleur secondaire : aucune information ne descend sous ce seuil.
- Chiffres et notes : chiffres tabulaires lorsque disponibles.

Les textes principaux conservent un contraste élevé : `#F4F4F8` sur `#0A0A12` atteint environ 18:1, et `#9C9CB0` sur `#0A0A12` environ 7,3:1. Les boutons à fond accent choisissent automatiquement une encre sombre ou blanche selon le contraste de l'accent.

## Logo et icône

Le logo LyaranTV est la piste **17 · Égaliseur** : un carré arrondi violet-bleu, cinq barres verticales blanches et lavande, puis un triangle de lecture cyan. Il est dessiné vectoriellement dans `UiKit.cs` (`DrawLyaranMark`), avec une variante simplifiée à trois barres sous 30 px pour rester lisible en 16 et 20 px. Le même dessin alimente la fenêtre, l'icône `.ico` multi-résolutions (16 à 256 px) et la page À propos. Le dessin est original et ne réutilise aucun logo existant.

## Mise en page

- Barre latérale fixe de 236 px : navigation, playlists, profil. Les compteurs et la version ne s'y affichent plus : ils restent dans les pages et la barre d'état. Le mode compact (`Ctrl+B`) la ramène à 76 px et ne garde que les icônes, avec infobulles.
- Barre supérieure d'environ 72 px : recherche centrale, thème, actualisation, ajout, paramètres.
- Zone de contenu avec marges de 28–38 px et largeur de lecture confortable.
- Barre d'état discrète : page à gauche, source active à droite.
- Bibliothèque découverte : hero de 400 px, filtres en onglets texte (`Tout`, `Direct`, `Films`, `Séries`, `Effacer les filtres`), étagères horizontales, puis exploration par catégorie.
- Recherche globale : par défaut, les résultats sont séparés en sections Films, Séries, Épisodes, Chaînes, Personnes et Avis et activité ; les filtres existants permettent de réduire à un type.
- Cartes de bibliothèque : affiches 2:3 de 236 px de large (280 × 158 px pour le direct), rayon 16 px. Le titre est nettoyé des préfixes de playlist et l'année finale n'est plus répétée ; la note reste discrète en haut à droite, sans badge de type.
- Navigation : la page courante, ses filtres et sa position de défilement sont mémorisés avant chaque changement ; le bouton **Retour** de la barre supérieure et celui des épisodes reviennent au dernier point quitté. Depuis le lecteur, le retour vise la **page des épisodes de la série** pour un épisode, sinon la **catégorie d'origine** (type, catégorie et défilement) pour un film ou une chaîne.
- Lignes de liste : hauteur compacte, vignette 48 px, actions à droite.
- Dialogues : rayon 16–20 px, fond panneau, ombre douce, bouton de fermeture explicite.

## Composants

### Boutons et surfaces

- **Survol façon streaming** : un reflet diagonal traverse le bouton, qui remonte de 2,5 % ; à l'appui, il s'enfonce de 2,5 %. Les rangées de navigation, filtres et listes utilisent la même famille de modèles sans mise à l'échelle, pour rester calmes.
- **Boutons principaux** : dégradé accent → seconde teinte, encre choisie automatiquement pour le contraste, rayon 9 px.
- **Barre supérieure** : filet inférieur en dégradé accent → seconde teinte → bordure.
- **Barre latérale** : dégradé vertical sombre, plus clair en haut, l'indicateur de page actif restant en dégradé d'accent.
- **Cartes** : surface légèrement dégradée de haut en bas, sans effet de verre ni ombre lourde au repos.
- **Listes groupées** : catégories et chaînes masquées réunies dans une seule carte, séparées par des filets fins, avec des lignes compactes au survol accentué.

### Ouverture et profils

- **Générique** : fond charcoal, deux halos radiaux (accent en haut à droite, seconde teinte en bas à gauche) qui dérivent très lentement. Le logo entre en fondu avec un léger dépassement, les lettres de **LyaranTV** se posent une à une (54 ms d'écart), puis la signature et la barre de progression apparaissent.
- **Progression** : piste de 300 px, remplissage en dégradé accent → seconde teinte, pourcentage à droite et message d'étape à gauche. La barre progresse de façon lissée sur environ 1,8 s ; cinq messages se succèdent toutes les 560 ms. Aucun travail réel n'est bloqué.
- **« Qui regarde ? »** : titre très espacé, sous-titre discret, puis une rangée de cartes centrées. Chaque carte fait 188 px de large et son avatar 148 px, avec coins arrondis de 15 px, dégradé de la couleur du profil, initiale blanche, reflet diagonal et anneau de focus. Les cartes entrent en cascade (90 ms d'écart) avec fondu, translation et léger rebond.
- **Détails** : badge **ENFANT** en bas à gauche, cadenas rond en haut à droite pour un profil protégé, mention **DERNIER PROFIL** sous le nom. La carte « Ajouter un profil » reprend le même gabarit avec un carré en pointillés et un signe plus.
- **Interaction** : le survol ou le focus agrandit l'avatar de 6,5 % et allume l'anneau ; le nom seul reste en texte clair, sans encadré. Un PIN incorrect fait trembler la carte horizontalement et affiche un message en rouge.
- **Transition** : l'avatar choisi zoome à 1,16, le voile disparaît en 460 ms et l'accueil apparaît en fondu décalé. Le toast « Bienvenue, … » arrive après le voile.
- **Réduction des animations** : toutes les entrées, dérives et secousses sont supprimées ; le générique dure 320 ms et les écrans apparaissent directement.

### Bibliothèque en mode découverte

- **Hero** : image TMDb plein cadre ou dégradé d'accent, voile dégradé pour la lisibilité, filigrane du titre et logo de chaîne en repli. Le hero reste sombre dans tous les thèmes pour garantir le contraste des titres et des affiches.
- **Contenu du hero** : genre, année, note, programme en cours pour le direct, synopsis court, bouton principal, détails et favori.
- **Rotation** : toutes les 8,5 s, avec pastilles de progression ; pause de 30 s au survol du hero et de 14 s après le survol d'une carte. Désactivée avec la réduction des animations.
- **Étagères** : défilement horizontal à la molette et au trackpad, bords fondus, en-tête avec compteur et action « Tout voir ».
- **Filtres** : onglets de type, catégorie, tri, **bascule liste / grille** et accès à la grille complète « Tout parcourir ». La grille remplace le hero et les étagères par toutes les cartes de la sélection courante, avec pagination par tranches ; le choix liste/grille est mémorisé, tandis que « Tout parcourir » l'impose sans modifier la préférence enregistrée.
- **Aperçu au survol** : la carte survolée remonte de 4 px, affiche un voile et un bouton lecture, et met immédiatement à jour le hero sans clic.
- **Clavier** : `Tab` pour entrer dans les cartes, `←` / `→` pour parcourir une étagère, `Entrée` / `Espace` pour ouvrir. Le focus garde la bordure accent et fait défiler l'étagère.
- **Toutes les catégories** : sous les étagères, trois blocs verticaux — **Chaînes**, **Films**, **Séries** — affichent l'intégralité des catégories, sans limite. Les cartes s'enroulent en grille fluide et la page grandit vers le bas ; chaque carte filtre le type et la catégorie, avec un compteur et un accès « Tout voir » par bloc.

### Cartes cinématographiques

- Affiche ou logo en couverture, dégradé sombre en bas, badge de type et note TMDb.
- **L'image occupe tout le cadre** : grandes cartes de 236 × 354 px (2:3 strict) où l'affiche remplit la surface de bord à bord ; un recadrage centré minimal s'applique si le ratio de l'image diffère. Les logos de chaînes, eux, restent centrés avec une marge de respiration sur les cartes 16:9.
- Titre et métadonnées sous l'image, sur une seule ligne avec ellipse.
- Repli avec initiales, icône de type et dégradé d'accent.
- Survol : élévation de 4 px, ombre douce, bordure accent et bouton lecture superposé.
- Focus clavier : même traitement visuel que le survol, sans dépendre de la souris.

### Champs de saisie

- Fond `Raised`, bordure `BorderColor`, rayon 10 px.
- Texte `TextPrimary`, placeholder `Muted`, curseur accent.
- Aucun double rembourrage : le `PART_ContentHost` a une marge nulle et le texte reste visible dans tous les thèmes.
- Focus : bordure accent, sans déplacer le contenu.

### Barres de défilement

- Épaisses de 6 px au repos, 10 px au survol.
- Pouce arrondi, sans flèches, sans boutons visibles.
- Couleur accent translucide, opacité renforcée au survol et pendant le glissement.
- Apparition pendant le défilement, fondu après environ 1,2 s.
- Molette, clavier (`Page préc.`, `Page suiv.`, `Home`, `End`) et restauration de position.
- Bulle de lettre pendant le défilement rapide dans la bibliothèque.

### Lecteur

- **Page immersive** : pendant la lecture, la barre supérieure, la barre latérale et le pied de page de l'application s'effacent. La page ne contient que l'image, une barre flottante discrète (retour rond, titre, saison) et, plus bas, la fiche du contenu — comme le lecteur Netflix. Le bouton Retour, `Retour arrière` et `Échap` remontent d'abord au dernier point de saut, puis rendent le chrome habituel une fois la pile épuisée.
- **Mise en page** : la vidéo occupe la largeur au 16:9, plafonnée pour laisser toujours apparaître une amorce de la fiche sous l'image (150 px réservés). Le reste défile : description, métadonnées, pistes, puis la rangée d'épisodes.
- **Superposition** : les contrôles vivent sur la vidéo, en fenêtré comme en plein écran — dégradé sombre en bas de l'image, voile dégradé en haut pour la lisibilité du titre. Ils s'effacent après 3,5 s en lecture et reviennent au mouvement de souris, à une touche ou à l'ouverture d'un menu ; en pause, ils restent affichés.
- **Barre de progression** : piste translucide de 6 px (10 px au survol), remplissage accentué, **tampon déjà chargé en blanc** devant le curseur, temps écoulé à gauche et durée à droite ; au survol, un curseur lumineux de 15 px (18 px au survol) suit le pointeur et une infobulle indique le temps visé. Le glissement pilote la lecture avec une recherche limitée à une toutes les 130 ms. En direct, le bouton **Retour au direct** recharge le flux au bord du live.
- **Contrôles** : lecture/pause accentué en 46 px, sauts ±5 s en pastilles translucides, son et curseur de volume, puis à droite les épisodes (`E`), les pistes audio (`A`), les sous-titres (`S`), le format d'image et le plein écran. Boutons ronds, sans cadre ni libellé superflu.
- **Double-clic** sur l'image : bascule plein écran, comme dans un lecteur vidéo classique.
- **Pause** : un bouton central circulaire rappelle l'état de lecture, superposé à l'image.
- **Plein écran** : `F11`, le bouton du lecteur ou un double-clic. Fenêtre sans bordure sur le moniteur courant, vidéo plein cadre, fiche et épisodes masqués ; les contrôles s'espacent de 26 px des bords. L'icône devient « retour à la fenêtre » et `Échap`/`F11` restaurent la position exacte.
- **Fiche du contenu** : page intégrée à la navigation, avec image d'ambiance plein cadre, affiche, sur-titre (`FILM`, `SÉRIE`, `CHAÎNE`), titre, ligne méta, synopsis, métadonnées, programme EPG et actions (lecture, favori, TMDb, lecteur externe). Depuis le lecteur, « Informations » fait défiler la fiche sous la vidéo sans interrompre la lecture.
- **Page de série** : en-tête cinématographique avec affiche et actions, onglets de saison et cartes d'épisodes interactives — survol et focus clavier, progression de reprise, états `EN COURS` et `À SUIVRE`, bouton de lecture. La rangée **Épisodes sous la vidéo** reprend les mêmes états en format horizontal.
- **Tiroir des chaînes** : colonne de 348 px ancrée à gauche, fond presque opaque, liseré à droite, en-tête `CHAÎNES EN DIRECT` avec compteur et fermeture. Le filtre occupe une pastille arrondie, chaque ligne montre le logo, le nom, le programme EPG et une barre de progression ; la chaîne en cours porte un fond et un contour d'accent. Les lignes se survolent sans déplacer la vidéo, qui reste visible à droite.
- **Fiche en superposition** : voile sombre sur la page, carte centrée de 1 120 px maximum, coins de 20 px et ombre profonde. Le hero interne fait 300 px, le contenu défile à l'intérieur et la croix, `Échap` ou un clic sur le voile referment la fiche.
- **Sous-titres** : texte centré sur un fond personnalisable, remonté au-dessus des contrôles lorsqu'ils sont visibles (122 px en fenêtré, 132 px en plein écran), puis reposé à 30 px. Le bouton `CC` indique le nombre de pistes détectées et ouvre le menu `S` : désactivation, pistes texte du fichier, fichiers à côté du média, ouverture manuelle et **Réglages des sous-titres** (taille, couleur du texte, couleur et opacité du fond, contour, aperçu en direct). Les pistes image (PGS/VobSub) restent listées comme non prises en charge.
- **Pistes audio** : menu `A`, détection MKV/MP4 et libellés langue/codec. Le lecteur intégré utilise la première piste ; les autres s'ouvrent dans VLC à la même position.

### Chargement

- Fenêtre de chargement compacte (540 × 432 px) : quatre étapes, barre de progression lissée, pourcentage, temps écoulé, estimation du temps restant et bouton d'annulation.
- La progression avance par interpolation (`0,28` par tick de 180 ms) plutôt que par sauts, et le pourcentage est aligné à droite de la piste.
- Les étapes terminées passent au vert, l'étape courante à l'ambre, l'étape EPG indique « en arrière-plan ».
- Vignettes de remplacement pour les images absentes ou indisponibles.
- Toasts de confirmation en bas à droite, 4,2 s, fondu de 220 ms.

## Mouvement

- Durées : 150–300 ms pour les apparitions, survols et transitions de thème ; 320–560 ms pour les entrées de page, les cartes de profil et le fondu du générique.
- Générique de lancement : logo 1,08 s, lettres de 360 à 790 ms, signature à 860 ms, barre à 920 ms, progression environ 1,76 s, choix du profil après un court repos de 280 ms.
- Cartes de profil : entrée décalée de 90 ms par carte (fondu 320 ms, translation 440 ms, échelle 540 ms), survol 170 ms, zoom de sortie 300 ms.
- Rotation du hero : 8,5 s entre deux contenus, 14 s de pause après un survol de carte, 30 s dans le hero.
- Courbes douces, pas de rebond.
- `UiMotion.Reduced` désactive les animations, les élévations et la rotation automatique lorsque l'option **Réduire les animations et les effets de mouvement** est active.
- Aucune animation ne bloque la lecture vidéo.

## Accessibilité

- Navigation clavier complète, focus visible.
- **Navigation salon** : focus spatial à la manette ou à la télécommande, clavier virtuel AZERTY dans la recherche, aide des touches affichée, activation automatique du mode dès qu'une manette est utilisée.
- Bibliothèque parcourable au clavier : `Tab`, `←` / `→`, `Entrée` / `Espace`.
- Aperçu au survol sans clic, avec repli sur le clavier et les contrôles du hero.
- Contrastes élevés pour le texte principal et les contrôles.
- Champs de saisie lisibles dans les thèmes sombre, clair et AMOLED.
- Scrollbars utilisables à la molette et au clavier.
- Textes alternatifs et infobulles sur les icônes.
- Respect de la réduction des animations.
- Interface en français, avec vocabulaire cohérent et messages explicites.

## Images en ligne

Les affiches TMDb, les vignettes et les logos de chaînes sont chargés à la demande depuis leurs URL d'origine. Ils sont décodés à 420 px de large, conservés dans un cache mémoire limité (environ 220 images) et dans un cache disque borné sous `%APPDATA%\LyaranTV\covers` : les fichiers de plus de 45 jours sont supprimés et le cache est ramené à environ 1 200 images. Les demandes les plus récentes — donc les images réellement affichées — sont traitées en priorité, ce qui garde la bibliothèque réactive même avec des milliers d'éléments. En cas d'échec, le composant affiche le repli graphique prévu.

## Originalité

LyaranTV s'inspire des usages d'un lecteur IPTV moderne (sources, bibliothèque, fiches, direct, EPG) mais conserve une identité, une palette, un logo, des composants et des textes propres. Les références publiques à IPTVX ont servi uniquement à comprendre les parcours fonctionnels ; aucun asset, code, texte ou visuel d'IPTVX n'est inclus.
