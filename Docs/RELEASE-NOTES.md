# LyaranTV 1.4.8

La carte de chargement attend la fin du générique de lancement.

## Correction

- **Carte de restauration affichée trop tôt** : elle s'ouvrait pendant l'animation d'ouverture, par-dessus le générique. Elle n'apparaît plus qu'une fois le générique terminé (ou après 6 s de garde-fou), puis suit son délai de 320 ms pour éviter tout clignotement sur les restaurations instantanées.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.
