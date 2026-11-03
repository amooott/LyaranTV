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
