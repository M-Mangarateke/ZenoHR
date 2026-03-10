// REQ-OPS-001: Shared rule-set instances used by all benchmarks.
// Values sourced from docs/seed-data/sars-paye-2025-2026.json — 2025/26 tax year.
// DO NOT hardcode arbitrary values — all numbers match SARS statutory data.

using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Benchmarks;

/// <summary>
/// Pre-built, reusable rule-set instances for benchmarks and the load-test harness.
/// Created once at startup so construction overhead is not included in measurements.
/// REQ-OPS-001
/// </summary>
public static class BenchmarkRuleSets
{
    // ── SARS PAYE 2025/26 (docs/seed-data/sars-paye-2025-2026.json) ───────────
    // Tax brackets — 7 bands, rates: 18%, 26%, 31%, 36%, 39%, 41%, 45%
    // Primary rebate:   R17,235
    // Secondary rebate: R9,444  (age >= 65)
    // Tertiary rebate:  R3,145  (age >= 75)
    // Thresholds: below65=R95,750 | 65-74=R148,217 | 75+=R165,689

    public static readonly SarsPayeRuleSet Paye = SarsPayeRuleSet.CreateForTesting(
        brackets:
        [
            new PayeTaxBracket { Min = 1m,         Max = 237100m,  Rate = 0.18m, BaseTax = 0m        },
            new PayeTaxBracket { Min = 237101m,     Max = 370500m,  Rate = 0.26m, BaseTax = 42678m    },
            new PayeTaxBracket { Min = 370501m,     Max = 512800m,  Rate = 0.31m, BaseTax = 77362m    },
            new PayeTaxBracket { Min = 512801m,     Max = 673000m,  Rate = 0.36m, BaseTax = 121475m   },
            new PayeTaxBracket { Min = 673001m,     Max = 857900m,  Rate = 0.39m, BaseTax = 179147m   },
            new PayeTaxBracket { Min = 857901m,     Max = 1817000m, Rate = 0.41m, BaseTax = 251258m   },
            new PayeTaxBracket { Min = 1817001m,    Max = null,     Rate = 0.45m, BaseTax = 644489m   },
        ],
        primary: 17235m,
        secondary: 9444m,
        tertiary: 3145m,
        thresholdBelow65: 95750m,
        thresholdAge65To74: 148217m,
        thresholdAge75Plus: 165689m,
        taxYear: "2026");

    // ── SARS UIF/SDL (docs/seed-data/sars-uif-sdl.json) ─────────────────────
    // UIF: 1% employee + 1% employer, ceiling R17,712/month, max R177.12 each
    // SDL: 1% employer-only, exempt if annual payroll < R500,000

    public static readonly SarsUifSdlRuleSet UifSdl = SarsUifSdlRuleSet.CreateForTesting(
        uifEmployeeRate: 0.01m,
        uifEmployerRate: 0.01m,
        uifMonthlyCeiling: 17712.00m,
        maxEmployeeMonthly: 177.12m,
        maxEmployerMonthly: 177.12m,
        sdlRate: 0.01m,
        sdlExemptionThresholdAnnual: 500_000.00m);

    // ── SARS ETI (docs/seed-data/sars-eti.json) ──────────────────────────────
    // Tier 1 (first 12 months):
    //   Band 0–R2,499.99:   60% of remuneration
    //   Band R2,500–R5,499.99: Fixed R1,500
    //   Band R5,500–R7,499.99: R1,500 - 0.75 × (remuneration - R5,500)
    // Tier 2 (months 13–24):
    //   Band 0–R2,499.99:   30% of remuneration
    //   Band R2,500–R5,499.99: Fixed R750
    //   Band R5,500–R7,499.99: R750 - 0.375 × (remuneration - R5,500)

    public static readonly SarsEtiRuleSet Eti = SarsEtiRuleSet.CreateForTesting(
        tier1Bands:
        [
            new EtiRateBand { MinRemuneration = 0m,       MaxRemuneration = 2499.99m, FormulaType = "percentage", Rate = 0.60m },
            new EtiRateBand { MinRemuneration = 2500m,    MaxRemuneration = 5499.99m, FormulaType = "fixed",      FlatAmount = 1500m },
            new EtiRateBand { MinRemuneration = 5500m,    MaxRemuneration = 7499.99m, FormulaType = "taper",      FlatAmount = 1500m, TaperRate = 0.75m,  TaperFloor = 5500m },
        ],
        tier2Bands:
        [
            new EtiRateBand { MinRemuneration = 0m,       MaxRemuneration = 2499.99m, FormulaType = "percentage", Rate = 0.30m },
            new EtiRateBand { MinRemuneration = 2500m,    MaxRemuneration = 5499.99m, FormulaType = "fixed",      FlatAmount = 750m },
            new EtiRateBand { MinRemuneration = 5500m,    MaxRemuneration = 7499.99m, FormulaType = "taper",      FlatAmount = 750m,  TaperRate = 0.375m, TaperFloor = 5500m },
        ],
        ageMin: 18,
        ageMax: 29,
        minWage: 2500m,
        maxRemuneration: 7500m,
        standardHours: 160);
}
