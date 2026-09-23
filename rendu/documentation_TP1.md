# TP1 - Structure documentation

## Intro

Cette application est un laboratoire expérimental et immersif de locomotion en réalité virtuelle (VR) conçu pour le casque autonome **Meta Quest 3** et le simulateur VR d'Unity. Développé dans le cadre du cours **LOG8704**, ce prototype s'inspire des paradigmes de conception de jeux phares tels que *Half-Life: Alyx* pour l'exploration libre et *First Encounters* pour l'acclimatation sécurisée du joueur.

L'objectif central est de comparer et de valider scientifiquement différentes modalités de locomotion en réalité virtuelle (déplacement physique 6DoF, déplacement continu au joystick, téléportation parabolique avec fondu au noir, et translation linéaire par dash) tout en intégrant des systèmes de pointe pour prévenir la cinétose (*motion sickness*).

Le joueur débute dans une salle d'entraînement sécurisée (`Début.unity`) où un tableau de bord holographique interactif le guide à travers un tutoriel en 5 étapes progressives. Une fois l'entraînement complété, un portail dimensionnel s'ouvre, lui permettant d'accéder à un vaste environnement urbain tridimensionnel (`Demo.unity`) pour tester les déplacements en milieu complexe (obstacles verticaux, toits, escaliers et plateformes surélevées).

---

## Choix techniques

L'application a été entièrement construite sur les standards modernes de l'industrie : **Unity 6 (6000.3.22f1)**, le pipeline de rendu **Universal Render Pipeline (URP 17.3.0)**, **OpenXR** (`com.unity.xr.openxr`, `com.unity.xr.meta-openxr`) et **Unity XR Interaction Toolkit (XRI 3.5.1)** (`com.unity.xr.interaction.toolkit`).

### 1. Déplacement Physique (Tracking 6DoF)
* **Composants utilisés** : `XROrigin`, `CameraOffset`, `TrackedPoseDriver`.
* **Fonctionnement** : Le joueur est libre de bouger dans son espace réel à l'échelle de la pièce (*Room-Scale*). Les mouvements de tête et des mains sont captés à 6 degrés de liberté (6DoF) avec une latence ultra-faible. La hauteur du joueur dans le monde virtuel est synchronisée en continu via le mode `TrackingOriginMode.Floor`.

### 2. Déplacement Continu par Joystick (Smooth Locomotion)
* **Composants & Scripts** : `ContinuousMoveProvider`, `CharacterController`, `CharacterControllerDriver`, `TP1ComfortManager.cs`.
* **Règle d'isolation des joysticks** : La translation est assignée **exclusivement au joystick gauche** (avant/arrière et strafe latéral), sans aucune composante de rotation.
* **Gestion des collisions** : Un `CharacterController` (hauteur 1.8m, rayon 0.35m) couplé à un `CharacterControllerDriver` bloque physiquement le joueur contre les surfaces verticales (murs, bâtiments, caisses), empêchant la traversée des décors (*wall-clipping*).

### 3. Déplacement par Téléportation Parabolique
* **Composants & Scripts** : `ComfortTeleportationProvider.cs`, `XRRayInteractor`, `TeleportationArea`, `BlockedTeleportArea.cs`.
* **Fonctionnement** : En inclinant le joystick gauche vers l'avant, un rayon parabolique interactif est projeté dans l'espace. Le joueur visualise précisément la zone cible au sol grâce à un réticule lumineux blanc. Au relâchement du stick (`OnSelectExited`), le joueur est téléporté à l'emplacement visé.
* **Rejet des zones interdites** : Si le joueur vise une surface non autorisée (ex: le sommet de la boîte d'obstacle ou les murs d'enceinte), le composant `BlockedTeleportArea` intercepte la sélection : la ligne devient orange et affiche un réticule barré d'interdiction, garantissant qu'aucune téléportation hors-limite n'est possible.

### 4. Systèmes de Protection contre la Cinétose
Trois mécanismes de confort ont été implémentés et peuvent être activés/désactivés indépendamment en temps réel :
1. **Téléportation par Blink (Fondu au noir instantané)** :
   * *Script* : `ScreenFadeCanvas.cs`.
   * *Fonctionnement* : Une transition d'occlusion noire ultra-rapide (0.15s) masque l'écran pendant le repositionnement instantané de la caméra, éliminant les conflits vestibulaires associés aux sauts d'image.
2. **Téléportation par Dash (Translation linéaire rapide)** :
   * *Composant* : `DashProvider` (XRI 3.5.1).
   * *Fonctionnement* : Effectue une translation rapide en 0.20 seconde vers le point cible, offrant une continuité spatiale sans la cinétose causée par une accélération prolongée.
3. **Œillère dynamique de champ visuel (Tunneling Vignette)** :
   * *Composant* : `TunnelingVignetteController`.
   * *Fonctionnement* : Réduit dynamiquement le champ de vision périphérique de l'utilisateur lors des accélérations, marches continues et translations dash. L'occlusion périphérique supprime le défilement optique latéral (*optical flow*), facteur principal de la cinétose en VR.

### 5. Rotation de Vue (Joystick Droit)
* **Composants & Scripts** : `SnapTurnProvider`, `ContinuousTurnProvider`.
* **Fonctionnement** : Assigné **exclusivement au joystick droit**. Par défaut, le virage angulaire instantané par crans de 45° (*Snap Turn*) est activé pour maximiser le confort. Le joueur peut également basculer vers une rotation fluide à 60°/s (*Continuous Turn*) via le menu.

### 6. Menu de Poignet Holographique (Smartwatch)
* **Scripts** : `WristUIController.cs`.
* **Ergonomie** : Positionné sur la face dorsale de l'avant-bras gauche, inspiré d'une montre connectée.
* **Détection du regard (*Glance-based*)** : Le menu n'apparaît que lorsque l'utilisateur effectue une supination naturelle de l'avant-bras et regarde sa montre (calcul d'angle vectoriel et de distance de champ de vision dans `UpdateGlanceVisibility`). Des cartes d'icônes avec rétroéclairage LED indiquent l'état en direct de chaque mode.

### 7. Salle d'Entraînement & Portail Cross-Scene
* **Scripts** : `TutorialFlowController.cs`, `TutorialTeleportZoneTrigger.cs`, `SceneTeleportPortal.cs`.
* **Tutoriel en 5 étapes** :
  1. *Étape 1* : Regarder sa montre gauche (ou presser [M]) pour ouvrir l'interface.
  2. *Étape 2* : Viser et se téléporter sur le socle lumineux cyan avec transition Blink.
  3. *Étape 3* : Effectuer un Dash rapide (0.2s).
  4. *Étape 4* : Marcher 3 mètres avec le joystick gauche tout en observant l'œillère dynamique.
  5. *Étape 5* : Le portail dimensionnel vers la ville se déverrouille (l'émission passe de l'orange au vert émeraude, le panneau indique "DÉVERROUILLÉ").
* **Portail dimensionnel** : Permet la transition instantanée avec fondu entre la salle d'entraînement et la ville complète, soit en marchant à travers, soit en téléportant son rayon sur le portail.

---

## Tests effectués

### 1. Appareils & Matériel
* **Meta Quest 3 (Standalone Android / Horizon OS)** : Tests effectués en conditions réelles avec suivi 6DoF des contrôleurs Touch Plus. Fluidité optimale à 90 Hz sans saccade.
* **Simulateur VR Unity (Desktop PC - Windows 11, DirectX 12)** : Utilisation de l'outil *XR Device Simulator* avec contrôle complet à la souris et au clavier pour valider l'ergonomie, la répétabilité des interactions et le respect des critères de correction.

### 2. Environnements
* **Intérieur / Salle fermée (`Début.unity`)** : Éclairage d'ambiance doux, surfaces planes, validation du tutoriel interactif et de la détection de proximité de zone.
* **Extérieur Urbain (`Demo.unity`)** : Environnement urbain complet (Synty Polygon) composé de routes, trottoirs, toits, conteneurs, obstacles verticaux et dénivelés. Permet de vérifier que la téléportation fonctionne sur les plateformes surélevées praticables tout en rejetant les surfaces interdites.
* **Arène de laboratoire (`TP1_TestArena.unity`)** : Environnement de contrôle avec boîte d'obstacle rejetée, plateforme surélevée praticable, piédestal avec cube interactif et murs d'enceinte infranchissables.

### 3. Limitations & Bogues Connus
* **Comportement du CharacterController sur les pentes très abruptes** : Les pentes inclinées à plus de 45° arrêtent la marche continue comme prévu, mais peuvent parfois provoquer de légères oscillations si le joueur force le joystick vers le haut.
* **Détection du regard pour le menu de poignet** : L'algorithme de détection de regard exige que le joueur amène sa main à hauteur de poitrine. Si le joueur joue assis avec les bras le long du corps, il est recommandé d'utiliser la touche `[M]` (sur PC) ou de lever franchement l'avant-bras.

---

## Pistes d’améliorations

1. **Orientation dynamique à l'atterrissage de la téléportation** : Permettre au joueur d'ajuster l'orientation finale de son avatar en tournant le joystick avant de relâcher le rayon de téléportation, comme dans *Half-Life: Alyx*.
2. **Système de franchissement et d'escalade (Mantling)** : Ajouter la possibilité d'agripper les rebords des obstacles élevés avec les deux mains pour hisser le corps virtuel au-dessus des caisses et murets.
3. **Audio spatialisé et haptiques différenciées** : Implémenter des bruits de pas spatialisés dont la cadence s'ajuste à la vitesse de déplacement réelle, ainsi que des impulsions haptiques variées selon le type de surface (béton, herbe, métal).
4. **Problèmes résolus pendant le développement** :
   * Migration réussie de l'écosystème propriétaire Meta OVR vers le standard multiplateforme OpenXR / Unity XRI 3.5.1 sans aucune perte de performance.
   * Réécriture complète des couches d'interaction pour assurer la compatibilité simultanée des surfaces praticables et bloquées.
   * Résolution des conflits de fusion git sur les scènes binaires et normalisation des Build Settings.

---

## Remise

* **Lien du dépôt Git** : [https://github.com/DupuisB/TP1.git](https://github.com/DupuisB/TP1.git)
  * Branche active de développement : `feature/panel-tuto`
* **Commit de remise** : Préparé avec tag/message de remise conforme pour l'échéance du 23 septembre 23h59.
* **Explication de la vidéo démonstrative** :
  * Une vidéo d'une durée de 1 à 2 minutes capturée via le simulateur VR d'Unity et/ou le casque Meta Quest 3 illustre :
    1. La consultation du menu smartwatch au poignet et la configuration des modes.
    2. La téléportation parabolique avec fondu au noir (Blink) vers la zone cible.
    3. L'exécution du Dash rapide linéaire.
    4. La marche continue au joystick gauche avec déclenchement de l'œillère de cinétose (Tunneling Vignette).
    5. Le déverrouillage du portail et la transition fluide vers la ville tridimensionnelle.
