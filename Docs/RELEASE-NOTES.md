# LyaranTV 1.6.3

Nouveau logo « Voile », sans triangle de lecture.

## Logo final

- La **proposition n° 24 · Voile** est retenue, **sans le triangle de lecture** : deux voiles superposés, l'un blanc, l'autre cyan, sur le carré arrondi violet-bleu historique.
- Le dessin remplace l'ancien égaliseur partout : barre supérieure, fenêtre, générique de lancement, page À propos, écran de profil et icône de l'application.
- Une version compacte à voiles pleins est utilisée sous 30 px pour rester lisible en 16 et 20 px.
- La planche de contrôle est disponible dans [`Docs/logo-final.png`](logo-final.png). Les quarante propositions restent consultables dans [`Docs/logo-proposals.png`](logo-proposals.png) et [`Docs/logo-proposals-2.png`](logo-proposals-2.png).

## Outils de marque

- `--logofinal <fichier.png>` génère la planche du logo final aux tailles réelles de 16 à 256 px.
- `--makeicon <fichier.ico>` régénère l'icône multi-résolutions à partir du même dessin vectoriel. L'icône livrée contient neuf tailles, de 16 à 256 px.

## Vérifications

- Compilation des 58 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.3.exe`.
