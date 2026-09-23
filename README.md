# LOG8704 - TP1 : Simulation Immersive de Locomotion en Réalité Virtuelle

Prototype de laboratoire expérimental de déplacement en réalité virtuelle pour **Meta Quest 3**, développé dans le cadre du cours LOG8704 (Polytechnique Montréal).

Conçu selon l'architecture standardisée **OpenXR** avec **Unity XR Interaction Toolkit (XRI 3.5.1)**, **URP 17.3.0** et **Unity 6 (6000.3.22f1)**.

---

## 🚀 Scène de Démarrage & Structure du Projet

* **Scène à compiler / lancer (Scene 0)** : `Assets/Synty/PolygonStarter/Scenes/Début.unity`
* **Scène principale d'exploration (Scene 1)** : `Assets/Synty/PolygonStarter/Scenes/Demo.unity`
* **Scène de calibration/backup (Scene 2, désactivée)** : `Assets/Scenes/TP1_TestArena.unity`

L'application démarre directement dans le **Centre d'entraînement VR (`Début.unity`)**, une salle sécurisée inspirée de *First Encounters*. Le joueur est guidé par un tableau de bord holographique interactif à travers un tutoriel en 5 étapes avant que le portail vers la ville (`Demo.unity`) ne se déverrouille.

---

## 🥽 Matériel & Environnements de Test

* **Casque VR cible** : **Meta Quest 3** (Standalone Android / Horizon OS via OpenXR Loader).
* **Simulateur Desktop** : **Unity XR Device Simulator** (`com.unity.xr.interaction.toolkit`) + Raccourcis clavier direct pour tests rapides sans casque.
* **Environnements testés** :
  1. *Intérieur / Entraînement (`Début.unity`)* : Éclairage d'ambiance doux, surfaces planes, salle d'acclimatation.
  2. *Extérieur Urbain (`Demo.unity`)* : Ville 3D complète (Synty Polygon) avec rues, trottoirs, caisses, toits et plateformes surélevées.
  3. *Arène de Contrôle (`TP1_TestArena.unity`)* : Arène fermée standardisée avec obstacles infranchissables, plateformes accessibles et piédestal d'interaction.

---

## 🕹️ Tableau des Contrôles

### 1. Contrôleurs VR (Meta Quest 3 Touch Plus)

| Contrôleur / Bouton | Rôle | Action & Comportement |
| :--- | :--- | :--- |
| **Joystick Gauche** | **Locomotion Exclusif** | Translation pure (aucun virage). Marche continue (Smooth Move), visée et relâchement de téléportation, ou dash directionnel. |
| **Joystick Droit** | **Vue / Rotation Exclusif** | Virage par crans (Snap Turn 45°) ou virage fluide (Continuous Turn 60°/s). Aucune translation. |
| **Poignet Gauche (Smartwatch)** | **Menu Holographique** | Glance-based : tournez votre poignet dorsal vers vos yeux pour faire apparaître le menu confort. |
| **Index Droit (Trigger / Gâchette)** | **Interaction Directe & Ray** | Appuyer sur les boutons du menu smartwatch, attraper les objets ou viser le portail. |

### 2. Raccourcis Simulateur / Clavier Desktop (Unity Editor)

Pour tester immédiatement dans l'éditeur sans casque :

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

## 🛡️ Systèmes de Prévention de la Cinétose

1. **Isolation stricte des joysticks** : La translation est assignée à 100% au joystick gauche, et la rotation au joystick droit. Aucune interférence croisée.
2. **Téléportation par Blink** : Fondu au noir rapide (0.15s) géré par `ScreenFadeCanvas` sur la caméra principale, éliminant tout inconfort visuel lors des sauts spatiaux.
3. **Téléportation par Dash** : Translation linéaire ultra-rapide (durée fixée à 0.20s via `DashProvider`), réduisant la sensation d'accélération inertielle.
4. **Œillère dynamique de champ visuel (`TunnelingVignetteController`)** : Réduction adaptative du champ de vision périphérique lors des mouvements continus et des accélérations.
5. **Rejet visuel des surfaces impossibles** : Lorsqu'un utilisateur vise une surface non praticable ou un obstacle supérieur interdit, le rayon devient orange avec un réticule barré (`BlockedTeleportArea`), empêchant la téléportation hors-limites.

---

## 🎓 Parcours Pédagogique (Salle Début)

1. **Étape 1 : Menu de poignet** — Le joueur lève son poignet ou presse `[M]` pour découvrir l'interface holographique.
2. **Étape 2 : Téléportation & Blink** — Le joueur vise le socle lumineux cyan au centre de la salle et se téléporte dessus.
3. **Étape 3 : Translation Dash** — Le joueur teste le dash de 0.2s.
4. **Étape 4 : Déplacement continu & Œillère** — Le joueur marche 3 mètres avec le joystick gauche en observant l'œillère périphérique.
5. **Étape 5 : Portail Déverrouillé** — Le portail énergétique passe de l'orange (verrouillé) au vert émeraude (déverrouillé). Le joueur peut le traverser ou s'y téléporter pour explorer la ville `Demo.unity`.

---

## 📦 Compilation & Déploiement Quest 3

1. Dans Unity Editor, ouvrir `File ▸ Build Settings`.
2. Sélectionner la plateforme **Android**.
3. Vérifier que `Début.unity` est en tête (Index 0) et `Demo.unity` en second (Index 1).
4. Cliquer sur **Build And Run** avec le Quest 3 branché en USB (Débogage USB activé).
