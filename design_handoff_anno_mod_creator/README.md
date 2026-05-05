# Handoff — Anno Mod Creator

> Éditeur visuel de mod pour un jeu de stratégie type Anno : création de cartes
> en losange composées de slots d'îles typés, regroupées par biome, avec import
> de cartes officielles, gestion de DLC, statistiques live et export du mod.

---

## 1. Overview

L'application est un **outil desktop** (cible : Avalonia / WPF / Electron / SwiftUI…)
permettant à un moddeur d'**Anno** de :

1. Créer un mod composé de **plusieurs biomes** (Latium, Albion, Désert, Nordique).
2. Pour chaque biome livré (`shipped`), produire **3 cartes obligatoires** :
   - Carte 1 = Facile
   - Carte 2 = Normal
   - Carte 3 = Difficile
   - (la difficulté est dérivée de l'index, pas saisie)
3. Chaque carte est un **losange** (carré tourné -45°) contenant :
   - une **grille 5×5 de slots typés** (taille S/M/L/XL × type Normal/Starter/3rd Party/Pirate/Vulkan/Continental)
   - **4 starters obligatoires** (un par joueur)
   - **4 spawns navire** placables librement sur la zone carrée englobante (hors grille)
4. Importer une carte officielle depuis le jeu (catalogue de cartes filtrable par **biome** et par **DLC**).
5. Suivre des statistiques live (nb d'îles posées par taille/type, spawns, total).
6. Exporter le mod quand tous les biomes livrés ont leurs 3 cartes complètes.

L'application est bilingue **fr / en**, avec un thème sombre « antique » (or + bordeaux + bleu nuit + fonts à empattement).

---

## 2. À propos des fichiers fournis

> ⚠️ **Les fichiers du dossier `prototype/` sont des références de design**,
> pas du code de production à recopier directement.
> Ce sont des prototypes HTML/React (Babel inline) qui illustrent l'intention
> visuelle, la composition, les états et les interactions.
>
> **L'objectif n'est pas de servir le HTML aux utilisateurs**, mais de **recréer
> ces écrans dans la stack cible** (idéalement Avalonia / .NET, ou tout autre
> framework adapté à un éditeur desktop) en respectant **les patterns établis
> dans ce codebase**.

### Comment ouvrir le proto
Ouvrir `prototype/Anno Mod Creator.html` dans un navigateur moderne. Tout est
inline (CDN React + Babel + scripts locaux). Aucun build requis.

Astuce : la barre d'outils du proto a un bouton **Tweaks** qui permet de
basculer langue, thème, etc.

---

## 3. Fidélité

**High-fidelity (hifi).**
Couleurs, typo, espacements, ornements, micro-interactions, copy : tout est
final. Le développeur doit reproduire pixel-near, en utilisant les libs et
patterns du projet cible. Le fichier `prototype/styles/tokens.css` contient
tous les design tokens (couleurs, fontes, espacements, rayons, ombres) — à
porter directement dans un `ResourceDictionary` Avalonia ou équivalent.

---

## 4. Stack cible recommandée

L'utilisateur a évoqué **Avalonia** comme cible. C'est un excellent choix :

- Multi-plateforme (Windows / macOS / Linux) — pertinent pour la communauté de
  moddeurs.
- XAML déclaratif → la mise en page (header / sidebars / canvas) se transpose
  presque 1:1.
- `Canvas` + `RenderTransform` natif pour le losange tourné à -45° et le
  placement libre des spawns.
- MVVM avec **CommunityToolkit.Mvvm** ou **ReactiveUI** → état centralisé propre.
- Skia derrière → rendu fluide même avec beaucoup de slots.

### Architecture suggérée

```
AnnoModCreator/
├── Models/
│   ├── Mod.cs                    // racine : nom, biomes
│   ├── Biome.cs                  // id, label fr/en, terrain color, glyph, shipped
│   ├── Map.cs                    // id, name, difficulty (derived), slots, spawns
│   ├── IslandSlot.cs             // id, x%, y%, size, type, placed, player
│   ├── ShipSpawn.cs              // id, x%, y%, player
│   ├── Dlc.cs                    // id, name, biomes[], adds, released, note
│   └── OfficialMap.cs            // id, name, biome, size, players, dlc
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── EditorViewModel.cs        // état actif (biome, mapIdx, sélection)
│   ├── StatsPanelViewModel.cs
│   ├── PlaceIslandPanelViewModel.cs
│   └── ImportDialogViewModel.cs  // filtres biome + DLC, vue grille/liste
├── Views/
│   ├── MainWindow.axaml          // header + barre biomes + corps
│   ├── EditorView.axaml          // sidebars + canvas
│   ├── MapCanvas.axaml           // losange + slots + spawns + cardinaux
│   ├── StatsPanel.axaml
│   ├── PlaceIslandPanel.axaml
│   ├── SelectionPopup.axaml
│   └── ImportDialog.axaml
├── Services/
│   ├── ModSerializer.cs          // (de)sérialisation JSON
│   ├── GameImporter.cs           // lit les fichiers .a7m d'Anno
│   └── ModExporter.cs            // packaging du mod final
├── Themes/
│   ├── Tokens.axaml              // couleurs / typo / espacements (depuis tokens.css)
│   ├── Controls.axaml            // styles boutons / panneaux / modales
│   └── Fonts/                    // Cinzel, Inter, JetBrains Mono
└── Assets/
    └── Icons/                    // ou pack vectoriel inline
```

---

## 5. Écrans / Vues

### 5.1 Fenêtre principale

```
┌──────────────────────────────────────────────────────────────────────┐
│ [Logo] Anno Mod Creator   [statut mod]   [Nouvelle][Import][Save]…   │  ← Header
├──────────────────────────────────────────────────────────────────────┤
│  Latium 3/3  Albion 2/3  Désert 0/3  …                  [Difficulté] │  ← Onglets biomes + cartes
│  Carte ① ② ③                                            [badge diff] │
├────────────┬─────────────────────────────────────┬───────────────────┤
│            │                                     │                   │
│  Stats     │     Canvas (losange)                │  Placer une île   │
│  panel     │                                     │  + Sélection      │
│            │                                     │                   │
└────────────┴─────────────────────────────────────┴───────────────────┘
```

#### Header
- Hauteur ~56 px, fond `--color-ink-900`, filet bas `--color-border`.
- Gauche : logo `<Logo>` (diamant orné) + wordmark « Anno Mod Creator » en
  `--font-display`, lettrage espacé.
- Centre : statut du mod (« 3/12 cartes complètes » etc.) en pill.
- Droite (alignée) : `Nouvelle carte`, `Importer du jeu` (ouvre la modale),
  `Sauvegarder`, `Exporter PNG`, `Exporter le mod` (CTA `--color-wine-700`,
  désactivé tant que `exportReady === false`).

#### Barre biomes / cartes
- Hauteur ~44 px, fond `--color-ink-800`.
- À gauche : 4 onglets biomes. Chaque onglet montre :
  - glyphe biome 16 px,
  - label fr/en,
  - compteur `X/3` (cartes complètes / total) en `--font-mono`,
  - une coche verte `--player-3` si X = 3.
  - Onglet inactif si `shipped === false` (Désert, Nordique pour l'instant).
- Au milieu : 3 ronds numérotés 1–2–3 (carte active), point vert si la carte
  est complète, point or vide sinon.
- À droite : badge difficulté en lecture seule
  - Carte 1 → vert (« Facile »), `bg rgba(77,140,74,.15)`, border `--player-3`
  - Carte 2 → or (« Normal »), `bg rgba(200,169,106,.15)`, border `--color-gold-700`
  - Carte 3 → bordeaux (« Difficile »), `bg rgba(143,52,57,.18)`, border `--color-wine-500`

#### Sidebar gauche — `StatsPanel`
- Largeur 200 px, fond `--color-ink-800`, filet droit.
- Titre eyebrow « STATISTIQUES » 9 px, lettre-espacé.
- Lignes :
  - **Standard** : `nbS+nbM+nbL+nbXL` avec sous-décomposition `(2S 3M 1L 1XL)`.
  - **Starter** `byType.S / 4` — passe en vert quand 4 atteints.
  - **3rd Party** `byType.T` — accent `#9a7ab0`.
  - **Pirate** `byType.P` — accent `#a85c5c`.
  - **Vulkan** `byType.V` — accent `#d68a3c`.
  - **Continental** `byType.C` — accent `#6a8a5c`.
  - Filet séparateur.
  - **Spawns navire** `spawnsCount / 4` — vert quand 4.
  - Filet doré.
  - **Total** `placed.length + spawnsCount`.

#### Canvas central
- Fond radial `radial-gradient(ellipse at center, #2a3850, var(--color-ink-950))`.
- Sur-couche fine grille diagonale 24 px (motif `repeating-linear-gradient` à 45° et -45°, opacité 2%).
- Wrapper carré `min(82%, 720px)`, ratio 1:1.
- À l'intérieur, **le losange** : carré 70.7% tourné -45°, bordure or
  `--color-gold-700` 2 px, fond `#1f2d48`, double inset shadow (0,0,0,1 noir +
  0,0,0,6 or 15%).
- Sur le losange : grille SVG 5×5 (lignes or 8% opacité).
- Les **slots** sont positionnés en pourcentage `inset 12 px` à l'intérieur du
  losange, tournés en sens inverse (+45°) pour rester droits visuellement.
- Hors du losange (sur le wrapper carré) :
  - **Marqueurs cardinaux** N / E / S / W aux 4 sommets du losange,
    cercle 28 px, fond dégradé `--color-ink-700` → `--color-ink-900`,
    bordure `--color-gold-500`, glyphe `--color-gold-300`, font display.
  - **Les 4 spawns navire** : losanges colorés 22 px, glissables anywhere
    sur le wrapper carré, non contraints par la grille. Couleurs par défaut :
    P1 or `#c8a96a`, P2 bleu `#7a8fb5`, P3 mauve `#9a6f9c`, P4 vert `#7aa78b`.
    Numéro du joueur centré, `--font-display` 700.
  - **Encart Sélection** flottant top-right du wrapper, visible uniquement
    quand un slot est sélectionné. Affiche : taille, type, position en %.
  - **Toggle « Images d'îles »** top-left.
  - **Légende Size/Type** bottom-left.
  - **Légende Contrôles** bottom-right.

#### Slots d'îles
- Petit losange 36×36 (ratio adaptatif), bordure `--color-gold-700`, fond
  `--color-ink-800`.
- Code court (`XL-S`, `M-N`…) en `--font-mono` 9 px.
- Si `placed === false` : `?` central or pâle, opacité 60%.
- Si `placed === true` : barre verticale colorée selon `--player-N` à droite.
- Si `selected` : double bordure or `--color-gold-500`, glow `--shadow-glow`.

#### Sidebar droite — `Placer une île`
- Largeur 240 px, fond `--color-ink-800`, filet gauche.
- Titre eyebrow « PLACER UNE ÎLE ».
- Sections (chacune avec son sous-titre) :
  - **Spawn** (1 bouton « Spawn navire »)
  - **Starter** (boutons L, XL — un compteur indique starters posés / 4)
  - **Normal** (boutons S, M, L, XL)
  - **NPCs** (3rd Party, Pirate)
  - **Vulkan** (S, M)
  - Bouton plein « Île personnalisée… »
- Sous une selection : encart Sélection avec actions Édit / Convert /
  Supprimer + sélecteur de joueur (4 ronds colorés).

---

### 5.2 Modale d'import depuis le jeu

Ouverte via le bouton « Importer du jeu » du header.

- Largeur max 720 px, fond `--color-bg-elevated`, bordure or, padding 24.
- Header : titre display, sous-titre 11 px gris.
- **Filtre Biome** (ligne 1) : pills `Tous`, `Latium`, `Albion`. Pill actif :
  fond `--color-wine-700`, border or, texte or. Inactif : fond ink-700, texte
  dim. Toggle de vue à droite : `Vignettes` / `Liste`.
- **Filtre DLC** (ligne 2) : pills `Tous`, `Jeu de base`, `DLC 1 — Mare Magnum`,
  `DLC 2 — Aegyptus`. Les DLC `released === false` sont grisés (opacité 0.7) et
  affichent un badge rouge `BIENTÔT` / `SOON`.
- **Cumul des filtres** : un DLC + un biome filtrent ET. Si le DLC actif n'a
  pas de cartes (`adds === 'islands'`) ou n'est pas sorti (`released === false`)
  ou si la combinaison ne renvoie rien → état vide remplacé par un grand bandeau :
  - Titre display or selon le cas (« Pas encore disponible » /
    « Ce DLC n'ajoute pas de cartes — uniquement de nouvelles îles » /
    « Aucun résultat »)
  - Sous-titre : `dlc.note[lang]` si présent.
- **Vue Vignettes** : grille 3 colonnes, chaque carte = mini-losange coloré au
  biome + nom + meta `biome · 4P · DLC d'origine`.
- **Vue Liste** : tableau `glyphe | nom | biome | size·players | DLC` zébré.

Cliquer une carte → la duplique dans la carte courante (régénère les slots
avec un seed déterministe, copie le nom).

---

### 5.3 Modale « Nouvelle carte »

Confirmation simple avec couronne de laurier ornementale, 2 boutons
`Annuler` / `Confirmer`. Confirme = régénère les slots de la carte active.

---

## 6. Modèle de données

```ts
// Tous les pourcentages sont 0-100 (pas 0-1) pour faciliter le rendu
// inline. À convertir en double 0..1 si besoin côté C#.

type ID = string;

interface Mod {
  name: string;
  biomes: Record<BiomeId, Map[]>;   // exactement 3 maps par biome shipped
}

type BiomeId = 'latium' | 'albion' | 'desert' | 'nordic';

interface Biome {
  id: BiomeId;
  label: { fr: string; en: string };
  terrain: string;                  // hex color
  glyph: string;                    // icon name
  shipped: boolean;                 // false = onglet désactivé
}

interface Map {
  id: string;
  name: string;
  difficulty: 'easy' | 'normal' | 'hard';   // dérivé de l'index 0|1|2
  slots: IslandSlot[];                       // grille 5x5 = 25 slots
  spawns: ShipSpawn[];                       // 4 spawns
}

interface IslandSlot {
  id: number;
  x: number;     // % du losange
  y: number;     // %
  size: 'S' | 'M' | 'L' | 'XL';
  type: 'N' | 'S' | 'P' | 'T' | 'V' | 'C';
  // N = Normal, S = Starter, P = Pirate, T = 3rd Party, V = Vulkan, C = Continental
  placed: boolean;
  player: 1 | 2 | 3 | 4 | null;    // null si placed === false
}

interface ShipSpawn {
  id: number;
  x: number;     // % du wrapper carré
  y: number;     // %
  player: 1 | 2 | 3 | 4;
}

interface Dlc {
  id: string;
  name: { fr: string; en: string };
  biomes: BiomeId[];
  adds: 'maps' | 'islands' | 'biome';
  // maps   = ajoute des cartes officielles à un biome existant
  // islands = ajoute des îles individuelles à un biome existant (pas de carte)
  // biome   = ajoute un biome entier (cartes + îles)
  released: boolean;
  note?: { fr: string; en: string };
}

interface OfficialMap {
  id: string;
  name: string;
  biome: BiomeId;
  size: 'S' | 'M' | 'L' | 'XL';
  players: number;
  dlc: string;        // → Dlc.id
}
```

### Règles métier

- **Carte complète** ≡ ≥ 5 slots posés ET 4 starters ET 4 spawns. (Le proto
  utilise un seuil de 5 slots placés pour la démo ; le dev validera la règle
  exacte avec le métier.)
- **Biome livré complet** ≡ 3 cartes complètes pour ce biome.
- **Mod exportable** ≡ tous les biomes `shipped` sont complets.
- **Difficulté** = fonction de l'index : `['easy','normal','hard'][i]`.
  Pas saisissable.
- **4 starters** : 1 par joueur, posés sur des slots typés `S` de la grille.
- **4 spawns** : free-form sur la zone carrée englobante, glissables, un par
  joueur, valeurs par défaut aux 4 sommets du losange (N / E / S / W).

---

## 7. Interactions et comportements

### Sélection de slot
- Click sur slot → `selectedSlotId = slot.id`.
- Click sur le canvas (zone vide) → `selectedSlotId = null`.
- Encart Sélection apparaît top-right + actions Édit/Convert/Supprimer +
  picker joueur s'affichent dans la sidebar droite.

### Drag d'un spawn
- Mouse-down sur un spawn → on capture le rect du wrapper carré.
- Mouse-move → on calcule la position en % bornée à `[2, 98]` pour ne pas
  sortir du wrapper, on update `spawn.x/y`.
- Mouse-up → relâche les listeners.
- État persistant dans `Map.spawns`.

### Pose d'une île
- Click sur un bouton de la sidebar droite (taille + type) → ouvre un mode
  "placement" qui surligne les slots compatibles.
- Click sur un slot compatible → `slot.placed = true`, `slot.size`/`slot.type`
  prennent les valeurs sélectionnées, `slot.player` = picker actif.
- (Dans le proto, la pose est simplifiée pour la démo ; le dev implémentera
  la machine d'état complète.)

### Filtres Import
- Filtres `biome` + `dlc` cumulent en AND.
- Empty/not-released states avec messages dédiés (cf. 5.2).

### Export
- Bouton désactivé tant que `missingTotal !== 0`.
- Hover affiche un tooltip listant les biomes incomplets.

### Animations / transitions
- Hover sur boutons : background lerp 120 ms cubic-bezier(.2,.7,.3,1)
  (`--t-fast`).
- Modales : fade + scale-from-0.96 sur 220 ms (`--t-med`).
- Sélection slot : glow apparaît en 220 ms.

---

## 8. Design tokens

Voir `prototype/styles/tokens.css` pour la liste exhaustive. Synthèse :

### Couleurs

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-ink-950` | `#0d1626` | Fond profond, modales |
| `--color-ink-900` | `#14213a` | Fond panneaux principal |
| `--color-ink-800` | `#1a2840` | Sidebars |
| `--color-ink-700` | `#243454` | Cards / inputs |
| `--color-wine-700` | `#7a2838` | Accent primaire boutons |
| `--color-wine-500` | `#b85365` | Hover/actif accent |
| `--color-gold-700` | `#8a7240` | Bordures filets or |
| `--color-gold-500` | `#c8a96a` | Or principal |
| `--color-gold-300` | `#e8d6a8` | Texte clair / labels |
| `--color-text` | `#e8e2d0` | Texte par défaut |
| `--color-text-dim` | `#a8a090` | Texte secondaire |
| `--player-1..8` | divers | Couleurs joueurs |

### Typographie

| Famille | Usage |
|---------|-------|
| `Cinzel` (display) | Titres, marques, badges |
| `Inter` (body) | UI, paragraphes |
| `JetBrains Mono` (mono) | Compteurs, codes slot, coordonnées |

Échelle : 10 / 11 / 13 / 15 / 17 / 21 / 28 / 38 / 52 px.

### Espacements

4 / 8 / 12 / 16 / 24 / 32 / 48 / 64 px.

### Rayons

2 / 4 / 6 / 10 / pill.

### Ombres

- `--shadow-md`: `0 4px 12px rgba(0,0,0,.45), 0 0 0 1px rgba(0,0,0,.25)`
- `--shadow-lg`: `0 12px 40px rgba(0,0,0,.55), 0 0 0 1px rgba(0,0,0,.3)`
- `--shadow-glow`: `0 0 24px rgba(200, 169, 106, 0.25)` (sélection)

---

## 9. Internationalisation

Toutes les chaînes UI sont en `src/Editor.jsx` (tableau `i18n` clés `fr` / `en`).
À porter dans des fichiers de ressources `.resx` (Avalonia/.NET) ou
équivalents. Le toggle de langue est exposé dans le panneau Tweaks du proto.

Clés notables :
- `panels.{stats,placeIsland,selection,starterCheck,total,shipSpawns,…}`
- `diff.{easy,normal,hard}` — labels de la difficulté.
- `filterDlc`, `filterAll`, `importNoMaps`, `importNotReleased`,
  `starterRequired`, `spawnsRequired`.

---

## 10. Fichiers fournis

```
design_handoff_anno_mod_creator/
├── README.md                          ← ce fichier
└── prototype/
    ├── Anno Mod Creator.html          ← entry point — ouvrir au navigateur
    ├── styles/
    │   ├── tokens.css                 ← design tokens (couleurs, typo, espacements)
    │   └── components.css             ← styles partagés (boutons, modales)
    ├── src/
    │   ├── Editor.jsx                 ← écran principal (header, barres, canvas, modales)
    │   ├── Charter.jsx                ← « charte graphique » de référence (système de design)
    │   └── Icons.jsx                  ← jeu d'icônes SVG inline
    └── tweaks-panel.jsx               ← panneau dev pour basculer langue/thème
```

`Charter.jsx` n'est pas un écran de l'app finale — c'est une page de
documentation visuelle du système. À utiliser comme **guide de style** pour
implémenter les contrôles de base (boutons, badges, panneaux, ornements).

---

## 11. Assets

- **Polices** : Cinzel et Inter via Google Fonts (chargées dans le `<head>` du
  proto). Pour Avalonia, embarquer les `.ttf` dans `Themes/Fonts/` et les
  référencer en `<FontFamily>`.
- **Icônes** : SVG inline simples, traçage vectoriel (cf. `Icons.jsx`). À
  porter en `Geometry` Avalonia, ou en `<PathIcon>`, ou via un pack
  (Material.Icons.Avalonia).
- **Logo / Laurel** : SVG originaux dessinés à la main dans `Icons.jsx`,
  inspirés de l'antiquité — libres de droits, à intégrer tels quels.
- **Aucun asset propriétaire d'Anno** n'est utilisé. Quand un dev intégrera
  les vraies images d'îles du jeu, c'est lui qui les fournira (le toggle
  « Images d'îles » du proto est prévu pour ça).

---

## 12. Points d'attention pour le dev

1. **Le losange est un carré tourné -45°**, mais les slots à l'intérieur sont
   placés sur une grille orthogonale et **re-tournés +45°** pour rester droits.
   Les coordonnées `x/y` des slots sont en % du carré _non tourné_.
2. **Les spawns vivent dans un autre repère** : le wrapper carré qui contient
   le losange. Leurs `x/y` sont en % de ce wrapper, pas du losange. C'est
   nécessaire pour qu'ils puissent se trouver « au-dessus » des sommets.
3. **Le clipping** du losange est implicite : la rotation -45° fait que ce qui
   est en dehors des bordures du carré tourné est juste invisible. Pas de
   `clip-path`. En Avalonia, même technique avec `RotateTransform`.
4. **La difficulté n'est pas stockée**, elle est dérivée de l'index. Ne pas
   créer de champ persisté.
5. **Le DLC `adds = 'islands'`** doit faire apparaître un état vide explicite
   dans la modale d'import — c'est important sémantiquement (le DLC existe et
   est sorti, mais n'a pas de cartes à proposer).
6. **L'état Tweaks du proto** (`langue`, `thème`) est purement pour le design
   — pas à porter en prod.
7. **Validation export** : c'est le seul moment où l'app refuse une action.
   Tout le reste est permissif (on peut sauver un mod incomplet).

---

## 13. Roadmap suggérée

1. **Sprint 1** — chrome de l'app : header, barre biomes, sidebars vides,
   thème/tokens, fontes, i18n.
2. **Sprint 2** — canvas losange + grille de slots (sans interaction), avec
   le fond, les marqueurs cardinaux, les légendes.
3. **Sprint 3** — interactions slots (sélection, pose, picker joueur),
   StatsPanel live.
4. **Sprint 4** — spawns drag&drop, persistence dans le model.
5. **Sprint 5** — modale d'import avec filtres biome + DLC, états vides,
   import effectif.
6. **Sprint 6** — sérialisation mod (JSON), export PNG, export package mod.
7. **Sprint 7** — import depuis fichiers `.a7m` du jeu (lecture binaire,
   nécessitera de la rétro-ingénierie format).

---

## 14. Questions ouvertes pour le dev

- Quelle est la **règle exacte** pour qu'une carte soit « complète » ?
  (le proto utilise ≥ 5 slots placés ; à valider avec le métier)
- Le format de fichier d'un mod — JSON natif ou format conventionné par la
  communauté Anno ?
- Les slots peuvent-ils être **ajoutés/supprimés** par l'utilisateur, ou la
  grille 5×5 est-elle figée ?
- Comportement du bouton « Île personnalisée… » : ouvre un éditeur ? Lit un
  fichier ?
- Combien de joueurs max — 4 fixe, ou paramétrable jusqu'à 8 (les tokens
  prévoient déjà 8 couleurs joueurs) ?

---

*Bonne implémentation. Le dossier est suffisant pour démarrer sans avoir
participé à la conversation de design.*
