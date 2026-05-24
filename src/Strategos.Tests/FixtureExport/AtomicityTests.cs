// =============================================================================
// <copyright file="AtomicityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// T24 — fixture-export staging + recoverable publish. A failure part-way
/// through the export must leave no half-written artifacts directory: fixtures
/// are staged in a temp directory and published move-aside (existing dest moved
/// to <c>.old</c>, temp moved into place, <c>.old</c> deleted) only on full
/// success — so a publish crash leaves the prior destination recoverable rather
/// than destroyed.
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

            // 3b. No move-aside (.old) directory leaks — the failure happened
            //     during staging (before publish), so the prior dest was never
            //     moved aside; and a publish-time aside is reclaimed in finally.
            var oldDirs = Directory.GetDirectories(parent, ".builder-fixtures.old-*");
            await Assert.That(oldDirs.Length).IsEqualTo(0)
                .Because("a failed export must not leak a move-aside (.old) directory.");

            // 4. The previously-good destination is untouched: the failed run
            //    failed during staging and never reached the publish step.
            await Assert.That(Directory.Exists(destination)).IsTrue();
            var afterFileCount = Directory.GetFiles(destination, "*.json", SearchOption.AllDirectories).Length;
            await Assert.That(afterFileCount).IsEqualTo(goodFileCount)
                .Because("the failed export must not corrupt the existing artifacts.");

            // The index manifest from the good run is still present and complete.
            await Assert.That(File.Exists(Path.Combine(destination, "index.json"))).IsTrue();
            await Assert.That(good.Count).IsGreaterThanOrEqualTo(100);

            // 5. A successful re-export over an existing destination exercises the
            //    move-aside publish path and leaves no .old residue.
            var republished = FixtureExporter.Export(WorkflowCorpus.All(), destination);
            await Assert.That(republished.Count).IsEqualTo(good.Count);
            await Assert.That(Directory.GetDirectories(parent, ".builder-fixtures.old-*").Length).IsEqualTo(0)
                .Because("a successful move-aside publish must delete its .old copy.");
            await Assert.That(Directory.GetDirectories(parent, ".builder-fixtures.tmp-*").Length).IsEqualTo(0)
                .Because("a successful publish must leave no temp staging directory.");
            var republishedFileCount = Directory.GetFiles(destination, "*.json", SearchOption.AllDirectories).Length;
            await Assert.That(republishedFileCount).IsEqualTo(goodFileCount);
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

            foreach (var old in Directory.GetDirectories(parent, ".builder-fixtures.old-*"))
            {
                Directory.Delete(old, recursive: true);
            }
        }
    }

    /// <summary>
    /// Injects a failure in the publish window — after the prior export has been
    /// moved aside but before the staged export lands at the destination — and
    /// asserts the prior good export is recovered (restored to the destination
    /// with its original contents), never destroyed.
    /// </summary>
    [Test]
    public async Task FixtureExport_PublishPhaseFailure_RecoversPriorExport()
    {
        var destination = Path.Combine(
            Path.GetTempPath(), "strategos-fixture-atomicity-" + Guid.NewGuid().ToString("N"));
        var parent = Path.GetDirectoryName(destination)!;

        try
        {
            // 1. Establish a good prior export and record a sentinel file count.
            var good = FixtureExporter.Export(WorkflowCorpus.All(), destination);
            var goodFileCount = Directory.GetFiles(destination, "*.json", SearchOption.AllDirectories).Length;
            await Assert.That(goodFileCount).IsGreaterThan(0);

            // 2. Re-export, failing in the publish window: staging completed and
            //    the prior dest was moved aside, but the temp has not yet landed.
            await Assert.That(() => FixtureExporter.Export(
                    WorkflowCorpus.All(),
                    destination,
                    onBeforePublish: () =>
                        throw new InvalidOperationException("injected publish-phase failure")))
                .Throws<InvalidOperationException>();

            // 3. The prior good export is recovered at the destination — not lost.
            await Assert.That(Directory.Exists(destination)).IsTrue()
                .Because("a publish-phase failure must restore the prior export, not destroy it.");
            var afterFileCount = Directory.GetFiles(destination, "*.json", SearchOption.AllDirectories).Length;
            await Assert.That(afterFileCount).IsEqualTo(goodFileCount)
                .Because("the recovered destination must hold the prior export's contents intact.");
            await Assert.That(File.Exists(Path.Combine(destination, "index.json"))).IsTrue();

            // 4. No staging or move-aside residue leaks after recovery.
            await Assert.That(Directory.GetDirectories(parent, ".builder-fixtures.tmp-*").Length).IsEqualTo(0)
                .Because("a failed publish must clean up its temp staging directory.");
            await Assert.That(Directory.GetDirectories(parent, ".builder-fixtures.old-*").Length).IsEqualTo(0)
                .Because("a recovered publish must not leak a move-aside (.old) directory.");
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

            foreach (var old in Directory.GetDirectories(parent, ".builder-fixtures.old-*"))
            {
                Directory.Delete(old, recursive: true);
            }
        }
    }
}
