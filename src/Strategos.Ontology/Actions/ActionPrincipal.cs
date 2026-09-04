namespace Strategos.Ontology.Actions;

/// <summary>
/// Identifies the authenticated principal requesting an ontology action.
/// </summary>
/// <remarks>
/// <paramref name="principalType"/> is the ontology descriptor name for the
/// principal (for example, <c>User</c> or <c>ServiceAccount</c>), and
/// <paramref name="principalId"/> is the identifier of that descriptor instance.
/// Both values are required so authorization checks can bind a caller to the
/// ontology relation graph without assuming a CLR identity type.
/// </remarks>
public sealed record ActionPrincipal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionPrincipal"/> record.
    /// </summary>
    /// <param name="principalType">Ontology descriptor name for the principal.</param>
    /// <param name="principalId">Identifier of the principal instance.</param>
    public ActionPrincipal(string principalType, string principalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalType);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);

        PrincipalType = principalType;
        PrincipalId = principalId;
    }

    /// <summary>
    /// Gets the ontology descriptor name for the principal.
    /// </summary>
    public string PrincipalType { get; }

    /// <summary>
    /// Gets the identifier of the principal instance.
    /// </summary>
    public string PrincipalId { get; }
}
