# LyaranTV 1.6.6

Les boîtes de dialogue se posent maintenant sur un fond en verre dépoli.

## Verre dépoli

- **Un voile derrière chaque dialogue** : ajout de playlist, correction TMDb, réglages, sous-titres, mises à jour, profils. La fenêtre principale apparaît floutée et assombrie, la carte reste nette.
- **Comment** : WPF ne sait pas flouter ce qui se trouve derrière une fenêtre. Un instantané de l'interface est pris à l'ouverture, fortement flouté puis recouvert d'un voile sombre — le résultat est celui d'une vitre dépolie, sans dépendance graphique.
- **Un seul instantané** : il est pris une fois, avant l'ouverture. Aucun coût pendant la saisie, aucune animation ajoutée.
- **Repli sûr** : si la capture échoue (pilote graphique, fenêtre réduite), le dialogue s'ouvre normalement, sans voile.
- **Tiroir des chaînes du lecteur inchangé** : la vidéo n'est pas capturable par WPF, un flou n'y donnerait qu'un aplat noir. Le panneau garde son fond opaque.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.
