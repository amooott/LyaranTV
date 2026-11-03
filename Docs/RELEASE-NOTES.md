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
