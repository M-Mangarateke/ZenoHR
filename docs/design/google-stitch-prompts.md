---
doc_id: DESIGN-STITCH-PROMPTS
version: 1.0.0
owner: Design Lead
updated_on: 2026-02-18
status: ARCHIVED
applies_to:
  - Google Stitch wireframe generation
  - ZenoHR UI design direction
---

> **ARCHIVED — DO NOT USE FOR IMPLEMENTATION**
>
> These prompts were used for initial wireframe ideation only. The canonical ZenoHR UI design
> is now the HTML mockups in `docs/design/mockups/` (01-login through 08-audit + shared.css).
> All Blazor Server components must implement those mockups — not these Stitch prompts.
>
> Use the MCP tool `get_mockup("screen-name")` or `get_design_tokens()` during implementation.

# Google Stitch Wireframe Prompts -- ZenoHR

This document contains 4 distinct style variations for generating ZenoHR wireframes in Google Stitch. Each variation maintains brand consistency while exploring different aesthetic directions. Generate wireframes for each screen listed below using your preferred variation, then compare results to select the best direction.

## Brand Constants (Apply to ALL Variations)

```
App name: ZenoHR
Logo: Diamond/chevron mark with gradient blues and orange diamond accent
Primary blue: #1d4777
Accent orange: #d4890e
Font: Inter (headings and body), JetBrains Mono (numbers in tables)
Icons: Lucide Icons (1.5px stroke)
Accessibility: WCAG 2.1 AA contrast ratios
Sidebar: Always dark (#0f1f33) in both light and dark modes
```

---

## Application Screens

1. Main Dashboard
2. Employee Directory & Profile
3. Payroll Run Dashboard
4. Leave Management
5. Compliance Dashboard
6. Timesheet Entry & Approval
7. Audit Trail Viewer
8. Employee Self-Service Portal
9. Settings & Configuration
10. Login / Authentication

---

# VARIATION A: "Executive Clarity"

**Aesthetic**: Clean, spacious, minimal. Heavy white space, large stat numbers, subtle card borders. Stripe dashboard meets compliance. Professional, trustworthy, zero visual noise.

**Design principles**: Content hierarchy through size and weight, not decoration. Cards with barely visible borders. Typography does all the heavy lifting. Maximum breathing room.

---

### A1. Main Dashboard

```
Design a modern HR management dashboard for "ZenoHR" -- a South African payroll and compliance platform.

LAYOUT:
- Left sidebar: 260px wide, dark navy background #0f1f33, logo at top, navigation with Lucide icons: Dashboard, Employees, Payroll, Leave, Compliance, Timesheets, Audit, Settings. Active item has #1d4777 background with white text. Other items are #cbd5e1 text.
- Top header bar: 64px height, white background, contains: greeting "Good morning, Sarah" (HR Admin), global search bar with magnifying glass icon, notification bell with orange #d4890e badge showing "3", user avatar with role badge "HR Admin".
- Main content area: #f4f6f9 background, 24px padding.

CONTENT:
- Row 1: 4 stat cards in a grid. Each card is white #ffffff, border-radius 16px, subtle shadow, 24px padding.
  - "Total Employees" = 247, with small green +3 badge (new this month), sparkline trend showing growth
  - "Monthly Payroll" = R 2,847,320.00 (use JetBrains Mono for the number), small text "March 2026"
  - "Compliance Score" = 94%, circular progress ring in #16a34a (green), text "3 items need attention"
  - "Leave Requests" = 8 pending, orange #d4890e accent, "5 require your approval"

- Row 2: Two cards side by side.
  - Left (60% width): "Payroll Trend" -- clean line/area chart showing last 6 months of payroll cost, #1d4777 fill with opacity gradient, axis labels in #64748b, grid lines barely visible #f1f5f9
  - Right (40% width): "Upcoming Deadlines" -- list of 4 items: "EMP201 March Due" (7 Apr, orange warning), "Leave Cycle Ends" (30 Apr, blue info), "EMP501 Interim" (Oct, green on-track), "Tax Year End" (Feb 2027, grey muted)

- Row 3: "Recent Activity" table -- columns: Time, Action, User, Status. 5 recent rows. Clean table with #f8fafc header, no heavy borders, subtle row dividers in #f1f5f9. Status uses colored chips: green "Approved", orange "Pending", blue "Processing".

STYLE: Extreme minimalism. Cards have 1px #e2e8f0 border, tiny shadow. No gradients except on chart area fill. Typography hierarchy through size and weight only. Inter font family. All numbers in tables use JetBrains Mono. Colors: #0f172a for primary text, #64748b for secondary. Accent orange #d4890e used very sparingly -- only on badges and CTAs.

Both light and dark mode. Dark mode: #0a0f1a page background, #111827 card background, #1e293b borders.
```

### A2. Employee Directory & Profile

```
Design an employee directory page for "ZenoHR".

LAYOUT: Same sidebar and header as dashboard.

CONTENT:
- Top section: Page title "Employees" (32px, bold), subtitle "247 active employees", primary button "Add Employee" (#1d4777 background, white text, border-radius 8px), filter row with dropdowns (Department, Status, Pay Period) and search input.

- Main section: Clean data table with columns: Name (with avatar circle), Employee ID, Department, Position, Status, Pay Type.
  - Row hover: #f8fafc background
  - Status chips: "Active" green, "On Leave" blue, "Notice Period" orange, "Probation" grey
  - Pay type: "Monthly" or "Weekly" in muted text
  - Pagination at bottom: "Showing 1-20 of 247"

- Clicking a row opens a side panel (400px) sliding from right:
  - Employee avatar (64px circle), name, position, department
  - Quick stats: Tenure "2y 4m", Leave Balance "12 days", Compliance "Valid"
  - Tabs: Profile, Contracts, Payslips, Leave History, Audit Log
  - Profile tab: read-only fields in 2-column grid: ID Number (masked: ******1234), Tax Reference, Bank Account (masked), Start Date, Pay Period, Ordinary Hours Policy

STYLE: Same Executive Clarity aesthetic. Table rows are airy (48px height). Avatar circles have #e8f0f8 background placeholder. Side panel has subtle left border and shadow. All sensitive fields masked by default with "Show" toggle eye icon.

Both light and dark mode.
```

### A3. Payroll Run Dashboard

```
Design a payroll processing dashboard for "ZenoHR".

LAYOUT: Same sidebar and header.

CONTENT:
- Page title: "Payroll" with subtitle "March 2026 -- Monthly Run"
- Status banner: Shows current payroll state as a horizontal stepper/pipeline: Draft → Calculated → Validated → Approved → Finalized → Filed. Current step highlighted in #1d4777, completed steps in #16a34a with checkmarks, future steps in #94a3b8. The stepper uses connected dots/circles with lines.

- Row 1: 3 stat cards:
  - "Gross Payroll" = R 2,847,320.00 (JetBrains Mono, large)
  - "Total Deductions" = R 892,456.78 with breakdown: PAYE R654,321, UIF R38,412, SDL R28,473, Pension R171,250
  - "Net Payroll" = R 1,954,863.22

- Row 2: "Employee Payroll Summary" table:
  - Columns: Employee, Gross, PAYE, UIF, Pension, Net, Status
  - All amounts in JetBrains Mono, right-aligned
  - Status: "Calculated" blue, "Validated" green, "Error" red with exclamation icon
  - Errors expand to show details: "Missing tax reference" with link to fix
  - Footer row: totals for each column, bold

- Action bar at bottom: "Calculate" (if Draft), "Validate" (if Calculated), "Submit for Approval" (if Validated), "Finalize" (if Approved, requires confirmation dialog with compliance warning). "Finalize" button is #d4890e accent color with lock icon, showing "Requires dual authorization".

STYLE: Executive Clarity. The stepper/pipeline is the hero element. Amounts use monospace for alignment. Error rows have subtle #fef2f2 background tint. Confirmation dialog is a centered modal with clear warning text and "I confirm this action is compliant" checkbox.

Both light and dark mode.
```

### A4. Leave Management

```
Design a leave management page for "ZenoHR".

LAYOUT: Same sidebar and header.

CONTENT:
- Page title: "Leave Management"
- Tab navigation: "Requests" (active), "Calendar View", "Balances", "Policies"

REQUESTS TAB:
- Filter bar: Status dropdown (All, Pending, Approved, Rejected), Date range, Employee search
- Card-based list of leave requests:
  - Each card: Employee name + avatar, leave type (Annual/Sick/Family), dates (from-to), duration "3 days", status chip, requested date
  - Pending cards have orange left border accent
  - Action buttons on pending: "Approve" (green outline), "Reject" (red outline)
  - Approved cards: green left border
  - Quick stats at top: "8 Pending", "23 Approved This Month", "2 Rejected"

CALENDAR TAB:
- Monthly calendar view showing leave blocks
  - Annual leave = #1d4777 blue bars
  - Sick leave = #d97706 orange bars
  - Family leave = #7c3aed purple bars
  - Public holidays = #f1f5f9 grey columns
  - Employee names on left side, dates across top

BALANCES TAB:
- Table: Employee, Annual Leave (entitled/used/remaining), Sick Leave (cycle balance), Family Leave (balance)
- Low balance warnings: orange text when <3 days annual leave remaining

STYLE: Executive Clarity. Clean, airy cards for requests. Calendar uses thin colored bars, not heavy blocks. All text in Inter, balances in JetBrains Mono.

Both light and dark mode.
```

### A5. Compliance Dashboard

```
Design a compliance monitoring dashboard for "ZenoHR".

LAYOUT: Same sidebar and header.

CONTENT:
- Page title: "Compliance Dashboard" with last-checked timestamp

- Row 1: Overall compliance score card (large) -- "94% Compliant" in a large circular gauge. Breakdown: SARS 96%, BCEA 92%, POPIA 95%. Each with a small horizontal progress bar.

- Row 2: "Action Required" cards (urgent items first):
  - Card 1: RED flag -- "3 employees missing tax references" -- blocks EMP201 submission. "Resolve" button.
  - Card 2: ORANGE warning -- "EMP201 March due in 12 days" -- "Prepare Submission" button.
  - Card 3: ORANGE warning -- "2 employees exceeded 45hr ordinary hours this week" -- BCEA violation.
  - Card 4: GREEN info -- "Annual leave accruals processed" -- no action needed.

- Row 3: Two panels side by side:
  - Left: "Filing Status" -- table showing: Filing Type, Period, Status, Due Date, Submitted Date
    - EMP201 Feb 2026: Filed (green), 7 Mar, 5 Mar
    - EMP201 Mar 2026: Pending (orange), 7 Apr, --
    - EMP501 Interim: Not Due (grey), Oct 2026, --
  - Right: "Compliance Trend" -- small line chart showing compliance score over last 6 months

- Row 4: "Control Status" -- expandable accordion for each framework:
  - SARS Controls: CTL-SARS-001 through CTL-SARS-008, each with green/orange/red status dot and last-checked date
  - BCEA Controls: CTL-BCEA-001 through CTL-BCEA-006
  - POPIA Controls: CTL-POPIA-001 through CTL-POPIA-014

STYLE: Executive Clarity with compliance-specific color coding. Red/orange/green status indicators are clear and bold. The overall score gauge is the hero element. Tables are clean with subtle row separators.

Both light and dark mode.
```

### A6-A10: Additional Screens (Executive Clarity)

```
A6. TIMESHEET ENTRY:
Design a weekly timesheet entry page. 7-day grid (Mon-Sun), rows for ordinary hours and overtime. Total column on right. "Submit for Approval" button. Manager approval section below with "Approve All" / "Flag Exception" actions. Clean grid with editable cells, validation warnings for hours exceeding BCEA limits (cells turn orange border). JetBrains Mono for all hour values.

A7. AUDIT TRAIL:
Design an immutable audit trail viewer. Filterable by: date range, entity type, user, action type. Table columns: Timestamp, Actor, Action, Entity, Changes (diff view), Hash (truncated). Hash chain visualization: each row has a small chain-link icon connecting to the previous hash. Read-only, no edit/delete buttons anywhere. Export to PDF/CSV buttons.

A8. EMPLOYEE SELF-SERVICE:
Design a simplified portal for employees. No sidebar -- top nav only with: My Profile, My Payslips, My Leave, My Timesheets. Dashboard shows: next pay date, current leave balances (3 donut charts for annual/sick/family), recent payslips list (downloadable as PDF), pending leave requests. Clean, simple, mobile-friendly layout.

A9. SETTINGS:
Design a settings/configuration page. Left tabs: Company Info, Statutory Rules, Pay Periods, Leave Policies, Roles & Permissions, Notifications. Statutory Rules tab shows: active rule set version, effective date, last updated. Table of rules with "View" links. "Import New Tax Tables" button with upload flow and validation step.

A10. LOGIN:
Design a login page. Centered card on #f4f6f9 background. ZenoHR logo at top. "Sign in to ZenoHR" heading. Email and password fields. "Sign in" button in #1d4777. "Forgot password?" link. MFA step: 6-digit code input with "Enter the code from your authenticator app" text. Footer: "Zenowethu HR System -- Compliance by design".

Apply Executive Clarity style to all. Both light and dark mode.
```

---

# VARIATION B: "Dark Intelligence"

**Aesthetic**: Dark-mode-first inspired by Nixtio/Bloomberg. Deep navy backgrounds, glassmorphism cards with frosted blur, glowing accent lines. Data-dense but organized. Sophisticated, powerful, command-center feel.

**Design principles**: Dark backgrounds create focus. Glassmorphism adds depth. Glowing accent borders on active elements. Compact data density. Color used strategically for status and emphasis.

---

### B1. Main Dashboard

```
Design a dark-mode-first HR management dashboard for "ZenoHR" inspired by Nixtio's dashboard aesthetic.

LAYOUT:
- Left sidebar: 72px collapsed, dark background #070c15. Navigation icons (Lucide, 24px, #64748b) with tooltip labels on hover. Active icon: #4a8fd4 with a subtle glow effect (0 0 8px rgba(74,143,212,0.3)). Small ZenoHR diamond logo at top.
- Top header: 64px, #111827 background with bottom border #1e293b. Contains: breadcrumb "Dashboard", right side: search (cmd+K shortcut hint), notification bell with glowing #f0a832 dot, user avatar, theme toggle (sun/moon).
- Main content: #0a0f1a background, 24px padding.

CONTENT:
- Row 1: 4 stat cards with GLASSMORPHISM effect:
  - backdrop-filter: blur(12px), background: rgba(29,71,119,0.08), border: 1px solid rgba(74,143,212,0.15), border-radius 16px
  - "Total Employees" = 247 (large, #f1f5f9, font-weight 700), subtitle in #94a3b8, tiny sparkline in #4a8fd4
  - "Monthly Payroll" = R 2,847,320 (JetBrains Mono, #f1f5f9), with gradient underline from #1d4777 to #4a8fd4
  - "Compliance" = 94% with animated circular gauge, ring color #4ade80 (green)
  - "Pending Actions" = 8, with glowing #f0a832 (orange) accent badge

- Row 2: Full-width area chart "Payroll Trend (6 Months)". #0a0f1a background, #111827 card. Chart area fill: gradient from rgba(74,143,212,0.2) to transparent. Line: #4a8fd4. Grid: #1e293b. Axis text: #64748b. Hover tooltip with glassmorphism background.

- Row 3: Two cards:
  - Left: "Compliance Status" -- 3 horizontal bars for SARS/BCEA/POPIA, each with progress fill. Colors: SARS #4ade80, BCEA #fbbf24, POPIA #4a8fd4. Dark card background #111827.
  - Right: "Critical Alerts" -- stacked notification items with left colored border (red/orange/blue). Subtle glow on urgent items. "3 missing tax refs" (red), "EMP201 due" (orange), "2 BCEA hour violations" (orange).

- Row 4: "Recent Payroll Activity" table. #111827 background. Header: #162033. Rows: alternating #0f1824. Text: #f1f5f9 primary, #94a3b8 secondary. Status pills: glow effect matching their color. Monospace for amounts.

STYLE: Deep dark navy palette. Glassmorphism on hero cards only. Subtle glow effects on interactive elements. No harsh white -- warmest text is #f1f5f9. Accent lines and borders use brand colors with low opacity. Dense but organized.

Also show light mode variant: #f4f6f9 page, #ffffff cards, glassmorphism uses rgba(29,71,119,0.04).
```

### B2-B10: Additional Screens (Dark Intelligence)

```
Apply the "Dark Intelligence" aesthetic to all screens (B2 through B10) using the same content requirements as Variation A screens A2-A10, but with:

- #0a0f1a base, #111827 surfaces, #1e293b borders
- Glassmorphism on stat/hero cards: backdrop-filter blur(12px), rgba(29,71,119,0.08) background
- Glowing accent borders on active/selected elements: 0 0 8px rgba(74,143,212,0.3)
- Compact data density (44px table rows instead of 48px)
- JetBrains Mono for ALL numerical data (amounts, hours, percentages, dates)
- Status indicators with subtle glow matching their color
- The collapsed sidebar (72px) with icon-only nav, expanding on hover
- Color-coded left borders on cards indicating status (green=good, orange=attention, red=critical)
- Gradient accent lines under section titles (from #1d4777 to transparent)
- All charts use dark backgrounds with luminous data lines

The payroll stepper (B3) should look like a glowing pipeline -- completed steps connected by illuminated lines in #4ade80, current step pulsing gently in #4a8fd4, future steps dim in #334155.

The compliance dashboard (B5) should feel like a command center -- dense grid of status indicators, each control displayed as a small card with status dot, name, and last-checked timestamp.

The audit trail (B7) should show the hash chain as a vertical connected timeline with glowing chain-link icons.

The login page (B10) should be full-dark with the ZenoHR logo having a subtle ambient glow. Form fields have #1e293b background with #4a8fd4 focus ring glow.

Also show light mode variant for each screen.
```

---

# VARIATION C: "Warm Professional"

**Aesthetic**: Light backgrounds with warm accent touches from the orange/gold brand color. Rounded, friendly cards. Accessible, welcoming. Notion meets Gusto payroll. Approachable for SME users who aren't tech-savvy.

**Design principles**: Warmth through color temperature and border radius. Generous padding. Friendly but professional. Orange/gold accent used more prominently. Soft shadows, rounded elements, inviting.

---

### C1. Main Dashboard

```
Design a warm, friendly HR dashboard for "ZenoHR" that feels approachable for small business owners.

LAYOUT:
- Left sidebar: 260px, dark navy #0f1f33 but with a warmer feel -- nav items have 12px border-radius, active item has #d4890e (orange) left accent bar (3px) instead of full blue background, with subtle warm glow. Logo and "ZenoHR" text at top.
- Top header: 64px, white #ffffff, with friendly greeting "Welcome back, Sarah!" (not "Good morning"), rounded search bar with warm border #e2e8f0 → #d4890e on focus, user avatar with name and "HR Admin" role below.
- Main: #f8f6f3 background (warm off-white instead of cool grey).

CONTENT:
- Row 1: 4 stat cards with warm styling:
  - White #ffffff cards, border-radius 20px (extra rounded), warm shadow (0 2px 8px rgba(212,137,14,0.08))
  - Each card has a colored icon circle at top-left (40px circle with icon):
    - Employees: blue #1d4777 circle, people icon
    - Payroll: orange #d4890e circle, wallet icon
    - Compliance: green #16a34a circle, shield-check icon
    - Leave: purple #7c3aed circle, calendar icon
  - Numbers are large (32px) and friendly. "247 employees" not "247".
  - Subtext: "3 joined this month" with a small upward arrow in green

- Row 2: "Quick Actions" -- 4 large rounded buttons in a row:
  - "Run Payroll" (blue #1d4777), "Request Leave" (orange #d4890e), "View Compliance" (green), "Add Employee" (outlined)
  - Each button: 48px height, border-radius 12px, icon + label, hover lifts with shadow

- Row 3: Two cards:
  - Left: "Payroll at a Glance" -- horizontal stacked bar chart for this month: Gross (blue), PAYE (red), UIF (orange), Net (green). Friendly, rounded bar corners.
  - Right: "Team Leave Calendar" -- mini week view showing who's out. Employee avatars in a row per day. Today highlighted with warm #d4890e underline.

- Row 4: "Things to Do" -- friendly task list with checkboxes:
  - "Review 5 pending leave requests" (orange dot)
  - "Prepare March EMP201" (orange dot)
  - "3 employees need tax reference update" (red dot)
  - "Annual leave cycle review" (blue dot)

STYLE: Warm white backgrounds (#f8f6f3 page, #ffffff cards). Orange #d4890e used liberally for CTAs, badges, and accents. Larger border-radius (20px cards, 12px buttons). Softer shadows with warm tint. Friendly language in labels. Inter font. Accessible contrast.

Both light and dark mode. Dark mode: #12100e (warm dark) instead of cold navy, #1f1c18 for cards.
```

### C2-C10: Additional Screens (Warm Professional)

```
Apply the "Warm Professional" aesthetic to all screens (C2 through C10) using the same content as A2-A10, but with:

- Warm off-white backgrounds (#f8f6f3 light, #12100e dark)
- Extra-rounded elements (20px card radius, 12px button radius)
- Orange #d4890e accent used prominently for CTAs, active states, and highlights
- Colored icon circles (40px) for visual anchoring on cards
- Friendly, human language: "Your Employees" not "Employee Directory", "Pay Day" not "Payroll Run"
- Larger touch targets (48px minimum for interactive elements)
- Soft warm shadows: rgba(212,137,14,0.08) instead of rgba(0,0,0,0.06)
- Generous padding (32px card padding instead of 24px)
- Progress indicators use rounded bars with warm colors
- The payroll stepper (C3) uses a friendly horizontal progress bar with emoji-like status: checkmark circles for complete, spinning circle for current, empty circles for pending
- Forms use warm focus rings: 0 0 0 3px rgba(212,137,14,0.25)
- Tables are warmer: #faf8f5 header, rows with comfortable 52px height
- The login page (C10) has a warm gradient background from #1d4777 to #0f1f33, centered white card with logo, warm orange "Sign in" button, illustration or pattern element

Both light and dark mode for each screen.
```

---

# VARIATION D: "Compliance Command Center"

**Aesthetic**: Split-panel layout with compliance status always visible. Navigation + compliance health strip on left. Workspace in center. Contextual alerts on right. Mission control for HR compliance. Status-heavy with color-coded indicators everywhere.

**Design principles**: Information density. Always-visible compliance health. Three-panel layout for power users. Real-time status indicators. Every screen shows compliance context. Designed for Compliance Officers and Payroll Officers who need to see everything at once.

---

### D1. Main Dashboard

```
Design a compliance-focused command center dashboard for "ZenoHR".

LAYOUT:
- Left panel (280px): Split into two sections:
  - Top: Navigation (sidebar style, dark #0f1f33)
  - Bottom: "Compliance Health Strip" -- always visible mini-dashboard:
    - SARS: green circle "96%"
    - BCEA: orange circle "92%"
    - POPIA: green circle "95%"
    - Next deadline: "EMP201 - 12 days" in orange
    - Active violations: "2" in red badge
    This strip is visible on EVERY screen, always showing compliance status.

- Center panel (flex): Main workspace, #f4f6f9 background.
- Right panel (320px, collapsible): "Context & Alerts" sidebar:
  - "Critical" section (red header): items requiring immediate action
  - "Attention" section (orange header): items approaching deadlines
  - "Info" section (blue header): recent completions, upcoming tasks
  - Each alert: icon, short text, timestamp, "Go" action link
  - Can be collapsed to 48px with just icons showing alert counts

- Top header: 48px (compact), shows current page title, breadcrumb, user info, search.

CONTENT (Center Panel):
- Row 1: Dense stat grid (6 cards in 3x2):
  - Employees: 247 (active), 3 new, 1 on notice
  - Payroll: R2.8M gross, status "Calculated"
  - PAYE: R654K, status "Verified"
  - UIF/SDL: R66K, status "Verified"
  - ETI Claims: R12K, 8 qualifying employees
  - Net Pay: R1.95M, status "Pending Approval"

- Row 2: "Control Matrix" -- heatmap-style grid showing ALL compliance controls:
  - Columns: Control ID, Description, Last Check, Status, Action
  - Status shown as colored dots: green (pass), orange (warning), red (fail), grey (not yet checked)
  - Filterable by framework: SARS | BCEA | POPIA | All
  - Failing controls are expanded to show details and remediation steps

- Row 3: "Timeline" -- horizontal timeline of upcoming compliance events:
  - EMP201 due dates, EMP501 periods, tax year end, leave cycle ends
  - Color-coded by urgency: red (overdue), orange (<14 days), blue (>14 days), grey (>60 days)

STYLE: Information-dense but organized. Three-panel layout. Compact typography (12-13px body). Status indicators everywhere. The compliance health strip is the signature element -- it follows you across every page. Borders are slightly heavier (#cbd5e1) for clear section delineation. Tables are dense (40px rows). Color coding is consistent and prominent.

Both light and dark mode. Dark mode compliance strip uses glowing status circles.
```

### D2-D10: Additional Screens (Compliance Command Center)

```
Apply the "Compliance Command Center" aesthetic to all screens (D2 through D10) using the same content as A2-A10, but with:

- THREE-PANEL LAYOUT on every screen: left (nav + compliance health strip), center (workspace), right (contextual alerts)
- The compliance health strip (SARS/BCEA/POPIA percentages + next deadline + active violations) is ALWAYS visible in the left panel bottom
- The right alerts panel is CONTEXTUAL: on the payroll page it shows payroll-related alerts, on leave page it shows leave-related alerts, etc.
- Compact 48px top header (minimal, no wasted space)
- Information density: 40px table rows, 12px body text, tight spacing
- Status dots/badges on EVERY row of every table (green/orange/red)
- The employee profile (D2) shows a compliance tab by default: tax ref status, BCEA hours compliance, POPIA consent status
- The payroll run (D3) has a split view: left shows the stepper and summary, right shows real-time validation results as controls are checked
- The compliance dashboard (D5) IS the center panel itself -- the most complex and data-dense screen, with the control matrix as a full interactive table with inline expansion
- The audit trail (D7) has a "compliance chain" view showing only compliance-affecting events with hash verification status
- Settings (D9) shows a compliance impact assessment when any statutory rule is being changed
- The login page (D10) is the only screen without the three-panel layout; it's a focused centered form with compliance badge: "SOC 2 Type II | POPIA Compliant | 99.9% Uptime"

Both light and dark mode for each screen.
```

---

## How to Use These Prompts

1. **Pick 2-3 variations** to generate first (recommended: A and B for contrast)
2. **Start with Screen 1** (Main Dashboard) for each variation
3. **Compare results** to identify the best direction
4. **Generate remaining screens** in the chosen variation
5. **Mix elements** if desired (e.g., Variation A layout with Variation B chart styling)

### Recommended Combinations

| User Persona | Best Variation | Reason |
|-------------|----------------|--------|
| HR Admin | C (Warm Professional) | Friendly, approachable, task-oriented |
| Payroll Officer | A (Executive Clarity) or B (Dark Intelligence) | Number-focused, clean data tables |
| Compliance Officer | D (Command Center) | Maximum information density, always-visible compliance status |
| Employee (self-service) | C (Warm Professional) | Simple, inviting, non-technical |
| Auditor | B (Dark Intelligence) | Dense data, audit trail focus |

### Role-Based Dashboard Approach

Consider generating the **main dashboard** in the variation that matches the primary user (HR Admin = C or A), and specific module screens in the variation best suited to that module's power user:
- Payroll screens: A or B
- Compliance screens: D
- Employee self-service: C
- Audit trail: B
