---
doc_id: DESIGN-MOBILE-GUIDELINES
version: 1.0.0
owner: Design Lead
updated_on: 2026-02-22
applies_to:
  - All ZenoHR Blazor Server components
  - HTML design mockups
depends_on:
  - DESIGN-TOKENS
---

# Mobile UX Guidelines — ZenoHR

## Overview

ZenoHR is a **web-first** application. While native mobile apps are out of scope (PRD-01), the Blazor Server UI must be fully usable on mobile devices. Employee and Manager roles will primarily access Clock In/Out, Leave, Payslips, and Analytics from their phones.

---

## Touch Target Standards

All interactive elements must meet **WCAG 2.1 Level AA** touch target requirements:

| Element Type | Minimum Size | CSS Token |
|-------------|-------------|-----------|
| Buttons, chips, nav items | 44px × 44px | `--touch-target-min` |
| Primary action buttons | 48px × 48px | `--touch-target-comfortable` |
| Hero CTAs (e.g., Clock In) | 56px × 56px | `--touch-target-large` |

**Spacing**: Ensure ≥8px gap between adjacent touch targets to prevent mis-taps.

---

## Navigation Patterns

### Bottom Navigation Bar (≤640px)
- Replaces the sidebar entirely on mobile phones
- Fixed to bottom, 64px height + safe area inset
- 5 items max: Dashboard · Leave · Clock In · Payslips · Analytics
- Clock In item gets special accent color emphasis
- Active item has an accent-colored top bar indicator
- CSS class: `.bottom-nav` (hidden by default, shown via media query)

### Sidebar Drawer (641px – 1023px)
- Sidebar slides in as an overlay from the left
- Triggered by hamburger menu icon in topbar
- Overlay backdrop closes the drawer on tap

### Collapsed Sidebar (1024px – 1280px)
- 64px wide, icon-only navigation

### Expanded Sidebar (>1280px)
- Full 260px sidebar with icons and labels

---

## Bottom Sheet Pattern (≤640px)

Forms, detail panels, and modals become **bottom sheets** on mobile:

- Anchored to bottom of viewport
- Rounded top corners (`--radius-2xl`)
- Maximum height: 90vh with scroll
- Drag handle indicator (40px × 4px bar) at top
- Slide-up animation (0.25s cubic-bezier)
- Body scroll prevented when open (`body.modal-open`)
- CSS class: `.bottom-sheet` or `.modal-overlay .modal-box` (auto-converted)

**Safe area**: Include `env(safe-area-inset-bottom)` padding for notched devices.

---

## Responsive Table → Card List

Tables with many columns become unreadable on mobile. Use the **card-list pattern**:

1. Add class `table-card-responsive` to the `<table>` element
2. Add `data-label="Column Name"` attributes to each `<td>`
3. At ≤768px, each table row transforms into a stacked card
4. Table header is hidden; `data-label` values appear as inline labels

For tables that don't need the full card transformation, selectively hide less-critical columns with:
```css
@media (max-width: 640px) {
  th:nth-child(N), td:nth-child(N) { display: none; }
}
```

---

## Typography Scaling

Large display values use `clamp()` for viewport-responsive sizing:

```css
font-size: clamp(minimum, preferred, maximum);
/* Example: clamp(22px, 5vw, 30px) */
```

- KPI values: `clamp(22px, 5vw, 30px)`
- Hero numbers: `clamp(24px, 6vw, 36px)`
- Page titles: 18px (fixed reduction at ≤640px)

---

## Input Fields — iOS Auto-Zoom Prevention

iOS Safari auto-zooms the viewport when focusing on input fields with `font-size < 16px`. 

**Rule**: All `<input>`, `<select>`, and `<textarea>` elements must have `font-size: 16px` minimum on mobile.

This is enforced globally in `shared.css`:
```css
@media (max-width: 640px) {
  input, select, textarea { font-size: 16px !important; }
}
```

---

## Safe Area Handling

For devices with notches, home indicators, or rounded corners:

```css
padding-bottom: env(safe-area-inset-bottom, 0px);
```

Apply to:
- Bottom navigation bar
- Bottom sheet modals
- Fixed-position footers
- Login page form area

Requires the viewport meta tag:
```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
```

---

## Gesture Support Guidelines

For future implementation in Blazor interactive components:

| Gesture | Use Case | Priority |
|---------|----------|----------|
| Swipe right on leave request | Approve | P2 |
| Swipe left on leave request | Reject | P2 |
| Pull to refresh | Reload data | P2 |
| Long press on employee | Quick actions menu | P3 |

---

## Breakpoint Reference

| Breakpoint | Target | Content Layout |
|-----------|--------|---------------|
| ≤640px | Mobile phone | 1 column, bottom nav, 16px padding |
| 641–768px | Large phone / small tablet | 1-2 columns, drawer nav |
| 769–1024px | Tablet | 2 columns, drawer nav |
| 1025–1280px | Small desktop | Multi-column, collapsed sidebar |
| >1280px | Desktop | Multi-column, expanded sidebar |

---

## Checklist for New Components

When building any new Blazor component, verify:

- [ ] All buttons/interactive elements ≥ 44px touch target
- [ ] Input fields use `font-size: 16px` on mobile
- [ ] Grid layouts collapse appropriately at each breakpoint
- [ ] Tables either card-transform or hide non-essential columns
- [ ] Modals/panels become bottom sheets on ≤640px
- [ ] Bottom padding accounts for bottom nav (80px)
- [ ] Safe area insets applied where needed
- [ ] Content readable without horizontal scrolling at 375px
- [ ] No text clipping or overflow at 320px minimum width
