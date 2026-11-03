# LyaranTV 1.4.11

Sept finitions d'interface, toutes visibles sans rien changer à l'usage.

## Navigation

- **Indicateur glissant** : le trait d'accent de la barre latérale ne réapparaît plus sous chaque libellé, il glisse d'un onglet à l'autre en 240 ms, avec un léger halo qui l'accompagne. Il suit aussi le défilement du menu.
- **Anneau de focus** : au clavier comme à la manette, le bouton actif reçoit un second liseré détaché de 2 px, lisible d'un coup d'œil au lieu d'un simple changement de bordure.

## Chargement

- **Squelettes animés** : tant qu'une affiche n'est pas arrivée, un reflet diagonal balaie le cadre toutes les 1,15 s au lieu d'un rectangle vide. Il disparaît dès l'image posée, et ne tourne jamais indéfiniment sur un visuel introuvable.
- **Transitions de page** : fondu, glissement et très court zoom arrière (300 ms) pour que la page se pose dans la continuité de la carte cliquée.

## Couleur de l'œuvre

- **Teinte dominante** : la fiche d'un film ou d'une série échantillonne son affiche (18 × 18 pixels) et en tire un halo coloré qui apparaît en fondu. Chaque œuvre éclaire sa propre fiche, au lieu de répéter l'accent de l'application.

## Fenêtre

- **Coins arrondis Windows 11** : la fenêtre demande enfin l'arrondi natif (attribut DWM 33) et un liseré assorti au thème (attribut 34), au lieu de rester à angles droits alors que tout l'intérieur est arrondi.
- **Rendu des textes en mode Display** : les petites tailles restent nettes sur un écran dense.

## Mises à jour

- **Reflet sur la barre** : un balayage lumineux parcourt la barre pendant le téléchargement, puis s'arrête quand l'installation commence.

## Sons

- **Sons d'interface discrets** : tick de navigation et carillon de lancement, synthétisés par le programme (aucun fichier audio embarqué), coupables d'un seul réglage dans Apparence.

## Thème clair

- **Vignettes d'épisodes** : leur fond suivait une couleur sombre codée en dur, il suit désormais la palette et reste cohérent en thème clair.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.
