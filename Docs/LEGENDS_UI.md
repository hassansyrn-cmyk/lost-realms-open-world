# Legends UI — Genshin / Wuthering Waves / Zelda Overhaul

The UI combines a clean, modern open-world layout (**Genshin Impact** / **Wuthering Waves** gameplay HUD and tabbed modal cards + **Zelda: Breath of the Wild / Tears of the Kingdom** minimal world UI and dual-ring stamina gauge) while preserving the **Legends of the Lost Realms** navy/gold identity, Cinzel / Crimson typography, and illustrated combat icons.

## Key Features

1. **Procedural Modern UI Kit ([`OpenWorldUITheme.cs`](../Assets/Scripts/OpenWorldUITheme.cs)):**
   - Generates anti-aliased sliced rounded cards, pills, outlines, gradients, and signed-distance-field (SDF) vector icons in memory at runtime without modifying any source assets in `Assets/Resources/UI`.
2. **Gameplay HUD ([`OpenWorldMobileHud.cs`](../Assets/Scripts/OpenWorldMobileHud.cs)):**
   - **Top-Left Circular Radar Minimap:** Framed by a golden ring with cardinal markers, a live camera view-cone, directional hero arrow, quest goal diamond, enemy blips, a time/weather readout pill, and a compact circular `TIME` button.
   - **Quest Objective Tracker & Loot Toast Feed:** Left-faded gradient banner with a golden diamond quest badge, distance/bearing indicator, and a 4-slot sliding pickup feed below it that announces gold, gems, keys, relics, bounties, and chest rewards.
   - **Top-Center Compass Ribbon, Discovery Banner & Boss Health Bar:** Minimalist cardinal compass with biome/relics/crypt status, golden-hairline discovery banner, and a wide Boss Health Bar with a delayed white/gold damage trail fill.
   - **Top-Right Resource Pill & Circular Icon Bar:** Displays live Gold & Aether Gems with original icons, a 1-tap Graphics tier pill button, and 6 circular icon buttons (`MAP`, `CHESTS`, `GEAR`, `SKINS`, `TITLE`, `PAUSE`).
   - **Bottom-Center Hero Status Pill:** Circular Aster portrait with an active element badge, level/title/state readout, HP bar with delayed damage trail, Aether bar, and micro stamina bar.
   - **Bottom-Right Radial Combat Cluster:** Ergonomic arc of circular skill buttons with live gauges:
     - `ATTACK` with a radial hold-to-charge ring.
     - `DASH`, `JUMP`, and `PARRY` (pulses with a golden halo when a counterattack is ready).
     - `SPELL` (`25 AE` radial readiness ring + cost pill) and `BURST` (`35 AE` radial energy ring + pulsing elemental glow when ready).
     - `ECHO` (`3.5s` radial cooldown sweep + timer) and `ELEMENT` (live Ember / Frost / Gale icon and color tint).
     - Compact upper `LOCK` (highlights cyan when locked on) and `GRAPPLE` buttons, plus a center-right contextual `F` interaction pill.
3. **Screen-Space World UI & Dual-Ring Stamina Wheel ([`OpenWorldStaminaWheel.cs`](../Assets/Scripts/OpenWorldStaminaWheel.cs), [`OpenWorldCombatFeedback.cs`](../Assets/Scripts/OpenWorldCombatFeedback.cs)):**
   - **Stamina Wheel:** Tracks beside Aster's right shoulder with cardinal ticks, a delayed depletion trail ring, and exhaustion pulse.
   - **Floating Enemy Health Bars & Lock-On Reticle:** Pooled screen-space enemy health bars with smooth damage trails and a rotating golden diamond lock-on reticle.
   - **Screen-Space Combat Popups:** Crisp uGUI damage numbers, critical hit scale-punch, and elemental reaction callouts (`MELT!`, `SWIRL!`, `PARRIED!`, `EXTREME EVASION!`).
4. **Tabbed Glass-Card Menus, Realm Chronometer & Title Screen ([`OpenWorldMenus.cs`](../Assets/Scripts/OpenWorldMenus.cs), [`OpenWorldClock.cs`](../Assets/Scripts/OpenWorldClock.cs)):**
   - In-place top navigation tab strip (`ATLAS`, `VAULT`, `ARSENAL`, `WARDROBE`, `SYSTEM`) with header currency display and circular close button.
   - **Realm Atlas:** Live 2D geographic region map with diamond/circle/arrow pins alongside fast-travel gate cards.
   - **Treasure Vault, Arsenal, Wardrobe & System (Pause):** Modern rounded cards with status accent bars, circular icon badges, weapon style/element tags, and a dedicated bottom bar in `SYSTEM` for the 4-digit PIN Equipment Testing Mode.
   - **Title Screen Overlay:** Fullscreen key art (`BG_MainMenu_Fullscreen_Landscape_Primary`) with ornamental header, `ENTER THE REALM` button, and quick links to Atlas, Time of Day, and Settings.

## Verification

`LegendsUIValidation.Verify` (and `LegendsUIValidation.RunFromBridge`) verifies Cinzel font and combat artwork usage, all menu row counts (`Atlas`, `Arsenal`, `Wardrobe`, `Vault`, `Pause`), scroll reachability to the final Arsenal row, locked-equipment rejection, and pause/resume time-scale restoration.
