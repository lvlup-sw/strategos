namespace Strategos.Agents.Configuration;

/// <summary>
/// Configuration options for Retrieval-Augmented Generation (RAG) behavior.
/// Maps conceptually to TextSearchProviderOptions in Microsoft Agent Framework.
/// </summary>
[Obsolete("RagConfiguration is no longer consumed. Configure vector search through IObjectSetProvider.", false)]
public class RagConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of results to retrieve.
    /// Default is 5.
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum relevance score (0.0 to 1.0) for results to be included.
    /// Default is 0.7.
    /// </summary>
    public double MinRelevance { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets a value indicating whether to include metadata in the context.
    /// Default is false.
    /// </summary>
    public bool IncludeMetadata { get; set; }

    /// <summary>
    /// Gets or sets a template for formatting each search result into the context.
    /// The template can use placeholders like {Content}, {Id}, {Score}.
    /// Default is "{Content}".
    /// </summary>
    public string ResultFormat { get; set; } = "{Content}";

    /// <summary>
    /// Gets or sets the header text to prepend to the retrieved context section.
    /// Default is "### Relevant Background Information".
    /// </summary>
    public string SectionHeader { get; set; } = "### Relevant Background Information";
}
