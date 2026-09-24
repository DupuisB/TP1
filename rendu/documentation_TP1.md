# TP1 — Documentation Groupe 1

## Introduction

Pour ce TP, on a implémenté plusieurs méthodes de déplacement et de navigation en VR (sur Quest 3) avec des méthodes de mitigation de la cinétose. On a implémenté le déplacement physique, le déplacement continu au joystick, la téléportation et un dash. On a également ajouté l'option d'avoir un effet blink pour la téléportation et une vignette de confort pour le dash/déplacement continu.

Le build démarre dans une petite salle qui reprend exactement les exigences du TP (`Debut` dans Unity). Un tutoriel y présente les contrôles. A la fin, un portail vers une autre scène (`Demo` dans Unity) s'ouvre. Cette scène sert à tester les déplacements dans un environnement extérieur plus grand.

## Choix techniques

En premier lieu, on a choisi d'utiliser OpenXR plutôt que le SDK Meta Quest. Bien que le SDK de Meta semble bien supporté, et que les tutoriels de Meta soient très complets, on s'est dit que OpenXR est plus standard et nous donnerai de meilleures bases pour des projets futurs (qui ne seront pas forcément sur Quest).

### IA pour Unity

Dans ce projet, on a pas mal utilisé de LLMs. Pour cette raison, plusieurs assets (en gros ceux qui n'ont pas été récupéré directement sur le store) sont directement définis dans le code, et non en Prefab. On pourrait les convertir pour permettre de mieux travailler avec dans l'éditeur, mais on a pas eu le temps (ni le besoin) de le faire.

### Interface, tutoriel et portails

On a mis le menu principal sur le contrôleur gauche, sur le dessus de l’avant-bras (comme une montre). Il apparaît quand la montre virtuelle est tournée vers le casque, à une distance de lecture comprise entre 0,18 m et 0,85 m. Le regard doit aussi être orienté vers le poignet.

Les boutons permettent de controller toutes les options mentionnées en dessous.

Dans la scène tutoriel, `TutorialFlowController` guide l’utilisateur sur quatre actions : ouvrir le menu, atteindre la cible de téléportation, effectuer un dash, puis parcourir 3 m en marche continue. On aurait aussi pu expliquer qu'on ne peut pas se téléporter sur les boites rouges, mais ca faisait un peu long. On a aussi ajouté un portail, qui reste verrouillé pendant le tutoriel.

Le portail (`SceneTeleportPortal`) relie les deux scènes. Il peut être activé par sélection avec le rayon ou en traversant son ouverture. La transition fait une vibration, un fondu puis charge l'autre scène.

Pour la seconde scène, on a utilisé un asset trouvé sur le store fait par le groupe Synty([disponible ici](https://assetstore.unity.com/packages/3d/environments/polygon-starter-pack-art-by-synty-156819)).

### Déplacement et contrôles

Le `TP1ComfortManager` active un seul mode de translation à la fois. La rotation est toujours sur le joystick droit et le gauche pour les déplacements. On avait commencé avec l'inverse, mais ca n'était pas intuitif et naturel (l'inverse est plus standard). On a implémenté ces types de déplacements et contrôles:

| Fonction | Contrôle Quest 3 | Implémentation |
| --- | --- | --- |
| Déplacement physique | Marcher dans la zone réelle | Suivi de la tête par le `XROrigin`, avec origine de tracking au sol |
| Marche continue | Joystick gauche | `ContinuousMoveProvider`, vitesse configurée à 4 m/s, direction relative à la caméra et strafe activé |
| Téléportation | Joystick gauche vers le haut pour viser, relâcher pour valider | `XRRayInteractor`, arc de prévisualisation, `TeleportationArea` et `ComfortTeleportationProvider` |
| Dash | Même visée que la téléportation, en mode Dash | `DashProvider`, durée de 0,20 s et cooldown de 0,25 s |
| Snap Turn | Joystick droit | `SnapTurnProvider`, incrément de 45° et debounce de 0,5 s (plus agréable) |
| Smooth Turn | Joystick droit | `ContinuousTurnProvider`, vitesse de 60°/s |
| Interface du poignet | Tourner le poignet gauche vers le regard (comme une montre) puis viser les boutons avec la manette droite | `WristUIController` et `XRUIInputModule`; la gâchette droite est mappée à `UI Press` |

Notre `CharacterController` et le `CharacterControllerDriver` suivent la hauteur du casque et bloquent les déplacements contre les colliders (i.e. pas possible de traverser un mur en se déplacant en jeu). La hitbox mesure initialement 1,8 m de haut et 0,35 m de rayon. Sa hauteur peut aller de 0,5 à 2,2 m.

En mode Téléportation, le rayon affiche la trajectoire et le point d’arrivée avant validation. Les surfaces valides utilisent `TeleportationArea`. `BlockedTeleportArea` laisse le rayon détecter la surface, mais refuse la sélection et la demande de téléportation. Cela est mieux que de ne rien mettre car ca offre un meilleur rendu visuel. 

Pour le dash, on interpole le rig vers la destination avec une courbe d’easing. Avant le déplacement, `DashProvider` regarde si il y a un obstacle sur le trajet pour le bloquer (pour un dash, on s'est dit que passer à travers un obstacle bloquant complétement le passage était étrange). Il compense le décalage entre le casque et l’origine du rig, gère les montées et descentes et recale l’arrivée sur le sol. Des vibrations signalent le départ et l’arrivée. Une vibration plus forte indique un trajet bloqué.

Le projet propose aussi quelques raccourcis clavier directs pour les essais dans l’Editor (qui ne marchent pas sur certains PC, on ne sait pas pourquoi...):

| Touche | Action |
| --- | --- |
| `M` | Afficher ou masquer le menu de poignet sur Desktop |
| `1` / `2` / `3` | Marche continue / téléportation / dash |
| `4` | Activer ou désactiver le blink de la téléportation |
| `5` | Activer ou désactiver la vignette |
| `6` | Basculer snap turn / continuous turn |
| `T` | Téléportation test à 3m  |

### Confort visuel

Le `TunnelingVignetteController` est attaché à la caméra. Il s'active en cas de smooth turn, de déplacement continu ou de dash. L’option est activée par défaut et peut être changée depuis le menu du poignet.

En mode Téléportation, `ComfortTeleportationProvider` masque le déplacement avec `ScreenFadeCanvas` : fondu au noir de 0,08 s, déplacement, puis retour à l’image en 0,08 s. Le blink est actif par défaut. Le menu permet de le désactiver. En mode Dash, le fondu est remplacé par la vignette.

Le `ScreenFadeCanvas` sert aussi aux changements de scène. Les portails appliquent un fondu avant le chargement asynchrone, avec un fondu sortant configuré à 0,25 s.

## Tests effectués

On a principalement testé sur Quest 3 directement, mais on a également tout testé en simulateur. Dans l'ensemble tout fonctionnait comme prévu. On a tout de même noté ces quelques bugs, qui pourraient être corrigés dans une version future :

- Le menu du poignet ne s'affiche pas toujours à un endroit agréable. On peut surêment modifier un peu la position de l'UI et ses conditions de déclenchement pour que ce soit plus naturel.
- Le dash peut parfois être bloqué par un obstacle minime, nottament lorsque la destination est plus haute/plus basse. On a quand même mis à jour notre système et il est assez robuste.
- Le haut des murs est parfois téléportable dans la seconde scène. C'est dû au fait que notre liste d'assets correspondant aux murs n'est pas complète.

## Problèmes rencontrés

- Faire marcher le simulateur meta: l'un d'entre nous avait un nom d'utilisateur Windows avec accent (Gaël) qui faisait planter le simulateur, il n'y avait pas de message d'erreur clair pour expliquer le problème. Après de nombreuses heures, on est tombé sur un post reddit qui mentionnait le problème et on l'a résolu en créant un nouvel utilisateur.  On a encore un problème avec quelqu'un d'autre et on n'a pas trouvé de solution pour qu'il marche à chaque fois.
- Pour une raison encore inconnue, sur un ordi directx11 et directx12 font planter le simulateur on utilise donc vulkan au final même si ce n'est pas la configuration recommandée.
- Pour continuer sur le simulateur: il est compliqué à utiliser (contrôles des contrôleurs), on a donc utilisé principalement le casque et les manettes réelles pour nos tests. On a aussi implémenté quelques tests pour débug sans utiliser les manettes (en simulant les entrées via le clavier)
- Pour faire un déplacement style dash il faut pouvoir le différencier d'une simple téléporation. Quand on dash on ne peut pas passer à travers un obstacle, contrairement à la téléportation. Il faut donc implémenter une détection de collision appropriée pour le dash
- Au début on pouvait se téléporter sur le côté des obstacles car la téléportation area était sur le côté des murs aussi et pas sur le dessus

## Remise

**Dépôt Git :** [github.com/DupuisB/TP1](https://github.com/DupuisB/TP1.git)

> Note: la vidéo sur simulateur sert juste à montrer que le projet fonctionne sur PC, et celle sur Quest montre toutes les features mentionnées. 

**Vidéo de démonstration PC :** [https://youtu.be/0g1k5r6x7jM](https://youtu.be/0g1k5r6x7jM)

> A partir de xx:xx, on change de scène. C'est du bonus comparé aux exigences du TP.

**Vidéo de démonstration Quest 3 :** [https://youtu.be/0g1k5r6x7jM](https://youtu.be/0g1k5r6x7jM)