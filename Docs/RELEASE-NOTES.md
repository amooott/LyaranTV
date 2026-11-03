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
