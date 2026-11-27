// =============================================================================
// <copyright file="WorkflowEdgesBinding.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

using Strategos.Definitions;
using Strategos.Tests.Fixtures;

namespace Strategos.Tests.Systematic;

internal static class WorkflowEdgesBinding
{
    public static Task<string> CreateModel() => Task.FromResult("[]|[]");

    public static Task<DefinitionHistory> CreateSystem() => Task.FromResult(new DefinitionHistory());

    public static Task DisposeSystem(DefinitionHistory system) => Task.CompletedTask;

    public static bool IsEnabled(string model, string operation, JsonElement args) =>
        operation == "replace" && args.ValueKind == JsonValueKind.String;

    public static Task<string> Transition(string model, string operation, JsonElement args)
    {
        var current = model.Split('|')[0];
        return Task.FromResult($"[{args.GetString()}]|{current}");
    }

    public static Task Execute(DefinitionHistory system, string operation, JsonElement args)
    {
        system.Previous = system.Current;
        system.Current = system.Current.WithTransitions([TransitionDefinition.Create("start", args.GetString()!)]);
        return Task.CompletedTask;
    }

    public static Task<string> Observe(DefinitionHistory system) =>
        Task.FromResult($"{Format(system.Current)}|{Format(system.Previous)}");

    public static Task<bool> Equivalent(string model, string observed) =>
        Task.FromResult(string.Equals(model, observed, StringComparison.Ordinal));

    private static string Format(WorkflowDefinition<TestWorkflowState> definition) =>
        $"[{string.Join(',', definition.Transitions.Select(edge => edge.ToStepId))}]";

    internal sealed class DefinitionHistory
    {
        internal WorkflowDefinition<TestWorkflowState> Current { get; set; } = WorkflowDefinition<TestWorkflowState>.Create("Campaign");

        internal WorkflowDefinition<TestWorkflowState> Previous { get; set; } = WorkflowDefinition<TestWorkflowState>.Create("Campaign");
    }
}
