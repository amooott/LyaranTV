# LyaranTV 1.3.2

Chargement visible à l'ouverture de la bibliothèque, sans blocage de la fenêtre.

## Nouveautés

- **Écran de chargement de la bibliothèque** : sur un gros catalogue, la page Bibliothèque apparaît immédiatement avec un indicateur animé, le nombre d'éléments indexés et un message clair ; elle laisse place à la bibliothèque dès que celle-ci est prête. Sur un petit compte, aucun écran intermédiaire n'est affiché : la page s'ouvre directement.
- **Préparation en arrière-plan** : le filtrage, le tri et la construction des listes ne s'exécutent plus sur le fil d'interface. Sur le compte de 239 662 éléments, la préparation prend **197 ms hors interface** et le rendu **232 ms**, soit **429 ms au total** contre 1 085 ms auparavant, sans gel de la fenêtre.
- **Barre d'état restaurée** après l'ouverture : elle ne reste plus bloquée sur « Chargement de la bibliothèque… ».

---

# LyaranTV 1.3.1

Correctif des gels perçus au lancement d'un film ou d'une série.

## Corrections

- **Gel au lancement d'une lecture** : le programme relisait et réécrivait le fichier `history-sources.json` (9,7 Mo, 239 671 entrées) à chaque ouverture de lecteur, sur le fil d'interface. Le fichier est maintenant chargé une fois en mémoire et enregistré en arrière-plan, 1,5 s après la dernière modification. Mesure sur le compte de 239 662 éléments : ouverture du lecteur en **371 ms** au lieu de plusieurs secondes.
- **File de lecture** : elle ne recopie plus tout le catalogue (jusqu'à 240 000 éléments) à chaque lecture ; seule une fenêtre autour du contenu lancé est conservée.
- **Recherche des contenus** : `FindChannel` et `SeriesForEpisode` parcouraient linéairement les 240 000 entrées, à chaque carte de l'accueil. Un index est construit une fois par chargement de bibliothèque. L'accueil passe de **2 227 ms à 928 ms**.
- **Moteur libVLC** : une seule instance est partagée par toute l'application (au lieu d'être recréée à chaque lecture), et elle est préchauffée en arrière-plan pendant l'affichage de l'accueil. L'inventaire des sorties audio, qui pouvait attendre l'initialisation du périphérique, est demandé hors du fil d'interface.
- **Pistes audio et sous-titres** : l'analyse des pistes au démarrage de la lecture ne bloque plus la fenêtre.
- **Reprendre la lecture** : les cartes de séries affichent maintenant la saison et l'épisode en clair (`S2 · E5` sur l'affiche, puis « Saison 2 · Épisode 5 » sous le titre), même quand le cache d'épisodes n'est pas encore chargé : la dernière trace de l'historique local est utilisée.
- L'option `--play <identifiant>` attend désormais la fin de la restauration de la bibliothèque au lieu de prendre l'identifiant pour un fichier externe.

## Diagnostic

- Les journaux `Perf lecture` (file, page, affichage, repos) et `UI gel` (avec `LYARANTV_UIPROBE=1`) permettent de mesurer un futur ralentissement sans outil externe.

---

# LyaranTV 1.3.0

Version de consolidation : démarrage beaucoup plus rapide, chargements visibles, mode langue et nettoyage de l'interface.

## Nouveautés

- **Mode langue** : dans **Paramètres → Mode langue**, activez l'option et saisissez un code (`FR`, `EN`, `AR`, `ES`…). Seules les catégories dont le nom contient ce code restent visibles. Le choix est enregistré et reste actif en revenant à l'accueil, à la bibliothèque ou après un redémarrage.
- **Bascule liste / grille** dans la bibliothèque : le mode choisi est mémorisé et retrouvé au retour du lecteur, d'une fiche ou au lancement suivant.
- **Bannière toujours habillée** : quand un film ou une série n'a pas de photo panoramique, son affiche sert de fond flou accordé, avec l'affiche nette présentée à droite — plus de cadre vide ni de portrait étiré.
- **Affiches plus complètes** : nouvelles sources de secours (iTunes en plus d'IMDb, TVmaze et Wikipédia) et nouvelle tentative automatique après cinq jours pour les titres précédemment introuvables. Le préchargement des affiches passe de 30 à 80 contenus.
- **Indicateur de travail** : un voile animé avec libellé couvre les opérations longues (sauvegarde, restauration, export de playlist) au lieu de laisser croire que l'application est figée.

## Performances

Sur un compte Xtream de **239 662 éléments** mesuré sur la machine de développement :

| Étape | Avant | Après |
|---|---|---|
| Restauration complète (interface) | 24,6 s | **2,2 s** |
| Application du cache TMDb | 17,3 s | 6,9 s la première fois, puis ignorée |
| Affichage de l'accueil | 6,1 s | 2,0 s |
| Barre latérale | 0,1 s | 0,1 s |

Le catalogue de l'accueil est désormais préparé pendant la restauration, les étagères ne reparcourent plus six fois les 240 000 éléments, le cache TMDb n'est réappliqué que s'il a changé et l'historique des sources est enregistré sans bloquer l'interface.

## Corrections

- **Plantait sur « Médias locaux »** avec « Caractères non conformes dans le chemin d'accès » : la page et ses fichiers de reprise corrompus ont été retirés.
- Favoris et récents restent à zéro tant qu'aucune playlist n'est chargée, au lieu d'afficher des compteurs sans contenu.
- Suppression complète de Letterboxd (interface, code et données) en attendant une intégration finalisée.
- La bibliothèque restaurée n'est plus reconstruite deux fois au démarrage.

## Technique

- Les traitements lourds (cache TMDb, historique, catalogue d'accueil) quittent le fil d'interface.
- Le cache de la bibliothèque est réenregistré une seule fois après application des fiches TMDb, pour que les lancements suivants n'aient plus rien à recalculer.
- `Dist\LyaranTV.exe` est l'unique binaire produit ; la copie à la racine du dépôt est supprimée.

---

# LyaranTV 1.2.1

## Mises à jour publiques sans jeton

- Le vérificateur essaie d'abord l'accès anonyme aux Releases publiques.
- Un jeton enregistré n'est utilisé qu'en secours pour les dépôts privés; un jeton expiré ne bloque donc plus les Releases publiques.
- Les téléchargements des installateurs et des empreintes SHA-256 utilisent le même accès anonyme avec secours authentifié.
- Les paramètres précisent qu'aucun jeton GitHub n'est nécessaire pour une Release publique.
- Installateur `setup-LyaranTV-1.2.1.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.2.0

## Test de mise à jour depuis la 1.1.9

- Version publiée pour vérifier le téléchargement, le remplacement dans le bon dossier et le redémarrage automatique corrigés en 1.1.9.
- Installateur `setup-LyaranTV-1.2.0.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.9

## Correctif de l’installation après téléchargement

- Le setup silencieux installe maintenant dans le dossier de l’exécutable qui a lancé la mise à jour, y compris les copies portables.
- Après une installation réussie, il relance la nouvelle version. En cas d’échec, il affiche le journal au lieu de fermer sans explication.
- L’installateur et les empreintes SHA-256 de la version 1.1.9 sont joints à la Release.

---

# LyaranTV 1.1.8

## Test de mise à jour depuis la 1.1.7

- Version publiée pour vérifier le téléchargement authentifié, le suivi de redirection et le contrôle SHA-256 du correctif 1.1.7.
- Installateur `setup-LyaranTV-1.1.8.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.7

## Correctif du téléchargement des mises à jour

- Le téléchargement d’un asset de dépôt privé passe par l’API GitHub authentifiée, puis suit sa redirection signée sans transmettre le jeton.
- Le programme exige une empreinte SHA-256 valide avant de proposer le fichier et vérifie le binaire téléchargé avant son lancement.
- Installateur `setup-LyaranTV-1.1.7.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.6

## Validation du vérificateur de mises à jour

- Version publiée pour vérifier la détection de la Release, le téléchargement de l’installateur et le contrôle SHA-256 depuis la version 1.1.5.
- Le correctif de lecture des assets GitHub de la 1.1.5 est inclus.
- Installateur `setup-LyaranTV-1.1.6.exe` et empreintes SHA-256 joints à la Release.

---

# LyaranTV 1.1.5

## Correctif des mises à jour

- Le vérificateur prend désormais en charge les différentes représentations JSON de la liste des fichiers d’une Release GitHub privée.
- L’installateur et les fichiers SHA-256 de la version 1.1.5 sont joints à la Release.

---

# LyaranTV 1.1.4

## Fiches Letterboxd intégrées

- Les notes, l’état vu/à voir et les critiques personnelles issues des CSV apparaissent en entier dans la fiche du film.
- Les critiques publiques récentes montrent le profil, l’auteur, la date, la note et le texte complet directement dans l’application.
- La fiche peut défiler pour lire les avis longs. Les boutons qui renvoyaient vers la page du film, l’avis ou le profil Letterboxd ont été retirés.
- L’installateur `setup-LyaranTV-1.1.4.exe` est joint à la Release GitHub avec les empreintes SHA-256.

---

# LyaranTV 1.1.3

## Letterboxd et mises à jour

- Le nom d’utilisateur Letterboxd suffit pour charger automatiquement l’activité publique récente via RSS; les profils d’amis deviennent facultatifs.
- Le vérificateur de mises à jour explique les permissions requises par les jetons GitHub fine-grained (`Contents: Read`).
- Aucun jeton personnel GitHub n’est intégré au nouvel installateur. Saisissez un jeton renouvelé dans **Paramètres → Mises à jour** après l’installation.
- Installateur `setup-LyaranTV-1.1.3.exe` et empreintes SHA-256 publiés avec la Release.

---

# LyaranTV 1.1.2

## Letterboxd et découverte

- Section **API Letterboxd** dans les réglages, avec l'explication des accès pris en charge et la configuration des profils publics via RSS.
- Import des notes et critiques personnelles depuis l'export CSV Letterboxd, ainsi que consultation des avis publics récents.
- Rapprochement des films et séries avec les titres de la bibliothèque IPTV, enrichissement des fiches et rangées de découverte Letterboxd sur l'accueil.
- Exécutable `LyaranTV.exe` fourni à la racine du dépôt. L'installateur `setup-LyaranTV-1.1.2.exe` est joint à la Release GitHub.

## Installation

Téléchargez `setup-LyaranTV-1.1.2.exe` depuis la Release GitHub, puis suivez l'assistant d'installation.

---

# LyaranTV 1.1.1

Jeton de mise à jour fourni dès l'installation.

## Nouveautés

- **Jeton GitHub intégré** : l'installateur enregistre le jeton du dépôt dans
  les réglages de l'application, chiffré par DPAPI pour le compte Windows
  courant. La vérification des mises à jour fonctionne donc dès la première
  ouverture, sans saisie. Un jeton déjà renseigné dans **Paramètres → Mises à
  jour** n'est jamais remplacé.
- **Installateur** `setup-LyaranTV-1.1.1.exe` : assistant en quatre étapes,
  cinq langues, choix du dossier avec contrôle de l'espace disque, progression
  détaillée, écran final avec lancement de l'application.

## Installation

1. Lancer `setup-LyaranTV-1.1.1.exe`.
2. Choisir la langue et le dossier si nécessaire.
3. Cocher ou décocher le raccourci Bureau, puis installer.

## Mise à jour

L'application interroge les Releases du dépôt dès le premier lancement grâce au
jeton fourni par l'installateur. Le jeton se change dans
**Paramètres → Mises à jour → Jeton GitHub**.
