// =============================================================================
// <copyright file="WorkflowActionReference.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Definitions;

/// <summary>
/// Language-neutral identity of an ontology action performed by one workflow step occurrence.
/// </summary>
/// <remarks>
/// The reference intentionally carries names only. It never retains CLR
/// <see cref="System.Type"/> handles, so it can cross the workflow wire-contract boundary
/// without coupling consumers to the producer's type system.
/// </remarks>
public sealed record WorkflowActionReference
{
    /// <summary>Initializes a new action reference from its three names.</summary>
    /// <param name="domainName">The ontology domain name.</param>
    /// <param name="objectTypeName">The ontology object type name.</param>
    /// <param name="actionName">The ontology action name.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when any identity component is empty or whitespace.
    /// </exception>
    public WorkflowActionReference(string domainName, string objectTypeName, string actionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName, nameof(domainName));
        ArgumentException.ThrowIfNullOrWhiteSpace(objectTypeName, nameof(objectTypeName));
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName, nameof(actionName));

        DomainName = domainName;
        ObjectTypeName = objectTypeName;
        ActionName = actionName;
    }

    /// <summary>Gets the ontology domain name.</summary>
    public string DomainName { get; }

    /// <summary>Gets the ontology object type name.</summary>
    public string ObjectTypeName { get; }

    /// <summary>Gets the ontology action name.</summary>
    public string ActionName { get; }

    /// <summary>Deconstructs the reference into its three identity names.</summary>
    /// <param name="domainName">The ontology domain name.</param>
    /// <param name="objectTypeName">The ontology object type name.</param>
    /// <param name="actionName">The ontology action name.</param>
    public void Deconstruct(out string domainName, out string objectTypeName, out string actionName)
    {
        domainName = DomainName;
        objectTypeName = ObjectTypeName;
        actionName = ActionName;
    }
}
