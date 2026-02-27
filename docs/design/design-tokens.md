---
doc_id: DESIGN-TOKENS
version: 1.0.0
owner: Design Lead
updated_on: 2026-02-18
applies_to:
  - All ZenoHR UI components (Blazor Server)
  - Google Stitch wireframe generation
  - Brand consistency across dark and light modes
---

# ZenoHR Design Tokens

## Brand Identity

**Logo**: Diamond/chevron shape with gradient blues and orange/gold diamond accent
**Primary Brand Color**: `#1d4777` (deep navy blue)
**Accent Brand Color**: `#d4890e` (warm orange/gold from logo diamond)
**Personality**: Compliance-confident, trustworthy, modern, professional

---

## Color Tokens

### Core Brand Palette

| Token | Light Mode | Dark Mode | Usage |
|-------|-----------|-----------|-------|
| `--brand-primary` | `#1d4777` | `#4a8fd4` | Primary buttons, active nav, links, focus rings |
| `--brand-primary-hover` | `#163a61` | `#5da0e2` | Hover state on primary elements |
| `--brand-primary-active` | `#0f2d4e` | `#6db0ee` | Active/pressed state |
| `--brand-primary-subtle` | `#e8f0f8` | `#1a2d44` | Selected row BG, subtle badges, active sidebar item BG |
| `--brand-accent` | `#d4890e` | `#f0a832` | CTAs, highlights, notification badges, important actions |
| `--brand-accent-hover` | `#b87408` | `#f5bc58` | Hover on accent buttons |
| `--brand-accent-subtle` | `#fdf3e0` | `#2d2210` | Accent badges, warning backgrounds |

### Semantic Colors

| Token | Light Mode | Dark Mode | Usage |
|-------|-----------|-----------|-------|
| `--success` | `#16a34a` | `#4ade80` | Compliant, approved, on-track, paid |
| `--success-subtle` | `#dcfce7` | `#14532d` | Success badge BG, compliant row highlight |
| `--danger` | `#dc2626` | `#f87171` | Non-compliant, blocked, overdue, error |
| `--danger-subtle` | `#fef2f2` | `#450a0a` | Error badge BG, compliance block highlight |
| `--warning` | `#d97706` | `#fbbf24` | Approaching deadline, review needed, partial |
| `--warning-subtle` | `#fffbeb` | `#422006` | Warning badge BG |
| `--info` | `#2563eb` | `#60a5fa` | In-progress, informational, pending |
| `--info-subtle` | `#eff6ff` | `#1e3a5f` | Info badge BG |

### Surfaces and Backgrounds

#### Light Mode

| Token | Value | Usage |
|-------|-------|-------|
| `--bg-page` | `#f4f6f9` | Page background |
| `--bg-surface` | `#ffffff` | Cards, panels, modals |
| `--bg-surface-hover` | `#f8fafc` | Card hover state |
| `--bg-sidebar` | `#0f1f33` | Sidebar background (always dark) |
| `--bg-sidebar-hover` | `#1a3050` | Sidebar item hover |
| `--bg-sidebar-active` | `#1d4777` | Sidebar active item |
| `--bg-elevated` | `#ffffff` | Dropdowns, popovers, tooltips |
| `--bg-input` | `#ffffff` | Form input backgrounds |
| `--bg-input-disabled` | `#f1f5f9` | Disabled input |
| `--bg-table-header` | `#f8fafc` | Table header row |
| `--bg-table-stripe` | `#fafbfc` | Alternating table rows |

#### Dark Mode

| Token | Value | Usage |
|-------|-------|-------|
| `--bg-page` | `#0a0f1a` | Page background |
| `--bg-surface` | `#111827` | Cards, panels, modals |
| `--bg-surface-hover` | `#1a2332` | Card hover state |
| `--bg-sidebar` | `#070c15` | Sidebar background |
| `--bg-sidebar-hover` | `#1a2744` | Sidebar item hover |
| `--bg-sidebar-active` | `#1d4777` | Sidebar active item |
| `--bg-elevated` | `#1e293b` | Dropdowns, popovers, tooltips |
| `--bg-input` | `#1e293b` | Form input backgrounds |
| `--bg-input-disabled` | `#111827` | Disabled input |
| `--bg-table-header` | `#162033` | Table header row |
| `--bg-table-stripe` | `#0f1824` | Alternating table rows |

### Borders

| Token | Light Mode | Dark Mode |
|-------|-----------|-----------|
| `--border-default` | `#e2e8f0` | `#1e293b` |
| `--border-subtle` | `#f1f5f9` | `#162033` |
| `--border-strong` | `#cbd5e1` | `#334155` |
| `--border-focus` | `#1d4777` | `#4a8fd4` |
| `--border-error` | `#dc2626` | `#f87171` |

### Text Colors

| Token | Light Mode | Dark Mode |
|-------|-----------|-----------|
| `--text-primary` | `#0f172a` | `#f1f5f9` |
| `--text-secondary` | `#64748b` | `#94a3b8` |
| `--text-muted` | `#94a3b8` | `#64748b` |
| `--text-on-primary` | `#ffffff` | `#ffffff` |
| `--text-on-accent` | `#ffffff` | `#0f172a` |
| `--text-on-sidebar` | `#cbd5e1` | `#cbd5e1` |
| `--text-on-sidebar-active` | `#ffffff` | `#ffffff` |
| `--text-link` | `#1d4777` | `#4a8fd4` |
| `--text-link-hover` | `#163a61` | `#5da0e2` |

---

## Typography

### Font Stack

| Role | Font | Fallback |
|------|------|----------|
| Primary | `Inter` | `-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif` |
| Monospace | `JetBrains Mono` | `'Fira Code', 'Cascadia Code', Consolas, monospace` |

### Type Scale

| Token | Size | Weight | Line Height | Usage |
|-------|------|--------|-------------|-------|
| `--text-xs` | 11px | 400 | 1.45 | Badges, timestamps |
| `--text-sm` | 12px | 400 | 1.5 | Captions, helper text, table cells |
| `--text-base` | 14px | 400 | 1.5 | Body text, form labels, nav items |
| `--text-md` | 16px | 500 | 1.5 | Subtitles, emphasized body |
| `--text-lg` | 20px | 600 | 1.3 | Card titles, section headers |
| `--text-xl` | 24px | 600 | 1.25 | Page section titles |
| `--text-2xl` | 32px | 700 | 1.2 | Page titles |
| `--text-3xl` | 40px | 700 | 1.15 | Dashboard hero numbers |
| `--text-mono` | 13px | 400 | 1.5 | Monetary amounts in tables (JetBrains Mono) |

### Font Weights

| Token | Value | Usage |
|-------|-------|-------|
| `--font-regular` | 400 | Body text |
| `--font-medium` | 500 | Labels, nav items |
| `--font-semibold` | 600 | Headings, card titles |
| `--font-bold` | 700 | Page titles, hero numbers |

---

## Spacing

### Base Unit: 4px

| Token | Value | Usage |
|-------|-------|-------|
| `--space-1` | 4px | Inline icon gaps |
| `--space-2` | 8px | Badge padding, tight gaps |
| `--space-3` | 12px | Input padding, small card gaps |
| `--space-4` | 16px | Card inner padding (compact), form field spacing |
| `--space-5` | 20px | Standard element spacing |
| `--space-6` | 24px | Card inner padding (standard), grid gaps |
| `--space-7` | 28px | - |
| `--space-8` | 32px | Section spacing |
| `--space-10` | 40px | Large section gaps |
| `--space-12` | 48px | Page padding top |
| `--space-16` | 64px | Sidebar collapsed width |

---

## Layout

| Token | Value | Usage |
|-------|-------|-------|
| `--sidebar-width-collapsed` | 64px | Icon-only sidebar |
| `--sidebar-width-expanded` | 260px | Full sidebar with labels |
| `--header-height` | 64px | Top header bar |
| `--content-max-width` | 1440px | Content area max width |
| `--content-padding` | 24px | Main content area padding |
| `--card-gap` | 24px | Gap between cards in grid |
| `--section-gap` | 32px | Gap between page sections |

---

## Border Radius

| Token | Value | Usage |
|-------|-------|-------|
| `--radius-sm` | 6px | Inputs, small buttons |
| `--radius-md` | 8px | Badges, chips, tags |
| `--radius-lg` | 12px | Nested cards, dropdowns |
| `--radius-xl` | 16px | Main cards, panels |
| `--radius-2xl` | 20px | Modal dialogs |
| `--radius-full` | 9999px | Avatars, circular badges |

---

## Shadows

| Token | Light Mode | Dark Mode |
|-------|-----------|-----------|
| `--shadow-sm` | `0 1px 2px rgba(0,0,0,0.04)` | `0 1px 2px rgba(0,0,0,0.2)` |
| `--shadow-md` | `0 1px 3px rgba(0,0,0,0.04), 0 1px 2px rgba(0,0,0,0.06)` | `0 1px 3px rgba(0,0,0,0.3), 0 1px 2px rgba(0,0,0,0.2)` |
| `--shadow-lg` | `0 4px 6px rgba(0,0,0,0.04), 0 2px 4px rgba(0,0,0,0.06)` | `0 4px 6px rgba(0,0,0,0.3), 0 2px 4px rgba(0,0,0,0.2)` |
| `--shadow-xl` | `0 10px 15px rgba(0,0,0,0.06), 0 4px 6px rgba(0,0,0,0.04)` | `0 10px 15px rgba(0,0,0,0.4), 0 4px 6px rgba(0,0,0,0.2)` |
| `--shadow-focus` | `0 0 0 3px rgba(29,71,119,0.25)` | `0 0 0 3px rgba(74,143,212,0.35)` |

---

## Motion

| Token | Value | Usage |
|-------|-------|-------|
| `--transition-fast` | `100ms ease` | Opacity changes, color shifts |
| `--transition-default` | `150ms ease` | Hover states, button presses |
| `--transition-slow` | `250ms cubic-bezier(0.4, 0, 0.2, 1)` | Sidebar expand/collapse, modals |
| `--transition-none` | `0ms` | Compliance status indicators (must be instant) |

---

## Iconography

| Property | Value |
|----------|-------|
| Library | Lucide Icons |
| Default size | 20px |
| Navigation size | 24px |
| Inline size | 16px |
| Stroke width | 1.5px |
| Color | Inherits from text color token |

---

## Chart Colors (Ordered)

| Index | Light Mode | Dark Mode | Usage |
|-------|-----------|-----------|-------|
| 1 | `#1d4777` | `#4a8fd4` | Primary data series |
| 2 | `#d4890e` | `#f0a832` | Secondary data series |
| 3 | `#16a34a` | `#4ade80` | Success/positive |
| 4 | `#2563eb` | `#60a5fa` | Info/neutral |
| 5 | `#7c3aed` | `#a78bfa` | Tertiary |
| 6 | `#dc2626` | `#f87171` | Danger/negative |
| 7 | `#64748b` | `#94a3b8` | Muted/other |

---

## Glassmorphism (Accent Cards Only)

Used sparingly on dashboard hero/stat cards for visual distinction:

```css
.glass-card {
  backdrop-filter: blur(12px);
  background: rgba(29, 71, 119, 0.06); /* light mode */
  /* dark mode: rgba(29, 71, 119, 0.15) */
  border: 1px solid rgba(29, 71, 119, 0.1);
  /* dark mode border: rgba(74, 143, 212, 0.15) */
}
```

**Restriction**: Glassmorphism is ONLY for non-data-critical decorative cards. Compliance status, payroll amounts, and audit data must use solid backgrounds for maximum readability.

---

## Touch Targets (WCAG 2.1 AA)

| Token | Value | Usage |
|-------|-------|-------|
| `--touch-target-min` | 44px | Minimum touch target for interactive elements |
| `--touch-target-comfortable` | 48px | Comfortable touch target for primary actions |
| `--touch-target-large` | 56px | Large touch target for hero CTAs (e.g., Clock In button) |

---

## Mobile Layout

| Token | Value | Usage |
|-------|-------|-------|
| `--mobile-content-padding` | 16px | Content area padding on screens ≤640px |
| `--mobile-card-gap` | 12px | Gap between cards on mobile |
| `--mobile-bottom-nav-height` | 64px | Bottom navigation bar height |
| `--mobile-safe-area-bottom` | `env(safe-area-inset-bottom, 0px)` | Notched device bottom padding |

---

## Mobile Typography Scaling

At ≤640px, large display values scale down to remain readable without horizontal overflow:

| Token | Desktop | Mobile (≤640px) | Method |
|-------|---------|-----------------|--------|
| `--text-3xl` | 40px | `clamp(24px, 6vw, 40px)` | Viewport-relative scaling |
| `--text-2xl` | 32px | `clamp(22px, 5vw, 32px)` | Viewport-relative scaling |
| `--text-xl` | 24px | 20px | Fixed reduction |
| `--text-lg` | 20px | 18px | Fixed reduction |

**Input font-size**: All `<input>` and `<select>` elements must use `font-size: 16px` minimum on mobile to prevent iOS auto-zoom on focus.

---

## Responsive Breakpoints

| Token | Value | Usage |
|-------|-------|-------|
| `--breakpoint-sm` | 640px | Mobile phones |
| `--breakpoint-md` | 768px | Tablet portrait |
| `--breakpoint-lg` | 1024px | Tablet landscape / small desktop |
| `--breakpoint-xl` | 1280px | Desktop |
| `--breakpoint-2xl` | 1440px | Large desktop (content max-width) |

### Navigation Behavior by Viewport
- `≤ 640px`: **Bottom navigation bar** (5 icon+label items, replaces sidebar entirely)
- `641px – 1023px`: Sidebar hidden, hamburger/drawer overlay
- `1024px – 1280px`: Sidebar collapsed (64px, icon-only)
- `> 1280px`: Sidebar expanded (260px, icons + labels)

### Bottom Navigation Items (Employee/Manager on ≤640px)
| Icon | Label | Route |
|------|-------|-------|
| `layout-dashboard` | Dashboard | `/dashboard` |
| `calendar-days` | Leave | `/leave` |
| `timer` | Clock In | `/clock-in` |
| `banknote` | Payslips | `/payroll` |
| `activity` | Analytics | `/my-analytics` |

**Note**: Bottom nav item set is role-dependent. Director/HRManager roles may use the hamburger/drawer even on mobile due to the larger number of nav items.
