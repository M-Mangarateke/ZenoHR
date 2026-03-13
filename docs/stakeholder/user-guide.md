---
doc_id: STAKE-03-USER-GUIDE
version: 1.0.0
updated_on: 2026-03-13
owner: HRManager
classification: Internal
applies_to: [Zenowethu (Pty) Ltd, ZenoHR Platform]
---

# ZenoHR User Guide

**For**: HR Managers at Zenowethu (Pty) Ltd
**System**: ZenoHR HR, Payroll and Compliance Platform
**Version**: 1.0
**Date**: 13 March 2026

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Dashboard](#2-dashboard)
3. [Managing Employees](#3-managing-employees)
4. [Running Payroll](#4-running-payroll)
5. [Leave Management](#5-leave-management)
6. [Compliance](#6-compliance)
7. [Generating Payslips](#7-generating-payslips)
8. [Timesheets](#8-timesheets)
9. [Audit Trail](#9-audit-trail)
10. [Settings](#10-settings)
11. [Analytics](#11-analytics)
12. [My Analytics (Personal)](#12-my-analytics-personal)
13. [Clock In/Out](#13-clock-inout)
14. [Mobile Access](#14-mobile-access)
15. [Dark Mode](#15-dark-mode)
16. [FAQ](#16-faq)

---

## 1. Getting Started

### Logging In

1. Open your web browser and navigate to the ZenoHR URL provided by your administrator
2. Enter your @zenowethu.co.za email address
3. Enter your password
4. If prompted, complete multi-factor authentication (MFA) -- this is required for security
5. You will be taken to the Dashboard

**First-time login**: On your first login, you will be asked to acknowledge the data processing notice. This is required by POPIA (Protection of Personal Information Act) and explains how ZenoHR processes employee data.

### Navigation

As an HR Manager, you will see the following items in the left sidebar:

- **Dashboard** -- Overview of key metrics and recent activity
- **Employees** -- Manage employee records and contracts
- **Payroll** -- Run payroll, view results, generate payslips
- **Leave** -- Manage leave requests, view balances and calendar
- **Compliance** -- SARS filing status, BCEA checks, POPIA controls
- **Timesheets** -- View and manage employee time entries
- **Audit Trail** -- Searchable log of all system actions
- **Analytics** -- Company-wide workforce analytics
- **My Analytics** -- Your personal earnings and leave summary
- **Settings** -- Company settings, departments, users, roles, security

**Note**: Managers see only team-scoped items. Employees see only their own data. Navigation items are completely hidden for roles without access.

---

## 2. Dashboard

The Dashboard provides an at-a-glance view of key HR metrics.

### Widgets

| Widget | What It Shows |
|--------|-------------|
| **Total Employees** | Current headcount of active employees |
| **Pending Leave Requests** | Number of leave requests awaiting approval |
| **Payroll Status** | Status of the current payroll period (draft, in progress, finalised) |
| **Compliance Score** | Overall compliance percentage across SARS, BCEA, and POPIA |
| **Recent Activity** | Latest actions in the system (new employees, payroll runs, leave approvals) |
| **Quick Stats** | Key figures: total payroll cost, average salary, leave utilisation |

### Actions from Dashboard

- Click on any widget to navigate to its detail page
- Pending leave requests can be approved directly from the dashboard
- Compliance warnings are highlighted in amber or red

---

## 3. Managing Employees

### Viewing Employees

Navigate to **Employees** in the sidebar. You will see a list of all employees with:
- Name
- Employee number
- Department
- Job title
- Status (Active, On Leave, Terminated)

Use the search bar to find employees by name, employee number, or department.

### Adding a New Employee

1. Click **Add Employee** at the top of the employee list
2. Fill in the required fields:
   - **Personal details**: First name, last name, date of birth, national ID or passport number
   - **Contact details**: Email (@zenowethu.co.za), phone number, address
   - **Employment details**: Job title, department, start date, pay frequency (monthly/weekly)
   - **Tax details**: SARS tax reference number, tax status
   - **Banking details**: Bank name, account number, branch code
3. Click **Save**
4. The system will validate the SA ID number format and tax reference format
5. An audit event is automatically recorded

### Editing an Employee

1. Click on the employee's name in the list
2. Click **Edit** to modify their details
3. Make your changes and click **Save**
4. Changes are recorded in the audit trail with before/after values

### Employee Contracts

Each employee can have one or more contracts (e.g., initial contract, promotion, transfer):

1. Open the employee's profile
2. Navigate to the **Contracts** tab
3. Click **Add Contract** to create a new contract
4. Previous contracts are preserved for audit purposes -- they are never deleted

### Sensitive Data

Sensitive fields (national ID, tax reference, bank account) are masked by default. To view the full value:
- Click the eye icon next to the masked field
- Select a purpose from the dropdown (e.g., "Payroll Processing", "SARS Filing")
- The unmask action is recorded in the audit trail

This is a POPIA requirement -- every viewing of sensitive data must have a documented business purpose.

---

## 4. Running Payroll

### Step-by-Step Payroll Process

#### Step 1: Create a Payroll Run

1. Navigate to **Payroll**
2. Click **New Payroll Run**
3. Select the pay period (month or week) and date range
4. The system creates a draft payroll run

#### Step 2: Review Calculations

The system automatically calculates for each employee:
- **Gross pay** from their contract
- **PAYE** (income tax) using SARS 2025/2026 tax tables
- **UIF** (1% employee contribution, capped at R177.12/month)
- **SDL** (1% employer-only, if applicable)
- **ETI** (for qualifying youth employees)
- **Pension** and **medical aid** deductions (if configured)
- **Net pay** = Gross pay minus all deductions

Review the payroll summary. Each employee's calculation is shown with a breakdown.

#### Step 3: Handle Exceptions

If any employee has:
- Missing tax reference: a warning is shown
- Invalid banking details: flagged for correction
- BCEA working time violations: compliance warning displayed

Resolve all exceptions before proceeding.

#### Step 4: Finalise the Payroll Run

1. Click **Finalise Payroll**
2. You will be prompted for MFA confirmation (enter your authentication code)
3. The payroll run is locked -- no further changes can be made
4. Payslips are generated automatically
5. An audit event records the finalisation with your user ID and timestamp

**Important**: Once finalised, a payroll run cannot be modified. If a correction is needed, a new adjustment payroll run must be created referencing the original.

#### Step 5: View Results

After finalisation:
- View individual payslip breakdowns
- Download payslips as PDF
- View totals: total gross, total PAYE, total UIF, total net pay
- Export data for SARS filing

---

## 5. Leave Management

### Viewing Leave

Navigate to **Leave** to see:
- **Calendar view**: visual display of who is on leave and when
- **Pending requests**: leave requests awaiting your approval
- **Balances**: each employee's remaining leave days by type

### Leave Types

ZenoHR tracks all five BCEA leave types:

| Type | Entitlement | Notes |
|------|------------|-------|
| Annual leave | 15 working days per year | Accrues from start date; minimum 21 consecutive days per 36 months |
| Sick leave | 30 days per 36-month cycle | Medical certificate required for 2+ consecutive days |
| Family responsibility | 3 days per year | Birth of child, illness of child, death of family member |
| Maternity leave | 4 consecutive months | At least 4 weeks before and 6 weeks after birth |
| Parental leave | 10 consecutive days | From date of birth or adoption |

### Approving Leave Requests

1. Navigate to **Leave** and click the **Pending** tab
2. Review each request: employee name, leave type, dates, days requested, remaining balance
3. Click **Approve** or **Reject**
4. If rejecting, provide a reason (this is shown to the employee)
5. The employee is notified by email of the decision
6. Leave balance is automatically updated on approval

### Leave Accrual Ledger

Every change to a leave balance is recorded in the accrual ledger:
- Initial entitlement credits
- Leave taken (debits)
- Balance adjustments
- Carry-over calculations

This ledger is part of the audit trail and cannot be modified after the fact.

---

## 6. Compliance

### Understanding the Compliance Page

Navigate to **Compliance** to see the compliance dashboard with three sections:

#### SARS Compliance
- **IRP5/IT3(a)**: Tax certificate status for each employee
- **EMP201**: Monthly employer declaration status
- **EMP501**: Mid-year reconciliation status
- **Filing calendar**: Upcoming SARS deadlines

#### BCEA Compliance
- **Working time**: Alerts if any employee exceeds BCEA limits
- **Leave entitlements**: Verification that minimum leave is granted
- **Payslip requirements**: Confirmation that payslips meet Section 33

#### POPIA Compliance
- **Control status**: Traffic light view of all 15 POPIA controls
- **Green**: Control fully implemented
- **Amber**: Control partially implemented
- **Red**: Control not yet implemented

### Filing with SARS

**Current status**: Live electronic filing to SARS (eFiling API) is not yet available. ZenoHR generates all required files in the correct format for manual upload to SARS eFiling.

To generate a filing:
1. Navigate to **Compliance**
2. Select the filing type (e.g., EMP201)
3. Select the period
4. Click **Generate**
5. Review the generated file
6. Download and upload to SARS eFiling portal manually

### Compliance Schedules

ZenoHR tracks filing deadlines:
- **EMP201**: Due by the 7th of each month for the previous month
- **EMP501**: Due by 31 October (interim) and end of tax year (annual)
- Automated reminders are sent when deadlines approach

---

## 7. Generating Payslips

### Automatic Generation

Payslips are generated automatically when a payroll run is finalised. Each payslip is a branded A4 PDF containing:

- **Zenowethu logo** and company details
- **Employee details**: name, employee number, tax reference, department
- **Pay period**: month/week, payment date
- **Earnings**: basic salary, overtime, allowances
- **Deductions**: PAYE, UIF, pension, medical aid
- **Employer contributions**: UIF employer, SDL, ETI
- **Net pay** and banking details
- **Year-to-date totals**
- **Leave balances**

This format complies with BCEA Section 33 (mandatory payslip information).

### Downloading Payslips

1. Navigate to **Payroll**
2. Select the payroll run
3. Click on an employee to view their payslip
4. Click **Download PDF**

### Bulk Payslip Operations

- **Email payslips**: Click **Send All Payslips** to email each employee their payslip at their @zenowethu.co.za address
- **Bulk download**: Download all payslips for a period as a ZIP file

---

## 8. Timesheets

### About Timesheets

In ZenoHR, HR enters timesheet data on behalf of employees. Employees do not enter their own timesheets (they use the Clock In/Out feature for self-service time recording).

### Viewing Timesheets

1. Navigate to **Timesheets**
2. Select the week to view
3. Employee time entries are displayed in a weekly grid format
4. Hours are totalled per day and per week

### BCEA Working Time

The system monitors BCEA working time limits:
- Maximum 45 ordinary hours per week
- Maximum 10 overtime hours per week
- Maximum 15 overtime hours per month

Violations are flagged with an amber or red warning.

---

## 9. Audit Trail

### What Is the Audit Trail?

Every action in ZenoHR is recorded in a tamper-evident audit trail. This includes:
- Employee record changes
- Payroll calculations and finalisations
- Leave approvals and rejections
- Compliance filing generation
- Login and logout events
- Role and permission changes
- Data access (including unmask events)

### Viewing the Audit Trail

1. Navigate to **Audit Trail**
2. Filter by:
   - **Date range**: when the action occurred
   - **User**: who performed the action
   - **Action type**: what type of action (e.g., PayrollFinalised, LeaveApproved)
   - **Module**: which area of the system (Employee, Payroll, Leave, Compliance)
3. Each entry shows:
   - Timestamp
   - User who performed the action
   - Action description
   - Hash (SHA-256 hash-chain reference)
   - Status indicator (green checkmark if hash chain is valid)

### Hash Chain Verification

The hash chain icon next to each entry confirms tamper integrity:
- **Green checkmark**: This event's hash is valid and chains correctly to the previous event
- **Red warning**: Hash chain is broken -- indicates potential tampering (this is a Sev-1 incident)

### Evidence Packs

For audits or inspections, you can generate an evidence pack:
1. Select the date range and scope
2. Click **Generate Evidence Pack**
3. A PDF bundle is created containing all audit events, payroll records, and compliance submissions for the selected period

---

## 10. Settings

Navigate to **Settings** to manage system configuration. Settings are organised into five tabs:

### Company Settings
- Company name, registration number, address
- Tax registration details (PAYE reference, UIF reference, SDL reference)
- Financial year-end
- Default pay frequency

### Departments
- Create, edit, and deactivate departments
- Assign department managers
- View headcount per department

### Users
- View all system users and their roles
- Invite new users (sends email to @zenowethu.co.za address)
- Assign or change roles
- Deactivate users (does not delete -- preserves audit trail)

### Roles
- View the 5 system roles and their permissions
- Create custom Manager roles with specific permission tokens:
  - Leave approval
  - Timesheet approval
  - Employee team view
  - Team headcount view
  - Team leave calendar
  - Team profile access
- Custom roles cannot grant payroll, compliance, audit, or cross-department access

### Security
- View POPIA control status
- Configure session timeout settings
- View rate limiting configuration
- Access review history
- Security event log

---

## 11. Analytics

Navigate to **Analytics** to view company-wide workforce insights.

### Available Charts and Metrics

| Chart | What It Shows |
|-------|-------------|
| **Headcount Trend** | Monthly employee count over time (hires, exits, net change) |
| **Payroll Cost Trend** | Monthly total payroll cost, broken down by department |
| **Salary Band Distribution** | Histogram of salary ranges across the company |
| **Department Breakdown** | Headcount and cost by department (pie/bar chart) |
| **Leave Heatmap** | Calendar heatmap showing leave patterns (peak periods highlighted) |
| **Turnover Rate** | Monthly/annual turnover percentage |
| **PAYE/UIF/SDL Summary** | Statutory contribution totals by period |

### Filtering

- Filter by department, date range, and employment status
- Export charts as images or data as CSV

### Role-Based Visibility

- **Director/HR Manager**: See all company data
- **Manager**: See only their department's data
- **Employee**: No access to company analytics (see My Analytics instead)

---

## 12. My Analytics (Personal)

Every user (including Directors, HR Managers, and Managers) can access **My Analytics** to view their own personal employment information.

### Available Views

| View | What It Shows |
|------|-------------|
| **Earnings History** | Your monthly/weekly gross and net pay over time |
| **Tax Summary** | Your PAYE, UIF, and other deductions for the current tax year |
| **IRP5 Preview** | Preview of your tax certificate with SARS source codes |
| **Leave Rings** | Visual rings showing used vs. remaining leave for each leave type |
| **Leave History** | List of all leave requests with status |
| **Payslip History** | Access to all your historical payslips |

---

## 13. Clock In/Out

The **Clock In/Out** page allows employees to record their working hours.

### For Employees
1. Navigate to **Clock In** (available on mobile bottom navigation)
2. Click **Clock In** when you start work
3. Click **Clock Out** when you finish
4. Your clock entries are recorded with timestamps

### For Managers
The Clock In/Out page also shows a **Team Status** panel:
- See which team members are currently clocked in
- View clock-in times for the current day
- Identify team members who have not yet clocked in

---

## 14. Mobile Access

ZenoHR works on mobile phones and tablets:

### Mobile Phone
- Bottom navigation bar with 5 role-specific items
- Tables convert to card lists for easy reading on small screens
- All functions are available -- nothing is desktop-only

### Tablet
- Compact sidebar with icon-only navigation (expands on hover)
- Full table views with responsive columns

### No App Required
ZenoHR runs in your mobile web browser. No app store download is needed. Simply navigate to the ZenoHR URL on your phone or tablet.

---

## 15. Dark Mode

ZenoHR supports both light and dark themes:

1. Click the theme toggle icon in the top navigation bar (sun/moon icon)
2. Your preference is saved and persists across sessions
3. The theme switches immediately with no page flash

---

## 16. FAQ

### General

**Q: I forgot my password. What do I do?**
A: Click "Forgot Password" on the login page. A password reset email will be sent to your @zenowethu.co.za address. If you are still unable to access your account, contact your administrator.

**Q: Why am I being asked for a second authentication code?**
A: Multi-factor authentication (MFA) is required for certain actions like finalising payroll or approving SARS filings. This is a security requirement to protect against unauthorised access. Enter the code from your authenticator app.

**Q: Can I access ZenoHR from home?**
A: Yes, ZenoHR is a web application accessible from any device with an internet connection and a web browser. Use the same URL you use at the office.

**Q: Why can I not see certain menu items?**
A: ZenoHR uses role-based access. You only see menu items relevant to your role. If you believe you should have access to a feature, contact your Director or HR Manager.

### Payroll

**Q: A payroll run has been finalised with an error. Can I fix it?**
A: No. Finalised payroll runs are locked to maintain audit integrity. To correct an error, create a new adjustment payroll run that references the original. This preserves the complete history.

**Q: How do I know the tax calculations are correct?**
A: ZenoHR uses the official SARS 2025/2026 tax tables. All calculations are verified by hundreds of automated tests using the annual equivalent method specified by SARS. The payslip formula (net = gross - PAYE - UIF - deductions) is verified to the cent.

**Q: Why is the UIF contribution always R177.12 (or less)?**
A: UIF is calculated as 1% of remuneration, capped at R17,712 per month. So the maximum employee contribution is R177.12 per month (R17,712 x 1%). If an employee earns less than R17,712, their UIF contribution is 1% of their actual salary.

**Q: What is ETI and which employees qualify?**
A: The Employment Tax Incentive (ETI) is a government programme that reduces the cost of hiring young workers. Employees qualify if they are aged 18-29, earn at least R2,500 per month, and no more than R7,500 per month. The incentive reduces the employer's PAYE liability.

### Leave

**Q: An employee says their leave balance is wrong. How do I check?**
A: Navigate to the employee's leave record and view the **Accrual Ledger**. This shows every credit and debit to their balance -- initial entitlement, leave taken, adjustments. The running balance is calculated from these entries and cannot be incorrect if the entries are correct.

**Q: Can I override a leave balance?**
A: Yes, as HR Manager you can create a manual adjustment entry in the accrual ledger. The adjustment is recorded with a reason and your user ID in the audit trail.

**Q: What happens to unused annual leave at year-end?**
A: Carry-over rules are configured in the system settings. The BCEA requires that annual leave must be granted within 6 months of the end of the annual leave cycle.

### Compliance

**Q: Why does the compliance page show some controls as amber or red?**
A: This is the POPIA compliance dashboard. Green means the control is fully implemented. Amber means partially implemented (working but not complete). Red means not yet implemented. The overall compliance score reflects the aggregate status.

**Q: How do I file with SARS?**
A: Currently, ZenoHR generates filing data in the correct SARS format. You download the file and upload it manually to the SARS eFiling portal. Automatic electronic filing will be available once SARS ISV accreditation is obtained.

### Data and Privacy

**Q: Who can see employee salaries?**
A: Only Directors and HR Managers can see salary information. Managers cannot see salary, tax, or banking details for their team members. Employees can see only their own payslips.

**Q: Why do I need to select a purpose when viewing a national ID number?**
A: POPIA requires that every access to sensitive personal information has a documented business purpose. When you unmask a national ID, tax reference, or bank account number, the purpose is recorded in the audit trail.

**Q: Can employees see their own data?**
A: Yes. Every employee can view their own profile, payslips, leave balances, and analytics. They cannot see other employees' information.

**Q: What happens if there is a data breach?**
A: ZenoHR has automated breach detection and a 72-hour notification procedure. If a breach is detected, the system logs it, the affected accounts are secured, and the Information Regulator and affected employees are notified within the legally required timeframe.

### Technical

**Q: Which browsers does ZenoHR support?**
A: ZenoHR works on all modern browsers: Google Chrome, Microsoft Edge, Mozilla Firefox, and Safari. We recommend keeping your browser up to date.

**Q: The system seems slow. What should I do?**
A: Check your internet connection first. If the system is still slow, contact your administrator. ZenoHR monitors its own performance and will alert the technical team if there are issues.

**Q: Can I use ZenoHR offline?**
A: No. ZenoHR requires an internet connection to access the database and ensure real-time data accuracy. This is necessary for security and audit trail integrity.

---

*For additional support, contact your system administrator or email admin@zenowethu.co.za.*
