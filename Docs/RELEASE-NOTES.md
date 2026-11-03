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
