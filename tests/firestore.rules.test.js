// TC-SEC-030: Firestore security rules unit tests.
// REQ-SEC-005: Tenant isolation verified.
// REQ-SEC-001: Authentication required for all reads/writes.
// CTL-POPIA-001: Role-based data access enforced.
// Run with: firebase emulators:exec --only firestore "npx mocha tests/firestore.rules.test.js"

'use strict';

const { initializeTestEnvironment, assertFails, assertSucceeds } = require('@firebase/rules-unit-testing');
const { doc, getDoc, setDoc, updateDoc, deleteDoc } = require('firebase/firestore');
const fs = require('fs');
const path = require('path');

const PROJECT_ID = 'zenohr-a7ccf';

describe('ZenoHR Firestore Security Rules', () => {
  let testEnv;

  before(async () => {
    testEnv = await initializeTestEnvironment({
      projectId: PROJECT_ID,
      firestore: {
        rules: fs.readFileSync(path.resolve(__dirname, '../firestore.rules'), 'utf8'),
        host: 'localhost',
        port: 8080,
      },
    });
  });

  after(async () => {
    await testEnv.cleanup();
  });

  afterEach(async () => {
    await testEnv.clearFirestore();
  });

  // ── TC-SEC-030-A: Unauthenticated access denied ──────────────────────────

  it('TC-SEC-030-A: Unauthenticated user cannot read any document', async () => {
    // REQ-SEC-001: Every operation requires a valid Firebase JWT.
    const unauthedDb = testEnv.unauthenticatedContext().firestore();
    await assertFails(getDoc(doc(unauthedDb, 'employees/emp-001')));
    await assertFails(getDoc(doc(unauthedDb, 'payroll_runs/run-001')));
    await assertFails(getDoc(doc(unauthedDb, 'audit_events/evt-001')));
  });

  // ── TC-SEC-030-B: Tenant isolation — cross-tenant read denied ────────────

  it("TC-SEC-030-B: HRManager cannot read another tenant's employee", async () => {
    // REQ-SEC-005: tenant_id scoping must prevent cross-tenant data access.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'employees/emp-tenant-a'), {
        tenant_id: 'tenant-a',
        employee_id: 'emp-tenant-a',
        legal_name: 'Test Employee A',
        department_id: 'dept-001',
        system_role: 'Employee',
        hire_date: '2024-01-01',
      });
    });

    const hrBDb = testEnv.authenticatedContext('hr-b-uid', {
      tenant_id: 'tenant-b',
      system_role: 'HRManager',
    }).firestore();

    await assertFails(getDoc(doc(hrBDb, 'employees/emp-tenant-a')));
  });

  // ── TC-SEC-030-C: HRManager can read own tenant's employee ───────────────

  it("TC-SEC-030-C: HRManager can read their own tenant's employee", async () => {
    // REQ-HR-001: HR has full read access within their tenant.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'employees/emp-001'), {
        tenant_id: 'tenant-zenowethu',
        employee_id: 'emp-001',
        legal_name: 'Lerato Dlamini',
        department_id: 'dept-eng',
        system_role: 'Employee',
        hire_date: '2024-01-15',
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertSucceeds(getDoc(doc(hrDb, 'employees/emp-001')));
  });

  // ── TC-SEC-030-D: Employee self-access guarantee ─────────────────────────

  it('TC-SEC-030-D: Employee can read own record but not another employee record', async () => {
    // REQ-SEC-002: Self-access guarantee — employee always reads own record regardless of role.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'employees/emp-001'), {
        tenant_id: 'tenant-zenowethu',
        employee_id: 'emp-001',
        legal_name: 'Lerato Dlamini',
        department_id: 'dept-eng',
        system_role: 'Employee',
        hire_date: '2024-01-15',
      });
      await setDoc(doc(adminDb, 'employees/emp-002'), {
        tenant_id: 'tenant-zenowethu',
        employee_id: 'emp-002',
        legal_name: 'Sipho Khumalo',
        department_id: 'dept-finance',
        system_role: 'Employee',
        hire_date: '2024-03-01',
      });
    });

    const empDb = testEnv.authenticatedContext('emp-001-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'Employee',
      employee_id: 'emp-001',
    }).firestore();

    await assertSucceeds(getDoc(doc(empDb, 'employees/emp-001')));
    await assertFails(getDoc(doc(empDb, 'employees/emp-002')));
  });

  // ── TC-SEC-030-E: Payroll runs immutability (Finalized status) ────────────

  it('TC-SEC-030-E: Finalized payroll run cannot be updated by HRManager', async () => {
    // REQ-OPS-001: Finalized payroll_runs are write-once — no updates allowed.
    // Gap 1 verification: status value 'Finalized' (PascalCase) must block updates.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'payroll_runs/run-finalized'), {
        tenant_id: 'tenant-zenowethu',
        status: 'Finalized',
        finalized_at: new Date().toISOString(),
        period: '2026-03',
        run_type: 'Monthly',
        employee_ids: ['emp-001'],
        initiated_by: 'hr-uid',
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertFails(updateDoc(doc(hrDb, 'payroll_runs/run-finalized'), {
      status: 'Draft',
    }));
  });

  // ── TC-SEC-030-E2: Filed payroll run also blocked ────────────────────────

  it('TC-SEC-030-E2: Filed payroll run cannot be updated by HRManager', async () => {
    // REQ-OPS-001: 'Filed' is the other terminal state — must also block updates.
    // Gap 1 verification: status value 'Filed' (PascalCase) must block updates.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'payroll_runs/run-filed'), {
        tenant_id: 'tenant-zenowethu',
        status: 'Filed',
        finalized_at: new Date().toISOString(),
        period: '2026-02',
        run_type: 'Monthly',
        employee_ids: ['emp-001'],
        initiated_by: 'hr-uid',
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertFails(updateDoc(doc(hrDb, 'payroll_runs/run-filed'), {
      status: 'Draft',
    }));
  });

  // ── TC-SEC-030-F: Payroll runs cannot be deleted ──────────────────────────

  it('TC-SEC-030-F: Payroll run cannot be deleted by HRManager', async () => {
    // REQ-OPS-001: No deletion of payroll runs — ever.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'payroll_runs/run-001'), {
        tenant_id: 'tenant-zenowethu',
        status: 'Draft',
        period: '2026-03',
        run_type: 'Monthly',
        employee_ids: ['emp-001'],
        initiated_by: 'hr-uid',
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertFails(deleteDoc(doc(hrDb, 'payroll_runs/run-001')));
  });

  // ── TC-SEC-030-G: Audit events are write-once ─────────────────────────────

  it('TC-SEC-030-G: Audit event cannot be updated after creation', async () => {
    // REQ-OPS-001: Audit trail is immutable — hash-chain integrity (CTL-POPIA-010).
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'audit_events/evt-001'), {
        tenant_id: 'tenant-zenowethu',
        event_type: 'EmployeeCreated',
        actor_id: 'hr-uid',
        timestamp: new Date().toISOString(),
        previous_event_hash: 'sha256-abc123',
        payload_hash: 'sha256-def456',
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertFails(updateDoc(doc(hrDb, 'audit_events/evt-001'), {
      event_type: 'Tampered',
    }));
  });

  // ── TC-SEC-030-H: SaasAdmin cannot write tenant data ─────────────────────

  it('TC-SEC-030-H: SaasAdmin cannot create employee records', async () => {
    // REQ-SEC-003: SaasAdmin is platform-only — no tenant data writes.
    // VUL-028 note: SaasAdmin can READ audit_events (accepted risk, Sev-4) but CANNOT write.
    const saasDb = testEnv.authenticatedContext('saas-uid', {
      system_role: 'SaasAdmin',
    }).firestore();

    await assertFails(setDoc(doc(saasDb, 'employees/emp-new'), {
      tenant_id: 'tenant-zenowethu',
      employee_id: 'emp-new',
      legal_name: 'Unauthorized Write',
      department_id: 'dept-eng',
      system_role: 'Employee',
      hire_date: '2026-01-01',
    }));
  });

  // ── TC-SEC-030-I: Manager cannot access payroll runs ─────────────────────

  it('TC-SEC-030-I: Manager cannot read payroll runs', async () => {
    // CTL-POPIA-001: Payroll data restricted to Director/HRManager only.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'payroll_runs/run-001'), {
        tenant_id: 'tenant-zenowethu',
        status: 'Draft',
        period: '2026-03',
        run_type: 'Monthly',
        employee_ids: ['emp-001'],
        initiated_by: 'hr-uid',
      });
    });

    const mgDb = testEnv.authenticatedContext('manager-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'Manager',
    }).firestore();

    await assertFails(getDoc(doc(mgDb, 'payroll_runs/run-001')));
  });

  // ── TC-SEC-030-J: payroll_results subcollection path verification ─────────

  it('TC-SEC-030-J: Employee can read own payroll_results subcollection document', async () => {
    // Gap 6 verification: subcollection must be 'payroll_results' (not 'results').
    // Matches PayrollResultRepository.cs: .Collection("payroll_results")
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'payroll_runs/run-001'), {
        tenant_id: 'tenant-zenowethu',
        status: 'Finalized',
        period: '2026-02',
        run_type: 'Monthly',
        employee_ids: ['emp-001'],
        initiated_by: 'hr-uid',
        finalized_at: new Date().toISOString(),
      });
      await setDoc(doc(adminDb, 'payroll_runs/run-001/payroll_results/emp-001'), {
        tenant_id: 'tenant-zenowethu',
        employee_id: 'emp-001',
        payroll_run_id: 'run-001',
        net_pay_zar: '25000.00',
        gross_pay_zar: '30000.00',
      });
    });

    const empDb = testEnv.authenticatedContext('emp-001-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'Employee',
      employee_id: 'emp-001',
    }).firestore();

    await assertSucceeds(getDoc(doc(empDb, 'payroll_runs/run-001/payroll_results/emp-001')));
  });

  // ── TC-SEC-030-K: break_glass_access_requests — SaasAdmin only ───────────

  it('TC-SEC-030-K: HRManager cannot read break_glass_access_requests', async () => {
    // REQ-SEC-006: Break-glass emergency access is SaasAdmin-controlled only.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'break_glass_access_requests/req-001'), {
        requested_by: 'saas-uid',
        tenant_id: 'tenant-zenowethu',
        reason: 'Production incident — data recovery',
        status: 'pending',
        requested_at: new Date().toISOString(),
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertFails(getDoc(doc(hrDb, 'break_glass_access_requests/req-001')));
  });

  // ── TC-SEC-030-L: Draft payroll run CAN be updated ───────────────────────

  it('TC-SEC-030-L: HRManager can update a Draft payroll run', async () => {
    // REQ-HR-003: Mutable until Finalized — Draft runs may be modified by HR.
    await testEnv.withSecurityRulesDisabled(async (context) => {
      const adminDb = context.firestore();
      await setDoc(doc(adminDb, 'payroll_runs/run-draft'), {
        tenant_id: 'tenant-zenowethu',
        status: 'Draft',
        period: '2026-04',
        run_type: 'Monthly',
        employee_ids: ['emp-001'],
        initiated_by: 'hr-uid',
      });
    });

    const hrDb = testEnv.authenticatedContext('hr-uid', {
      tenant_id: 'tenant-zenowethu',
      system_role: 'HRManager',
    }).firestore();

    await assertSucceeds(updateDoc(doc(hrDb, 'payroll_runs/run-draft'), {
      status: 'Calculated',
    }));
  });
});
