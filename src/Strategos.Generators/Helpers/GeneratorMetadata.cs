// -----------------------------------------------------------------------
// <copyright file="GeneratorMetadata.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Single source of truth for the identity a generated type advertises via
/// <c>[GeneratedCode(tool, version)]</c>. Stamping is centralised in
/// <see cref="GeneratedCodeStamper"/>; this type supplies the <c>tool</c> and
/// <c>version</c> arguments so neither is hand-typed at an emit site (see #148).
/// </summary>
internal static class GeneratorMetadata
{
    /// <summary>
    /// The tool name advertised in the generated <c>[GeneratedCode(...)]</c> attribute.
    /// This is the package family (<c>LevelUp.Strategos.*</c>), not the assembly name,
    /// so a consumer's coverage report attributes the generated code to the product.
    /// </summary>
    public const string ToolName = "LevelUp.Strategos";

    /// <summary>
    /// The version advertised in the generated <c>[GeneratedCode(...)]</c> attribute,
    /// resolved once from this generator assembly's informational version (MinVer-stamped)
    /// with build metadata stripped, so it tracks releases without a hand-maintained constant.
    /// </summary>
    public static readonly string ToolVersion = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = typeof(GeneratorMetadata).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrEmpty(informational))
        {
            // Strip SemVer build metadata ("2.10.0-alpha.0.5+sha" -> "2.10.0-alpha.0.5")
            // so the emitted attribute does not vary by commit SHA.
            var plus = informational!.IndexOf('+');
            return plus >= 0 ? informational.Substring(0, plus) : informational;
        }

        var version = assembly.GetName().Version;
        return version is not null ? version.ToString() : "1.0.0";
    }
}
