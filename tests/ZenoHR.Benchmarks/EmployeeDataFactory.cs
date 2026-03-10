// REQ-OPS-001: Generates realistic SA employee profiles for load testing.
// Salary range R15,000–R150,000/month, ages 22–60.
// Ensures the 500-employee mix exercises all 7 PAYE brackets + ETI eligibility range.

using ZenoHR.Domain.Common;

namespace ZenoHR.Benchmarks;

/// <summary>
/// Factory that generates deterministic, realistic employee profiles for load testing.
/// Uses a seeded RNG so benchmark runs are reproducible across machines.
/// REQ-OPS-001
/// </summary>
public static class EmployeeDataFactory
{
    // REQ-OPS-001: Salary range: R15,000 (junior) to R150,000 (director) per month.
    // Age range: 22–60 (all working-age adult brackets).
    // ETI eligibility: age 18–29 AND salary <= R7,500/month. At R15k+ this is rare;
    // a small proportion of employees are added at R2,500–R7,500 to exercise the ETI path.

    private const decimal EtiMaxSalary = 7_500m;
    private const decimal MinSalary = 15_000m;
    private const decimal MaxSalary = 150_000m;
    private const int MinAge = 22;
    private const int MaxAge = 60;

    // ~10% of employees get an ETI-eligible salary (R2,500–R7,500), age 22–29
    private const double EtiProportion = 0.10;

    public sealed record TestEmployee(
        string EmployeeId,
        int AgeYears,
        MoneyZAR MonthlySalary,
        bool IsEtiEligible,
        string Department
    );

    private static readonly string[] Departments = ["Engineering", "Operations", "Finance", "HR", "Sales"];

    /// <summary>
    /// Generates a deterministic list of test employees.
    /// </summary>
    /// <param name="count">Number of employees to generate.</param>
    /// <param name="seed">RNG seed — use the same seed for reproducible results (default 42).</param>
    public static IReadOnlyList<TestEmployee> Generate(int count, int seed = 42)
    {
        var rng = new Random(seed);
        var employees = new List<TestEmployee>(count);

        for (int i = 0; i < count; i++)
        {
            bool makeEtiCandidate = i < (int)(count * EtiProportion);

            int age;
            decimal salaryAmount;

            if (makeEtiCandidate)
            {
                // ETI-eligible employee: age 18–29, salary R2,500–R7,500
                age = rng.Next(18, 30);
                // Round to nearest R100
                salaryAmount = Math.Round(rng.Next(2_500, 7_501) / 100m) * 100m;
            }
            else
            {
                // Standard employee: age 22–60, salary R15,000–R150,000
                age = rng.Next(MinAge, MaxAge + 1);
                // Round to nearest R100
                salaryAmount = Math.Round(rng.Next((int)MinSalary, (int)MaxSalary + 1) / 100m) * 100m;
            }

            var salary = new MoneyZAR(salaryAmount);
            bool isEti = age is >= 18 and <= 29 && salary.Amount <= EtiMaxSalary;

            employees.Add(new TestEmployee(
                EmployeeId: $"bench-emp-{i:D4}",
                AgeYears: age,
                MonthlySalary: salary,
                IsEtiEligible: isEti,
                Department: Departments[i % Departments.Length]
            ));
        }

        return employees;
    }
}
