# LyaranTV 1.4.10

La barre de téléchargement de la mise à jour est refaite.

## Nouveau

- **Bandeau de progression pleine largeur** : il remplace la petite barre de 210 px coincée entre les boutons. Il occupe toute la largeur de la fenêtre, sous les notes de version.
- **Chiffres utiles** : pourcentage à droite, puis volume reçu sur total, débit moyen et temps restant estimé (« 12,4 Mo / 38,0 Mo · 1,7 Mo/s · ≈ 14 s restantes »).
- **Étapes explicites** : « Téléchargement de la mise à jour », puis « Installation » avec la mention de l'empreinte SHA-256 vérifiée, au lieu d'une barre sans contexte.

## Correction

- La barre ne passe plus par un état indéterminé au départ : elle affiche 0 % et « Connexion à GitHub… », puis progresse dès les premiers octets.
- Le pourcentage est borné à 100 même si GitHub annonce une taille approximative.

## Vérifications

- Auto-test : **34/34**.
- Compilation de l'application et de l'installateur.
