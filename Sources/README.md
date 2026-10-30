# Sources de LyaranTV

La documentation complète se trouve dans les fichiers à la racine du projet :

- [`README.md`](../README.md) — présentation, démarrage, compilation et tests.
- [`Docs/REFERENCE.md`](../Docs/REFERENCE.md) — options CLI, raccourcis, formats et fichiers de données.
- [`Docs/DESIGN.md`](../Docs/DESIGN.md) — identité visuelle, thèmes, composants et accessibilité.
- [`Docs/BUGS.md`](../Docs/BUGS.md) — corrections, limites connues et diagnostic.
- [`Docs/FEATURES.md`](../Docs/FEATURES.md) — état des fonctionnalités P0/P1/P2.

## Organisation

| Dossier | Contenu |
|---|---|
| `Core/` | Modèles, stockage, chargement M3U/Xtream/Stalker, EPG, TMDb, affiches de secours (TVmaze/IMDb/Wikipédia), OpenSubtitles, sous-titres et pistes embarquées MKV/MP4, réseau, proxy de lecture, chiffrement. |
| `UI/` | Fenêtre principale, pages (dont `PagesLibrary.cs` pour la bibliothèque découverte), lecteur, recherche de sous-titres en ligne, résolution des affiches et aperçus, dialogues, thèmes, composants et chargement des images. |
| `LyaranTV.cs` | Point d'entrée, options de ligne de commande et auto-tests. |
| `Reconstruire.bat` | Compilation avec le `csc.exe` de .NET Framework 4.x, sortie dans `..\Dist\LyaranTV.exe`. |

Le code reste volontairement compatible C# 5 et ne dépend d'aucun SDK NuGet. Les images distantes (affiches TMDb, vignettes, logos de chaînes) sont lues à la demande, décodées à 420 px de large, puis conservées dans un cache mémoire (environ 220 images) et un cache disque borné sous `%APPDATA%\LyaranTV\covers`.
