// -----------------------------------------------------------------------
// <copyright file="_SagaEmitDumpTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// One-shot diagnostic that writes a representative saga emit to /tmp for
/// manual diff inspection during T10.
/// </summary>
/// <remarks>
/// Disabled by default. Enable via the <c>STRATEGOS_DUMP_SAGA</c> env var.
/// Not intended for CI — the snapshot inspection contract lives in
/// <see cref="SagaSnapshotInspectionTests"/>.
/// </remarks>
[Property("Category", "Diagnostic")]
public class _SagaEmitDumpTests
{
    /// <summary>Writes the linear workflow saga emit to /tmp/ProcessOrderSaga.emit.cs.</summary>
    [Test]
    public async Task DumpEmit_LinearWorkflow_WhenEnvVarSet()
    {
        if (Environment.GetEnvironmentVariable("STRATEGOS_DUMP_SAGA") is null)
        {
            return;
        }

        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        File.WriteAllText("/tmp/ProcessOrderSaga.emit.cs", sagaSource);

        await Assert.That(sagaSource).IsNotEmpty();
    }
}
