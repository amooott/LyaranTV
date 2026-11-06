# LyaranTV 1.6.2

Navigation par type clarifiée et deuxième série de propositions de logo.

## Rail de catégories

- Le rail supérieur ne garde que les cinq entrées de découverte : **Accueil**, **Chaînes**, **Films**, **Séries** et **Ma liste**. Le **Guide EPG** reste accessible depuis la barre latérale, sans encombrer la navigation principale.
- Chaque entrée ouvre une page dédiée : **Chaînes en direct**, **Films**, **Séries** ou **Ma liste**, avec un titre et un sous-titre qui rappellent le nombre d'éléments et la source active.
- Les pages de type regroupent désormais le contenu en **rangées par catégorie du fournisseur** (films d'action, séries policières, chaînes sportives…), les catégories les plus fournies en premier. La bibliothèque complète conserve son exploration par catégories.

## Filtres sans doublon

- La rangée secondaire `Tout / Direct / Films / Séries / Effacer les filtres` disparaît de **Chaînes**, **Films**, **Séries** et **Ma liste** : le type est déjà porté par le rail, et la barre de recherche, le sélecteur de catégorie et le tri restent disponibles.
- Cette rangée reste utile dans la bibliothèque complète, où elle permet de passer d'un type à l'autre ; elle s'efface également quand une catégorie précise est déjà ouverte.

## Logos

- **Vingt nouvelles propositions** rejoignent les vingt premières : quarante variantes vectorielles au total, présentées en deux planches 5 × 4.
- [`Docs/logo-proposals.png`](logo-proposals.png) contient les propositions **01 à 20**, [`Docs/logo-proposals-2.png`](logo-proposals-2.png) les propositions **21 à 40**. Elles se régénèrent avec `--logogallery` et `--logogallery2`.
- Le logo actuel (**n° 17 · Égaliseur**) reste inchangé jusqu'au choix explicite de l'utilisateur.

## Vérifications

- Compilation des 57 sources.
- Auto-test : **35/35**.
- Compilation de l'installateur.
- Empreintes SHA-256 régénérées pour `LyaranTV.exe` et `setup-LyaranTV-1.6.2.exe`.
