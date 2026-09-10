using System.Threading.Tasks;
using TUnit.Core;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// DR-9 (t13) DB-gate: skips the decorated test unless a Postgres connection
/// string is available in the <see cref="ConnectionEnvVar"/> environment
/// variable. There is NO local Postgres in the default dev/CI lane, so the
/// cross-provider EXECUTION-parity test must SKIP (not fail) here; it RUNS and
/// asserts parity only in a provisioned database lane that exports
/// <c>STRATEGOS_PG_TEST_CONN</c>.
///
/// <para>
/// Mirrors the established <c>SkipIfRoslynIntegrationDisabledAttribute</c>
/// pattern (a <see cref="SkipAttribute"/> whose <see cref="ShouldSkip"/> reads an
/// env var), but with the INVERSE polarity: present connection string =&gt; run;
/// absent =&gt; skip. The skip is explicit so the test is reported as Skipped
/// rather than silently passing, and the test body is correctly structured to
/// run and assert when the variable is set.
/// </para>
/// </summary>
internal sealed class SkipIfNoPostgresAttribute : SkipAttribute
{
    /// <summary>The environment variable naming the test Postgres connection
    /// string. When unset/blank, this attribute skips the gated execution-parity
    /// test.</summary>
    public const string ConnectionEnvVar = "STRATEGOS_PG_TEST_CONN";

    public SkipIfNoPostgresAttribute()
        : base($"No Postgres connection string in {ConnectionEnvVar}; "
            + "skipping cross-provider execution-parity (runs only in a provisioned DB lane).")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        var conn = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        return Task.FromResult(string.IsNullOrWhiteSpace(conn));
    }
}
