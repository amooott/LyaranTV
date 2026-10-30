# LyaranTV 1.0.0

Première version publiée et version de référence du système de mise à jour.

## Nouveautés

- **Installateur** `setup-LyaranTV.exe` : français, anglais, espagnol et russe
  (langue de l'ordinateur par défaut), raccourci Bureau coché, installation par
  utilisateur dans `%LocalAppData%\LyaranTV\app` sans droits administrateur.
  Les données existantes de `%AppData%\LyaranTV` ne sont jamais modifiées.
- **libVLC embarqué** : plus besoin d'installer VLC à part, la lecture des pistes
  AC-3, E-AC-3, DTS et TrueHD est assurée par le moteur livré avec l'application.
- **Cinq langues d'interface** : français, anglais, espagnol, russe et portugais,
  avec détection de la langue de Windows et sélecteur en bas de la barre latérale.
- **Mises à jour** : vérification des Releases GitHub, jeton du dépôt privé
  stocké chiffré, empreinte SHA-256 contrôlée avant exécution.
- **Fondations** : version 1.0.0 dans l'assembly (description, éditeur, icône),
  TLS 1.2 forcé, instance unique, page À propos avec attribution TMDb.

## Installation

1. Lancer `setup-LyaranTV.exe`.
2. Choisir la langue et le dossier si nécessaire.
3. Cocher ou décocher le raccourci Bureau, puis installer.

## Mise à jour

L'application interroge les Releases du dépôt. Renseigner le jeton GitHub dans
**Paramètres → Mises à jour → Jeton GitHub** (dépôt privé), puis
**Rechercher une mise à jour**.
