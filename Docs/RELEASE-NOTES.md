# LyaranTV 1.4.4

Le générique de lancement s'affiche réellement au démarrage.

## Corrections

- **Générique visible** : la séquence d'ouverture était posée dans la fenêtre principale, où les reconstructions de page du démarrage la recouvraient aussitôt. Elle vit désormais dans sa propre fenêtre, calée sur la fenêtre principale — en fenêtré comme en plein écran — et disparaît d'elle-même après 1,5 s, ou d'un clic.
- **Chargement de playlist compact** : carte de 376 × 138 en bas à gauche, affichée seulement si le chargement dépasse 320 ms ; l'application reste utilisable et le ✕ annule toujours l'opération.
- **Logo non rogné en mode cinéma** : le bouton retour et la marque tiennent ensemble dans la colonne de 76 px.

## Notes de version

- Chaque release GitHub ne contient que sa propre version ; les notes des versions précédentes sont dans `Docs/RELEASE-HISTORY.md`.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.
