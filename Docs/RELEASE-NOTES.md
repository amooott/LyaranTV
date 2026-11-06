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
