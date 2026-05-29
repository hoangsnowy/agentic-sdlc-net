# Before / after — UI redesign `feat/ui-enterprise-os`

This file describes the three main surfaces against the seven C-commits on the branch. Live screenshots were skipped — the in-Claude preview screenshot tool timed out repeatedly on this preview server, and per-element verification via `preview_eval` / `preview_snapshot` was used instead. To reproduce visually, run `dotnet run --project src/AgenticSdlc.Web` and visit:

- `http://localhost:5180/` — Desktop
- `http://localhost:5180/_/components` — components catalog
- click **Pipeline** to open the Pipeline window
- click **Workflow** to open the Workflow window

The Appearance tab (System app → Appearance) drives `data-theme` × `data-wallpaper` × `--glass-blur` live.

---

## 1. Desktop

| Aspect | Before | After |
| --- | --- | --- |
| Wallpaper | Purple/cyan radial mesh (`bg-drift` 30 s animation), violet `#0c0a1f`→`#181b3a` base, always-dark text. | Per `data-wallpaper`: `enterprise-light` (default) is a neutral slate gradient with `--text-primary` dark text; `aurora` keeps the original mesh + animation; `midnight` / `sunset` are quieter two-radial gradients. `--desktop-text` / `--desktop-text-shadow` flip white + drop-shadow on dark wallpapers automatically. |
| Tile icon | Emoji (🚀 🕸 ⚙ 🛠 🚀 🔨 ↗) on a heavy `linear-gradient(135deg, blue, violet)` blob with a 6 px ambient shadow + 14 px radius. | `<Icon Name="…" Size="28" />` (Phosphor-flavoured Lucide SVG) on a flat `var(--bg-2)` tile, 1 px `--border-subtle`, `--radius-md` (6 px), `--shadow-2`. Hover: tile background → `--accent-soft`, border → `--accent`. |
| Clock | Position absolute top-right `#fff` with `0 2px 6px rgba(0,0,0,.60)` shadow. | Same position, but `color: var(--desktop-text)`, `text-shadow: var(--desktop-text-shadow)` → token-driven. |
| Group labels (`APPS`, `QUICK ACTIONS`) | White 75 % opacity. | `var(--desktop-text-secondary)`. |
| Right-click context menu | Emoji glyphs (⟳ 🛠 ⚙ 🔔 ▶). | `<Icon Name="arrow-clockwise/wrench/gear/bell/play" />`. |
| Cascading Start menu | Lived in `AppShellLayout.razor:23-76` — four nested categories (Apps / Tools / Help / Session) duplicating the Desktop tiles + the TopBar user menu. | Deleted. Desktop tiles + Taskbar dock + TopBar dropdowns cover every action. The Taskbar Start button still toggles `_startOpen` as a hook for a future Krunner-style search overlay. |

## 2. Pipeline window (Run mode)

| Aspect | Before | After |
| --- | --- | --- |
| Window chrome | 34 px titlebar with `linear-gradient(180deg, #4a8cff, #2563eb)` blue gradient + white text + 1 px `text-shadow`, action buttons inset-glass white, 10 px radius, 50 px / 100 px monster shadow, cubic-bezier(.34, 1.56, .64, 1) overshoot animation, single bottom-right `⤡` resize glyph. | KDE Plasma 6 Breeze: 30 px flat `--bg-titlebar` titlebar with `--text-secondary` text, **focused** window's titlebar bottom-border swaps to `--accent` (the Breeze "underscore the active window" gesture), 6 px radius, `--shadow-window-focused` (2 layers + 1 px `--accent` ring), standard easing. 8 invisible resize zones (4 edges 6 px thick + 4 corners 14 px). Action buttons 28×22, transparent default, hover `--bg-3`, close hover `--err`, focus-visible `--focus-ring`. ARIA `role="dialog" aria-labelledby="appwin-title-<guid>"`, action buttons `aria-label`-ed. |
| Title icon | `@W.Icon` rendered as the literal emoji `▶` stored in `AppWindowState.Icon`. | `<Icon Name="@W.Icon" Size="14" />`; `AppWindowState.Icon` now stores the Phosphor `Name` `"play"`. |
| Inner toolbar (Pipeline's own) | Same — Pipeline already had an inner Run/Cancel/NMax/provider chip toolbar (`.appins`); unchanged. | Same, but reading tokens so the chip, run button, etc., pick up `--accent` automatically. |
| Buttons (Run / Cancel) | `linear-gradient(180deg, #4a8cff, #2563eb)` + white text + text-shadow + 1 px `#1d4ed8` border. | Flat `var(--accent)` background, no gradient, no text-shadow, no bevel; hover → `var(--accent-hover)`; active → `var(--accent-active)`; focus → `var(--focus-ring)`. |

## 3. Workflow window (canvas)

| Aspect | Before | After |
| --- | --- | --- |
| Window chrome | Same blue gradient titlebar as Pipeline. | Same Plasma Breeze chrome as Pipeline (above). |
| Inner studio (`.syn-root`) toolbar | Mix of emoji (🌙 ☀ ⚡ ▶ 🗑 💾) and unicode (◆ for the Agent node category). | Unchanged in this branch — **OrchestrationStudio emoji swap is the one deferred bit of C5.** The inner canvas vocabulary needs its own pass because the studio has node-category glyphs (Agent / Tool / Decision) that benefit from a richer icon palette and a node-status colour ramp. Tracked separately. |
| Canvas background | `var(--bg-3)` for the diagram area. | Unchanged. |
| Status bar (live provider, "Anthropic", "Stateless" indicators) | `rgba(255,255,255,…)` chips on the blue titlebar gradient. | Now reads `var(--text-on-accent)` on a flat `var(--accent)` chip — same colour role, far cleaner contrast. |

---

## Token swatches (call out the most-visible flips)

| What | Before | After |
| --- | --- | --- |
| Primary accent | `#2563eb` (blue-600) | `#3daee9` (KDE Breeze blue) |
| Brand secondary | `#7c3aed` (violet-600) | Removed. `--accent-2` is an alias of `--accent`; the dual-accent gradient brand mark goes away. |
| Window radius | `10 px` | `6 px` (`--radius-md`) |
| Titlebar height | `34 px` | `30 px` |
| Window-open animation | `cubic-bezier(.34, 1.56, .64, 1) .22s` (overshoot) | `cubic-bezier(.2, 0, 0, 1) 160ms` (`--ease-standard` / `--duration-base`) |
| Default font-size body | `13 px` literal | `var(--fs-base)` (still 13 px, now tokenised) |
| Btn primary background | `linear-gradient(180deg, #4a8cff, #2563eb)` | `var(--accent)` (flat) |

## Verifying any theme x wallpaper x glass combo

1. `dotnet run --project src/AgenticSdlc.Web`
2. Visit `http://localhost:5180/_/components` — every primitive lives there.
3. Use the seven swatch buttons at the top of the catalog to flip `data-theme` / `data-wallpaper` without leaving the page.
4. Or open System → Appearance and drag **Glass strength** (0..100 %) — every glass surface (`.appwin`, `.topbar`, `.dock`, `.toast`, `.dropdown`, `.ctxmenu`, `.dialog`, `.login-card`, `.start-menu`, `.menubar`, `.shell`, `.shell-sidebar`) responds because they all read `var(--glass-blur)` / `var(--glass-saturate)`.
