using System.Reflection;
using NetArchTest.Rules;

namespace Ledgerline.ArchitectureTests;

/// <summary>
/// Microservices only stay independent if nothing lets them reach into each other.
/// These rules fail the build on the shortcut, not in a code review six months later.
/// </summary>
public sealed class ServiceBoundaryTests
{
    private static readonly string[] Infrastructure = ["Marten", "Wolverine", "Microsoft.AspNetCore", "Npgsql", "JasperFx"];

    private static readonly (string Name, Assembly Service, Assembly Domain)[] Services =
    [
        ("Ledger", Assembly.Load("Ledgerline.Ledger"), Assembly.Load("Ledgerline.Ledger.Domain")),
        ("Payments", Assembly.Load("Ledgerline.Payments"), Assembly.Load("Ledgerline.Payments.Domain")),
        ("Fraud", Assembly.Load("Ledgerline.Fraud"), Assembly.Load("Ledgerline.Fraud.Domain")),
    ];

    public static TheoryData<string> ServiceNames => new(Services.Select(service => service.Name));

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Domains_ArePure(string name)
    {
        var domain = Services.Single(s => s.Name == name).Domain;

        Assert(Types.InAssembly(domain).ShouldNot().HaveDependencyOnAny(Infrastructure).GetResult());
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void Services_NeverReferenceAnotherService(string name)
    {
        var service = Services.Single(s => s.Name == name);
        var others = Services.Where(s => s.Name != name).SelectMany(s => new[] { s.Service.GetName().Name!, s.Domain.GetName().Name! }).ToArray();

        Assert(Types.InAssembly(service.Service).ShouldNot().HaveDependencyOnAny(others).GetResult());
    }

    [Fact]
    public void Contracts_DependOnNothing()
    {
        var contracts = Assembly.Load("Ledgerline.Contracts");

        Assert(Types.InAssembly(contracts).ShouldNot().HaveDependencyOnAny([.. Infrastructure, "Ledgerline.Ledger", "Ledgerline.Payments", "Ledgerline.Fraud", "Ledgerline.SharedKernel"]).GetResult());
    }

    [Fact]
    public void Contracts_AreImmutableRecords_WithPrimitivePayloads()
    {
        var contracts = Assembly.Load("Ledgerline.Contracts").GetExportedTypes();
        var allowed = new[] { typeof(Guid), typeof(string), typeof(long), typeof(DateTimeOffset), typeof(IReadOnlyList<string>) };

        foreach (var type in contracts)
        {
            type.GetMethod("<Clone>$").ShouldNotBeNull($"{type.Name} must be a record");
            type.GetProperties().Where(p => p.Name != "EqualityContract").ShouldAllBe(p => allowed.Contains(p.PropertyType) && p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Length > 0,
                $"{type.Name}: contract properties are init-only primitives");
        }
    }

    private static void Assert(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful.ShouldBeTrue($"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
