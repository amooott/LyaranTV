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
