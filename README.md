# LOG8704 - TP1 : Développement pour Quest 3

TP1 du cours LOG8704 (Polytechnique Montréal).

Application simple pour Meta Quest 3 dévéloppé dans Unity en utilisant OpenXR. Le but est de tester plusieurs méthodes de locomotion et de navigation en VR, avec des méthodes de mitigation de la cinétose.

> La méthode de test testé de bout en bout est le build sur Meta Quest 3. Le simulateur Unity XR Device Simulator est utilisé pour des tests rapides sans casque, mais certaines fonctionnalités n'ont pas été téstés de manière fiable.

---

## Prérequis

Une fois le projet cloné, il est nécéssaire d'installer le package Synty Polygon Starter Pack. C'est possible depuis Synty -> Package Helper -> Install packages dans la barre de menu.

> Note: il est possible de vérifier l'installation dans le package manager de Unity.

## Présentation du projet & Scènes

Le joueur commence dans une petite salle d'entraînement où il apprend à utiliser le menu de poignet, la téléportation Blink, le dash et la marche continue avec vignette. Après avoir complété le tutoriel, un portail vers une scène extérieure plus grande est déverrouillé.

* **Scène à compiler / lancer (Scene 0)** : `Assets/Synty/PolygonStarter/Scenes/Debut.unity`
* **Scène principale d'exploration (Scene 1)** : `Assets/Synty/PolygonStarter/Scenes/Demo.unity`
* **Scène de calibration/backup (Scene 2, désactivée)** : `Assets/Scenes/TP1_TestArena.unity`

---

## Environnements de Test

* **Meta Quest 3** en buildant l'apk. C'est la méthode où nous avons tout testé et validé.
* **Meta XR Device Simulator** Fonctionne, mais nos application meta simulator crashent pour des raisons indépendantes, donc a pas pu absolument tout tester de manière fiable.
* **Unity XR Device Simulator**  Raccourcis clavier direct pour tests rapides sans casque directement dans unity.

---

## Contrôles

### 1. Contrôleurs VR (Meta Quest 3 Touch Plus)

| Contrôleur / Bouton | Rôle | Action & Comportement |
| :--- | :--- | :--- |
| **Joystick Gauche** | **Locomotion Exclusif** | Translation pure (aucunvirage ). Marche continue (Smooth Move), visée et relâchement de téléportation, ou dash directionnel. |
| **Joystick Droit** | **Vue / Rotation Exclusif** | Snap Turn 45° ou Smooth Turn. |
| **Poignet Gauche (Smartwatch)** | **Menu Holographique** | Tournez votre poignet gauche vers vos yeux pour faire apparaître le menu. (comme si vous regardiez une montre) |
| **Index Droit (Trigger / Gâchette)** | **Interaction Directe & Ray** | Appuyer sur les boutons du menu smartwatch, attraper les objets ou viser le portail. |

### 2. Raccourcis Simulateur / Clavier Desktop (Unity Editor)

Pour tester immédiatement dans l'éditeur sans casque (les raccourcis ne marchent par sur certains ordinateurs de manière inconnu) :

| Touche Clavier | Action |
| :--- | :--- |
| **[M]** | Ouvrir / Fermer le menu de poignet holographique (force-visibility) |
| **[1]** | Activer le mode **Marche Continue (Smooth Move)** |
| **[2]** | Activer le mode **Téléportation Parabolique** |
| **[3]** | Activer le mode **Dash (Translation rapide 0.2s)** |
| **[4]** | Activer / Désactiver le **Blink (Fondu au noir instantané)** |
| **[5]** | Activer / Désactiver l'**Œillère dynamique de cinétose (Tunneling Vignette)** |
| **[6]** | Basculer entre **Snap Turn (45°)** et **Continuous Turn (60°/s)** |
| **[Espace]** | Déclencher un **Dash** immédiat vers l'avant |
| **[T]** | Déclencher une **Téléportation test** |
| **[W, A, S, D]** | Déplacement translationnel de la caméra |
| **Souris (clic droit)** | Orientation de la tête / rotation du regard |

---

## Systèmes de Prévention de la Cinétose

1. **Isolation stricte des joysticks** : La translation est assignée à 100% au joystick gauche, et la rotation au joystick droit. Aucune interférence croisée.
2. **Téléportation par Blink** : Fondu au noir rapide (0.15s) géré par `ScreenFadeCanvas` sur la caméra principale, éliminant tout inconfort visuel lors des sauts spatiaux.
3. **Téléportation par Dash** : Translation linéaire ultra-rapide (durée fixée à 0.20s via `DashProvider`), réduisant la sensation d'accélération inertielle.
4. **Œillère dynamique de champ visuel (`TunnelingVignetteController`)** : Réduction adaptative du champ de vision périphérique lors des mouvements continus et des accélérations.
5. **Rejet visuel des surfaces impossibles** : Lorsqu'un utilisateur vise une surface non praticable ou un obstacle supérieur interdit, le rayon devient orange avec un réticule barré (`BlockedTeleportArea`), empêchant la téléportation hors-limites.

---

## Tutoriel

1. **Étape 1 : Menu de poignet** : Le joueur lève son poignet ou presse `[M]` pour découvrir l'interface holographique.
2. **Étape 2 : Téléportation & Blink**: Le joueur vise le socle lumineux cyan au centre de la salle et se téléporte dessus.
3. **Étape 3 : Translation Dash**: Le joueur teste le dash de 0.2s.
4. **Étape 4 : Déplacement continu & Œillère**: Le joueur marche 3 mètres avec le joystick gauche en observant l'œillère périphérique.
5. **Étape 5 : Portail Déverrouillé**: Le portail énergétique passe de l'orange (verrouillé) au vert émeraude (déverrouillé). Le joueur peut le traverser ou s'y téléporter pour explorer la ville `Demo.unity`.

---

## Compilation pour Quest 3

1. Dans Unity Editor, ouvrir `File ▸ Build Settings`.
2. Sélectionner la plateforme **Android**.
3. Vérifier que `Debut.unity` est en tête (Index 0) et `Demo.unity` en second (Index 1).
4. Cliquer sur **Build And Run** avec le Quest 3 branché en USB (Débogage USB activé).
