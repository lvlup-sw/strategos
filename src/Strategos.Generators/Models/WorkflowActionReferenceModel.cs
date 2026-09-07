// -----------------------------------------------------------------------
// <copyright file="WorkflowActionReferenceModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Generator-internal, language-neutral identity of the ontology action performed
/// by one workflow step occurrence.
/// </summary>
/// <remarks>
/// The source generator is an isolated netstandard2.0 analyzer and cannot reference
/// the core Strategos assembly, so this is its name-only twin of
/// <c>Strategos.Definitions.WorkflowActionReference</c>.
/// </remarks>
internal sealed record WorkflowActionReferenceModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowActionReferenceModel"/> class.
    /// </summary>
    /// <param name="domainName">The ontology domain name.</param>
    /// <param name="objectTypeName">The ontology object type name.</param>
    /// <param name="actionName">The ontology action name.</param>
    public WorkflowActionReferenceModel(
        string domainName,
        string objectTypeName,
        string actionName)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(domainName, nameof(domainName));
        ThrowHelper.ThrowIfNullOrWhiteSpace(objectTypeName, nameof(objectTypeName));
        ThrowHelper.ThrowIfNullOrWhiteSpace(actionName, nameof(actionName));

        DomainName = domainName;
        ObjectTypeName = objectTypeName;
        ActionName = actionName;
    }

    /// <summary>
    /// Gets the ontology domain name.
    /// </summary>
    public string DomainName { get; }

    /// <summary>
    /// Gets the ontology object type name.
    /// </summary>
    public string ObjectTypeName { get; }

    /// <summary>
    /// Gets the ontology action name.
    /// </summary>
    public string ActionName { get; }
}
