// Charter.jsx — Page de charte graphique (artboard 1)
// Documente couleurs, typo, ornements et composants UI.

const Swatch = ({ name, value, varName, light }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
    <div style={{
      width: '100%', height: 72,
      background: value,
      borderRadius: 4,
      border: '1px solid var(--color-border)',
      boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.04)'
    }} />
    <div style={{ fontSize: 11, color: light ? 'var(--color-text-on-light)' : 'var(--color-text)' }}>
      <div style={{ fontWeight: 600, letterSpacing: '0.02em' }}>{name}</div>
      <div style={{ color: 'var(--color-text-mute)', fontFamily: 'var(--font-mono)', fontSize: 10 }}>{value}</div>
      {varName && <div style={{ color: 'var(--color-gold-700)', fontFamily: 'var(--font-mono)', fontSize: 10 }}>{varName}</div>}
    </div>
  </div>
);

const Section = ({ eyebrow, title, children }) => (
  <section style={{ marginBottom: 56 }}>
    <div className="t-eyebrow" style={{ marginBottom: 8 }}>{eyebrow}</div>
    <h2 className="t-display" style={{ fontSize: 'var(--fs-2xl)', margin: '0 0 24px', color: 'var(--color-gold-300)' }}>{title}</h2>
    {children}
  </section>
);

const ComponentRow = ({ label, children }) => (
  <div style={{
    display: 'grid', gridTemplateColumns: '160px 1fr',
    gap: 24, alignItems: 'center',
    padding: '20px 0',
    borderBottom: '1px solid var(--color-border)'
  }}>
    <div style={{ color: 'var(--color-gold-500)', fontFamily: 'var(--font-mono)', fontSize: 11, letterSpacing: '0.05em' }}>{label}</div>
    <div style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>{children}</div>
  </div>
);

function Charter() {
  const [activeTab, setActiveTab] = React.useState('display');
  const [arrowVal, setArrowVal] = React.useState('Oui');
  const [toggle, setToggle] = React.useState('on');
  const [showModal, setShowModal] = React.useState(false);
  const [select, setSelect] = React.useState('Rectangle');
  const [slider, setSlider] = React.useState(50);
  const [tool, setTool] = React.useState('brush');

  return (
    <div className="tex-marble" style={{ minHeight: '100%', padding: '64px 80px', position: 'relative', overflow: 'hidden' }}>
      {/* Vignette */}
      <div style={{
        position: 'absolute', inset: 0, pointerEvents: 'none',
        background: 'radial-gradient(ellipse at center, transparent 50%, rgba(13,22,38,0.45) 100%)'
      }} />

      <div style={{ position: 'relative', maxWidth: 1100, margin: '0 auto' }}>
        {/* ── Cover ── */}
        <div style={{ textAlign: 'center', marginBottom: 80, paddingTop: 24 }}>
          <Laurel size={120} />
          <div className="t-eyebrow" style={{ marginTop: 8 }}>Charte graphique · v1.0</div>
          <h1 className="t-display" style={{ fontSize: 'var(--fs-3xl)', color: 'var(--color-gold-300)', margin: '12px 0 8px' }}>
            ANNO MOD CREATOR
          </h1>
          <div className="divider-ornate" style={{ maxWidth: 360, margin: '12px auto 16px' }}>
            <DiamondOrnament size={10} />
          </div>
          <p className="t-dim" style={{ fontSize: 'var(--fs-base)', maxWidth: 520, margin: '0 auto', lineHeight: 1.6 }}>
            Système visuel pour l'éditeur de carte. Inspiration antique romaine, palette nocturne,
            ornements géométriques mesurés.
          </p>
        </div>

        {/* ── Couleurs ── */}
        <Section eyebrow="01 — Palette" title="Couleurs">
          <div style={{ marginBottom: 12, color: 'var(--color-gold-500)', fontSize: 13, letterSpacing: '0.05em' }}>Surfaces sombres — chrome principal</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 12, marginBottom: 32 }}>
            <Swatch name="Ink 950" value="#0d1626" varName="--color-ink-950" />
            <Swatch name="Ink 900" value="#14213a" varName="--color-ink-900" />
            <Swatch name="Ink 800" value="#1a2840" varName="--color-ink-800" />
            <Swatch name="Ink 700" value="#243454" varName="--color-ink-700" />
            <Swatch name="Ink 600" value="#324466" varName="--color-ink-600" />
            <Swatch name="Ink 500" value="#4a5e82" varName="--color-ink-500" />
          </div>

          <div style={{ marginBottom: 12, color: 'var(--color-gold-500)', fontSize: 13, letterSpacing: '0.05em' }}>Bordeaux — accent primaire</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 32 }}>
            <Swatch name="Wine 900" value="#4a1620" varName="--color-wine-900" />
            <Swatch name="Wine 800" value="#5e1f2c" varName="--color-wine-800" />
            <Swatch name="Wine 700" value="#7a2838" varName="--color-wine-700" />
            <Swatch name="Wine 600" value="#963747" varName="--color-wine-600" />
            <Swatch name="Wine 500" value="#b85365" varName="--color-wine-500" />
          </div>

          <div style={{ marginBottom: 12, color: 'var(--color-gold-500)', fontSize: 13, letterSpacing: '0.05em' }}>Or — filets, bordures, texte fin</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 32 }}>
            <Swatch name="Gold 700" value="#8a7240" varName="--color-gold-700" />
            <Swatch name="Gold 600" value="#a88a52" varName="--color-gold-600" />
            <Swatch name="Gold 500" value="#c8a96a" varName="--color-gold-500" />
            <Swatch name="Gold 400" value="#d8bf83" varName="--color-gold-400" />
            <Swatch name="Gold 300" value="#e8d6a8" varName="--color-gold-300" />
          </div>

          <div style={{ marginBottom: 12, color: 'var(--color-gold-500)', fontSize: 13, letterSpacing: '0.05em' }}>Pierre — surfaces claires (zone carte)</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12, marginBottom: 32 }}>
            <Swatch name="Stone 100" value="#f0e8d8" varName="--color-stone-100" light />
            <Swatch name="Stone 200" value="#e0d4be" varName="--color-stone-200" light />
            <Swatch name="Stone 300" value="#c8b89c" varName="--color-stone-300" light />
            <Swatch name="Stone 400" value="#a89878" varName="--color-stone-400" />
            <Swatch name="Stone 500" value="#877657" varName="--color-stone-500" />
          </div>

          <div style={{ marginBottom: 12, color: 'var(--color-gold-500)', fontSize: 13, letterSpacing: '0.05em' }}>Couleurs joueurs (zones territoriales)</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(8, 1fr)', gap: 12 }}>
            {[
              ['#b94a4a', 'Rouge'], ['#3e6cb5', 'Bleu'],
              ['#4d8c4a', 'Vert'], ['#d4a93b', 'Jaune'],
              ['#8a4ca8', 'Violet'], ['#d68a3c', 'Orange'],
              ['#2a2a2a', 'Noir'], ['#d8d4c8', 'Blanc'],
            ].map(([c, n]) => (
              <div key={n} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                <div style={{
                  width: 56, height: 56, borderRadius: '50%',
                  background: c,
                  border: '2px solid var(--color-gold-700)',
                  boxShadow: 'inset 0 2px 4px rgba(0,0,0,0.4), 0 2px 6px rgba(0,0,0,0.5)'
                }} />
                <div style={{ fontSize: 11, color: 'var(--color-text-dim)' }}>{n}</div>
              </div>
            ))}
          </div>
        </Section>

        {/* ── Typographie ── */}
        <Section eyebrow="02 — Typographie" title="Type System">
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 32, marginBottom: 32 }}>
            <div style={{ padding: 20, background: 'var(--color-ink-800)', border: '1px solid var(--color-border)', borderRadius: 4 }}>
              <div className="t-eyebrow" style={{ marginBottom: 8 }}>Display · Cinzel</div>
              <div style={{ fontFamily: 'var(--font-display)', fontSize: 56, fontWeight: 500, color: 'var(--color-gold-300)', letterSpacing: '0.06em', lineHeight: 1.1 }}>Aa Ωβ</div>
              <div style={{ fontFamily: 'var(--font-display)', fontSize: 13, color: 'var(--color-text-dim)', letterSpacing: '0.1em', marginTop: 12, textTransform: 'uppercase' }}>
                ABCDEFGHIJKLMNOPQRSTUVWXYZ<br/>0123456789
              </div>
              <div style={{ marginTop: 16, fontSize: 11, color: 'var(--color-text-mute)', fontFamily: 'var(--font-mono)' }}>
                Titres · Boutons primaires · Section headers
              </div>
            </div>
            <div style={{ padding: 20, background: 'var(--color-ink-800)', border: '1px solid var(--color-border)', borderRadius: 4 }}>
              <div className="t-eyebrow" style={{ marginBottom: 8 }}>UI · Inter</div>
              <div style={{ fontFamily: 'var(--font-body)', fontSize: 56, fontWeight: 500, color: 'var(--color-text)' }}>Aa Ωβ</div>
              <div style={{ fontFamily: 'var(--font-body)', fontSize: 13, color: 'var(--color-text-dim)', marginTop: 12 }}>
                ABCDEFGHIJKLMNOPQRSTUVWXYZ<br/>
                abcdefghijklmnopqrstuvwxyz<br/>
                0123456789
              </div>
              <div style={{ marginTop: 16, fontSize: 11, color: 'var(--color-text-mute)', fontFamily: 'var(--font-mono)' }}>
                Corps · Inputs · Labels · Tooltips
              </div>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '180px 1fr', gap: 24 }}>
            {[
              ['Display 3xl · 52', 'fontFamily:var(--font-display)', 52, "Quod scimus est gutta"],
              ['Display 2xl · 38', 'fontFamily:var(--font-display)', 38, "Concevoir un monde"],
              ['Display lg · 21', 'fontFamily:var(--font-display)', 21, "Un titre de section"],
              ['Body md · 17', 'fontFamily:var(--font-body)', 17, "Un paragraphe d'introduction qui décrit le contexte."],
              ['Body sm · 13', 'fontFamily:var(--font-body)', 13, "Texte d'interface, dense, lisible à grande échelle."],
              ['Body xs · 11', 'fontFamily:var(--font-body)', 11, "Légendes, métadonnées, timestamps."],
              ['Eyebrow xxs · 10', 'eyebrow', 10, "PARAMÈTRES · 01 — INTRODUCTION"],
            ].map(([label, kind, size, text], i) => (
              <React.Fragment key={i}>
                <div style={{ color: 'var(--color-gold-500)', fontFamily: 'var(--font-mono)', fontSize: 11, alignSelf: 'center' }}>{label}</div>
                <div style={kind === 'eyebrow' ? {
                  fontSize: size, letterSpacing: '0.18em', textTransform: 'uppercase',
                  color: 'var(--color-gold-500)', fontWeight: 500
                } : {
                  fontFamily: kind.includes('display') ? 'var(--font-display)' : 'var(--font-body)',
                  fontSize: size,
                  color: 'var(--color-text)',
                  letterSpacing: kind.includes('display') ? '0.04em' : '0',
                  lineHeight: 1.3,
                }}>{text}</div>
              </React.Fragment>
            ))}
          </div>
        </Section>

        {/* ── Ornements ── */}
        <Section eyebrow="03 — Ornements" title="Motifs & Marques">
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 24 }}>
            <div style={{ padding: 24, background: 'var(--color-ink-800)', border: '1px solid var(--color-border)', borderRadius: 4, textAlign: 'center' }}>
              <Logo size={48} color="var(--color-gold-500)" />
              <div className="t-eyebrow" style={{ marginTop: 12 }}>Marque</div>
              <div style={{ fontSize: 11, color: 'var(--color-text-mute)', marginTop: 4 }}>Diamant inscrit</div>
            </div>
            <div style={{ padding: 24, background: 'var(--color-ink-800)', border: '1px solid var(--color-border)', borderRadius: 4, textAlign: 'center' }}>
              <Laurel size={64} />
              <div className="t-eyebrow" style={{ marginTop: 12 }}>Couronne</div>
              <div style={{ fontSize: 11, color: 'var(--color-text-mute)', marginTop: 4 }}>Marqueur d'achèvement</div>
            </div>
            <div style={{ padding: 24, background: 'var(--color-ink-800)', border: '1px solid var(--color-border)', borderRadius: 4, textAlign: 'center' }}>
              <div style={{ display: 'flex', justifyContent: 'center', gap: 8, padding: '20px 0' }}>
                <DiamondOrnament size={14} />
                <DiamondOrnament size={14} />
                <DiamondOrnament size={14} />
              </div>
              <div className="t-eyebrow">Losanges</div>
              <div style={{ fontSize: 11, color: 'var(--color-text-mute)', marginTop: 4 }}>Séparateurs</div>
            </div>
            <div style={{ padding: 24, background: 'var(--color-ink-800)', border: '1px solid var(--color-border)', borderRadius: 4, textAlign: 'center' }}>
              <div className="meander" style={{ marginTop: 22, marginBottom: 22 }} />
              <div className="t-eyebrow">Méandre</div>
              <div style={{ fontSize: 11, color: 'var(--color-text-mute)', marginTop: 4 }}>Bordure répétitive</div>
            </div>
          </div>

          <div style={{ marginTop: 24 }}>
            <div className="divider-ornate"><DiamondOrnament size={10} /></div>
            <div style={{ height: 16 }} />
            <div className="meander" />
          </div>
        </Section>

        {/* ── Composants ── */}
        <Section eyebrow="04 — Composants" title="UI Kit">
          <ComponentRow label="Boutons">
            <button className="btn btn-primary">Sauvegarder</button>
            <button className="btn btn-secondary">Annuler</button>
            <button className="btn btn-ghost">En savoir plus</button>
            <button className="btn btn-primary" disabled>Désactivé</button>
          </ComponentRow>

          <ComponentRow label="Tailles">
            <button className="btn btn-primary btn-sm">Petit</button>
            <button className="btn btn-primary">Standard</button>
            <button className="btn btn-primary btn-lg">Grand</button>
          </ComponentRow>

          <ComponentRow label="Boutons ronds">
            <button className={`btn-round ${tool === 'brush' ? 'is-active' : ''}`} onClick={() => setTool('brush')}><IconBrush /></button>
            <button className={`btn-round ${tool === 'eraser' ? 'is-active' : ''}`} onClick={() => setTool('eraser')}><IconEraser /></button>
            <button className={`btn-round ${tool === 'hand' ? 'is-active' : ''}`} onClick={() => setTool('hand')}><IconHand /></button>
            <button className={`btn-round ${tool === 'island' ? 'is-active' : ''}`} onClick={() => setTool('island')}><IconIsland /></button>
            <button className={`btn-round ${tool === 'layers' ? 'is-active' : ''}`} onClick={() => setTool('layers')}><IconLayers /></button>
          </ComponentRow>



          <ComponentRow label="Onglets">
            <div className="tabs" style={{ width: '100%' }}>
              {[
                ['display', 'Affichage', IconSun],
                ['terrain', 'Terrain', IconMountain],
                ['players', 'Joueurs', IconUsers],
                ['settings', 'Paramètres', IconSettings],
              ].map(([id, label, IC]) => (
                <button key={id} className={`tab ${activeTab === id ? 'is-active' : ''}`} onClick={() => setActiveTab(id)}>
                  <IC size={14} />{label}
                </button>
              ))}
            </div>
          </ComponentRow>

          <ComponentRow label="Toggle Oui/Non">
            <div className="arrow-toggle">
              <button onClick={() => setArrowVal(arrowVal === 'Oui' ? 'Non' : 'Oui')}>‹</button>
              <div className="arrow-toggle-value">{arrowVal}</div>
              <button onClick={() => setArrowVal(arrowVal === 'Oui' ? 'Non' : 'Oui')}>›</button>
            </div>
            <div className="toggle">
              <button className={toggle === 'on' ? 'is-active' : ''} onClick={() => setToggle('on')}>Activé</button>
              <button className={toggle === 'off' ? 'is-active' : ''} onClick={() => setToggle('off')}>Désactivé</button>
            </div>
          </ComponentRow>

          <ComponentRow label="Select">
            <div style={{ width: 240 }}>
              <div className="select-wrap">
                <select className="select" value={select} onChange={e => setSelect(e.target.value)}>
                  <option>Rectangle</option>
                  <option>Losange</option>
                  <option>Cercle</option>
                </select>
              </div>
            </div>
            <input className="input" placeholder="Saisir un nom de carte…" style={{ width: 240 }} />
          </ComponentRow>

          <ComponentRow label="Slider">
            <div className="slider" style={{ width: 320 }}>
              <div className="slider-frame">
                <input type="range" min="0" max="100" value={slider} onChange={e => setSlider(+e.target.value)} />
              </div>
            </div>
            <span className="t-mute" style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>{slider}</span>
          </ComponentRow>

          <ComponentRow label="Modale">
            <button className="btn btn-secondary" onClick={() => setShowModal(true)}>Ouvrir une modale</button>
            <span className="t-mute" style={{ fontSize: 12 }}>Cadre orné, titre centré, bordures dorées</span>
          </ComponentRow>

          <ComponentRow label="Tooltip">
            <div style={{ position: 'relative', display: 'inline-block', padding: '0 4px' }}>
              <div className="tooltip" style={{ bottom: '120%', left: '50%', transform: 'translateX(-50%)' }}>
                Texture de terrain
              </div>
              <button className="btn-round"><IconInfo /></button>
            </div>
          </ComponentRow>

          <ComponentRow label="Card / Fiche">
            <div className="card" style={{ width: 280 }}>
              <div className="card-header">
                <div className="t-eyebrow">Île préfabriquée</div>
                <h3 className="card-title">Insula Magna</h3>
              </div>
              <div className="card-body">
                <div style={{ fontSize: 12, color: 'var(--color-text-dim)', lineHeight: 1.6, marginBottom: 12 }}>
                  Île de taille moyenne, biome méditerranéen, 4 emplacements de ressources.
                </div>
                <div style={{ display: 'flex', gap: 8 }}>
                  <button className="btn btn-primary btn-sm">Placer</button>
                  <button className="btn btn-ghost btn-sm">Détails</button>
                </div>
              </div>
            </div>
          </ComponentRow>
        </Section>

        {/* ── Modale demo ── */}
        {showModal && (
          <div className="modal-backdrop" style={{ position: 'fixed' }} onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <button className="modal-close" onClick={() => setShowModal(false)}><IconClose size={14} /></button>
              <div className="modal-header">
                <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 8 }}>
                  <Laurel size={40} />
                </div>
                <div className="t-eyebrow" style={{ marginBottom: 6 }}>Confirmation</div>
                <h2 className="modal-title">Quitter sans sauvegarder ?</h2>
              </div>
              <div className="modal-body" style={{ textAlign: 'center', color: 'var(--color-text-dim)' }}>
                Vos modifications seront perdues. Vous pouvez aussi sauvegarder
                votre travail puis quitter.
              </div>
              <div className="modal-footer">
                <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Rester</button>
                <button className="btn btn-primary" onClick={() => setShowModal(false)}>Quitter</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

window.Charter = Charter;
