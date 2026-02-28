// REQ-HR-002, CTL-BCEA-003: Leave types mandated by the Basic Conditions of Employment Act.
// All 5 BCEA types are in scope for v1 (see MEMORY.md user preferences).

namespace ZenoHR.Module.Leave.Aggregates;

/// <summary>
/// BCEA-mandated leave type categories. All 5 types are in scope for v1.
/// Statutory entitlements come from <c>StatutoryRuleSet</c> — never hardcoded.
/// </summary>
public enum LeaveType
{
    Unknown = 0,

    /// <summary>BCEA §20: 21 consecutive days per leave cycle (3 weeks).</summary>
    Annual = 1,

    /// <summary>BCEA §22: 30 days in a 3-year cycle (max 10 per year).</summary>
    Sick = 2,

    /// <summary>BCEA §27: 3 days per annum for specified family circumstances.</summary>
    FamilyResponsibility = 3,

    /// <summary>BCEA §25: 4 consecutive months for qualifying employees.</summary>
    Maternity = 4,

    /// <summary>BCEA §25B: 10 consecutive days for co-parents.</summary>
    Parental = 5,
}
