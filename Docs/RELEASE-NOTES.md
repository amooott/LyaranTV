# LyaranTV 1.4.13

Six finitions de plus : continuité visuelle avec l'installateur, thème clair
cohérent, étagères en cascade, pied de page lisible, états vides illustrés et
fondu entre thèmes.

## Installateur

- **Même langage visuel que l'application** : il dessine désormais avec Inter quand elle est installée, sinon Segoe UI — exactement la règle de la fenêtre principale. La deuxième teinte du dégradé devient une constante partagée (`Theme.Secondary`) au lieu d'une valeur recopiée.
- **Coins arrondis et liseré natifs** sur la fenêtre de progression de mise à jour, comme la fenêtre principale.

## Thème clair

- **Bandeaux de fiche teintés selon la palette** : la fiche d'un film, celle d'une série, le bandeau de la bibliothèque et l'aperçu des sous-titres partageaient un noir codé en dur (`#08080E`). Ils reposent maintenant sur une couleur `HeroBase` propre à chaque thème : prune profond en thème clair, noir en AMOLED, charbon en sombre. Le bandeau reste cinématographique sans former un trou noir sur une page claire.
- Les dégradés de secours (sans affiche) mélangent cette base à l'accent au lieu d'une teinte violette figée.

## Accueil et navigation

- **Étagères en cascade** : les blocs d'une page entrent avec 30 ms d'écart (plafonné à huit), au lieu d'apparaître d'un seul mur. La réduction des animations reste respectée.
- **États vides illustrés** : halo diffus, médaillon arrondi teinté par l'accent et pictogramme large remplacent la petite icône seule.

## Pied de page

- **40 px au lieu de 32** : c'était la zone la plus compressée de l'application, avec du texte à 10,5 px. Le pied affiche maintenant une pastille d'état colorée, un libellé à 12 px et la source active à 11,5 px.

## Thème et accent

- **Fondu de 220 ms** entre l'ancienne et la nouvelle palette lors d'un changement de thème, de couleur d'accent ou d'ambiance : le réglage se lit comme une transition, pas comme un rechargement.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.
