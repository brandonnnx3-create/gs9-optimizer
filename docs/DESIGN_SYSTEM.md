# G.S.9 — Design System

**BLACK · WHITE · RAW · TECHNICAL · MINIMAL**

A technical instrument built by G.S.9 that happens to be aimed at gaming. It must never look like a "gaming app" or a SaaS dashboard.

The XAML source of truth lives in `src/ConnectionOptimizer/Themes/`:

| File | Contents |
|---|---|
| `Tokens.xaml` | Colors, brushes, fonts, type sizes, spacing |
| `Typography.xaml` | Text styles (`Text.*`) |
| `Controls.xaml` | Buttons, cells, scroll bars, tooltips, log box, converters |

`docs/design-reference.png` is the visual reference: use it for tone and hierarchy, not as a layout to copy.

---

## 1. Principles

1. **Black and white first.** White is the main color. Grays are only for secondary information.
2. **Structure through lines, not surfaces.** 1 px lines, grids and dividers build the panel. Nothing floats.
3. **Square.** Corner radius is 0 everywhere.
4. **Heavy type, big numbers.** States and key figures should read from across the room.
5. **Every state is readable without color.** Glyph + word first; color only confirms.
6. **Say only what is known.** "APPLIED · unverified" is correct. "OPTIMIZED" without proof is not.

**Never:** gradients, glassmorphism, blur, neumorphism, drop shadows, rounded cards, pill buttons, RGB or neon accents, generic tech blue, decorative icons.

---

## 2. Color

| Token | Hex | Use |
|---|---|---|
| `Color.Background` | `#050505` | Window background. Everything sits on it. |
| `Color.Raised` | `#0E0E0E` | Hover fill of a cell, disabled primary button. Never a card background. |
| `Color.Line` | `#242424` | Every divider and border. |
| `Color.LineStrong` | `#3A3A3A` | Scroll thumbs, tooltip border. |
| `Color.Text` | `#FFFFFF` | Main text, primary button, values. |
| `Color.TextSecondary` | `#A0A0A0` | Labels, descriptions, captions. |
| `Color.TextMuted` | `#6E6E6E` | Indexes (01, 02), timestamps, footer. Nothing essential. |
| `Color.Ok` | `#00FF88` | Glyph of APPLIED / ACTIVE / COMPLETED / CONNECTED only. |
| `Color.Error` | `#FF3B3B` | ERROR glyph and text, alert bar. |

Green and red are accents, never fills: no green buttons, no red backgrounds.

---

## 3. Typography

| Style | Font | Size | Use |
|---|---|---|---|
| `Text.Hero` | Arial Black | 60 | The `G.S.9` mark. Once per screen. |
| `Text.Display` | Arial Black | 30 | SYSTEM STATUS value, dialog titles (26). |
| `Text.Figure` | Arial Black | 20 | Key technical figures: ETHERNET, 1 GBPS. |
| `Text.Title` | Arial Black | 19 | Tool names. |
| `Text.Section` | Arial Black | 13 | Section titles: NETWORK, CLEANUP, ACTIVITY. |
| `Text.Label` | Segoe UI SemiBold | 11 | Uppercase label above a value. |
| `Text.Body` | Segoe UI | 13 | Descriptions and explanations. |
| `Text.Value` | Segoe UI SemiBold | 14 | Plain value next to a label. |
| `Text.Mono` / `Text.MonoValue` | Cascadia Mono → Consolas | 12 / 16 | Exit codes, times, IPs, GUIDs, script output. |

Rules:

- **UPPERCASE** for labels, section titles, tool names, states and buttons. Sentence case for explanations.
- **Tracking:** WPF has no letter-spacing, so labels use `{m:Tracked 'TEXT'}` or the `Tracking` converter, which inserts thin spaces. Use it on labels, section titles and buttons. Do not use it on body text or values.
- All fonts ship with Windows. Nothing is downloaded or embedded.

---

## 4. Space and layout

- Spacing scale: **4 · 8 · 12 · 16 · 24 · 32 · 40**.
- Page margin: **40** left and right.
- Cell padding: **24 / 20** (`Space.Cell`). Table cell padding: **20 / 14** (`Space.TableCell`).
- Sections are separated by **28–32** of empty space plus a title. Do not add boxes around sections.
- **Grid construction:** the container draws the left and top line (`1,1,0,0`) and each cell draws its right and bottom line (`0,0,1,1`). This produces single 1 px lines and never doubles them.
- Leave negative space. If a screen feels empty, that is usually correct.

Main screen hierarchy:

1. Header: `G.S.9` / CONNECTION OPTIMIZER · SYSTEM STATUS · PRIVILEGES
2. CONNECTION: technical readout table
3. Alert strip (only when something failed)
4. Tools grid (NETWORK, CLEANUP) | ACTIVITY column
5. ACTIVATE ALL: the only primary button, always visible
6. Footer: version and scripts path

---

## 5. States

| State | Glyph | Word | Color |
|---|---|---|---|
| Ready | ● | READY | white |
| Running / Optimizing | ● (pulsing) | RUNNING… / OPTIMIZING | white |
| Checking | ● (pulsing) | CHECKING… | gray |
| Applied (exit 0, not verifiable) | ✓ | APPLIED | green glyph |
| Completed | ● | COMPLETED | green glyph |
| Active (verified) | ● | ACTIVE | green glyph |
| Partial (verified) | ◐ | PARTIAL | white |
| Inactive (verified) | ○ | INACTIVE | gray |
| Error | ✕ | ERROR / MISSING / CHECK FAILED | red |

Implemented by `Views/StatusBadge`. A new state needs a new glyph and a new word, not just a new color.

Under every state there is a **mono caption** with the facts behind it, for example `exit 0 · 14s · 12:43 · unverified` or `10/10 values · checked 12:43`.

---

## 6. Components

**Buttons**

| Style | Look | Use |
|---|---|---|
| `Button.Primary` | Solid white block, black Arial Black text, 72 px | ACTIVATE ALL only. |
| `Button.PrimaryCompact` | Same look, 44 px | Confirm button in dialogs. |
| `Button.Secondary` | 1 px white outline, inverts to white on hover | ACTIVATE, CHECK STATUS, VIEW LOG in alerts. |
| `Button.Ghost` | Gray text, white on hover | REFRESH, VIEW LOG in cells, OPEN LOGS FOLDER. |

Disabled buttons turn to line-gray outlines. Keyboard focus is a dashed white frame.

**Tool cell** (`Views/OptimizationCell`): index + category label, then name (Title), description (Body), empty space, status badge, mono caption, live output line while running, then actions.

**Technical table** (CONNECTION, log header): cells divided by 1 px lines, with a label on top and a Figure or Mono value below.

**Alert strip:** a 4 px red bar on the left, `✕ OPTIMIZATION FAILED` plus the subject, a readable sentence, the technical detail in mono, then VIEW LOG and DISMISS. Never a modal pop-up for an error.

**Activity:** mono timestamp · glyph · message. Info messages in gray, results in white, errors in red.

**Dialogs:** black window with a native dark title bar. Contents top to bottom: label, big title, message, optional list, "WHAT WILL HAPPEN", optional boxed note, then CANCEL (secondary) and the confirm button (primary).

**Motion:** only a pulse on running states. No transitions, slides or fades.

---

## 7. Checklist for a new screen

- [ ] Only colors and fonts from `Tokens.xaml`
- [ ] No radius, shadow, gradient or blur
- [ ] Structure made with 1 px `Brush.Line` lines, not filled boxes
- [ ] Labels in uppercase with tracking; values large
- [ ] At most one `Button.Primary`
- [ ] Every state has a glyph and a word
- [ ] Nothing is claimed that the app did not verify
