# Historique des versions

# LyaranTV 1.6.8

La carte « Préparation de votre playlist » ne se détache plus de l'application.

## Carte ancrée dans la fenêtre

- **Elle appartient à la fenêtre** : la carte était une petite fenêtre séparée, plantée à une position d'écran. En déplaçant l'application, elle restait sur place, parfois hors du cadre. Elle est désormais dessinée dans la fenêtre, en bas à gauche, au-dessus de la page.
- **Elle suit les déplacements et les redimensionnements** sans décalage : un seul élément visuel, plus deux fenêtres à synchroniser.
- **Même comportement qu'avant** : la croix annule le chargement, la carte n'apparaît qu'après 320 ms, laisse l'application utilisable et se retire d'elle-même à la fin.
- **La restauration d'une copie locale** utilise la même carte et en profite.

## Vérifications

- Compilation de toutes les sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.6.7

Une seule échelle de tailles, une seule profondeur d'ombre.

## Échelle typographique

- **Quatre paliers** au lieu de trente-sept tailles : 12,5 px pour les métadonnées, 15 px pour le texte courant, 19 px pour les titres de section, 23 px pour les grands titres. Les affichages d'accueil au-delà de 25 px gardent leur valeur.
- **Plus de taille morte** : les 9,5 / 10 / 10,5 / 11 / 11,5 demandées au fil du temps sont ramenées au palier bas. Elles s'affichaient déjà à 12,5 par le plancher de lisibilité, mais le code laissait croire à une hiérarchie qui n'existait pas.
- **Hiérarchie réelle** : 13, 13,5 et 14 passent à 12,5 ; 14,5 à 16 passent à 15 ; 17 à 20 passent à 19 ; 21 à 24 passent à 23. Deux textes voisins ne peuvent plus se disputer la même place de 1 px près.

## Ombres

- **Deux profondeurs** : « carte » (flou 20, décalage 6, 34 %) et « boîte » (flou 34, décalage 12, 50 %). Les dix-huit ombres noires héritées de valeurs dispersées passent par l'une ou l'autre.
- **Les halos d'accent restent** : ce sont des lueurs de sélection, pas des élévations — ils gardent leur couleur et leur flou propres.
- **Les ombres de texte restent** : sous les sous-titres et sur la vidéo, un flou de 3,5 à 5 px sert la lisibilité, pas la profondeur.

## Vérifications

- Compilation de toutes les sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.6.6

Les boîtes de dialogue se posent maintenant sur un fond en verre dépoli.

## Verre dépoli

- **Un voile derrière chaque dialogue** : ajout de playlist, correction TMDb, réglages, sous-titres, mises à jour, profils. La fenêtre principale apparaît floutée et assombrie, la carte reste nette.
- **Comment** : WPF ne sait pas flouter ce qui se trouve derrière une fenêtre. Un instantané de l'interface est pris à l'ouverture, fortement flouté puis recouvert d'un voile sombre — le résultat est celui d'une vitre dépolie, sans dépendance graphique.
- **Un seul instantané** : il est pris une fois, avant l'ouverture. Aucun coût pendant la saisie, aucune animation ajoutée.
- **Repli sûr** : si la capture échoue (pilote graphique, fenêtre réduite), le dialogue s'ouvre normalement, sans voile.
- **Tiroir des chaînes du lecteur inchangé** : la vidéo n'est pas capturable par WPF, un flou n'y donnerait qu'un aplat noir. Le panneau garde son fond opaque.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.6.5

Les boutons de fenêtre suivent la barre d'avancée, ils ne restent plus à l'écran.

## Lecteur

- **Plus de pastille permanente** : réduire, agrandir/restaurer et fermer n'apparaissent que pendant que les commandes du lecteur sont affichées, en fenêtré comme en plein écran. La règle de la 1.6.4 (« toujours visible en fenêtré ») est remplacée.
- **Disparition avec la barre d'avancée** : après 3,5 s sans mouvement sur l'image, le minuteur escamote les commandes et les trois boutons en même temps.
- **Retour au survol** : un simple mouvement de souris dans la vidéo les ramène en fondu avec le reste des commandes.
- **Pendant une pause** : les commandes restent affichées, donc les boutons aussi — on garde de quoi fermer la fenêtre sans quitter la lecture.
- **Aucun clignotement à l'ouverture** : la barre naît masquée et n'apparaît qu'avec le premier affichage des commandes.
- **Sortie de plein écran** : l'état des boutons est recalé sur celui des commandes réaffichées, sans décalage.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.6.4

Une barre de fenêtre accompagne désormais la lecture.

## Barre de fenêtre du lecteur

Trois boutons posés dans le coin supérieur droit de l'image : **réduire**, **agrandir / restaurer**, **fermer**.

- **En fenêtré** : la barre est toujours affichée, même quand les commandes du lecteur s'effacent après quelques secondes d'inactivité. On garde ainsi la main sur la fenêtre pendant la lecture.
- **En plein écran** : elle apparaît avec les commandes au passage de la souris, puis disparaît avec elles.
- **En plein écran, le bouton du milieu** quitte le plein écran : sans cadre, agrandir n'aurait pas de sens. Son icône devient celle du retour en fenêtre.
- **Fermer** termine l'application comme le bouton de la barre de titre.
- **Discrétion** : fond sombre translucide, pastille arrondie, icônes blanches, survol éclairci et rouge pour la fermeture.
- **Le titre du lecteur** réserve la place à droite pour ne jamais passer sous les boutons.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.6.3

Nouveau logo « Voile », sans triangle de lecture.

## Logo final

- La **proposition n° 24 · Voile** est retenue, **sans le triangle de lecture** : deux voiles superposés, l'un blanc, l'autre cyan, sur le carré arrondi violet-bleu historique.
- Le dessin remplace l'ancien égaliseur partout : barre supérieure, fenêtre, générique de lancement, page À propos, écran de profil et icône de l'application.
- Une version compacte à voiles pleins est utilisée sous 30 px pour rester lisible en 16 et 20 px.
- La planche de contrôle est disponible dans [`Docs/logo-final.png`](logo-final.png). Les quarante propositions restent consultables dans [`Docs/logo-proposals.png`](logo-proposals.png) et [`Docs/logo-proposals-2.png`](logo-proposals-2.png).

## Outils de marque

- `--logofinal <fichier.png>` génère la planche du logo final aux tailles réelles de 16 à 256 px.
- `--makeicon <fichier.ico>` régénère l'icône multi-résolutions à partir du même dessin vectoriel. L'icône livrée contient neuf tailles, de 16 à 256 px.

## Vérifications

- Compilation des 58 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.3.exe`.

# LyaranTV 1.6.3

Nouveau logo « Voile », sans triangle de lecture.

## Logo final

- La **proposition n° 24 · Voile** est retenue, **sans le triangle de lecture** : deux voiles superposés, blanc et cyan, sur le carré arrondi violet-bleu historique.
- Le dessin remplace l'ancien égaliseur dans la barre supérieure, la fenêtre, le générique, la page À propos, l'écran de profil et l'icône `.ico`.
- Version compacte à voiles pleins sous 30 px pour rester lisible en 16 et 20 px.
- Planche de contrôle : `Docs/logo-final.png`. Les quarante propositions restent dans `Docs/logo-proposals.png` et `Docs/logo-proposals-2.png`.

## Outils de marque

- `--logofinal <fichier.png>` génère la planche du logo final (16 à 256 px).
- `--makeicon <fichier.ico>` régénère l'icône multi-résolutions (neuf tailles, 16 à 256 px).

## Vérifications

- Compilation des 58 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.3.exe`.

# LyaranTV 1.6.2

Navigation par type clarifiée et deuxième série de propositions de logo.

## Rail de catégories

- Le rail supérieur ne garde que **Accueil**, **Chaînes**, **Films**, **Séries** et **Ma liste**. Le **Guide EPG** reste dans la barre latérale.
- Chaque entrée ouvre une page dédiée (**Chaînes en direct**, **Films**, **Séries**, **Ma liste**) avec titre, nombre d'éléments et source active.
- Les pages de type regroupent le contenu en rangées par catégorie du fournisseur, les plus fournies en premier.

## Filtres sans doublon

- La rangée `Tout / Direct / Films / Séries / Effacer les filtres` disparaît de **Chaînes**, **Films**, **Séries** et **Ma liste**. Elle reste dans la bibliothèque complète et s'efface quand une catégorie précise est ouverte.

## Logos

- Vingt nouvelles propositions portent le total à **quarante** variantes vectorielles, réparties entre `Docs/logo-proposals.png` (01–20) et `Docs/logo-proposals-2.png` (21–40). Commandes `--logogallery` et `--logogallery2`.
- Le logo actuel (**n° 17 · Égaliseur**) reste inchangé jusqu'au choix explicite de l'utilisateur.

## Vérifications

- Compilation des 57 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.2.exe`.

# LyaranTV 1.6.1

Navigation plus directe, comptes par profil et affichage des programmes repensé.

## Navigation et catégories

- **Bouton hamburger** dessiné en trois traits, en haut à gauche, toujours visible hors lecture. Un survol allume le trait central ; un clic fait glisser la barre latérale hors de l'écran (largeur, translation et opacité animées) et l'état est mémorisé. L'ancien bouton de réduction de la barre supérieure disparaît.
- **Rail de catégories** façon site de streaming sur ordinateur : Accueil, Chaînes, Films, Séries, Ma liste et Guide. Libellés à 17 px, agrandissement et soulignement animés au survol, compatibles clavier et manette.
- Le raccourci `Ctrl+B` commande désormais ce même tiroir latéral.

## Profils et comptes

- **Un compte IPTV par profil** : chaque playlist, compte Xtream ou portail MAC appartient au profil qui l'a créé. Changer de profil recharge uniquement ses sources ; aucun catalogue ne se mélange.
- **Création de profil avec compte obligatoire** : après le nom et l'avatar, une étape animée propose les quatre types de compte (M3U, Xtream, portail MAC, fichier local). Annuler cette étape annule la création du profil.
- Le renommage et la suppression d'un profil suivent ses comptes ; l'export portable et les sauvegardes conservent cette appartenance.

## Accueil et fiches

- **Bannières sans reflet** : le balayage lumineux est retiré, les images sont décodées en haute définition (1 600 px, cache dédié) et les anciens titres/logos en filigrane ne recouvrent plus l'affiche.
- **Aperçu intégré à gauche** : la fiche d'un film ou d'une série n'est plus une grande fenêtre centrale. Elle glisse dans la colonne des fonctionnalités, sous le rail de catégories, avec un en-tête collant, un fond presque transparent et une fermeture au clic extérieur ou par `Échap`.

## Programmes et mouvements

- **Affichage des programmes repensé** dans l'esprit du générique de lancement : cartes « à l'antenne » en verre teinté avec halo, point direct pulsé et progression vivante ; lignes de guide avec filet d'accent, heure lisible et badge « EN COURS » ; étapes de découverte numérotées avec ligne de progression animée.
- **Micro-animations partout** : chaque bouton s'approche au survol et s'enfonce au clic, les cartes respirent légèrement, les onglets de filtre animent leur texte et leur soulignement. L'option de réduction des animations continue de tout désactiver.

## Affiches

- **Sources complémentaires** : OMDb, Fanart.tv et TheTVDB peuvent être activés avec une clé personnelle, en plus de TMDb. TVmaze, IMDb, iTunes et Wikipédia restent sans clé. ThePosterDB, MoviePosterDB, IMP Awards et Cinematerial sont documentés comme sources de recherche manuelle.
- Les clés se saisissent dans **Paramètres → Métadonnées et sous-titres → Sources d'affiches**, ou par les variables `LYARANTV_OMDB_KEY`, `LYARANTV_FANART_KEY` et `LYARANTV_TVDB_KEY`. Elles ne sont jamais incluses dans les exports ni les journaux.
- Les nouvelles sources ne prennent le relais que lorsqu'une affiche manque : la qualité des sources publiques reste prioritaire.

## Logo

- **Vingt propositions vectorielles** sont disponibles dans `Docs/logo-proposals.png` (planche 5 × 4) et se régénèrent avec `LyaranTV.exe --logogallery <fichier.png>`. Le logo actuel reste inchangé jusqu'au choix explicite de l'utilisateur.

## Vérifications

- Compilation des 57 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.1.exe`.

# LyaranTV 1.6.0

Photos de profil et finitions premium.

## Photos de profil

- **Photos personnelles** : importez une photo (PNG, JPG, BMP, GIF) depuis vos fichiers. Elle est recadrée en carré, l'orientation EXIF est appliquée, puis réduite à 256 px et stockée en base64 dans `profiles.json` — elle suit le profil dans les sauvegardes et les exports portables.
- **Avatars prédéfinis** : huit illustrations vectorielles dessinées par le programme (Aurore, Nuit, Vague, Flamme, Forêt, Cosmos, Or, Glacier), sans fichier image ajouté au projet.
- **Initiale** : l'initiale du profil sur un dégradé de sa couleur reste une option.
- **Dialogue repensé** : prévisualisation en direct, galerie des présélections, tuile d'import, choix de la couleur d'accent, action « Retirer la photo », boutons nommés pour l'accessibilité, ouverture en fondu et à l'échelle.

## Écran « Qui regarde ? »

- Chaque carte affiche maintenant la photo ou l'avatar du profil (plus seulement l'initiale).
- Au survol : l'avatar s'agrandit, un anneau et une lueur d'accent s'allument, le nom s'illumine et un reflet diagonal balaie la carte.
- La phrase d'aide en bas à gauche a été retirée ; seuls le titre et le lien « Quitter LyaranTV » restent.

## Réglages allégés

- Les descriptions des cartes de réglages sont raccourcies ; l'introduction de la section profils est retirée.
- Le profil actif est mis en avant par une pastille **ACTIF**.
- Les cases et champs de contrôle parental sont plus compacts.

## Vérifications

- Compilation des 54 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.0.exe`.

# LyaranTV 1.5.1

Une retouche nette sur l’écran des profils.

## Écran « Qui regarde ? »

- **Anneau de focus ajusté** : le liseré épouse désormais exactement le bord de l’avatar (même rayon intérieur, 2 px de bord). Les croissants sombres qui apparaissaient aux coins supérieurs de la carte sélectionnée ont disparu.
- **Bas d’écran allégé** : la phrase d’aide « Flèches ou manette pour choisir · Entrée pour continuer » a été retirée. Seul le lien **Quitter LyaranTV**, aligné à droite, reste visible.

## Vérifications

- Compilation des 53 sources.
- Auto-test : **34/34**.
- Compilation de l’installateur.

# LyaranTV 1.5.0

Une ouverture cinématique, un vrai choix de profil, et une interface pensée aussi pour le canapé.

## Ouverture et profils

- **Générique de lancement** : logo animé, lettres de LyaranTV qui se posent une à une, signature, barre de progression et cinq messages d’étape. Le tout dure environ 1,8 s et respecte l’option de réduction des animations.
- **Écran « Qui regarde ? »** : cartes de 148 px avec la couleur et l’initiale du profil, badge **ENFANT**, cadenas des profils protégés et mention **DERNIER PROFIL**. Entrée en cascade, anneau de focus, agrandissement au survol.
- **Sélection complète** : clavier, souris ou manette ; PIN vérifié avec message d’erreur et secousse de la carte ; ajout d’un profil depuis l’écran ; transition en zoom et fondu vers l’accueil, puis toast « Bienvenue, … ».
- **Réglage** : l’écran peut être désactivé dans **Paramètres → Démarrage**. LyaranTV ouvre alors directement le dernier profil.

## Navigation salon

- **Manette Xbox (XInput)** : croix directionnelle et stick pour le focus spatial, **A** valider, **B** revenir, **Y** recherche et clavier virtuel, **X** tiroir des chaînes, **Start** plein écran, **LB/RB** chaînes précédente/suivante.
- **Télécommande multimédia** : touches navigateur et média standard pour revenir, actualiser, changer de chaîne et lire/pause.
- **Clavier virtuel AZERTY** dans la recherche, avec aperçu, retour arrière et validation. La souris reprend la main dès qu’elle bouge.
- **Barre latérale compacte** : `Ctrl+B` ou bouton dédié, colonne de 76 px en icônes avec infobulles.
- **Plein écran application** : `F11` ou bouton de la barre supérieure, fenêtre sans bordure sur le moniteur courant ; `Échap` restaure la position exacte.

## Lecteur et fiches

- **Tiroir des chaînes en direct** : colonne de 348 px avec filtre instantané, logos, programme EPG, badge de la chaîne en cours et changement de chaîne sans quitter la lecture (`C`, flèches, `Page préc./suiv.`, LB/RB).
- **Fiche en superposition** : la fiche d’un contenu s’ouvre dans une grande carte modale (1 120 px maximum) au-dessus de la page, avec défilement interne. `Échap`, la croix ou un clic sur le fond la referment sans perdre la page.
- **Recherche mémorisée** : les 12 dernières recherches validées apparaissent en pastilles cliquables, avec **Effacer l’historique**.

## Vérifications

- Compilation des 53 sources.
- Auto-test : **34/34**.
- Compilation de l’installateur.

# LyaranTV 1.4.19

Le rail de volume reste lisible quand il est au maximum.

## Volume

- **Remplissage qui rejoint la pastille** : le rose s'arrête désormais au bord droit de la pastille et non à son centre.
- **Piste toujours visible** : le rail vide passe de 36 % à 62 % de blanc translucide (84 % au survol).
- **Pastille plus étroite** : 14 px de gabarit au lieu de 20.
- **Résultat** : à 100 %, le rose touche le bord droit du rail, la pastille est posée dessus et il ne reste qu'un liseré d'un pixel avant le pourcentage.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.18

Le mini lecteur est retiré, à la demande.

## Ce qui disparaît

- **Le bouton dans la barre du lecteur** et le raccourci `P`.
- **La petite fenêtre toujours au premier plan**, sa barre de survol et son déplacement.
- **Le fichier `Sources/UI/MiniPlayer.cs`** et tous ses points d'appel : fermeture de l'application, retour depuis le lecteur, garde de la barre de commandes.

## Ce qui reste inchangé

- Le lecteur, ses commandes, le rail de volume intégré, le plein écran, les sous-titres et les pistes audio fonctionnent exactement comme en 1.4.16.
- La version 1.4.17 reste téléchargeable depuis GitHub, mais la 1.4.18 la remplace.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.17

Le lecteur gagne un mini lecteur : la vidéo sort dans une petite fenêtre
toujours au premier plan, sans interrompre la lecture.

## Mini lecteur

- **Bouton et raccourci** : un nouveau bouton dans la barre du lecteur (icône « réduire »), et la touche `P`. Elle bascule entre la page de lecture et la petite fenêtre.
- **Lecture continue** : la surface déplacée est exactement le même élément WPF que dans le lecteur — image VLC ou MediaElement. Position, son, piste audio et sous-titres sont conservés, rien ne redémarre.
- **Fenêtre compacte** : 440 × 272, redimensionnable (minimum 320 × 190), toujours au premier plan, placée en bas à droite de l'écran de travail, sans bouton dans la barre des tâches.
- **Barre discrète** : titre du contenu, lecture/pause, retour au lecteur et fermeture (en rouge au survol). Elle apparaît au survol de la fenêtre, sert de poignée de déplacement, et disparaît quand la souris sort.
- **Clavier dans la fenêtre** : `Espace` lecture/pause, `M` couper le son, `Échap` revenir au lecteur.
- **Les grandes commandes ne suivent pas** : la barre complète est masquée pendant le mini lecteur, et la surveillance de la souris ne la fait plus réapparaître par-dessus la vidéo.

## Robustesse

- Quitter la page de lecture, fermer l'application ou fermer la petite fenêtre par Alt+F4 remet la surface vidéo dans la page avant de détruire le lecteur : aucune fenêtre orpheline, aucun moteur piloté après coup.
- Le plein écran est quitté automatiquement à l'ouverture du mini lecteur.

## Vérifications

- Compilation des 53 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.16

Le rail de volume est redessiné : plus fin, sans anneau, mieux proportionné.

## Volume

- **Haut-parleur sans pastille** : l'icône n'est plus enfermée dans un cercle avec fond et bordure. C'est une icône nue de 30 px, qui ne s'éclaire qu'au survol, comme le reste de la barre. Elle reste le bouton de coupure du son (raccourci M).
- **Rail affiné** : 3 px au lieu de 4, rayons réduits en conséquence, pastille de 9 px (11 au survol ou pendant le glissement) au lieu de 11-13. Le curseur pèse moins lourd à côté du texte.
- **Pourcentage plus discret** : 11 px en graisse normale au lieu de 10,5 px en gras, largeur minimale de 36 px, espacement resserré. « Muet » prend une majuscule.
- **Groupe plus compact** : 36 px de haut, curseur de 108 px, pour laisser respirer les commandes voisines.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.15

Le volume du lecteur devient une vraie barre intégrée à la barre de commandes.

## Volume intégré

- **Rail permanent** : haut-parleur, curseur et pourcentage sont désormais côte à côte dans la barre de commandes, toujours visibles pendant la lecture. Plus besoin de cliquer sur une icône ni de la survoler pour régler le son : le rail se glisse directement.
- **Carte flottante supprimée** : elle n'avait plus de raison d'être, et c'est elle qui flottait au-dessus de l'image. Toute la mécanique d'ouverture, de fermeture différée et de repositionnement disparaît avec elle.
- **Pourcentage lisible** : la valeur suit le curseur (« muet » quand le son est coupé), avec une largeur fixe pour que la barre ne bouge pas quand le nombre de chiffres change.
- **Haut-parleur conservé comme coupure** : l'icône reste un bouton de mute (raccourci M), mais elle n'est plus le seul accès au volume.

## Comportement

- **Aucun clic parasite** : glisser le rail, cliquer dessus ou sur le pourcentage ne met jamais la vidéo en pause, et ne bascule pas en plein écran au double-clic.
- **Flèches haut et bas** : elles règlent toujours le volume par pas de 5 % ; le pourcentage s'allume une fraction de seconde au lieu d'ouvrir une carte.
- **Plein écran** : le rail disparaît et revient avec le reste des commandes, comme avant.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.14

Deux corrections dans le lecteur : le volume ne met plus la lecture en pause,
et les flèches haut/bas règlent le son comme sur YouTube.

## Son sans pause

- **Régler le volume ne met plus la vidéo en pause** : la carte de volume et la pastille du haut-parleur flottent au-dessus de l'image, et le clic « sur la vidéo » (lecture / pause) les prenait pour la surface du film. Elles en sont maintenant exclues, ainsi que le curseur de volume lui-même.
- **Double-clic sur la carte de volume** : il ne bascule plus en plein écran.
- **Règle générale** : un clic issu d'un contrôle (bouton, curseur, barre de défilement, champ) n'est plus interprété comme lecture / pause, même hors de la barre de commandes.

## Flèches haut et bas

- **Flèche haut / flèche bas** : le volume monte ou descend de 5 % pendant la lecture, avec la carte de volume qui s'ouvre une seconde pour afficher le pourcentage, puis se referme.
- **Elles fonctionnent même quand un bouton ou la timeline a le focus** : les flèches sont désormais traitées avant la navigation clavier de WPF, comme les flèches gauche et droite pour le déplacement dans le flux.
- **Le tiroir des chaînes garde ses flèches** : ouvert, il continue de parcourir la liste au clavier.
- Modificateur enfoncé (Ctrl, Alt…) : les flèches reprennent leur rôle habituel.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.13

Six finitions de plus : continuité visuelle avec l'installateur, thème clair
cohérent, étagères en cascade, pied de page lisible, états vides illustrés et
fondu entre thèmes.

## Installateur

- **Même langage visuel que l'application** : il dessine désormais avec Inter quand elle est installée, sinon Segoe UI — exactement la règle de la fenêtre principale. La deuxième teinte du dégradé devient une constante partagée (`Theme.Secondary`) au lieu d'une valeur recopiée.
- **Coins arrondis et liseré natifs** sur la fenêtre de progression de mise à jour, comme la fenêtre principale.

## Thème clair

- **Bandeaux de fiche teintés selon la palette** : la fiche d'un film, celle d'une série, le bandeau de la bibliothèque et l'aperçu des sous-titres partageaient un noir codé en dur (`#08080E`). Ils reposent maintenant sur une couleur `HeroBase` propre à chaque thème : prune profond en thème clair, noir en AMOLED, charbon en sombre. Le bandeau reste cinématographique sans former un trou noir sur une page claire.
- Les dégradés de secours (sans affiche) mélangent cette base à l'accent au lieu d'une teinte violette figée.

## Accueil et navigation

- **Étagères en cascade** : les blocs d'une page entrent avec 30 ms d'écart (plafonné à huit), au lieu d'apparaître d'un seul mur. La réduction des animations reste respectée.
- **États vides illustrés** : halo diffus, médaillon arrondi teinté par l'accent et pictogramme large remplacent la petite icône seule.

## Pied de page

- **40 px au lieu de 32** : c'était la zone la plus compressée de l'application, avec du texte à 10,5 px. Le pied affiche maintenant une pastille d'état colorée, un libellé à 12 px et la source active à 11,5 px.

## Thème et accent

- **Fondu de 220 ms** entre l'ancienne et la nouvelle palette lors d'un changement de thème, de couleur d'accent ou d'ambiance : le réglage se lit comme une transition, pas comme un rechargement.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.12

Deux réparations : le plein écran du lecteur et la relance de la sortie audio.

## Plein écran

- **Plus de filet autour de l'image** : le liseré natif demandé à Windows 11 (attribut DWM 34) restait dessiné une fois la fenêtre sans bordure, et encadrait tout l'écran. Il est maintenant retiré en plein écran, avec l'arrondi, puis rétabli à la sortie.
- **Contour de l'application masqué** : le filet d'un pixel qui dessine la fenêtre en mode normal disparaît lui aussi en plein écran. L'image touche réellement les bords de la dalle.

## Son

- **« Réparer le son » fonctionne à nouveau après une première tentative** : le moteur mémorisait chaque sortie essayée (mmdevice, directsound, waveout) et refusait de la retenter. Une réparation demandée par l'utilisateur rejouait donc une chaîne vide et échouait, même en relançant la sortie. Elle rejoue désormais tous les candidats, en commençant par la sortie choisie dans les réglages.
- **Une sortie valide n'est plus écartée** : la bascule attend le périphérique (quatre lectures espacées de 25 ms) avant de conclure qu'il ne rend pas de volume.
- **Journal plus utile** : un échec de réparation note explicitement que le périphérique est occupé ou refuse le format.
- **Sons d'interface coupés pendant la lecture** : aucune note ne vient se mêler à la piste du flux.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.11

Sept finitions d'interface, toutes visibles sans rien changer à l'usage.

## Navigation

- **Indicateur glissant** : le trait d'accent de la barre latérale ne réapparaît plus sous chaque libellé, il glisse d'un onglet à l'autre en 240 ms, avec un léger halo qui l'accompagne. Il suit aussi le défilement du menu.
- **Anneau de focus** : au clavier comme à la manette, le bouton actif reçoit un second liseré détaché de 2 px, lisible d'un coup d'œil au lieu d'un simple changement de bordure.

## Chargement

- **Squelettes animés** : tant qu'une affiche n'est pas arrivée, un reflet diagonal balaie le cadre toutes les 1,15 s au lieu d'un rectangle vide. Il disparaît dès l'image posée, et ne tourne jamais indéfiniment sur un visuel introuvable.
- **Transitions de page** : fondu, glissement et très court zoom arrière (300 ms) pour que la page se pose dans la continuité de la carte cliquée.

## Couleur de l'œuvre

- **Teinte dominante** : la fiche d'un film ou d'une série échantillonne son affiche (18 × 18 pixels) et en tire un halo coloré qui apparaît en fondu. Chaque œuvre éclaire sa propre fiche, au lieu de répéter l'accent de l'application.

## Fenêtre

- **Coins arrondis Windows 11** : la fenêtre demande enfin l'arrondi natif (attribut DWM 33) et un liseré assorti au thème (attribut 34), au lieu de rester à angles droits alors que tout l'intérieur est arrondi.
- **Rendu des textes en mode Display** : les petites tailles restent nettes sur un écran dense.

## Mises à jour

- **Reflet sur la barre** : un balayage lumineux parcourt la barre pendant le téléchargement, puis s'arrête quand l'installation commence.

## Sons

- **Sons d'interface discrets** : tick de navigation et carillon de lancement, synthétisés par le programme (aucun fichier audio embarqué), coupables d'un seul réglage dans Apparence.

## Thème clair

- **Vignettes d'épisodes** : leur fond suivait une couleur sombre codée en dur, il suit désormais la palette et reste cohérent en thème clair.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.

# LyaranTV 1.4.10

La barre de téléchargement de la mise à jour est refaite.

## Nouveau

- **Bandeau de progression pleine largeur** : il remplace la petite barre de 210 px coincée entre les boutons. Il occupe toute la largeur de la fenêtre, sous les notes de version.
- **Chiffres utiles** : pourcentage à droite, puis volume reçu sur total, débit moyen et temps restant estimé (« 12,4 Mo / 38,0 Mo · 1,7 Mo/s · ≈ 14 s restantes »).
- **Étapes explicites** : « Téléchargement de la mise à jour », puis « Installation » avec la mention de l'empreinte SHA-256 vérifiée, au lieu d'une barre sans contexte.

## Correction

- La barre ne passe plus par un état indéterminé au départ : elle affiche 0 % et « Connexion à GitHub… », puis progresse dès les premiers octets.
- Le pourcentage est borné à 100 même si GitHub annonce une taille approximative.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.

# LyaranTV 1.4.9

La carte de chargement attend vraiment la fin du générique.

## Correction

- **Carte affichée pendant le générique** : au démarrage, le chargement de la playlist part avant le générique, donc la carte croyait qu'aucune animation n'était en cours et s'ouvrait à 320 ms. Elle considère maintenant un générique « à venir » comme en cours et n'apparaît qu'une fois le fondu terminé.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.

# LyaranTV 1.4.8

La carte de chargement attend la fin du générique de lancement.

## Correction

- **Carte de restauration affichée trop tôt** : elle s'ouvrait pendant l'animation d'ouverture, par-dessus le générique. Elle n'apparaît plus qu'une fois le générique terminé (ou après 6 s de garde-fou), puis suit son délai de 320 ms pour éviter tout clignotement sur les restaurations instantanées.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.

# LyaranTV 1.4.7

Les commandes du lecteur redeviennent cliquables.

## Correction

- **Boutons en haut à gauche du lecteur** : la zone de déplacement de la fenêtre (WindowChrome) couvrait la bande haute et avalait les clics sur le retour, la liste des chaînes et la pastille DIRECT. Elle est désactivée pendant la lecture d'une fenêtre maximisée comme en plein écran, et ces boutons sont explicitement marqués comme cliquables dans la zone de déplacement.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.

# LyaranTV 1.4.6

La mise à jour affiche de nouveau sa progression pendant l'installation.

## Nouveautés

- **Barre de progression d'installation** : après le téléchargement, l'application se ferme et l'installateur affiche une fenêtre compacte — « Installation de la mise à jour », étape en cours, pourcentage et barre animée — puis « Redémarrage de LyaranTV… » et relance la nouvelle version. Elle reste au premier plan et ne s'ouvre que pour les mises à jour interactives.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.

# LyaranTV 1.4.5

Le lecteur retrouve ses commandes : retour, « C » et liste des chaînes.

## Correction

- **Générique qui bloquait l'interface** : la fenêtre du générique restait ouverte au-dessus de l'application quand son fondu échouait, et interceptait clics et raccourcis — d'où le bouton retour et la touche « C » sans effet dans le lecteur. Elle est désormais transparente aux clics, sans activation, se ferme par minuterie (et par un filet de sécurité à 4 s) et s'efface au premier clic dans l'application.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.

# LyaranTV 1.4.4

Le générique de lancement s'affiche réellement au démarrage.

## Corrections

- **Générique visible** : la séquence d'ouverture était posée dans la fenêtre principale, où les reconstructions de page du démarrage la recouvraient aussitôt. Elle vit désormais dans sa propre fenêtre, calée sur la fenêtre principale — en fenêtré comme en plein écran — et disparaît d'elle-même après 1,5 s, ou d'un clic.
- **Chargement de playlist compact** : carte de 376 × 138 en bas à gauche, affichée seulement si le chargement dépasse 320 ms ; l'application reste utilisable et le ✕ annule toujours l'opération.
- **Logo non rogné en mode cinéma** : le bouton retour et la marque tiennent ensemble dans la colonne de 76 px.

## Notes de version

- Chaque release GitHub ne contient que sa propre version ; les notes des versions précédentes sont dans `Docs/RELEASE-HISTORY.md`.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.

# LyaranTV 1.4.3

Générique de lancement, chargement discret et barre latérale soignée.

## Nouveautés

- **Générique de lancement** : halo qui s'ouvre, logo qui prend sa place avec un léger rebond, nom écrit lettre à lettre et ligne de progression, puis fondu vers l'interface. La séquence couvre toute la fenêtre, en fenêtré comme en plein écran, et s'efface d'un clic.
- **Chargement de playlist discret** : la grande fenêtre centrée est remplacée par une carte compacte (376 × 138) ancrée en bas à gauche de la fenêtre, avec marque, titre, playlist, barre fine, pourcentage, temps écoulé et estimation. L'application reste utilisable pendant le chargement, et le ✕ annule toujours l'opération.
- **Plus de clignotement** : la carte n'apparaît qu'après 320 ms ; une restauration instantanée depuis le cache n'affiche plus rien.

## Corrections

- **Logo rogné en mode cinéma** : dans la colonne réduite à 76 px, le bouton retour et la marque ne tenaient pas ensemble, et le logo était coupé au bord. Les deux sont maintenant dimensionnés pour la colonne.

## Notes de version

- Chaque release GitHub ne contient plus que sa propre version ; les notes des versions précédentes sont regroupées dans `Docs/RELEASE-HISTORY.md`.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.

Chaque release GitHub ne décrit que sa propre version ; ce fichier conserve les notes des versions précédentes.

# LyaranTV 1.4.1

Pensée pour le salon : pilotage à la manette ou à la télécommande, clavier virtuel, historique de recherche et tiroir des chaînes façon box TV.

## Nouveautés

- **Navigation manette / télécommande** : focus spatial visible, croix et stick pour se déplacer, A pour valider, B pour revenir, Y pour la recherche, Start pour le plein écran, X pour le tiroir des chaînes ou lecture/pause, LB/RB pour zapper.
- **Clavier virtuel** : il s'ouvre dans la recherche (et le filtre du tiroir) quand une manette ou une télécommande est utilisée sans clavier, ou via le bouton « Clavier à l'écran ».
- **Historique de recherche** : la page Recherche vide affiche les dernières recherches en pastilles cliquables, avec un bouton « Effacer l'historique ».
- **Tiroir des chaînes du lecteur** : bouton TV, touche `C` ou X sur manette. La vidéo reste en place pendant qu'on zappe : vrais logos des chaînes, programme en cours avec barre de progression, badge « EN COURS », favoris, filtre rapide et compteur.
- **Fiche de contenu compacte** : panneau flottant resserré façon Netflix (hero réduit, coins adoucis), sans prendre tout l'écran.
- **Mode cinéma** : `Ctrl+B` réduit la colonne de gauche aux seuls logos pour laisser toute la place aux affiches.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.

---

# LyaranTV 1.4.0

Une mise à jour dédiée aux finitions : des boutons qui réagissent au survol, des dégradés plus soignés et une personnalisation d'apparence beaucoup plus large.

## Nouveautés

- **Personnalisation du dégradé** : une deuxième couleur, réglable depuis Paramètres, colore les boutons principaux, la bordure supérieure, les halos et les accents secondaires. Choisissez-la proche de l'accent pour un rendu discret, ou opposée pour un effet plus vibrant.
- **Ambiance du fond** : trois intensités — **Discrète**, **Équilibrée** et **Vive** — règlent la présence des halos colorés derrière l'interface, sans jamais gêner la lecture des affiches.
- **Surfaces teintées** : une option applique une légère teinte d'accent aux panneaux et aux cartes au lieu du gris neutre, pour une interface plus chaleureuse.
- **Boutons réactifs** : au survol, les boutons laissent passer un reflet diagonal et remontent légèrement ; à l'appui, ils s'enfoncent. Les rangées de navigation et de filtres gardent un mouvement plus sobre.

## Finitions

- **Dégradés plus présents** : bordure supérieure accent → seconde teinte, barre latérale en dégradé vertical, surfaces de cartes légèrement dégradées et boutons principaux en dégradé.
- **Paramètres réorganisés** : pastilles d'apparence avec anneau et coche, deuxième couleur, ambiance et teinte des surfaces sont regroupées dans la section Apparence, avec des libellés et des espacements plus lisibles.
- **Catégories et chaînes masquées** : chaque ensemble devient une seule carte groupée, séparée par des filets fins, avec des lignes compactes qui réagissent au survol.
- **Guide EPG** : colonne d'horaire fixe de 96 px, description sur une seule ligne avec ellipse et survol accentué, pour un guide plus calme et plus lisible.
- **Fiche de contenu** : bloc « À propos » avec métadonnées sans bordures, repli de hero plus graphique et sur-titre qui ne répète plus le type et la catégorie.
- **Accords automatiques** : les compteurs utilisent désormais le singulier ou le pluriel correct (« 1 catégorie », « 2 catégories »).
- **Fond nettoyé** : le dégradé de fond ne produit plus de voile gris au milieu de l'écran ; la lumière vient uniquement des halos colorés.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.

---

# LyaranTV 1.3.7

Correction interne de la lecture libVLC.

## Correction

- **Références média libérées** : la lecture des statistiques de tampon et l'analyse des pistes audio et de sous-titres demandaient le média à libVLC sans relâcher sa référence. Ces références sont maintenant libérées après usage, ce qui évite une rétention progressive pendant les longues sessions et les changements de contenu répétés.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.

---

# LyaranTV 1.3.6

Une mise à jour centrée sur le lecteur, les sous-titres et les fiches de contenus.

## Nouveautés

- **Fenêtre de mise à jour épurée** : seule la section de la version publiée est affichée, mise en forme avec titres, puces et gras au lieu d'un pavé de texte brut.
- **Tampon de lecture visible** : la partie déjà chargée de la barre de progression s'affiche en blanc. Avec libVLC, elle est estimée à partir des octets lus et du débit moyen ; un fichier local apparaît comme entièrement chargé.
- **Retour au direct** : un bouton dans la barre supérieure et une entrée du menu contextuel rechargent le flux au bord du live. Les compteurs de reconnexion sont remis à zéro pour repartir proprement.
- **Réglages des sous-titres dans le lecteur** : taille, couleur du texte, couleur du fond, opacité, affichage du fond et contour, avec aperçu en direct, réinitialisation et application immédiate. Accessible depuis le menu des sous-titres.
- **Fiche de contenu pleine page** : image d'ambiance, affiche, synopsis, métadonnées, programme EPG, actions et informations TMDb dans une page intégrée à la navigation. Le retour est conservé et « Informations » dans le lecteur fait défiler la fiche sans arrêter la lecture.
- **Page de série repensée** : en-tête cinématographique, onglets de saison, compteur d'épisodes et cartes d'épisodes interactives avec survol, focus clavier, progression de reprise et états « En cours » et « À suivre ».

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.
- Fiche, épisodes et réglages de sous-titres vérifiés sur la démonstration et un catalogue Xtream.

---

# LyaranTV 1.3.5

Le mode grille de la bibliothèque fonctionne de nouveau.

## Correction

- **Bascule liste / grille réparée** : le bouton en haut des filtres changeait bien l'icône et enregistrait le choix, mais la page continuait d'afficher le hero et les étagères. La bibliothèque affiche désormais réellement la grille complète de la sélection courante (avec « Afficher plus »), et revient aux étagères en mode liste.
- **Choix mémorisé** : la présentation liste ou grille est conservée au redémarrage, au retour du lecteur et après une fiche de contenu.
- **« Tout parcourir » sans effet de bord** : la vue grille reste imposée sur cette page, mais elle n'écrase plus la préférence enregistrée. Le bouton retour restaure la découverte, et « Tout voir » depuis l'accueil ramène bien à l'accueil.

## Vérifications

- Auto-test : **32/32**.
- Bascule grille → liste et liste → grille vérifiée à la souris sur un catalogue de 26 792 éléments.
- Rendu de 150 cartes en grille : environ 0,4 s.

---

# LyaranTV 1.3.4

Interface plus calme, recherches TMDb/OpenSubtitles fiables et affiches préchargées.

## Interface

- **Moins d'informations à l'écran** : barre latérale sans compteurs ni version, barre d'état réduite à la source active, en-têtes d'étagères sans eyebrow ni sous-titre, hero et cartes allégés.
- **Filtres intégrés** : `Tout`, `Direct`, `Films`, `Séries` et `Effacer les filtres` sont des onglets texte soulignés par l'accent, sans pastille ni cadre.
- **Titres nettoyés** : les préfixes `FR - ` / `EN - ` et l'année finale ne s'affichent plus sur les cartes, le hero, le Top 10, les fiches et le lecteur. Le nom d'origine reste utilisé pour la recherche et TMDb.
- **Typographie** : toutes les petites informations sont remontées à **12,5 px minimum** ; les titres de cartes passent à 14–14,5 px.
- **Cartes épurées** : plus de badge FILM/SÉRIE/MÉDIA, note discrète, méta limitée à l'année, la saison/épisode ou la reprise.

## TMDb et OpenSubtitles

- **Recherches qui n'attendent plus indéfiniment** : les appels JSON passent d'abord par le moteur HTTPS standard avec un délai court, puis par le client IPv4 en repli. Chaque adresse CDN morte ne bloque plus la requête.
- **Enrichissement TMDb parallèle** : 4 requêtes simultanées, priorité aux contenus visibles sur l'accueil, jusqu'à 600 groupes par passe. Les correspondances exactes restent préférées ; les doublons de titre sans année sont départagés par popularité.
- **Correction TMDb** : la recherche affiche les résultats avec leur score, la correction est mémorisée par profil et par source et rafraîchit immédiatement la fiche, la bibliothèque et l'accueil.
- **OpenSubtitles** : délai réseau borné et message explicite si l'API ne répond pas ; le bouton redevient actif au lieu de laisser « Recherche… » bloqué.

## Affiches

- **Préchargement prioritaire** des contenus visibles (hero, reprises, nouveautés, films et séries) avant le reste du catalogue.
- **Téléchargement d'images** : moteur standard d'abord, repli IPv4 court ; une adresse CDN morte ne retient plus la file derrière elle.
- Cache mémoire et disque conservés : les lancements suivants ouvrent la bibliothèque déjà remplie.

## Vérifications

- Auto-test : **32/32**.
- TMDb : recherche en **0,3 s**, fiche et affiche trouvées.
- OpenSubtitles : **50 résultats en 0,7 s**.
- Accueil et bibliothèque capturés avec les affiches en place.

---

# LyaranTV 1.3.3

Affiches préchargées, et téléchargements HTTPS réparés sur les réseaux à IPv6 défaillant.

## Corrections

- **Affiches vides qui ne se chargeaient jamais** : sur un réseau qui annonce une adresse IPv6 sans la router (fréquent chez certains fournisseurs), chaque téléchargement attendait ~20 s avant de basculer en IPv4, alors que les jaquettes abandonnaient au bout de 8 s. Un moteur de téléchargement HTTPS **IPv4 d'abord** (TLS 1.2 avec SNI, redirections, réponses chunked, gzip) remplace cette attente ; le moteur historique reste utilisé derrière un proxy ou en repli. Une jaquette passe ainsi de « jamais chargée » à **~250 ms**. TMDb, TVmaze, iTunes et OpenSubtitles sont réparés par la même occasion.
- **Drapeau TLS 1.3 non pris en charge** retiré du démarrage : .NET Framework ne le négocie pas, son maintien dans l'énumération était au mieux inutile.
- **Jaquette morte réessayée sans fin** : une ancienne URL TMDb en 404 est désormais écartée après le premier échec ; la carte bascule sur ses autres visuels puis sur son affiche dessinée.

## Nouveautés

- **Préchargement continu des jaquettes** : une file de fond parcourt l'accueil (reprises, défilé, nouveautés, films, séries), puis la bibliothèque au fil de la navigation, par petits lots de 6 images toutes les 120 ms. Les cartes visibles restent prioritaires et les images sont conservées dans le cache disque `covers/` : les lancements suivants ouvrent la bibliothèque déjà remplie.
- **Écran de chargement** : l'étape « Chargement des affiches de films et séries » affiche la progression réelle (`mise en cache · 36/40`), la file de fond est suspendue le temps de couvrir le premier écran, et la fenêtre ne se ferme plus avant que l'essentiel des jaquettes soit prêt (3,2 s au pire).
- **Diagnostic borné** : le journal indique `Affiche non chargée`, `Affiche illisible` ou `Hôte d'affiches ignoré` (8 lignes maximum par session) au lieu de laisser un cadre vide sans explication.

## Vérifications

- Cache d'affiches vidé : **36/40 jaquettes prêtes en 2 s** (les 4 restantes sont des URL TMDb réellement supprimées, HTTP 404), 98 images en cache après 8 s, ouverture de l'interface en **1,5 s**.
- Accueil et bibliothèque capturés avec les affiches en place, avant et après réouverture à cache chaud.

## Nouveautés

- **Écran de chargement de la bibliothèque** : sur un gros catalogue, la page Bibliothèque apparaît immédiatement avec un indicateur animé, le nombre d'éléments indexés et un message clair ; elle laisse place à la bibliothèque dès que celle-ci est prête. Sur un petit compte, aucun écran intermédiaire n'est affiché : la page s'ouvre directement.
- **Préparation en arrière-plan** : le filtrage, le tri et la construction des listes ne s'exécutent plus sur le fil d'interface. Sur le compte de 239 662 éléments, la préparation prend **197 ms hors interface** et le rendu **232 ms**, soit **429 ms au total** contre 1 085 ms auparavant, sans gel de la fenêtre.
- **Barre d'état restaurée** après l'ouverture : elle ne reste plus bloquée sur « Chargement de la bibliothèque… ».

---

# LyaranTV 1.3.1

Correctif des gels perçus au lancement d'un film ou d'une série.

## Corrections

- **Gel au lancement d'une lecture** : le programme relisait et réécrivait le fichier `history-sources.json` (9,7 Mo, 239 671 entrées) à chaque ouverture de lecteur, sur le fil d'interface. Le fichier est maintenant chargé une fois en mémoire et enregistré en arrière-plan, 1,5 s après la dernière modification. Mesure sur le compte de 239 662 éléments : ouverture du lecteur en **371 ms** au lieu de plusieurs secondes.
- **File de lecture** : elle ne recopie plus tout le catalogue (jusqu'à 240 000 éléments) à chaque lecture ; seule une fenêtre autour du contenu lancé est conservée.
- **Recherche des contenus** : `FindChannel` et `SeriesForEpisode` parcouraient linéairement les 240 000 entrées, à chaque carte de l'accueil. Un index est construit une fois par chargement de bibliothèque. L'accueil passe de **2 227 ms à 928 ms**.
- **Moteur libVLC** : une seule instance est partagée par toute l'application (au lieu d'être recréée à chaque lecture), et elle est préchauffée en arrière-plan pendant l'affichage de l'accueil. L'inventaire des sorties audio, qui pouvait attendre l'initialisation du périphérique, est demandé hors du fil d'interface.
- **Pistes audio et sous-titres** : l'analyse des pistes au démarrage de la lecture ne bloque plus la fenêtre.
- **Reprendre la lecture** : les cartes de séries affichent maintenant la saison et l'épisode en clair (`S2 · E5` sur l'affiche, puis « Saison 2 · Épisode 5 » sous le titre), même quand le cache d'épisodes n'est pas encore chargé : la dernière trace de l'historique local est utilisée.
- L'option `--play <identifiant>` attend désormais la fin de la restauration de la bibliothèque au lieu de prendre l'identifiant pour un fichier externe.

## Diagnostic

- Les journaux `Perf lecture` (file, page, affichage, repos) et `UI gel` (avec `LYARANTV_UIPROBE=1`) permettent de mesurer un futur ralentissement sans outil externe.

---

# LyaranTV 1.3.0

Version de consolidation : démarrage beaucoup plus rapide, chargements visibles, mode langue et nettoyage de l'interface.

## Nouveautés

- **Mode langue** : dans **Paramètres → Mode langue**, activez l'option et saisissez un code (`FR`, `EN`, `AR`, `ES`…). Seules les catégories dont le nom contient ce code restent visibles. Le choix est enregistré et reste actif en revenant à l'accueil, à la bibliothèque ou après un redémarrage.
- **Bascule liste / grille** dans la bibliothèque : le mode choisi est mémorisé et retrouvé au retour du lecteur, d'une fiche ou au lancement suivant.
- **Bannière toujours habillée** : quand un film ou une série n'a pas de photo panoramique, son affiche sert de fond flou accordé, avec l'affiche nette présentée à droite — plus de cadre vide ni de portrait étiré.
- **Affiches plus complètes** : nouvelles sources de secours (iTunes en plus d'IMDb, TVmaze et Wikipédia) et nouvelle tentative automatique après cinq jours pour les titres précédemment introuvables. Le préchargement des affiches passe de 30 à 80 contenus.
- **Indicateur de travail** : un voile animé avec libellé couvre les opérations longues (sauvegarde, restauration, export de playlist) au lieu de laisser croire que l'application est figée.

## Performances

Sur un compte Xtream de **239 662 éléments** mesuré sur la machine de développement :

| Étape | Avant | Après |
|---|---|---|
| Restauration complète (interface) | 24,6 s | **2,2 s** |
| Application du cache TMDb | 17,3 s | 6,9 s la première fois, puis ignorée |
| Affichage de l'accueil | 6,1 s | 2,0 s |
| Barre latérale | 0,1 s | 0,1 s |

Le catalogue de l'accueil est désormais préparé pendant la restauration, les étagères ne reparcourent plus six fois les 240 000 éléments, le cache TMDb n'est réappliqué que s'il a changé et l'historique des sources est enregistré sans bloquer l'interface.

## Corrections

- **Plantait sur « Médias locaux »** avec « Caractères non conformes dans le chemin d'accès » : la page et ses fichiers de reprise corrompus ont été retirés.
- Favoris et récents restent à zéro tant qu'aucune playlist n'est chargée, au lieu d'afficher des compteurs sans contenu.
- Suppression complète de Letterboxd (interface, code et données) en attendant une intégration finalisée.
- La bibliothèque restaurée n'est plus reconstruite deux fois au démarrage.

## Technique

- Les traitements lourds (cache TMDb, historique, catalogue d'accueil) quittent le fil d'interface.
- Le cache de la bibliothèque est réenregistré une seule fois après application des fiches TMDb, pour que les lancements suivants n'aient plus rien à recalculer.
- `Dist\LyaranTV.exe` est l'unique binaire produit ; la copie à la racine du dépôt est supprimée.

---

# LyaranTV 1.2.1

## Mises à jour publiques sans jeton

- Le vérificateur essaie d'abord l'accès anonyme aux Releases publiques.
- Un jeton enregistré n'est utilisé qu'en secours pour les dépôts privés; un jeton expiré ne bloque donc plus les Releases publiques.
- Les téléchargements des installateurs et des empreintes SHA-256 utilisent le même accès anonyme avec secours authentifié.
- Les paramètres précisent qu'aucun jeton GitHub n'est nécessaire pour une Release publique.
- Installateur `setup-LyaranTV-1.2.1.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.2.0

## Test de mise à jour depuis la 1.1.9

- Version publiée pour vérifier le téléchargement, le remplacement dans le bon dossier et le redémarrage automatique corrigés en 1.1.9.
- Installateur `setup-LyaranTV-1.2.0.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.9

## Correctif de l’installation après téléchargement

- Le setup silencieux installe maintenant dans le dossier de l’exécutable qui a lancé la mise à jour, y compris les copies portables.
- Après une installation réussie, il relance la nouvelle version. En cas d’échec, il affiche le journal au lieu de fermer sans explication.
- L’installateur et les empreintes SHA-256 de la version 1.1.9 sont joints à la Release.

---

# LyaranTV 1.1.8

## Test de mise à jour depuis la 1.1.7

- Version publiée pour vérifier le téléchargement authentifié, le suivi de redirection et le contrôle SHA-256 du correctif 1.1.7.
- Installateur `setup-LyaranTV-1.1.8.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.7

## Correctif du téléchargement des mises à jour

- Le téléchargement d’un asset de dépôt privé passe par l’API GitHub authentifiée, puis suit sa redirection signée sans transmettre le jeton.
- Le programme exige une empreinte SHA-256 valide avant de proposer le fichier et vérifie le binaire téléchargé avant son lancement.
- Installateur `setup-LyaranTV-1.1.7.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.6

## Validation du vérificateur de mises à jour

- Version publiée pour vérifier la détection de la Release, le téléchargement de l’installateur et le contrôle SHA-256 depuis la version 1.1.5.
- Le correctif de lecture des assets GitHub de la 1.1.5 est inclus.
- Installateur `setup-LyaranTV-1.1.6.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.5

## Correctif des mises à jour

- Le vérificateur prend désormais en charge les différentes représentations JSON de la liste des fichiers d’une Release GitHub privée.
- L’installateur et les fichiers SHA-256 de la version 1.1.5 sont joints à la Release.

---

# LyaranTV 1.1.4

## Fiches Letterboxd intégrées

- Les notes, l’état vu/à voir et les critiques personnelles issues des CSV apparaissent en entier dans la fiche du film.
- Les critiques publiques récentes montrent le profil, l’auteur, la date, la note et le texte complet directement dans l’application.
- La fiche peut défiler pour lire les avis longs. Les boutons qui renvoyaient vers la page du film, l’avis ou le profil Letterboxd ont été retirés.
- L’installateur `setup-LyaranTV-1.1.4.exe` est joint à la Release GitHub avec les empreintes SHA-256.

---

# LyaranTV 1.1.3

## Letterboxd et mises à jour

- Le nom d’utilisateur Letterboxd suffit pour charger automatiquement l’activité publique récente via RSS; les profils d’amis deviennent facultatifs.
- Le vérificateur de mises à jour explique les permissions requises par les jetons GitHub fine-grained (`Contents: Read`).
- Aucun jeton personnel GitHub n’est intégré au nouvel installateur. Saisissez un jeton renouvelé dans **Paramètres → Mises à jour** après l’installation.
- Installateur `setup-LyaranTV-1.1.3.exe` et empreintes SHA-256 publiés avec la Release.

---

# LyaranTV 1.1.2

## Letterboxd et découverte

- Section **API Letterboxd** dans les réglages, avec l'explication des accès pris en charge et la configuration des profils publics via RSS.
- Import des notes et critiques personnelles depuis l'export CSV Letterboxd, ainsi que consultation des avis publics récents.
- Rapprochement des films et séries avec les titres de la bibliothèque IPTV, enrichissement des fiches et rangées de découverte Letterboxd sur l'accueil.
- Exécutable `LyaranTV.exe` fourni à la racine du dépôt. L'installateur `setup-LyaranTV-1.1.2.exe` est joint à la Release GitHub.

## Installation

Téléchargez `setup-LyaranTV-1.1.2.exe` depuis la Release GitHub, puis suivez l'assistant d'installation.

---

# LyaranTV 1.1.1

Jeton de mise à jour fourni dès l'installation.

## Nouveautés

- **Jeton GitHub intégré** : l'installateur enregistre le jeton du dépôt dans
  les réglages de l'application, chiffré par DPAPI pour le compte Windows
  courant. La vérification des mises à jour fonctionne donc dès la première
  ouverture, sans saisie. Un jeton déjà renseigné dans **Paramètres → Mises à
  jour** n'est jamais remplacé.
- **Installateur** `setup-LyaranTV-1.1.1.exe` : assistant en quatre étapes,
  cinq langues, choix du dossier avec contrôle de l'espace disque, progression
  détaillée, écran final avec lancement de l'application.

## Installation

1. Lancer `setup-LyaranTV-1.1.1.exe`.
2. Choisir la langue et le dossier si nécessaire.
3. Cocher ou décocher le raccourci Bureau, puis installer.

## Mise à jour

L'application interroge les Releases du dépôt dès le premier lancement grâce au
jeton fourni par l'installateur. Le jeton se change dans
**Paramètres → Mises à jour → Jeton GitHub**.
