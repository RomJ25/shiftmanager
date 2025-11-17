# Design System Documentation

## Overview

This application uses a modern, depth-first design system built with OKLCH color space for perceptually uniform colors and proper visual hierarchy.

## Design Philosophy

**Core Principles:**
- Build with layers and depth, not flat colors
- Use OKLCH for perceptually uniform shades
- Start with dark mode, derive light mode
- Think in elevation: darker = deeper, lighter = closer

## Color System

### Neutral Backgrounds (5 Levels)

Dark Mode:
- `--bg-darkest`: oklch(0.15 0.025 250) - Deepest recesses (tables, inputs)
- `--bg-dark`: oklch(0.18 0.028 250) - Page background
- `--bg`: oklch(0.22 0.032 250) - Normal surfaces
- `--bg-light`: oklch(0.28 0.038 250) - Cards, elevated elements
- `--bg-lightest`: oklch(0.35 0.045 250) - Interactive highlights

Light Mode:
- `--bg-darkest`: oklch(0.82 0.020 250) - Darkest in light mode
- `--bg-dark`: oklch(0.88 0.018 250) - Secondary surfaces
- `--bg`: oklch(0.96 0.015 250) - Page background
- `--bg-light`: oklch(0.99 0.012 250) - Cards, elevated
- `--bg-lightest`: oklch(1.0 0.008 250) - Brightest highlights

**Chroma Values:** 0.025-0.045 (dark) / 0.015-0.020 (light)
**Hue:** 250° (cool blue personality)

### Text Hierarchy

- `--text-strong`: High contrast for headings and key text
- `--text`: Standard body text
- `--text-muted`: Secondary, less important text

### Brand Colors

- `--primary`: Main brand blue (chroma: 0.19-0.21)
- `--primary-hover`: Lighter variant for interactions
- `--danger`: Error/destructive actions (red, hue: 25°)
- `--success`: Success states (green, hue: 145°)
- `--warning`: Warning states (yellow, hue: 85°)
- `--focus`: Focus indicator (orange, hue: 65°)

## Shadow System

### Combined Shadows (Soft + Dark)

All shadows use dual-layer approach for realism:

- `--shadow-sm`: Subtle elevation (buttons, small cards)
- `--shadow-md`: Medium elevation (cards, nav)
- `--shadow-lg`: High elevation (modals, important sections)
- `--shadow-inset-top`: Recessed elements (inputs, tables)
- `--shadow-inset-bottom`: Alternative inset

**Why Combined?** Two shadows (one soft, one dark) create more realistic depth than single shadows.

## Visual Hierarchy

### Element Layers

| Layer | Elements | Background | Shadow | Use Case |
|-------|----------|------------|--------|----------|
| **Recessed** | Tables, Inputs | `--bg-darkest` | Inset | Data entry, data display |
| **Base** | Page | `--bg-dark` | None | Foundation |
| **Normal** | Surfaces | `--bg` | None | Default content areas |
| **Elevated** | Cards, Nav, Buttons | `--bg-light` | `--shadow-md` | Interactive sections |
| **Highlight** | Hover states | `--bg-lightest` | `--shadow-sm` | Active interactions |

## Component Patterns

### Buttons

**Regular Buttons:**
- Background: `--bg-light`
- Border: `--border`
- Hover: `--bg-lightest` + border becomes `--primary`
- Shadow: `--shadow-sm`

**Primary Buttons:**
- Gradient: `--primary` → `--primary-hover`
- Top highlight: 1px light edge
- Shadow: Custom OKLCH shadow with primary color
- Hover: Reverse gradient + stronger shadow

**Danger Buttons:**
- Gradient: `--danger` → lighter variant
- Similar treatment to primary

### Cards

- Background: `--bg-light`
- Border: `--border` (bottom/sides), `--highlight` (top)
- Shadow: `--shadow-md`
- Hover: Lift up 1px, border becomes `--primary`, shadow becomes `--shadow-lg`

### Tables

- Background: `--bg-darkest` (recessed)
- Inset shadow for "sunken" feel
- Headers: `--bg` with uppercase text
- Row hover: `--bg` background

### Forms

- Inputs: `--bg-darkest` with inset shadow
- Hover: Border becomes `--primary`
- Focus: Focus ring (3px) + inset shadow
- Placeholder: `--text-muted` italic

## Strategic Gradients

Gradients are used **strategically**, not universally:

✅ **Use Gradients:**
- Primary/Danger buttons
- Weekday calendar headers
- Employee topbar
- Employee welcome section

❌ **Don't Use Gradients:**
- Regular buttons
- Cards
- Shift lines
- Regular navigation

## Responsive Design

Breakpoint: 768px

**Mobile Adjustments:**
- Calendar: Single column grid
- Week view: Single column
- Shift info: Vertical stacking
- Navigation: Vertical layout
- Reduced padding/font sizes

## Accessibility

### Focus States

All interactive elements have focus indicators:
- 2px solid outline with `--focus` color
- 2px offset for visibility
- Additional focus rings on form inputs

### Color Contrast

- Text hierarchy ensures WCAG AA compliance
- Primary color has sufficient contrast on white/dark backgrounds
- Shift badges use dark text (#333) for readability

### Interactive Feedback

- Hover states: Border color changes to `--primary`
- Active states: Scale transform (0.98)
- Disabled states: Reduced opacity + disabled cursor

## Browser Support

OKLCH color space requires modern browsers:
- Chrome/Edge 111+
- Firefox 113+
- Safari 16.4+

For older browsers, CSS will gracefully degrade (browsers ignore unknown functions).

## Maintenance

### Adding New Colors

1. Choose appropriate hue (0-360)
2. Set lightness based on layer (use existing values as reference)
3. Set chroma: 0.15-0.22 for colors, 0.02-0.04 for neutrals
4. Test in both light and dark modes
5. Add CSS variable to `:root` and `:root[data-theme="dark"]`

### Modifying Shadows

All shadows use OKLCH for consistency. Modify shadow CSS variables in the design system section.

### Theme Switching

Themes switch via `data-theme` attribute on `:root`:
- Default: Light mode
- Dark mode: `data-theme="dark"`

## Performance

- CSS variables enable instant theme switching
- No JavaScript-based color calculations
- Minimal CSS (2166 lines including all components)
- Optimized transitions (0.2-0.3s cubic-bezier)

## Design Decisions

### Why OKLCH?

- Perceptually uniform (unlike HSL where L=50% looks different for each hue)
- Predictable lightness steps
- Smooth gradients without weird color shifts
- Better color interpolation

### Why Cool Blue Tint?

- Professional, trustworthy feeling
- Harmonizes with vibrant shift badges
- Not distracting, but adds personality
- Works well in both light and dark modes

### Why 5 Background Levels?

Provides enough separation for clear hierarchy without being overwhelming:
- 2 levels: Too flat
- 3 levels: Not enough distinction
- 5 levels: Perfect balance
- 7+ levels: Overkill, hard to maintain

## Credits

Design system built following modern UI depth principles with OKLCH color science.
