# LyaranTV 1.4.3

Générique de lancement, chargement discret et barre latérale soignée.

## Nouveautés

- **Générique de lancement** : halo qui s'ouvre, logo qui prend sa place avec un léger rebond, nom écrit lettre à lettre et ligne de progression, puis fondu vers l'interface. La séquence couvre toute la fenêtre, en fenêtré comme en plein écran, et s'efface d'un clic.
- **Chargement de playlist discret** : la grande fenêtre centrée est remplacée par une carte compacte (376 × 138) ancrée en bas à gauche de la fenêtre, avec marque, titre, playlist, barre fine, pourcentage, temps écoulé et estimation. L'application reste utilisable pendant le chargement, et le ✕ annule toujours l'opération.
- **Plus de clignotement** : la carte n'apparaît qu'après 320 ms ; une restauration instantanée depuis le cache n'affiche plus rien.

## Corrections

- **Logo rogné en mode cinéma** : dans la colonne réduite à 76 px, le bouton retour et la marque ne tenaient pas ensemble, et le logo était coupé au bord. Les deux sont maintenant dimensionnés pour la colonne.

## Notes de version

- Chaque release GitHub ne contient plus que sa propre version ; les notes des versions précédentes sont regroupées dans `Docs/RELEASE-HISTORY.md`.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.
