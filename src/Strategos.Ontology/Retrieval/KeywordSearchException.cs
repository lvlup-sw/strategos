namespace Strategos.Ontology.Retrieval;

/// <summary>
/// The single exception type thrown by <see cref="IKeywordSearchProvider"/> implementations.
/// </summary>
/// <remarks>
/// Providers must wrap any underlying transport, parse, or backend exception as the inner
/// exception of a <see cref="KeywordSearchException"/> so that callers observe exactly one
/// exception type from this seam. Collection-not-found is signalled by throwing a
/// <see cref="KeywordSearchException"/> whose <see cref="Exception.Message"/> names the
/// missing collection. <see cref="OperationCanceledException"/> from cancellation is NOT
/// wrapped — it must propagate.
/// </remarks>
public sealed class KeywordSearchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeywordSearchException"/> class.
    /// </summary>
    /// <param name="message">A human-readable description of the failure.</param>
    /// <param name="inner">
    /// The underlying transport / backend exception, if any. Must be preserved so callers
    /// can inspect it for diagnostics.
    /// </param>
    public KeywordSearchException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
