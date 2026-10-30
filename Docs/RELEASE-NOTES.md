# LyaranTV 1.1.0

Installateur entièrement redessiné, binaires versionnés et dépôt réorganisé.

## Nouveautés

- **Nouvel installateur** : assistant en quatre étapes (bienvenue, dossier,
  installation, fin) avec barre latérale d'étapes, contrôles dessinés dans le
  thème du programme et barre de titre sombre. Cinq langues (français, anglais,
  espagnol, russe, portugais), détection de la langue de Windows, choix du
  dossier avec contrôle de l'espace disque, raccourci Bureau optionnel,
  progression détaillée par opération avec temps écoulé et temps restant, écran
  final avec lancement de l'application et reprise en cas d'échec.
- **Installateur versionné** : `setup-LyaranTV-1.1.0.exe`, accompagné de son
  empreinte SHA-256. Le numéro vient de `Sources/App/AppInfo.cs` et se change
  partout avec `Tools/version.sh`.
- **Dépôt rangé** : sorties dans `Dist\`, scripts dans `Tools\`, données de test
  dans `build\tmp\`, esquisses de logo avec les logos.
- **libVLC embarqué** : plus besoin d'installer VLC à part, la lecture des pistes
  AC-3, E-AC-3, DTS et TrueHD est assurée par le moteur livré avec l'application.
- **Cinq langues d'interface** : français, anglais, espagnol, russe et portugais,
  avec détection de la langue de Windows et sélecteur en bas de la barre latérale.
- **Mises à jour** : vérification des Releases GitHub, jeton du dépôt privé
  stocké chiffré, empreinte SHA-256 contrôlée avant exécution.

## Installation

1. Lancer `setup-LyaranTV-1.1.0.exe`.
2. Choisir la langue et le dossier si nécessaire.
3. Cocher ou décocher le raccourci Bureau, puis installer.

## Mise à jour

L'application interroge les Releases du dépôt. Renseigner le jeton GitHub dans
**Paramètres → Mises à jour → Jeton GitHub** (dépôt privé), puis
**Rechercher une mise à jour**.
