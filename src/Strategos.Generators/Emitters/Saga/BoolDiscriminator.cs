// -----------------------------------------------------------------------
// <copyright file="BoolDiscriminator.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Shared bool-discriminator exhaustiveness for generated switch expressions.
/// Both the main-flow branch emitter and the loop-exit emitter must omit a leftover
/// <c>_ =&gt;</c> arm when <c>true</c> and <c>false</c> are already present (CS8510 / #179).
/// </summary>
internal static class BoolDiscriminator
{
    /// <summary>
    /// Returns <see langword="true"/> when the discriminator is <see cref="bool"/> and both
    /// <see langword="true"/> and <see langword="false"/> arms are present, so a discarded
    /// default arm would be unreachable (CS8510).
    /// </summary>
    /// <param name="branch">The branch whose discriminator and cases to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when the default arm must be omitted; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public static bool IsExhaustive(BranchModel branch)
    {
        ThrowHelper.ThrowIfNull(branch, nameof(branch));

        if (!IsBoolType(branch.DiscriminatorTypeName))
        {
            return false;
        }

        var hasTrue = false;
        var hasFalse = false;
        foreach (var branchCase in branch.Cases)
        {
            if (branchCase.CaseValueLiteral == "true")
            {
                hasTrue = true;
            }
            else if (branchCase.CaseValueLiteral == "false")
            {
                hasFalse = true;
            }
        }

        return hasTrue && hasFalse;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="typeName"/> names the
    /// <see cref="bool"/> discriminator type stored on <see cref="BranchModel"/>.
    /// </summary>
    /// <param name="typeName">The discriminator type name from the branch model.</param>
    /// <returns>
    /// <see langword="true"/> for <c>bool</c>, <c>Boolean</c>, or <c>System.Boolean</c>.
    /// </returns>
    public static bool IsBoolType(string typeName)
        => typeName is "bool" or "Boolean" or "System.Boolean";
}
