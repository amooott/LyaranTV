# LyaranTV 1.6.7

Une seule échelle de tailles, une seule profondeur d'ombre.

## Échelle typographique

- **Quatre paliers** au lieu de trente-sept tailles : 12,5 px pour les métadonnées, 15 px pour le texte courant, 19 px pour les titres de section, 23 px pour les grands titres. Les affichages d'accueil au-delà de 25 px gardent leur valeur.
- **Plus de taille morte** : les 9,5 / 10 / 10,5 / 11 / 11,5 demandées au fil du temps sont ramenées au palier bas. Elles s'affichaient déjà à 12,5 par le plancher de lisibilité, mais le code laissait croire à une hiérarchie qui n'existait pas.
- **Hiérarchie réelle** : 13, 13,5 et 14 passent à 12,5 ; 14,5 à 16 passent à 15 ; 17 à 20 passent à 19 ; 21 à 24 passent à 23. Deux textes voisins ne peuvent plus se disputer la même place de 1 px près.

## Ombres

- **Deux profondeurs** : « carte » (flou 20, décalage 6, 34 %) et « boîte » (flou 34, décalage 12, 50 %). Les dix-huit ombres noires héritées de valeurs dispersées passent par l'une ou l'autre.
- **Les halos d'accent restent** : ce sont des lueurs de sélection, pas des élévations — ils gardent leur couleur et leur flou propres.
- **Les ombres de texte restent** : sous les sous-titres et sur la vidéo, un flou de 3,5 à 5 px sert la lisibilité, pas la profondeur.

## Vérifications

- Compilation de toutes les sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.
