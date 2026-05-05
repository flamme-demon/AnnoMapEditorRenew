// Editor.jsx — Anno Mod Creator : éditeur de carte
// Inspiré du logiciel Taludas v1.0 mais avec esthétique antique originale.
// Structure : stats à gauche, grille de slots dans le losange, panneau "Placer une île" à droite,
// légendes (Size/Type/Controls) + sélection en encarts flottants.

const PLAYER_COLORS = [
  { id: 1, color: '#b94a4a', name: { fr: 'Rouge', en: 'Red' } },
  { id: 2, color: '#3e6cb5', name: { fr: 'Bleu', en: 'Blue' } },
  { id: 3, color: '#4d8c4a', name: { fr: 'Vert', en: 'Green' } },
  { id: 4, color: '#d4a93b', name: { fr: 'Jaune', en: 'Yellow' } },
];

const BiomeGlyph = ({ id, size = 24, color = "currentColor" }) => {
  const s = size;
  if (id === 'latium') return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 3 L20 12 L12 21 L4 12 Z"/>
      <path d="M8 12 L12 8 L16 12 L12 16 Z" opacity="0.7"/>
      <circle cx="12" cy="12" r="1.2" fill={color}/>
    </svg>
  );
  if (id === 'albion') return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 4 C16 4 19 7 19 11 C19 14 16 16 13 16 C11 16 9 14 9 12 C9 11 10 10 11 10"/>
      <path d="M12 20 C8 20 5 17 5 13 C5 10 8 8 11 8 C13 8 15 10 15 12 C15 13 14 14 13 14"/>
    </svg>
  );
  if (id === 'desert') return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="1.6" strokeLinecap="round">
      <circle cx="12" cy="12" r="3.5"/>
      {[0, 45, 90, 135, 180, 225, 270, 315].map(a => {
        const rad = (a * Math.PI) / 180;
        return <line key={a} x1={12 + Math.cos(rad) * 6} y1={12 + Math.sin(rad) * 6} x2={12 + Math.cos(rad) * 9} y2={12 + Math.sin(rad) * 9}/>;
      })}
    </svg>
  );
  if (id === 'nordic') return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 19 L9 8 L13 14 L17 6 L21 19 Z"/>
      <circle cx="9" cy="8" r="0.8" fill={color}/>
      <circle cx="17" cy="6" r="0.8" fill={color}/>
    </svg>
  );
  return null;
};

const BIOMES = [
  { id: 'latium', label: { fr: 'Latium', en: 'Latium' }, accent: '#c8a96a', terrain: '#e8d8a8', shipped: true },
  { id: 'albion', label: { fr: 'Albion', en: 'Albion' }, accent: '#a8c4d8', terrain: '#d8e0e8', shipped: true },
  { id: 'desert', label: { fr: 'Désert', en: 'Desert' }, accent: '#d8a868', terrain: '#f0d8a0', shipped: false },
  { id: 'nordic', label: { fr: 'Nordique', en: 'Nordic' }, accent: '#a8b8d0', terrain: '#d0d8e8', shipped: false },
];
const REQUIRED_MAPS_PER_BIOME = 3;

// Types d'îles avec couleurs d'accent
const ISLAND_TYPES = {
  N: { fr: 'Normal',     en: 'Normal',     short: 'N', color: '#8a9cb0' },
  S: { fr: 'Starter',    en: 'Starter',    short: 'S', color: '#c8a96a' },
  P: { fr: 'Pirate',     en: 'Pirate',     short: 'P', color: '#a85c5c' },
  T: { fr: '3rd Party',  en: '3rd Party',  short: 'T', color: '#9a7ab0' },
  V: { fr: 'Vulkan',     en: 'Vulkan',     short: 'V', color: '#d68a3c' },
  C: { fr: 'Continental',en: 'Continental',short: 'C', color: '#6a8a5c' },
};
const ISLAND_SIZES = { S: 'Small', M: 'Medium', L: 'Large', XL: 'Extra Large' };

// DLC catalogue
// adds: 'maps' = ajoute des cartes officielles ; 'islands' = ajoute des îles à un biome existant ; 'biome' = ajoute un nouveau biome (cartes + îles)
const DLCS = [
  { id: 'base', name: { fr: 'Jeu de base', en: 'Base game' }, biomes: ['latium', 'albion'], adds: 'maps', released: true },
  { id: 'dlc1', name: { fr: 'DLC 1 — Mare Magnum', en: 'DLC 1 — Mare Magnum' }, biomes: ['latium'], adds: 'islands', released: true, note: { fr: 'Ajoute de nouvelles îles au Latium', en: 'Adds new islands to Latium' } },
  { id: 'dlc2', name: { fr: 'DLC 2 — Aegyptus', en: 'DLC 2 — Aegyptus' }, biomes: ['desert'], adds: 'biome', released: false, note: { fr: 'Nouveau biome égyptien (à venir)', en: 'New Egyptian biome (upcoming)' } },
];

// Officielles (mock pour la modale d'import) — chacune liée à un DLC
const OFFICIAL_MAPS = [
  { id: 'celtic_xl_01', name: 'Celtic Province XL', biome: 'albion', size: 'XL', players: 4, dlc: 'base' },
  { id: 'latium_m_01',  name: 'Mare Nostrum',       biome: 'latium', size: 'L',  players: 4, dlc: 'base' },
  { id: 'latium_xl_01', name: 'Septem Insulae',     biome: 'latium', size: 'XL', players: 6, dlc: 'base' },
  { id: 'albion_m_01',  name: 'Caledonia',          biome: 'albion', size: 'M',  players: 2, dlc: 'base' },
  { id: 'albion_l_01',  name: 'Hibernia',           biome: 'albion', size: 'L',  players: 4, dlc: 'base' },
  { id: 'latium_l_02',  name: 'Insulae Aeoliae',    biome: 'latium', size: 'L',  players: 4, dlc: 'base' },
];

const I18N = {
  fr: {
    appName: 'Anno Mod Creator', subtitle: 'Éditeur de cartes',
    actions: { new: 'Nouvelle', importGame: 'Importer du jeu', save: 'Sauvegarder', export: 'Exporter le mod', exportPng: 'Exporter PNG' },
    panels: {
      stats: 'Statistiques', placeIslands: 'Placer une île', selection: 'Sélection',
      difficulty: 'Difficulté', view: 'Affichage', sizeLegend: 'Taille', typeLegend: 'Type',
      controls: 'Contrôles', total: 'Total', shipSpawns: 'Spawns navire',
      starterCheck: 'Starter', position: 'Pos', empty: 'Aucune sélection',
      placeCustom: 'Île personnalisée…', edit: 'Éditer', convert: 'Convertir', remove: 'Supprimer',
      showImages: 'Images d\'îles', resetZoom: 'Réinitialiser zoom',
    },
    biomeBar: { maps: 'cartes', map: 'Carte', complete: 'complet', futureDlc: 'Bientôt', requiredHint: '3 cartes requises par biome', exportBlocked: 'Export bloqué : {n} carte(s) manquante(s)', exportReady: 'Mod prêt à exporter' },
    diff: { easy: 'Facile', normal: 'Normal', hard: 'Difficile' },
    diffHint: { easy: 'Carte 1 · Facile', normal: 'Carte 2 · Normal', hard: 'Carte 3 · Difficile' },
    spawnLabel: 'Spawn navire',
    spawnHint: 'Glisser n\'importe où sur la carte',
    starterRequired: 'Starter requis',
    spawnsRequired: 'Spawns requis',
    importTitle: 'Importer une carte du jeu',
    importSub: 'Sélectionnez une carte officielle. Elle sera dupliquée dans la carte courante.',
    importNoMaps: 'Ce DLC n\'ajoute pas de cartes — uniquement de nouvelles îles.',
    importNotReleased: 'Pas encore disponible',
    filterDlc: 'DLC',
    filterAll: 'Tous',
    importBtn: 'Importer', cancel: 'Annuler', confirm: 'Confirmer',
    confirmTitle: 'Nouvelle carte ?', confirmBody: 'Le contenu actuel sera effacé.',
    controls: { scroll: 'Molette · Zoom', ctrl: 'Ctrl + Molette · Pan ↑↓', shift: 'Shift + Molette · Pan ←→', drag: 'Bouton du milieu · Pan' },
    spawn: 'Spawn',
  },
  en: {
    appName: 'Anno Mod Creator', subtitle: 'Map editor',
    actions: { new: 'New', importGame: 'Import from game', save: 'Save', export: 'Export mod', exportPng: 'Export PNG' },
    panels: {
      stats: 'Statistics', placeIslands: 'Place island', selection: 'Selection',
      difficulty: 'Difficulty', view: 'View', sizeLegend: 'Size', typeLegend: 'Type',
      controls: 'Controls', total: 'Total', shipSpawns: 'Ship spawns',
      starterCheck: 'Starter', position: 'Pos', empty: 'No selection',
      placeCustom: 'Custom island…', edit: 'Edit', convert: 'Convert', remove: 'Remove',
      showImages: 'Show island images', resetZoom: 'Reset zoom',
    },
    biomeBar: { maps: 'maps', map: 'Map', complete: 'complete', futureDlc: 'Soon', requiredHint: '3 maps required per biome', exportBlocked: 'Export blocked: {n} map(s) missing', exportReady: 'Mod ready to export' },
    diff: { easy: 'Easy', normal: 'Normal', hard: 'Hard' },
    diffHint: { easy: 'Map 1 · Easy', normal: 'Map 2 · Normal', hard: 'Map 3 · Hard' },
    spawnLabel: 'Ship spawn',
    spawnHint: 'Drag anywhere on the map',
    starterRequired: 'Starters required',
    spawnsRequired: 'Spawns required',
    importTitle: 'Import a map from the game',
    importSub: 'Pick an official map. It will be cloned into the current map.',
    importNoMaps: 'This DLC adds no maps — only new islands.',
    importNotReleased: 'Not yet released',
    filterDlc: 'DLC',
    filterAll: 'All',
    importBtn: 'Import', cancel: 'Cancel', confirm: 'Confirm',
    confirmTitle: 'New map?', confirmBody: 'Current content will be cleared.',
    controls: { scroll: 'Scroll · Zoom', ctrl: 'Ctrl + Scroll · Pan ↑↓', shift: 'Shift + Scroll · Pan ←→', drag: 'Middle drag · Pan' },
    spawn: 'Spawn',
  },
};

// ─────────────────────────────────────────────────────────────
// Slot d'île dans la grille (losange contenant un label "XL-S" + état)
// ─────────────────────────────────────────────────────────────
function IslandSlot({ slot, selected, onClick }) {
  const type = ISLAND_TYPES[slot.type];
  const isSpawn = slot.size === 'spawn';
  const w = slot.size === 'XL' ? 88 : slot.size === 'L' ? 72 : slot.size === 'M' ? 60 : 48;
  return (
    <div
      onClick={(e) => { e.stopPropagation(); onClick(); }}
      style={{
        position: 'absolute',
        left: slot.x, top: slot.y,
        transform: 'translate(-50%, -50%)',
        width: w, height: w,
        cursor: 'pointer', zIndex: selected ? 3 : 2,
      }}>
      {/* le losange du slot — orienté comme la carte (le canvas est tourné -45°, donc on contre-tourne pour rester aligné à la grille du jeu, qui elle est orthogonale dans ce repère) */}
      <div style={{
        position: 'absolute', inset: 0,
        background: isSpawn
          ? 'linear-gradient(135deg, rgba(60,108,181,0.25), rgba(60,108,181,0.08))'
          : `linear-gradient(135deg, rgba(20,33,58,0.85), rgba(20,33,58,0.55))`,
        border: `1.5px solid ${selected ? 'var(--color-gold-300)' : (isSpawn ? '#5e7fb0' : type ? type.color : 'var(--color-gold-700)')}`,
        boxShadow: selected
          ? '0 0 0 2px rgba(200,169,106,0.35), inset 0 0 0 1px rgba(255,255,255,0.08)'
          : 'inset 0 0 0 1px rgba(255,255,255,0.04), 0 2px 8px rgba(0,0,0,0.4)',
      }} />
      {/* contenu droit (counter-rotated) */}
      <div style={{
        position: 'absolute', inset: 0,
        transform: 'rotate(45deg)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        flexDirection: 'column', gap: 2,
        color: '#fff8e0', fontFamily: 'var(--font-display)',
        textShadow: '0 1px 2px rgba(0,0,0,0.7)',
        pointerEvents: 'none',
      }}>
        {isSpawn ? (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#b8d0f0" strokeWidth="1.8" strokeLinecap="round">
            <path d="M12 2 L12 22 M6 8 L18 8 M9 22 C7 20 6 17 6 14 M15 22 C17 20 18 17 18 14"/>
          </svg>
        ) : (
          <React.Fragment>
            <div style={{ fontSize: slot.size === 'XL' ? 13 : 12, fontWeight: 600, letterSpacing: '0.05em' }}>
              {slot.size}-{slot.type}
            </div>
            {slot.placed ? (
              <div style={{ width: 20, height: 12, background: type.color, opacity: 0.7, borderRadius: 1 }} />
            ) : (
              <div style={{ fontSize: 10, color: 'var(--color-gold-400)', opacity: 0.7 }}>?</div>
            )}
          </React.Fragment>
        )}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────
// Slot d'île dans la grille — rangée pour le panneau "Place Islands"
// ─────────────────────────────────────────────────────────────
function PlaceRow({ label, children }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '60px 1fr', alignItems: 'center', gap: 8, marginBottom: 6 }}>
      <div style={{ fontSize: 10, color: 'var(--color-text-dim)', textAlign: 'right', fontFamily: 'var(--font-display)', letterSpacing: '0.06em' }}>{label}</div>
      <div style={{ display: 'flex', gap: 4 }}>{children}</div>
    </div>
  );
}
function PlaceBtn({ children, color = 'var(--color-gold-700)', filled, onClick, dragId }) {
  return (
    <button
      onClick={onClick}
      draggable={!!dragId}
      onDragStart={dragId ? (e) => e.dataTransfer.setData('place', dragId) : undefined}
      style={{
        flex: 1, minWidth: 36, height: 28, padding: '0 6px',
        background: filled
          ? 'linear-gradient(180deg, var(--color-wine-600), var(--color-wine-800))'
          : 'linear-gradient(180deg, var(--color-ink-700), var(--color-ink-800))',
        border: `1px solid ${color}`,
        borderRadius: 3, color: filled ? '#fff' : 'var(--color-text)',
        fontFamily: 'var(--font-display)', fontSize: 11, letterSpacing: '0.05em',
        cursor: dragId ? 'grab' : 'pointer',
        boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.08)',
        whiteSpace: 'nowrap',
      }}>{children}</button>
  );
}

// ─────────────────────────────────────────────────────────────
// Panneau de stats à gauche
// ─────────────────────────────────────────────────────────────
function StatsPanel({ slots, spawns: spawnsCount, t }) {
  const placed = slots.filter(s => s.placed);
  const bySize = { S: 0, M: 0, L: 0, XL: 0 };
  const byType = { N: 0, S: 0, P: 0, T: 0, V: 0, C: 0 };
  placed.forEach(s => { bySize[s.size] = (bySize[s.size] || 0) + 1; byType[s.type] = (byType[s.type] || 0) + 1; });
  const starterReq = 4;
  const spawnsReq = 4;

  const Row = ({ label, value, sub, accent }) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', padding: '3px 0', fontSize: 11 }}>
      <span style={{ color: accent || 'var(--color-text-dim)' }}>{label}</span>
      <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--color-gold-400)' }}>{value}{sub && <span style={{ color: 'var(--color-text-mute)' }}> {sub}</span>}</span>
    </div>
  );

  return (
    <div style={{
      background: 'linear-gradient(180deg, rgba(20,33,58,0.92), rgba(13,22,38,0.92))',
      border: '1px solid var(--color-border)',
      borderRadius: 4,
      padding: '10px 12px',
      backdropFilter: 'blur(6px)',
      width: 180,
    }}>
      <div className="t-eyebrow" style={{ fontSize: 9, marginBottom: 6 }}>{t.panels.stats}</div>
      <div style={{ borderTop: '1px solid var(--color-border)', paddingTop: 6 }}>
        <Row label="Standard" value={bySize.S + bySize.M + bySize.L + bySize.XL} sub={`(${bySize.S}S ${bySize.M}M ${bySize.L}L ${bySize.XL}XL)`} accent="var(--color-text)" />
        <Row label="Starter" value={`${byType.S} / ${starterReq}`} accent={byType.S >= starterReq ? '#9bc88e' : '#c8a96a'} />
        <Row label="3rd Party" value={byType.T} accent="#9a7ab0" />
        <Row label="Pirate" value={byType.P} accent="#a85c5c" />
        <Row label="Vulkan" value={byType.V} accent="#d68a3c" />
        <Row label="Continental" value={byType.C} accent="#6a8a5c" />
        <div style={{ borderTop: '1px solid var(--color-border)', marginTop: 6, paddingTop: 6 }}>
          <Row label={t.panels.shipSpawns} value={`${spawnsCount} / ${spawnsReq}`} accent={spawnsCount >= spawnsReq ? '#9bc88e' : '#5e7fb0'} />
        </div>
        <div style={{ borderTop: '1px solid var(--color-gold-700)', marginTop: 6, paddingTop: 6 }}>
          <Row label={t.panels.total} value={placed.length + spawnsCount} accent="var(--color-gold-300)" />
        </div>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────
// Editor principal
// ─────────────────────────────────────────────────────────────
function Editor({ lang = 'fr' }) {
  const t = I18N[lang];

  // Génère une grille de slots pour une carte (positions en % du canvas)
  // Carte tournée -45°, mais le repère interne est orthogonal, donc on
  // place les slots sur une grille classique. Le canvas masque ce qui
  // dépasse le losange.
  // Slots de la grille (îles fixes : starter, normal, npc, vulkan)
  const generateSlots = (seed) => {
    const slots = [];
    let id = 1;
    const N = 5;
    const cell = 100 / N;
    // 4 starters aux 4 coins de la zone jouable, le reste varié
    const grid = [
      ['L-S','M-N','S-N','M-N','L-S'],
      ['M-N','XL-N','S-T','XL-N','M-N'],
      ['S-V','S-T','M-N','S-T','S-V'],
      ['M-N','XL-N','S-T','XL-N','M-N'],
      ['L-S','M-N','S-N','M-N','L-S'],
    ];
    for (let r = 0; r < N; r++) {
      for (let c = 0; c < N; c++) {
        const code = grid[r][c];
        const x = cell * c + cell / 2;
        const y = cell * r + cell / 2;
        const [size, type] = code.split('-');
        const placed = (r + c + seed) % 3 === 0;
        slots.push({ id: id++, x, y, size, type, placed, player: placed ? ((r + c) % 4) + 1 : null });
      }
    }
    return slots;
  };
  // Génère 4 spawns navire à des positions libres (coordonnées % du carré contenant le losange)
  const generateSpawns = (seed = 0) => {
    // par défaut, sur les 4 sommets du losange (N, E, S, W) — l'utilisateur peut les déplacer
    const positions = [
      { x: 50, y: 14 }, // N
      { x: 86, y: 50 }, // E
      { x: 50, y: 86 }, // S
      { x: 14, y: 50 }, // W
    ];
    return positions.map((p, i) => ({ id: 1000 + i, ...p, player: i + 1 }));
  };

  const initialMaps = () => {
    const m = {};
    BIOMES.forEach((b, bi) => {
      const diffs = ['easy', 'normal', 'hard'];
      m[b.id] = Array.from({ length: REQUIRED_MAPS_PER_BIOME }, (_, i) => ({
        id: `${b.id}-${i}`, name: '', difficulty: diffs[i] || 'easy',
        slots: b.shipped && i === 0 ? generateSlots(bi) : (b.shipped && i === 1 && b.id === 'latium' ? generateSlots(bi + 1).map(s => ({ ...s, placed: s.id % 4 === 0 })) : []),
        spawns: b.shipped && i === 0 ? generateSpawns(bi) : [],
      }));
    });
    return m;
  };

  const [maps, setMaps] = React.useState(initialMaps);
  const [activeBiome, setActiveBiome] = React.useState('latium');
  const [activeMapIdx, setActiveMapIdx] = React.useState(0);
  const [selectedSlotId, setSelectedSlotId] = React.useState(null);
  const [showConfirm, setShowConfirm] = React.useState(false);
  const [showImport, setShowImport] = React.useState(false);
  const [importView, setImportView] = React.useState('grid');
  const [importFilter, setImportFilter] = React.useState('all');
  const [importDlc, setImportDlc] = React.useState('all');
  const [tooltip, setTooltip] = React.useState(null);
  const [showImages, setShowImages] = React.useState(true);
  const canvasRef = React.useRef(null);

  const currentBiomeData = BIOMES.find(b => b.id === activeBiome);
  const currentMap = maps[activeBiome][activeMapIdx];
  const slots = currentMap.slots || [];
  const selectedSlot = slots.find(s => s.id === selectedSlotId);

  const isMapComplete = (bid, mi) => (maps[bid][mi].slots || []).filter(s => s.placed).length >= 5;
  const biomeCompleteCount = (bid) => maps[bid].filter((_, i) => isMapComplete(bid, i)).length;
  const isBiomeShippedComplete = (b) => b.shipped && biomeCompleteCount(b.id) === REQUIRED_MAPS_PER_BIOME;

  const shippedBiomes = BIOMES.filter(b => b.shipped);
  const missingTotal = shippedBiomes.reduce((acc, b) => acc + (REQUIRED_MAPS_PER_BIOME - biomeCompleteCount(b.id)), 0);
  const exportReady = missingTotal === 0;

  const updateSlots = (fn) => {
    setMaps(prev => ({
      ...prev,
      [activeBiome]: prev[activeBiome].map((m, i) => i === activeMapIdx ? { ...m, slots: fn(m.slots || []) } : m),
    }));
  };
  const setMapField = (key, value) => {
    setMaps(prev => ({
      ...prev,
      [activeBiome]: prev[activeBiome].map((m, i) => i === activeMapIdx ? { ...m, [key]: value } : m),
    }));
  };

  const toggleSlotPlaced = (id) => {
    updateSlots(prev => prev.map(s => s.id === id ? { ...s, placed: !s.placed } : s));
  };
  const setSelectedSlotProp = (key, value) => {
    if (!selectedSlot) return;
    updateSlots(prev => prev.map(s => s.id === selectedSlot.id ? { ...s, [key]: value } : s));
  };
  const removeSlot = () => {
    if (!selectedSlot) return;
    updateSlots(prev => prev.map(s => s.id === selectedSlot.id ? { ...s, placed: false, player: null } : s));
    setSelectedSlotId(null);
  };

  const importOfficial = (mapId) => {
    // Simule l'import : régénère des slots pour la carte courante avec un seed différent
    const idx = OFFICIAL_MAPS.findIndex(m => m.id === mapId);
    updateSlots(() => generateSlots(idx + 7));
    setMapField('name', OFFICIAL_MAPS[idx].name);
    setShowImport(false);
  };

  const placeFromPanel = (size, type) => {
    // place la prochaine île vide correspondant
    updateSlots(prev => {
      const target = prev.find(s => !s.placed && s.size === size && s.type === type);
      if (!target) return prev;
      return prev.map(s => s.id === target.id ? { ...s, placed: true, player: 1 } : s);
    });
  };

  const filteredOfficial = OFFICIAL_MAPS.filter(m =>
    (importFilter === 'all' || m.biome === importFilter) &&
    (importDlc === 'all' || m.dlc === importDlc)
  );
  const activeDlc = DLCS.find(d => d.id === importDlc);
  const dlcAddsNoMaps = activeDlc && activeDlc.adds === 'islands';
  const dlcNotReleased = activeDlc && !activeDlc.released;

  // ──────── Layout ────────
  return (
    <div style={{
      width: '100%', height: '100%',
      display: 'flex', flexDirection: 'column',
      background: 'var(--color-ink-900)', color: 'var(--color-text)',
      position: 'relative', overflow: 'hidden',
      fontFamily: 'var(--font-body)',
    }}>
      {/* ── Header ── */}
      <header style={{
        display: 'flex', alignItems: 'center', gap: 12,
        padding: '8px 16px',
        borderBottom: '1px solid var(--color-border)',
        background: 'linear-gradient(180deg, var(--color-ink-800), var(--color-ink-900))',
        flexShrink: 0,
      }}>
        <Logo size={24} color="var(--color-gold-500)" />
        <div>
          <div className="t-display" style={{ fontSize: 12, color: 'var(--color-gold-300)', letterSpacing: '0.12em', textTransform: 'uppercase', lineHeight: 1 }}>
            ANNO 117 — {t.appName}
          </div>
          <div style={{ fontSize: 9, color: 'var(--color-text-mute)', letterSpacing: '0.1em', marginTop: 2 }}>{t.subtitle}</div>
        </div>

        {/* Status mod au centre */}
        <div style={{
          marginLeft: 'auto', marginRight: 'auto',
          display: 'flex', alignItems: 'center', gap: 8,
          padding: '5px 14px',
          background: 'var(--color-ink-950)',
          border: '1px solid ' + (exportReady ? 'var(--color-gold-500)' : 'var(--color-border)'),
          borderRadius: 999,
          fontSize: 10,
          color: exportReady ? 'var(--color-gold-300)' : 'var(--color-text-dim)',
        }}>
          <DiamondOrnament size={8} color={exportReady ? '#c8a96a' : '#8a7240'} />
          <span style={{ letterSpacing: '0.06em' }}>
            {exportReady ? t.biomeBar.exportReady : t.biomeBar.exportBlocked.replace('{n}', missingTotal)}
          </span>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button className="btn btn-secondary btn-sm" onClick={() => setShowConfirm(true)}>
            <IconPlus size={12} /> {t.actions.new}
          </button>
          <button className="btn btn-secondary btn-sm" onClick={() => setShowImport(true)}>
            <IconImport size={12} /> {t.actions.importGame}
          </button>
          <div style={{ width: 1, height: 18, background: 'var(--color-border)' }} />
          <button className="btn btn-secondary btn-sm"><IconSave size={12} /> {t.actions.save}</button>
          <button className="btn btn-ghost btn-sm" title={t.actions.exportPng}><IconImage size={12} /></button>
          <button className={`btn ${exportReady ? 'btn-primary' : 'btn-secondary'} btn-sm`} disabled={!exportReady}>
            <IconExport size={12} /> {t.actions.export}
          </button>
        </div>
      </header>

      {/* ── Onglets de biome (style officiel) + onglets de cartes ── */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 16,
        padding: '8px 16px',
        background: 'var(--color-ink-800)',
        borderBottom: '1px solid var(--color-border)',
        flexShrink: 0,
      }}>
        {/* Boutons biomes (compacts) */}
        <div style={{ display: 'flex', gap: 8 }}>
          {BIOMES.map(b => {
            const active = activeBiome === b.id;
            const count = biomeCompleteCount(b.id);
            return (
              <button key={b.id}
                disabled={!b.shipped}
                onClick={() => { setActiveBiome(b.id); setActiveMapIdx(0); setSelectedSlotId(null); }}
                style={{
                  appearance: 'none', cursor: b.shipped ? 'pointer' : 'not-allowed',
                  display: 'flex', alignItems: 'center', gap: 8,
                  padding: '6px 14px',
                  background: active
                    ? 'linear-gradient(180deg, var(--color-wine-700), var(--color-wine-900))'
                    : 'linear-gradient(180deg, var(--color-ink-700), var(--color-ink-800))',
                  border: '1px solid ' + (active ? 'var(--color-gold-500)' : 'var(--color-border)'),
                  borderRadius: 4,
                  color: active ? 'var(--color-gold-300)' : (b.shipped ? 'var(--color-text)' : 'var(--color-text-mute)'),
                  fontFamily: 'var(--font-display)', fontSize: 12, letterSpacing: '0.06em',
                  opacity: b.shipped ? 1 : 0.5,
                  boxShadow: active ? 'inset 0 1px 0 rgba(255,255,255,0.1)' : 'none',
                }}>
                <BiomeGlyph id={b.id} size={14} color={active ? 'var(--color-gold-300)' : (b.shipped ? 'var(--color-gold-500)' : 'var(--color-text-mute)')} />
                <span>{b.label[lang]}</span>
                {b.shipped && (
                  <span style={{
                    fontSize: 9, padding: '1px 5px', borderRadius: 999,
                    background: count === REQUIRED_MAPS_PER_BIOME ? '#4d8c4a' : 'var(--color-ink-950)',
                    color: count === REQUIRED_MAPS_PER_BIOME ? '#fff' : 'var(--color-gold-400)',
                    fontFamily: 'var(--font-mono)',
                  }}>{count}/{REQUIRED_MAPS_PER_BIOME}</span>
                )}
                {!b.shipped && <span style={{ fontSize: 9, color: 'var(--color-text-mute)' }}>{t.biomeBar.futureDlc}</span>}
              </button>
            );
          })}
        </div>

        <div style={{ width: 1, height: 24, background: 'var(--color-border)' }} />

        {/* Onglets de cartes */}
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <span className="t-eyebrow" style={{ fontSize: 9 }}>{t.biomeBar.map}</span>
          {maps[activeBiome].map((m, i) => {
            const complete = isMapComplete(activeBiome, i);
            const active = activeMapIdx === i;
            return (
              <button key={m.id} onClick={() => { setActiveMapIdx(i); setSelectedSlotId(null); }} style={{
                appearance: 'none', cursor: 'pointer',
                width: 32, height: 28,
                background: active ? 'var(--color-wine-800)' : 'var(--color-ink-700)',
                border: '1px solid ' + (active ? 'var(--color-gold-500)' : 'var(--color-border)'),
                color: active ? 'var(--color-gold-300)' : 'var(--color-text-dim)',
                fontFamily: 'var(--font-display)', fontSize: 12,
                position: 'relative',
              }}>
                {i + 1}
                <span style={{
                  position: 'absolute', bottom: -2, right: -2,
                  width: 8, height: 8, borderRadius: '50%',
                  background: complete ? '#4d8c4a' : 'transparent',
                  border: complete ? 'none' : '1px solid var(--color-gold-700)',
                }} />
              </button>
            );
          })}
        </div>

        {/* Difficulté (auto par numéro de carte) */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginLeft: 'auto' }}>
          <span className="t-eyebrow" style={{ fontSize: 9 }}>{t.panels.difficulty}</span>
          {(() => {
            const dKey = ['easy','normal','hard'][activeMapIdx] || 'easy';
            const dColors = {
              easy:   { bg: 'rgba(77,140,74,0.15)',  fg: '#9bc88e', bd: '#4d8c4a' },
              normal: { bg: 'rgba(200,169,106,0.15)',fg: 'var(--color-gold-300)', bd: 'var(--color-gold-700)' },
              hard:   { bg: 'rgba(143,52,57,0.18)',  fg: '#e6a4a8', bd: 'var(--color-wine-500)' },
            }[dKey];
            return (
              <span style={{
                background: dColors.bg, border: `1px solid ${dColors.bd}`,
                color: dColors.fg, padding: '4px 12px', borderRadius: 3,
                fontSize: 11, fontFamily: 'var(--font-body)',
                letterSpacing: '0.04em', textTransform: 'uppercase',
              }}>{t.diff[dKey]}</span>
            );
          })()}
        </div>
      </div>

      {/* ── Corps ── */}
      <div style={{ flex: 1, display: 'flex', minHeight: 0, position: 'relative' }}>

        {/* ── Stats à gauche (flottant, en col) ── */}
        <aside style={{
          width: 200, flexShrink: 0,
          display: 'flex', flexDirection: 'column',
          gap: 8, padding: 10,
          background: 'var(--color-ink-800)',
          borderRight: '1px solid var(--color-border)',
        }}>
          <StatsPanel slots={slots} spawns={(currentMap.spawns || []).length} t={t} />
        </aside>

        {/* ── Canvas central ── */}
        <main style={{
          flex: 1, position: 'relative',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          overflow: 'hidden',
          background: 'radial-gradient(ellipse at center, #2a3850, var(--color-ink-950))',
        }}>
          {/* fine pattern bg */}
          <div style={{
            position: 'absolute', inset: 0,
            backgroundImage: 'repeating-linear-gradient(45deg, rgba(200,169,106,0.02) 0 1px, transparent 1px 24px), repeating-linear-gradient(-45deg, rgba(200,169,106,0.02) 0 1px, transparent 1px 24px)',
            pointerEvents: 'none',
          }} />

          {/* Wrapper carré (zone du losange) */}
          <div style={{
            position: 'relative',
            width: 'min(82%, 720px)', aspectRatio: '1',
          }}>
            {/* Le losange (carré tourné -45°) */}
            <div
              ref={canvasRef}
              onClick={() => setSelectedSlotId(null)}
              className="tex-stone"
              style={{
                position: 'absolute',
                top: '50%', left: '50%',
                width: '70.7%', height: '70.7%',
                transform: 'translate(-50%, -50%) rotate(-45deg)',
                border: '2px solid var(--color-gold-700)',
                background: '#1f2d48',
                boxShadow: 'inset 0 0 0 1px rgba(0,0,0,0.4), inset 0 0 0 6px rgba(200,169,106,0.15), 0 20px 60px rgba(0,0,0,0.6)',
              }}>
              {/* Liseré violet des bords (comme dans le screenshot) */}
              <div style={{
                position: 'absolute', inset: 6,
                border: '1px solid #8a4ca8',
                opacity: 0.6, pointerEvents: 'none',
              }} />
              {/* Grille de fond (carrés isométriques) */}
              <svg style={{ position: 'absolute', inset: 12, width: 'calc(100% - 24px)', height: 'calc(100% - 24px)', opacity: 0.3, pointerEvents: 'none' }}>
                <defs>
                  <pattern id="diaGrid" width="20%" height="20%" patternUnits="userSpaceOnUse" x="0" y="0">
                    <path d="M 0 0 L 100 0 L 100 100 L 0 100 Z" fill="none" stroke="#5a7290" strokeWidth="0.5"/>
                  </pattern>
                </defs>
                <rect width="100%" height="100%" fill="url(#diaGrid)"/>
              </svg>
              {/* Slots */}
              <div style={{ position: 'absolute', inset: 12, pointerEvents: 'none' }}>
                {slots.map(slot => (
                  <div key={slot.id} style={{
                    position: 'absolute',
                    left: `${slot.x}%`, top: `${slot.y}%`,
                    pointerEvents: 'auto',
                  }}>
                    <IslandSlot
                      slot={slot}
                      selected={selectedSlotId === slot.id}
                      onClick={() => setSelectedSlotId(slot.id)}
                    />
                  </div>
                ))}
              </div>
            </div>

            {/* Marqueurs cardinaux N/E/S/W aux 4 sommets, hors du losange */}
            {[
              { label: 'N', style: { left: '50%', top: 0, transform: 'translate(-50%, -50%)' } },
              { label: 'E', style: { right: 0, top: '50%', transform: 'translate(50%, -50%)' } },
              { label: 'S', style: { left: '50%', bottom: 0, transform: 'translate(-50%, 50%)' } },
              { label: 'W', style: { left: 0, top: '50%', transform: 'translate(-50%, -50%)' } },
            ].map(c => (
              <div key={c.label} style={{
                position: 'absolute', ...c.style,
                width: 28, height: 28, borderRadius: '50%',
                background: 'radial-gradient(circle at 30% 25%, var(--color-ink-700), var(--color-ink-900))',
                border: '1px solid var(--color-gold-500)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                color: 'var(--color-gold-300)',
                fontFamily: 'var(--font-display)', fontWeight: 600, fontSize: 11,
                boxShadow: '0 4px 10px rgba(0,0,0,0.5)',
                zIndex: 5,
              }}>{c.label}</div>
            ))}

            {/* Spawns navire (draggables sur tout le carré, pas dans la grille) */}
            {(currentMap.spawns || []).map(sp => (
              <div key={sp.id}
                onMouseDown={(e) => {
                  e.stopPropagation();
                  const wrap = e.currentTarget.parentElement;
                  const rect = wrap.getBoundingClientRect();
                  const onMove = (ev) => {
                    const nx = Math.max(2, Math.min(98, ((ev.clientX - rect.left) / rect.width) * 100));
                    const ny = Math.max(2, Math.min(98, ((ev.clientY - rect.top) / rect.height) * 100));
                    setMaps(prev => ({
                      ...prev,
                      [activeBiome]: prev[activeBiome].map((m, i) =>
                        i === activeMapIdx
                          ? { ...m, spawns: (m.spawns || []).map(s => s.id === sp.id ? { ...s, x: nx, y: ny } : s) }
                          : m
                      ),
                    }));
                  };
                  const onUp = () => {
                    window.removeEventListener('mousemove', onMove);
                    window.removeEventListener('mouseup', onUp);
                  };
                  window.addEventListener('mousemove', onMove);
                  window.addEventListener('mouseup', onUp);
                }}
                title={`Spawn P${sp.player}`}
                style={{
                  position: 'absolute',
                  left: `${sp.x}%`, top: `${sp.y}%`,
                  transform: 'translate(-50%, -50%)',
                  width: 22, height: 22,
                  cursor: 'grab',
                  zIndex: 6,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  filter: 'drop-shadow(0 3px 6px rgba(0,0,0,0.6))',
                }}>
                <svg viewBox="0 0 24 24" width="22" height="22">
                  <polygon points="12,2 22,12 12,22 2,12"
                    fill={['#c8a96a','#7a8fb5','#9a6f9c','#7aa78b'][sp.player - 1] || '#c8a96a'}
                    stroke="#0d1626" strokeWidth="1.5"/>
                  <text x="12" y="16" textAnchor="middle"
                    fill="#0d1626" fontFamily="var(--font-display)" fontWeight="700" fontSize="10">
                    {sp.player}
                  </text>
                </svg>
              </div>
            ))}
            {selectedSlot && (
              <div style={{
                position: 'absolute', top: 0, right: 8,
                background: 'rgba(13,22,38,0.95)',
                border: '1px solid var(--color-gold-500)',
                borderRadius: 4, padding: '8px 10px',
                fontSize: 10, color: 'var(--color-gold-300)',
                fontFamily: 'var(--font-mono)', letterSpacing: '0.05em',
                minWidth: 150, zIndex: 4,
              }}>
                <div className="t-eyebrow" style={{ fontSize: 9, marginBottom: 4 }}>{t.panels.selection}</div>
                {selectedSlot.size === 'spawn' ? (
                  <div style={{ color: '#b8d0f0' }}>{t.spawn}</div>
                ) : (
                  <React.Fragment>
                    <div>{ISLAND_SIZES[selectedSlot.size]} · {ISLAND_TYPES[selectedSlot.type][lang]}</div>
                    <div style={{ color: 'var(--color-text-mute)', marginTop: 2 }}>
                      {t.panels.position}: ({Math.round(selectedSlot.x * 15)}, {Math.round(selectedSlot.y * 15)})
                    </div>
                  </React.Fragment>
                )}
              </div>
            )}
          </div>

          {/* Légende en bas-gauche du main */}
          <div style={{
            position: 'absolute', bottom: 14, left: 14,
            background: 'rgba(13,22,38,0.92)',
            border: '1px solid var(--color-border)',
            borderRadius: 4, padding: '8px 12px',
            fontSize: 10, color: 'var(--color-text-dim)',
            display: 'grid', gridTemplateColumns: 'auto auto', gap: '2px 16px',
            backdropFilter: 'blur(6px)',
            zIndex: 6,
          }}>
            <div>
              <div className="t-eyebrow" style={{ fontSize: 8, marginBottom: 3 }}>{t.panels.sizeLegend}</div>
              <div><span style={{ color: 'var(--color-gold-400)' }}>S</span> Small</div>
              <div><span style={{ color: 'var(--color-gold-400)' }}>M</span> Medium</div>
              <div><span style={{ color: 'var(--color-gold-400)' }}>L</span> Large</div>
              <div><span style={{ color: 'var(--color-gold-400)' }}>XL</span> Extra Large</div>
            </div>
            <div>
              <div className="t-eyebrow" style={{ fontSize: 8, marginBottom: 3 }}>{t.panels.typeLegend}</div>
              <div><span style={{ color: '#8a9cb0' }}>N</span> Normal</div>
              <div><span style={{ color: '#c8a96a' }}>S</span> Starter</div>
              <div><span style={{ color: '#a85c5c' }}>P</span> Pirate</div>
              <div><span style={{ color: '#9a7ab0' }}>T</span> 3rd Party</div>
              <div><span style={{ color: '#d68a3c' }}>V</span> Vulkan</div>
              <div><span style={{ color: '#6a8a5c' }}>C</span> Continental</div>
            </div>
          </div>

          {/* Contrôles en bas-droite */}
          <div style={{
            position: 'absolute', bottom: 14, right: 14,
            background: 'rgba(13,22,38,0.92)',
            border: '1px solid var(--color-border)',
            borderRadius: 4, padding: '8px 12px',
            fontSize: 10, color: 'var(--color-text-dim)',
            backdropFilter: 'blur(6px)',
            zIndex: 6, lineHeight: 1.6,
          }}>
            <div className="t-eyebrow" style={{ fontSize: 8, marginBottom: 3 }}>{t.panels.controls}</div>
            <div>{t.controls.scroll}</div>
            <div>{t.controls.ctrl}</div>
            <div>{t.controls.shift}</div>
            <div>{t.controls.drag}</div>
          </div>

          {/* Toggle View en haut-gauche */}
          <div style={{
            position: 'absolute', top: 14, left: 14,
            background: 'rgba(13,22,38,0.92)',
            border: '1px solid var(--color-border)',
            borderRadius: 4, padding: '6px 10px',
            display: 'flex', alignItems: 'center', gap: 8,
            fontSize: 10, color: 'var(--color-text-dim)',
            backdropFilter: 'blur(6px)',
            zIndex: 6,
          }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
              <input type="checkbox" checked={showImages} onChange={(e) => setShowImages(e.target.checked)}
                style={{ accentColor: 'var(--color-gold-500)' }} />
              {t.panels.showImages}
            </label>
          </div>
        </main>

        {/* ── Panneau droit : Place Islands + actions ── */}
        <aside style={{
          width: 240, flexShrink: 0,
          background: 'var(--color-ink-800)',
          borderLeft: '1px solid var(--color-border)',
          display: 'flex', flexDirection: 'column',
          overflow: 'hidden',
        }}>
          {/* Titre du biome */}
          <div style={{
            padding: '12px 14px',
            borderBottom: '1px solid var(--color-border)',
            display: 'flex', alignItems: 'center', gap: 10,
          }}>
            <BiomeGlyph id={activeBiome} size={18} color="var(--color-gold-400)" />
            <div className="t-display" style={{ fontSize: 14, color: 'var(--color-gold-300)', letterSpacing: '0.1em', textTransform: 'uppercase' }}>
              {currentBiomeData.label[lang]}
            </div>
          </div>

          {/* Place Islands */}
          <div style={{ padding: 12, flex: 1, overflowY: 'auto' }}>
            <div className="t-eyebrow" style={{ marginBottom: 10 }}>{t.panels.placeIslands}</div>

            <PlaceRow label={t.spawn}>
              <PlaceBtn dragId="spawn" filled color="#5e7fb0">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="1.8" style={{ verticalAlign: 'middle' }}>
                  <path d="M12 2 L12 22 M6 8 L18 8 M9 22 C7 20 6 17 6 14 M15 22 C17 20 18 17 18 14"/>
                </svg>
              </PlaceBtn>
            </PlaceRow>
            <PlaceRow label="Starter">
              <PlaceBtn dragId="L-S" onClick={() => placeFromPanel('L', 'S')} color="#c8a96a">L</PlaceBtn>
              <PlaceBtn dragId="XL-S" onClick={() => placeFromPanel('XL', 'S')} color="#c8a96a">XL</PlaceBtn>
            </PlaceRow>
            <PlaceRow label="Normal">
              <PlaceBtn dragId="S-N" onClick={() => placeFromPanel('S', 'N')}>S</PlaceBtn>
              <PlaceBtn dragId="M-N" onClick={() => placeFromPanel('M', 'N')}>M</PlaceBtn>
              <PlaceBtn dragId="L-N" onClick={() => placeFromPanel('L', 'N')}>L</PlaceBtn>
              <PlaceBtn dragId="XL-N" onClick={() => placeFromPanel('XL', 'N')}>XL</PlaceBtn>
            </PlaceRow>
            <PlaceRow label="NPCs">
              <PlaceBtn dragId="L-T" onClick={() => placeFromPanel('L', 'T')} color="#9a7ab0">3rd Party</PlaceBtn>
              <PlaceBtn dragId="L-P" onClick={() => placeFromPanel('L', 'P')} color="#a85c5c">Pirate</PlaceBtn>
            </PlaceRow>
            <PlaceRow label="Vulkan">
              <PlaceBtn dragId="S-V" onClick={() => placeFromPanel('S', 'V')} color="#d68a3c">S</PlaceBtn>
              <PlaceBtn dragId="M-V" onClick={() => placeFromPanel('M', 'V')} color="#d68a3c">M</PlaceBtn>
            </PlaceRow>

            <button className="btn btn-secondary btn-sm" style={{ width: '100%', marginTop: 10 }}>
              {t.panels.placeCustom}
            </button>

            {/* Actions sur la sélection */}
            {selectedSlot && selectedSlot.size !== 'spawn' && (
              <div style={{ marginTop: 14, padding: 10, background: 'var(--color-ink-950)', border: '1px solid var(--color-border)', borderRadius: 4 }}>
                <div className="t-eyebrow" style={{ fontSize: 9, marginBottom: 8 }}>{t.panels.selection}</div>
                <div style={{ display: 'flex', gap: 4, marginBottom: 8 }}>
                  <button className="btn btn-secondary btn-sm" style={{ flex: 1, fontSize: 10 }}>{t.panels.edit}</button>
                  <button className="btn btn-secondary btn-sm" style={{ flex: 1, fontSize: 10 }}>{t.panels.convert}</button>
                  <button className="btn btn-primary btn-sm" style={{ width: 28, padding: 0, background: 'var(--color-wine-700)' }} onClick={removeSlot}>
                    <IconClose size={11} />
                  </button>
                </div>
                <div style={{ marginTop: 8 }}>
                  <div style={{ fontSize: 9, color: 'var(--color-text-mute)', marginBottom: 4 }}>Joueur</div>
                  <div style={{ display: 'flex', gap: 4 }}>
                    {PLAYER_COLORS.map(p => (
                      <button key={p.id}
                        onClick={() => setSelectedSlotProp('player', p.id)}
                        style={{
                          width: 22, height: 22, borderRadius: '50%',
                          background: p.color,
                          border: '2px solid ' + (selectedSlot.player === p.id ? 'var(--color-gold-300)' : 'var(--color-gold-700)'),
                          cursor: 'pointer', padding: 0,
                        }} />
                    ))}
                  </div>
                </div>
              </div>
            )}

            {/* Compteur Starter */}
            <div style={{
              marginTop: 12,
              padding: '8px 10px',
              background: 'var(--color-ink-950)',
              border: '1px solid var(--color-gold-700)',
              borderRadius: 4,
              fontSize: 11,
              display: 'flex', justifyContent: 'space-between',
              color: 'var(--color-gold-300)',
            }}>
              <span>✓ {t.panels.starterCheck}</span>
              <span style={{ fontFamily: 'var(--font-mono)' }}>
                {slots.filter(s => s.type === 'S' && s.placed).length} / 4
              </span>
            </div>
          </div>
        </aside>
      </div>

      {/* ── Modale d'import ── */}
      {showImport && (
        <div className="modal-backdrop" onClick={() => setShowImport(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 720 }}>
            <button className="modal-close" onClick={() => setShowImport(false)}><IconClose size={14} /></button>
            <div className="modal-header">
              <h2 className="modal-title">{t.importTitle}</h2>
              <div style={{ fontSize: 11, color: 'var(--color-text-dim)', textAlign: 'center', marginTop: 4 }}>{t.importSub}</div>
            </div>
            <div className="modal-body">
              {/* Filtres */}
              <div style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
                <span className="t-eyebrow" style={{ fontSize: 9 }}>Biome</span>
                {[{ id: 'all', label: t.filterAll }, ...BIOMES.filter(b => b.shipped).map(b => ({ id: b.id, label: b.label[lang] }))].map(f => (
                  <button key={f.id} onClick={() => setImportFilter(f.id)} style={{
                    background: importFilter === f.id ? 'var(--color-wine-700)' : 'var(--color-ink-700)',
                    border: '1px solid ' + (importFilter === f.id ? 'var(--color-gold-500)' : 'var(--color-border)'),
                    color: importFilter === f.id ? 'var(--color-gold-300)' : 'var(--color-text-dim)',
                    padding: '3px 10px', borderRadius: 3, fontSize: 11, cursor: 'pointer',
                  }}>{f.label}</button>
                ))}
                <div style={{ marginLeft: 'auto', display: 'flex', gap: 4 }}>
                  <button onClick={() => setImportView('grid')} style={{
                    background: importView === 'grid' ? 'var(--color-wine-700)' : 'var(--color-ink-700)',
                    border: '1px solid var(--color-border)', color: 'var(--color-text)',
                    padding: '3px 10px', borderRadius: 3, fontSize: 11, cursor: 'pointer',
                  }}>{lang === 'fr' ? 'Vignettes' : 'Grid'}</button>
                  <button onClick={() => setImportView('list')} style={{
                    background: importView === 'list' ? 'var(--color-wine-700)' : 'var(--color-ink-700)',
                    border: '1px solid var(--color-border)', color: 'var(--color-text)',
                    padding: '3px 10px', borderRadius: 3, fontSize: 11, cursor: 'pointer',
                  }}>{lang === 'fr' ? 'Liste' : 'List'}</button>
                </div>
              </div>
              {/* Filtre DLC */}
              <div style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
                <span className="t-eyebrow" style={{ fontSize: 9 }}>{t.filterDlc}</span>
                {[{ id: 'all', label: t.filterAll }, ...DLCS.map(d => ({ id: d.id, label: d.name[lang], released: d.released }))].map(f => (
                  <button key={f.id} onClick={() => setImportDlc(f.id)} style={{
                    background: importDlc === f.id ? 'var(--color-wine-700)' : 'var(--color-ink-700)',
                    border: '1px solid ' + (importDlc === f.id ? 'var(--color-gold-500)' : 'var(--color-border)'),
                    color: importDlc === f.id ? 'var(--color-gold-300)' : (f.released === false ? 'var(--color-text-mute)' : 'var(--color-text-dim)'),
                    padding: '3px 10px', borderRadius: 3, fontSize: 11, cursor: 'pointer',
                    opacity: f.released === false ? 0.7 : 1,
                    display: 'inline-flex', alignItems: 'center', gap: 5,
                  }}>
                    {f.label}
                    {f.released === false && <span style={{ fontSize: 8, fontFamily: 'var(--font-mono)', padding: '1px 4px', background: 'rgba(143,52,57,0.4)', border: '1px solid var(--color-wine-500)', color: '#e6a4a8', borderRadius: 2, letterSpacing: '0.05em' }}>{lang === 'fr' ? 'BIENTÔT' : 'SOON'}</span>}
                  </button>
                ))}
              </div>
              {(dlcAddsNoMaps || dlcNotReleased || filteredOfficial.length === 0) ? (
                <div style={{
                  padding: '40px 20px', textAlign: 'center',
                  border: '1px dashed var(--color-border)', borderRadius: 4,
                  color: 'var(--color-text-dim)', fontSize: 12,
                  background: 'rgba(0,0,0,0.15)',
                }}>
                  <div style={{ fontFamily: 'var(--font-display)', color: 'var(--color-gold-400)', fontSize: 14, marginBottom: 6, letterSpacing: '0.06em' }}>
                    {dlcNotReleased ? t.importNotReleased : (dlcAddsNoMaps ? t.importNoMaps : (lang === 'fr' ? 'Aucun résultat' : 'No results'))}
                  </div>
                  {activeDlc && activeDlc.note && (
                    <div style={{ fontSize: 11, color: 'var(--color-text-mute)', maxWidth: 360, margin: '0 auto' }}>{activeDlc.note[lang]}</div>
                  )}
                </div>
              ) : importView === 'grid' ? (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10, maxHeight: 380, overflowY: 'auto' }}>
                  {filteredOfficial.map(m => {
                    const biome = BIOMES.find(b => b.id === m.biome);
                    return (
                      <button key={m.id} onClick={() => importOfficial(m.id)} style={{
                        appearance: 'none', cursor: 'pointer',
                        background: 'var(--color-ink-700)',
                        border: '1px solid var(--color-border)',
                        borderRadius: 4, padding: 10, textAlign: 'left',
                        color: 'var(--color-text)', display: 'flex', flexDirection: 'column', gap: 8,
                      }}
                      onMouseOver={(e) => e.currentTarget.style.borderColor = 'var(--color-gold-500)'}
                      onMouseOut={(e) => e.currentTarget.style.borderColor = 'var(--color-border)'}>
                        {/* mini diamant */}
                        <div style={{ aspectRatio: '1', position: 'relative', background: 'var(--color-ink-950)', borderRadius: 3, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                          <div style={{
                            width: '70%', height: '70%',
                            transform: 'rotate(-45deg)',
                            background: biome.terrain,
                            border: '1px solid var(--color-gold-700)',
                            opacity: 0.6,
                          }} />
                          <div style={{ position: 'absolute', top: 4, right: 4, background: 'rgba(0,0,0,0.5)', padding: '1px 5px', borderRadius: 2, fontSize: 9, color: 'var(--color-gold-300)', fontFamily: 'var(--font-mono)' }}>{m.size}</div>
                        </div>
                        <div>
                          <div style={{ fontFamily: 'var(--font-display)', fontSize: 12, color: 'var(--color-gold-300)', letterSpacing: '0.04em' }}>{m.name}</div>
                          <div style={{ fontSize: 10, color: 'var(--color-text-mute)', marginTop: 2 }}>
                            {biome.label[lang]} · {m.players}P · {(DLCS.find(d => d.id === m.dlc) || {}).name?.[lang]}
                          </div>
                        </div>
                      </button>
                    );
                  })}
                </div>
              ) : (
                <div style={{ maxHeight: 380, overflowY: 'auto', border: '1px solid var(--color-border)', borderRadius: 4 }}>
                  {filteredOfficial.map((m, i) => {
                    const biome = BIOMES.find(b => b.id === m.biome);
                    return (
                      <button key={m.id} onClick={() => importOfficial(m.id)} style={{
                        appearance: 'none', cursor: 'pointer',
                        width: '100%', display: 'grid', gridTemplateColumns: '24px 1fr 80px 60px 60px',
                        gap: 10, alignItems: 'center',
                        padding: '8px 12px',
                        background: i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,0.02)',
                        border: 'none', borderTop: i === 0 ? 'none' : '1px solid var(--color-border)',
                        color: 'var(--color-text)', textAlign: 'left', fontSize: 11,
                      }}>
                        <BiomeGlyph id={m.biome} size={16} color="var(--color-gold-500)" />
                        <span style={{ fontFamily: 'var(--font-display)', color: 'var(--color-gold-300)' }}>{m.name}</span>
                        <span style={{ color: 'var(--color-text-dim)' }}>{biome.label[lang]}</span>
                        <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--color-gold-400)' }}>{m.size} · {m.players}P</span>
                        <span style={{ color: 'var(--color-text-dim)' }}>{(DLCS.find(d => d.id === m.dlc) || {}).name?.[lang]}</span>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ── Modale "nouvelle carte" ── */}
      {showConfirm && (
        <div className="modal-backdrop" onClick={() => setShowConfirm(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <button className="modal-close" onClick={() => setShowConfirm(false)}><IconClose size={14} /></button>
            <div className="modal-header">
              <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 4 }}>
                <Laurel size={48} />
              </div>
              <h2 className="modal-title">{t.confirmTitle}</h2>
            </div>
            <div className="modal-body" style={{ textAlign: 'center', color: 'var(--color-text-dim)', fontSize: 13 }}>
              {t.confirmBody}
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setShowConfirm(false)}>{t.cancel}</button>
              <button className="btn btn-primary" onClick={() => { updateSlots(() => generateSlots(Date.now() % 11)); setShowConfirm(false); }}>{t.confirm}</button>
            </div>
          </div>
        </div>
      )}

      {tooltip && (
        <div className="tooltip" style={{
          position: 'fixed', left: tooltip.x, top: tooltip.y,
          transform: tooltip.below ? 'translate(-50%, 0)' : 'translate(0, -50%)',
          zIndex: 9999,
        }}>{tooltip.text}</div>
      )}
    </div>
  );
}

window.Editor = Editor;
window.BiomeGlyph = BiomeGlyph;
window.BIOMES = BIOMES;
