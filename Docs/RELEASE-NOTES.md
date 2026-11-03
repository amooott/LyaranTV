# LyaranTV 1.4.9

La carte de chargement attend vraiment la fin du générique.

## Correction

- **Carte affichée pendant le générique** : au démarrage, le chargement de la playlist part avant le générique, donc la carte croyait qu'aucune animation n'était en cours et s'ouvrait à 320 ms. Elle considère maintenant un générique « à venir » comme en cours et n'apparaît qu'une fois le fondu terminé.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.
