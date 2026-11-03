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
