// =============================================================================
// <copyright file="AtomicityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// T24 — fixture-export atomicity. A failure part-way through the export must
/// leave no half-written artifacts directory: fixtures are staged in a temp
/// directory and published with a single atomic move only on full success.
/// </summary>
[Property("Category", "FixtureExport")]
[NotInParallel("fixture-export")]
public class AtomicityTests
{
    /// <summary>
    /// Injects a failure mid-export and asserts (a) the call throws, (b) no
    /// temp staging directory is left behind, and (c) a pre-existing good
    /// destination is left intact (the failed run did not corrupt it).
    /// </summary>
    [Test]
    public async Task FixtureExport_SingleFailure_LeavesNoHalfWrittenArtifacts()
    {
        // Export to an isolated destination so we never disturb the shared one.
        var destination = Path.Combine(
            Path.GetTempPath(), "strategos-fixture-atomicity-" + Guid.NewGuid().ToString("N"));
        var parent = Path.GetDirectoryName(destination)!;

        try
        {
            // 1. A clean, complete export establishes a good destination.
            var good = FixtureExporter.Export(WorkflowCorpus.All(), destination);
            await Assert.That(Directory.Exists(destination)).IsTrue();
            var goodFileCount = Directory.GetFiles(destination, "*.json", SearchOption.AllDirectories).Length;
            await Assert.That(goodFileCount).IsGreaterThan(0);

            // 2. A failing re-export: fail on the 50th case, mid-flight.
            var seen = 0;
            await Assert.That(() => FixtureExporter.Export(
                    WorkflowCorpus.All(),
                    destination,
                    _ =>
                    {
                        if (++seen == 50)
                        {
                            throw new InvalidOperationException("injected mid-export failure");
                        }
                    }))
                .Throws<InvalidOperationException>();

            // 3. No half-written staging directory remains alongside the dest.
            var tempDirs = Directory.GetDirectories(parent, ".builder-fixtures.tmp-*");
            await Assert.That(tempDirs.Length).IsEqualTo(0)
                .Because("a failed export must clean up its temp staging directory.");

            // 4. The previously-good destination is untouched (atomic publish:
            //    the failed run never replaced it).
            await Assert.That(Directory.Exists(destination)).IsTrue();
            var afterFileCount = Directory.GetFiles(destination, "*.json", SearchOption.AllDirectories).Length;
            await Assert.That(afterFileCount).IsEqualTo(goodFileCount)
                .Because("the failed export must not corrupt the existing artifacts.");

            // The index manifest from the good run is still present and complete.
            await Assert.That(File.Exists(Path.Combine(destination, "index.json"))).IsTrue();
            await Assert.That(good.Count).IsGreaterThanOrEqualTo(100);
        }
        finally
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            foreach (var tmp in Directory.GetDirectories(parent, ".builder-fixtures.tmp-*"))
            {
                Directory.Delete(tmp, recursive: true);
            }
        }
    }
}
