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
