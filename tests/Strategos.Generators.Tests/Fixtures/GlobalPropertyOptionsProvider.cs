// -----------------------------------------------------------------------
// <copyright file="GlobalPropertyOptionsProvider.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>
/// Supplies MSBuild properties to a generator under test, in the
/// <c>build_property.&lt;Name&gt;</c> form the compiler uses.
/// </summary>
/// <remarks>
/// A project opts into behavior with a property; a test that could not set one could
/// only exercise the default. Only global options are populated — no generator in this
/// repository reads per-file options, and inventing them here would let a test pass
/// through a channel production never uses.
/// </remarks>
internal sealed class GlobalPropertyOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly PropertyOptions options;

    public GlobalPropertyOptionsProvider(IReadOnlyDictionary<string, string> properties)
    {
        this.options = new PropertyOptions(properties);
    }

    public override AnalyzerConfigOptions GlobalOptions => this.options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => this.options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => this.options;

    private sealed class PropertyOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> values;

        public PropertyOptions(IReadOnlyDictionary<string, string> properties)
        {
            this.values = properties.ToImmutableDictionary(
                pair => "build_property." + pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        public override bool TryGetValue(string key, out string value) =>
            this.values.TryGetValue(key, out value!);
    }
}
