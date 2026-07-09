// -----------------------------------------------------------------------
// <copyright file="ApprovalPointNaming.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Strategos.Generators.Helpers;

/// <summary>
/// The single, shared derivation of an approval point's generated C# identifier from its approver
/// type name. Both authoring channels resolve the approval-point name through THIS one method so they
/// cannot drift:
/// <list type="bullet">
///   <item><description>
///     the C#-authoring path (<see cref="ApprovalExtractor"/>, from <c>AwaitApproval&lt;TApprover&gt;()</c>), and
///   </description></item>
///   <item><description>
///     the JSON-import path (<c>WireToModelBridge.MapApprovals</c>).
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// The wire IR carries an <c>approvalPointId</c> that is a GUID identity (e.g.
/// <c>Guid.NewGuid().ToString("N")</c>), NOT a C# identifier. It must never be used as the generated
/// point name: a digit-leading GUID is not a valid identifier, so feeding it to
/// <c>ApprovalModel.Create</c> throws and crashes the generator (CS8785). The generated point name is
/// always DERIVED from the approver type name here, and the wire id is kept for identity/lookup only.
/// </remarks>
internal static class ApprovalPointNaming
{
    private const string ApproverSuffix = "Approver";

    /// <summary>
    /// Derives the approval-point name (a valid C# identifier) from the approver's SIMPLE type name.
    /// The <c>Approver</c> suffix is stripped for a cleaner phase name (e.g. <c>ManagerApprover</c> →
    /// <c>Manager</c>); if stripping would empty the name, an index-suffixed fallback is used.
    /// </summary>
    /// <param name="approverTypeName">The approver's simple (unqualified) type name.</param>
    /// <param name="index">The zero-based approval index, used only for the empty-name fallback.</param>
    /// <returns>A valid C# identifier for the approval point.</returns>
    public static string Derive(string approverTypeName, int index)
    {
        var baseName = approverTypeName ?? string.Empty;

        // Remove the "Approver" suffix for a cleaner phase name.
        if (baseName.EndsWith(ApproverSuffix, StringComparison.Ordinal))
        {
            baseName = baseName.Substring(0, baseName.Length - ApproverSuffix.Length);
        }

        // If the name would be empty after removing the suffix, fall back to an index-suffixed name.
        return string.IsNullOrEmpty(baseName) ? $"Approval{index}" : baseName;
    }
}
