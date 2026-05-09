using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Configuration;

/// <summary>
/// Opt-in dispatcher decorator registrations. Each extension schedules a factory
/// that wraps the registered <see cref="IActionDispatcher"/>; decorators are
/// composed in ascending <c>Order</c> at <see cref="OntologyServiceCollectionExtensions.AddOntology"/>
/// time so that lower-order decorators sit closer to the inner dispatcher and
/// higher-order decorators sit closer to the caller.
/// </summary>
public static class OntologyDispatcherDecoratorExtensions
{
    private const int ConstraintReportingOrder = 25;
    private const int DispatchObservationOrder = 75;

    /// <summary>
    /// Wraps the registered dispatcher with <see cref="ConstraintReportingActionDispatcher"/>.
    /// Sits closer to the inner dispatcher (Order = 25) so that observers, when
    /// also enabled, see the violation-enriched <see cref="ActionResult"/>.
    /// </summary>
    public static OntologyOptions AddConstraintReporting(this OntologyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DispatcherDecorators.Any(d => d.Order == ConstraintReportingOrder))
        {
            return options;
        }

        options.DispatcherDecorators.Add((
            ConstraintReportingOrder,
            (sp, inner) =>
            {
                // Defer query resolution: IOntologyQuery transitively depends on
                // IActionDispatcher (via OntologyQueryService construction), which
                // would cycle if resolved during decorator-chain construction.
                var lazyQuery = new Lazy<IOntologyQuery>(sp.GetRequiredService<IOntologyQuery>);
                return ConstraintReportingActionDispatcher.CreateDeferred(
                    inner,
                    lazyQuery,
                    sp.GetRequiredService<ILogger<ConstraintReportingActionDispatcher>>());
            }));
        return options;
    }

    /// <summary>
    /// Wraps the registered dispatcher with <see cref="ObservableActionDispatcher"/>.
    /// Sits closer to the caller (Order = 75) so it fires last and observes the
    /// final result (including any violation enrichment).
    /// </summary>
    public static OntologyOptions AddDispatchObservation(this OntologyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DispatcherDecorators.Any(d => d.Order == DispatchObservationOrder))
        {
            return options;
        }

        options.DispatcherDecorators.Add((
            DispatchObservationOrder,
            (sp, inner) => new ObservableActionDispatcher(
                inner,
                sp.GetServices<IActionDispatchObserver>(),
                sp.GetRequiredService<ILogger<ObservableActionDispatcher>>())));
        return options;
    }
}
