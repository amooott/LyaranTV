# LyaranTV 1.4.5

Le lecteur retrouve ses commandes : retour, « C » et liste des chaînes.

## Correction

- **Générique qui bloquait l'interface** : la fenêtre du générique restait ouverte au-dessus de l'application quand son fondu échouait, et interceptait clics et raccourcis — d'où le bouton retour et la touche « C » sans effet dans le lecteur. Elle est désormais transparente aux clics, sans activation, se ferme par minuterie (et par un filet de sécurité à 4 s) et s'efface au premier clic dans l'application.

## Vérifications

- Auto-test : **34/34**.
- Compilation avec `Sources\Reconstruire.bat`.
