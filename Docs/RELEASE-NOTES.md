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
