// Icons.jsx — SVG icons originaux (style ligne fine, esprit antique)

const Icon = ({ children, size = 16, stroke = 1.6, color = "currentColor", style }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
       stroke={color} strokeWidth={stroke} strokeLinecap="round" strokeLinejoin="round" style={style}>
    {children}
  </svg>
);

const IconBrush = (p) => <Icon {...p}><path d="M14 4l6 6-9 9-6-2-2-6 9-9z"/><path d="M5 17l-2 4 4-2"/></Icon>;
const IconEraser = (p) => <Icon {...p}><path d="M3 17l8-8 7 7-6 6H6l-3-3z"/><path d="M14 6l4 4"/></Icon>;
const IconHand = (p) => <Icon {...p}><path d="M7 11V6a2 2 0 014 0v5"/><path d="M11 10V4a2 2 0 014 0v7"/><path d="M15 11V6a2 2 0 014 0v8a7 7 0 01-7 7H9a4 4 0 01-3-1l-4-4 2-2 3 2V8a2 2 0 014 0v3"/></Icon>;
const IconIsland = (p) => <Icon {...p}><path d="M3 16c2-1 4-1 6 0s4 1 6 0 4-1 6 0"/><path d="M5 13l2-3 3 2 3-4 3 3 3-2"/></Icon>;
const IconGrid = (p) => <Icon {...p}><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></Icon>;
const IconLayers = (p) => <Icon {...p}><path d="M12 2l10 6-10 6L2 8z"/><path d="M2 12l10 6 10-6"/><path d="M2 16l10 6 10-6"/></Icon>;
const IconUsers = (p) => <Icon {...p}><circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.5"/><path d="M3 20c1-3 3-5 6-5s5 2 6 5"/><path d="M14 20c1-2 2-3 4-3"/></Icon>;
const IconSave = (p) => <Icon {...p}><path d="M5 3h11l3 3v15H5z"/><path d="M8 3v5h8V3"/><rect x="8" y="13" width="8" height="6"/></Icon>;
const IconExport = (p) => <Icon {...p}><path d="M12 3v12"/><path d="M7 8l5-5 5 5"/><path d="M5 17v3a1 1 0 001 1h12a1 1 0 001-1v-3"/></Icon>;
const IconMountain = (p) => <Icon {...p}><path d="M3 20l6-10 4 6 3-4 5 8z"/></Icon>;
const IconForest = (p) => <Icon {...p}><path d="M12 3l5 7h-3l4 6h-3l3 5H6l3-5H6l4-6H7z"/></Icon>;
const IconWater = (p) => <Icon {...p}><path d="M3 8c2-2 4-2 6 0s4 2 6 0 4-2 6 0"/><path d="M3 14c2-2 4-2 6 0s4 2 6 0 4-2 6 0"/><path d="M3 20c2-2 4-2 6 0s4 2 6 0 4-2 6 0"/></Icon>;
const IconResource = (p) => <Icon {...p}><path d="M12 3l3 6 6 1-4 4 1 6-6-3-6 3 1-6-4-4 6-1z"/></Icon>;
const IconBuilding = (p) => <Icon {...p}><path d="M4 21V9l8-5 8 5v12"/><path d="M9 21v-7h6v7"/><path d="M4 21h16"/></Icon>;
const IconChevron = (p) => <Icon {...p}><path d="M9 6l6 6-6 6"/></Icon>;
const IconClose = (p) => <Icon {...p}><path d="M6 6l12 12M18 6L6 18"/></Icon>;
const IconCheck = (p) => <Icon {...p}><path d="M5 12l5 5 9-11"/></Icon>;
const IconPlus = (p) => <Icon {...p}><path d="M12 5v14M5 12h14"/></Icon>;
const IconMinus = (p) => <Icon {...p}><path d="M5 12h14"/></Icon>;
const IconUndo = (p) => <Icon {...p}><path d="M3 8h11a6 6 0 010 12H10"/><path d="M7 4L3 8l4 4"/></Icon>;
const IconRedo = (p) => <Icon {...p}><path d="M21 8H10a6 6 0 000 12h4"/><path d="M17 4l4 4-4 4"/></Icon>;
const IconSettings = (p) => <Icon {...p}><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 00-.1-1.2l2-1.5-2-3.5-2.4.9a7 7 0 00-2-1.2L14 3h-4l-.5 2.5a7 7 0 00-2 1.2l-2.4-.9-2 3.5 2 1.5A7 7 0 005 12c0 .4 0 .8.1 1.2l-2 1.5 2 3.5 2.4-.9a7 7 0 002 1.2L10 21h4l.5-2.5a7 7 0 002-1.2l2.4.9 2-3.5-2-1.5c.1-.4.1-.8.1-1.2z"/></Icon>;
const IconInfo = (p) => <Icon {...p}><circle cx="12" cy="12" r="9"/><path d="M12 8h.01M12 12v5"/></Icon>;
const IconSun = (p) => <Icon {...p}><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.5 1.5M17.5 17.5L19 19M5 19l1.5-1.5M17.5 6.5L19 5"/></Icon>;
const IconGlobe = (p) => <Icon {...p}><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 010 18M12 3a14 14 0 000 18"/></Icon>;
const IconImport = (p) => <Icon {...p}><path d="M12 3v12M7 10l5 5 5-5"/><path d="M5 21h14"/></Icon>;
const IconImage = (p) => <Icon {...p}><rect x="3" y="5" width="18" height="14" rx="1"/><circle cx="9" cy="10" r="1.5"/><path d="M21 17l-5-5-9 9"/></Icon>;

// Logo / Marque — diamant orné inspiré de l'antiquité (original)
const Logo = ({ size = 28, color = "currentColor" }) => (
  <svg width={size} height={size} viewBox="0 0 32 32" fill="none" stroke={color} strokeWidth="1.4">
    <path d="M16 3 L29 16 L16 29 L3 16 Z"/>
    <path d="M16 8 L24 16 L16 24 L8 16 Z" opacity="0.55"/>
    <circle cx="16" cy="16" r="2" fill={color} stroke="none"/>
  </svg>
);

// Couronne de laurier ornementale (originale, simplifiée)
const Laurel = ({ size = 80, color = "#c8a96a" }) => (
  <svg width={size} height={size} viewBox="0 0 100 100" fill="none">
    <circle cx="50" cy="50" r="36" stroke={color} strokeWidth="0.8" opacity="0.5"/>
    {[...Array(10)].map((_, i) => {
      const a = (Math.PI * (180 - i * 14)) / 180;
      const x1 = 50 + Math.cos(a) * 28;
      const y1 = 50 + Math.sin(a) * 28;
      const x2 = 50 + Math.cos(a) * 38;
      const y2 = 50 + Math.sin(a) * 38;
      return <ellipse key={`l${i}`} cx={(x1+x2)/2} cy={(y1+y2)/2}
                rx="6" ry="2.2" fill={color} opacity="0.85"
                transform={`rotate(${(a*180/Math.PI)+90} ${(x1+x2)/2} ${(y1+y2)/2})`}/>;
    })}
    {[...Array(10)].map((_, i) => {
      const a = (Math.PI * (i * 14)) / 180;
      const x1 = 50 + Math.cos(a) * 28;
      const y1 = 50 + Math.sin(a) * 28;
      const x2 = 50 + Math.cos(a) * 38;
      const y2 = 50 + Math.sin(a) * 38;
      return <ellipse key={`r${i}`} cx={(x1+x2)/2} cy={(y1+y2)/2}
                rx="6" ry="2.2" fill={color} opacity="0.85"
                transform={`rotate(${(a*180/Math.PI)+90} ${(x1+x2)/2} ${(y1+y2)/2})`}/>;
    })}
    <path d={`M${50 - 38} 50 Q50 ${50 - 6} ${50 + 38} 50`} stroke={color} strokeWidth="1" fill="none"/>
  </svg>
);

// Ornement à losange
const DiamondOrnament = ({ size = 14, color = "#c8a96a" }) => (
  <svg width={size} height={size} viewBox="0 0 14 14" fill="none">
    <path d="M7 1 L13 7 L7 13 L1 7 Z" stroke={color} strokeWidth="1"/>
    <circle cx="7" cy="7" r="1.2" fill={color}/>
  </svg>
);

Object.assign(window, {
  Icon, IconBrush, IconEraser, IconHand, IconIsland, IconGrid, IconLayers,
  IconUsers, IconSave, IconExport, IconMountain, IconForest, IconWater,
  IconResource, IconBuilding, IconChevron, IconClose, IconCheck, IconPlus,
  IconMinus, IconUndo, IconRedo, IconSettings, IconInfo, IconSun, IconGlobe,
  IconImport, IconImage,
  Logo, Laurel, DiamondOrnament
});
