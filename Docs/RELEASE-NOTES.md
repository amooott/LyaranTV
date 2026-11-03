# LyaranTV 1.4.19

Le rail de volume reste lisible quand il est au maximum.

## Volume

- **Remplissage qui rejoint la pastille** : le rose s'arrête désormais au bord droit de la pastille et non à son centre. Une marge négative de 7 px (la moitié de la largeur de la pastille) comble le vide qui donnait l'impression d'une barre interrompue.
- **Piste toujours visible** : le rail vide passe de 36 % à 62 % de blanc translucide (84 % au survol). À 100 %, on voit donc encore la piste et le contrôle se lit comme un curseur, pas comme un simple trait.
- **Pastille plus étroite** : 14 px de gabarit au lieu de 20, pour que son centre coïncide avec l'extrémité du remplissage. Le point visible reste à 9 px (11 au survol).
- **Résultat** : à 100 %, le rose touche le bord droit du rail, la pastille est posée dessus et il ne reste qu'un liseré d'un pixel avant le pourcentage.

## Vérifications

- Compilation des 52 sources.
- Auto-test : **34/34**.
- Compilation de l'installateur.
