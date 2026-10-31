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
