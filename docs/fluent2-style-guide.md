# Fluent 2 Style Guide — Luso

This document captures the Fluent 2 design language as interpreted for a **native .NET MAUI Android app**.  
All decisions are sourced from [fluent2.microsoft.design](https://fluent2.microsoft.design) (read March 2026).  
Implemented in: `Resources/Styles/Colors.xaml` and `Resources/Styles/Styles.xaml`.

---

## 1. Design Principles (TL;DR)

| Principle | Practical meaning in this app |
|---|---|
| **Natural on every platform** | Use the Android system font (Roboto). Follow Android touch-target sizes (48 dp). Don't fight platform conventions. |
| **Built for focus** | One primary action per screen. Reduce visual noise. White space creates hierarchy. Brand colour only on CTAs and selected states. |
| **One for all, all for one** | 4.5:1 minimum text contrast. Touch targets ≥ 48 dp. Never use colour as the *only* signal. |
| **Unmistakably Microsoft** | Communication Blue as brand accent. Warm-shifted neutrals. Rounded corners. Fluent System Icons. |

---

## 2. Color System

Source: https://fluent2.microsoft.design/color

### 2.1 Brand Palette (Communication Blue)

| Token | Hex | Use |
|---|---|---|
| `BrandPrimary` | `#0078D4` | Buttons (rest), links, selected indicator, navbar |
| `BrandHover` | `#106EBE` | Hover / focus state (not applicable on Android, reserved) |
| `BrandPressed` | `#005A9E` | Pressed / active CTA (= `Tertiary` alias) |
| `BrandTint10` | `#2B88D8` | Icon fills, light accents |
| `BrandTint20` | `#69AFE5` | Dark mode brand fills |
| `BrandTint40` | `#A9D3F0` | Disabled accent fill |
| `BrandBackground` | `#EFF6FC` | Brand-tinted surface (light mode) |
| `BrandBackgroundDark` | `#004578` | Brand-tinted surface (dark mode) |

**Rules:**
- Use brand colour **only** on interactive primary CTAs and selected states.
- Never use it as a large background fill — it dilutes hierarchy.
- Pad buttons (full-screen interactive tiles) are an exception: they are deliberate chromatic signals to identify devices, not brand colour.

### 2.2 Neutral Palette

Fluent neutrals are **warm-shifted** (not pure grey). Lighter = higher visual hierarchy.

| Token | Hex | Role |
|---|---|---|
| `Neutral10` | `#FAF9F8` | Page / app background (light) |
| `Neutral20` | `#F3F2F1` | Card / elevated surface (light) |
| `Neutral30` | `#EDEBE9` | Hover surface |
| `Neutral40` | `#E1DFDD` | Subtle border / divider |
| `Neutral50` | `#D2D0CE` | Border |
| `Neutral60` | `#C8C6C4` | Strong border |
| `Neutral70` | `#B3B0AD` | Disabled icon / disabled text |
| `Neutral80` | `#8A8886` | Placeholder text |
| `Neutral90` | `#605E5C` | Secondary / caption text |
| `Neutral100` | `#323130` | Primary body text |
| `Neutral110` | `#201F1E` | Strong heading text |

Dark mode surfaces:

| Token | Hex | Role |
|---|---|---|
| `DarkBackground` | `#1C1C1C` | Page / app background |
| `DarkSurface` | `#2D2D2D` | Card / elevated surface |
| `DarkSurfaceRaised` | `#383838` | Raised / active surface |
| `DarkBorder` | `#404040` | Border |
| `DarkBorderStrong` | `#525252` | Strong border / divider |
| `DarkTextPrimary` | `#FFFFFF` | Primary body text |
| `DarkTextSecondary` | `#B3B0AD` | Secondary / caption text |
| `DarkTextDisabled` | `#605E5C` | Disabled text |
| `DarkPlaceholder` | `#8A8886` | Placeholder text |

### 2.3 Semantic Palette

**Use ONLY to convey status / urgency. Never decoratively. Always pair with text or an icon (don't rely on colour alone — accessibility).**

| Token (foreground) | Token (background) | Use |
|---|---|---|
| `SemanticSuccess` `#107C10` | `SemanticSuccessBg` `#DFF6DD` | Operation succeeded |
| `SemanticWarning` `#FFB900` | `SemanticWarningBg` `#FFF4CE` | Caution / degraded |
| `SemanticDanger` `#D13438` | `SemanticDangerBg` `#FDE7E9` | Error / destructive |
| `SemanticInfo` `#0078D4` | `SemanticInfoBg` `#EFF6FC` | Informational |

### 2.4 Interaction States

Elements darken as interaction deepens: Rest → Pressed.  
Android does NOT use hover states (reserve `BrandHover` for future pointer/desktop support).

### 2.5 Backward-Compatible Aliases

Old MAUI template key names are preserved as aliases pointing to Fluent 2 values.  
**These exist only for backward compatibility. Prefer the full token names in new code.**

| Old key | Maps to | Fluent 2 token |
|---|---|---|
| `Primary` | `#0078D4` | `BrandPrimary` |
| `Secondary` | `#EFF6FC` | `BrandBackground` |
| `Tertiary` | `#005A9E` | `BrandPressed` |
| `Gray100` | `#F3F2F1` | `Neutral20` |
| `Gray200` | `#E1DFDD` | `Neutral40` |
| `Gray300` | `#D2D0CE` | `Neutral50` |
| `Gray400` | `#B3B0AD` | `Neutral70` |
| `Gray500` | `#8A8886` | `Neutral80` |
| `Gray600` | `#605E5C` | `Neutral90` |
| `Gray900` | `#323130` | `Neutral100` |
| `Gray950` | `#1C1C1C` | `DarkBackground` |

---

## 3. Typography

Source: https://fluent2.microsoft.design/typography

**Font:** Platform system default — **do not set `FontFamily` in XAML**.  
On Android → Roboto. On iOS → SF Pro. This satisfies "Natural on every platform".

**Weight note:** MAUI's `FontAttributes` only has `Bold`. Fluent 2's "Semibold" roles map to `Bold` in this implementation.

**Casing:** Always sentence case. Never all-caps.  
**Alignment:** Left-align body text. Use centre-align only for small callout text.

### Type Ramp (Android dp)

| Named Style | Weight | Size / Line-height | Typical use |
|---|---|---|---|
| `Caption2` | Regular | 10 / 14 | Legal, metadata |
| `Caption1` | Regular | 12 / 16 | Secondary labels, timestamps |
| `Caption1Strong` | Bold | 12 / 16 | Small emphasis labels |
| `Body1` *(implicit)* | Regular | 14 / 20 | Default body text |
| `Body1Strong` | Bold | 14 / 20 | Inline emphasis, list titles |
| `Subtitle2` | Bold | 16 / 22 | Section headers, card titles |
| `Subtitle1` | Bold | 20 / 28 | Page section titles |
| `Title3` | Bold | 24 / 32 | Page sub-heading |
| `Title2` | Bold | 28 / 36 | Room names, feature titles |
| `Title1` | Bold | 32 / 40 | Hero titles |

Usage in XAML:
```xml
<Label Text="Room name" Style="{StaticResource Title2}"/>
<Label Text="Connected · 12 ms" Style="{StaticResource Caption1}"/>
```

**Contrast requirement:** Standard text 4.5:1. Large text (≥ 24 px regular / ≥ 18.5 px bold) 3:1.

---

## 4. Shapes

Source: https://fluent2.microsoft.design/shapes

### Corner Radius Tokens

| Name | Value | Use |
|---|---|---|
| None | 0 px | Nav bars, tab bars, full-bleed elements at screen edge |
| Small | 2 px | Small badges |
| **Medium** | **4 px** | **Standard buttons, dropdowns, input fields** |
| **Large** | **8 px** | **Large buttons (implicit Button style default)** |
| **X-Large** | **12 px** | **Cards, bottom sheets, pads (implicit Frame default)** |
| Circle | 50% | Avatars, persona chips |

**Rule:** Rounded corners are **not applied** when a component reaches the screen edge (e.g., a bottom sheet that spans full width).

### Stroke Thickness

| Name | Value | Use |
|---|---|---|
| Thin | 1 px | Default border (`Border` implicit style) |
| Thick | 2 px | Focus ring, active indicator |
| Thicker | 3 px | Extra emphasis |

---

## 5. Elevation (Shadows)

Source: https://fluent2.microsoft.design/elevation

Fluent uses **dual-layer shadows** (key + ambient). In MAUI `Shadow`, we approximate with a single layer.  
Shadow opacity adjusts for light vs dark mode (dark needs more opacity to read against dark surfaces).

| Token | Blur | Y-offset | Opacity (light / dark) | Use |
|---|---|---|---|---|
| `$shadow2` | 2 px | 1 px | 14% / 28% | Cards without borders, FAB pressed |
| **`$shadow4`** | **4 px** | **2 px** | **14% / 28%** | **Cards, list items, grid items** ← **default Shadow style** |
| `$shadow8` | 8 px | 4 px | 14% / 28% | Floating buttons, raised cards, app bars |
| `$shadow16` | 16 px | 8 px | 14% / 28% | Callouts, hover cards |
| `$shadow28` | 28 px | 14 px | 24% / 28% | Bottom sheet, side nav |
| `$shadow64` | 64 px | 32 px | 24% / 28% | Dialogs, modals |

Named MAUI style `FluentCard` applies `$shadow4`.

XAML inline example for `$shadow8`:
```xml
<Border ...>
    <Border.Shadow>
        <Shadow Brush="Black" Offset="0,4" Radius="8" Opacity="0.14"/>
    </Border.Shadow>
</Border>
```

**On colour surfaces:** Shadow opacity should be adjusted using the luminosity formula if you put a shadow on a coloured tile. For the device pads (which have strong colours), consider `Opacity="0.25"` as a reasonable approximation.

---

## 6. Layout & Spacing

Source: https://fluent2.microsoft.design/layout

### Spacing Ramp (4 px base unit)

Use values from this ramp: `2, 4, 6, 8, 10, 12, 16, 20, 24, 28, 32, 36, 40, 48`

| Usage | Value |
|---|---|
| Icon padding offset | 2–6 px |
| Component internal spacing | 8–12 px |
| Section / card padding | 16 px |
| Page horizontal margin | 16–24 px |
| Layout section gap | 24–32 px |

### Touch Targets

**Android (Fluent 2 / MAUI):** minimum **48 × 48 dp**  
iOS / Web: 44 × 44  
All interactive elements must meet this — enforced via `MinimumHeightRequest="48"` and `MinimumWidthRequest="48"` in the implicit Button and input styles.

### Grid

- **Mobile (< 640 dp wide):** use a fluid single-column or 2-column layout.
- Content that isn't a perfect rectangle should use a **masonry / smart-span layout** (hero item absorbs leftover columns). See `HostRoomPage.xaml.cs → RefreshPadGrid()`.
- Gutter / spacing between grid cells: 8 dp.
- Page outer padding: 12–16 dp horizontal.

---

## 7. Material

Source: https://fluent2.microsoft.design/material

| Material | Android availability | Use |
|---|---|---|
| **Solid** | ✅ Always | Default surface — use `Neutral10/Neutral20/DarkBackground/DarkSurface` |
| **Acrylic** | ⚠️ Simulate | Frosted-glass effect for transient surfaces (menus, bottom sheets). Simulate with semi-transparent `BackgroundColor` (e.g. `rgba(45,45,45,0.9)`) |
| **Mica** | ❌ Windows only | Not available on Android |
| **Smoke** | ✅ Via overlay | Modal scrim — always `rgba(0,0,0,0.4)` |

For this app, **Solid** is the correct choice everywhere. Acrylic is not needed in the current design.

---

## 8. Motion

Source: https://fluent2.microsoft.design/motion

### Principles
- **Functional:** every animation explains a state change or confirms an action.
- **Natural:** follow physical laws — ease-out for enter, ease-in for exit.
- **Fast:** keep durations short. Users should not wait for animations.
- **Accessible:** respect `PreferReduceMotion` — provide a no-motion fallback.

### Duration Guide

| Element size / distance | Duration |
|---|---|
| Small (icon, badge) | 100–150 ms |
| Medium (button, card) | 150–200 ms |
| Large (page, bottom sheet) | 250–300 ms |

### Easing

| Pattern | Easing | Use |
|---|---|---|
| Enter | Ease-out | Element enters screen or expands |
| Exit | Ease-in | Element leaves screen or collapses |
| Emphasis / press | Ease-in-out | Button scale on press/release |
| Rotation / loop | Linear | Spinner, progress ring |

### Key Transitions

| Transition | Description | MAUI approach |
|---|---|---|
| **Enter/Exit** | Slide + fade | `FadeTo` + `TranslateTo` combined |
| **Elevation** | Scale + shadow on press | `ScaleTo(0.96)` on Pressed, `ScaleTo(1.0)` on Released |
| **Top-level nav** | Quick cross-fade | MAUI Shell default fade is acceptable |
| **Container transform** | Resize / reposition | Layout animations on `RefreshPadGrid` |

**Press animation (pads & buttons):**
```csharp
await view.ScaleTo(0.95, 80, Easing.CubicIn);   // on Pressed
await view.ScaleTo(1.00, 120, Easing.CubicOut);  // on Released
```

---

## 9. Iconography

Source: https://fluent2.microsoft.design/iconography

Fluent System Icons — open source (MIT), available via **`FluentIcons.Maui`** NuGet.

### Themes
- **Regular** — navigation, actions (wayfinding)
- **Filled** — selected state, small sizes needing more visual weight

### Sizes (Android dp)
`16, 20, 24, 28, 32, 48`  
Use **24 dp** for toolbar / nav icons. Use **20 dp** for inline / list icons.

### Rules
- Icons are named for the **shape/object**, not the function (e.g. `Shield`, not `Security`).
- Only one solid colour per icon. Don't mix colours.
- Place **Filled modifiers** in the bottom-right corner of an icon.
- Validate cultural connotations for target regions.

### Current state (TODO)
The toolbar currently uses Unicode characters (`＋`, `✕`, `Kick`).  
These should be replaced with `FluentIcons.Maui` icons during the UI rework phase.

---

## 10. Named XAML Styles Reference

| Key | Type | Description |
|---|---|---|
| `FluentCard` | `Border` | Elevated card surface, $shadow4, corner radius 12, padding 16 |
| `BrandButton` | `Button` | Filled CTA, BrandPrimary bg, white text, radius 4 |
| `SubtleButton` | `Button` | Low-emphasis action, neutral fill, radius 4 |
| `Caption2` | `Label` | 10 px, regular, secondary colour |
| `Caption1` | `Label` | 12 px, regular, secondary colour |
| `Caption1Strong` | `Label` | 12 px, bold, primary colour |
| `Body1` | `Label` | 14 px, regular — same as implicit Label |
| `Body1Strong` | `Label` | 14 px, bold |
| `Subtitle2` | `Label` | 16 px, bold |
| `Subtitle1` | `Label` | 20 px, bold |
| `Title3` | `Label` | 24 px, bold |
| `Title2` | `Label` | 28 px, bold |
| `Title1` | `Label` | 32 px, bold |

---

## 11. What NOT to do

- ❌ Don't use `BrandPrimary` as a large background surface.
- ❌ Don't use semantic colours (red/yellow/green) for decoration.
- ❌ Don't set a custom `FontFamily` — let the platform supply its system font.
- ❌ Don't apply rounded corners to components that bleed to the screen edge.
- ❌ Don't use colour as the only signal for status (pair with text or icon).
- ❌ Don't use spacing values outside the 4 px ramp (use 8, 12, 16, 24 — not 15, 11, 7).
- ❌ Don't add motion that runs longer than 300 ms unless it is a top-level page transition.
- ❌ Don't use `HasShadow="True"` on `Frame` — use `Border` + explicit `Shadow` with the ramp values.

---

## 12. Quick Reference Card

```
Brand:     #0078D4  (rest)  |  #106EBE (hover)  |  #005A9E (pressed)
Page bg:   #FAF9F8  (light) |  #1C1C1C (dark)
Card bg:   #F3F2F1  (light) |  #2D2D2D (dark)
Border:    #D2D0CE  (light) |  #404040 (dark)
Text/1°:   #323130  (light) |  #FFFFFF (dark)
Text/2°:   #605E5C  (light) |  #B3B0AD (dark)
Radius:    4 (button) | 8 (large button) | 12 (card/pad)
Spacing:   8 | 12 | 16 | 24
Shadow:    Y=2, Blur=4, 14% black (card default)
Font:      System default (Roboto/Android, SF Pro/iOS)
Body:      14 sp  |  Titles: 20–32 sp bold
Touch:     48×48 dp minimum
```
