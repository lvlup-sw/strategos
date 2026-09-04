// =============================================================================
// <copyright file="ISchemaEmissionExtension.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Codegen;

/// <summary>
/// Extends the canonical JSON-Schema-to-artifact pass without coupling the
/// base record emitter to a downstream product model.
/// </summary>
internal interface ISchemaEmissionExtension
{
    /// <summary>Emits an additional artifact from the canonical schema directory.</summary>
    Task<int> EmitAsync(string schemasDir);
}
