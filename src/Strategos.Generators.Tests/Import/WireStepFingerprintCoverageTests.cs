// -----------------------------------------------------------------------
// <copyright file="WireStepFingerprintCoverageTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Strategos.Generators.Import;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// Guards the fork-path echo fingerprint against silently omitting a wire-DTO field.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class WireStepFingerprintCoverageTests
{
    /// <summary>
    /// Discovers every wire DTO reachable from a step, mutates every declared public property,
    /// and proves the fingerprint changes. This is a kill test: if traversal omits one current or
    /// future property, that property's mutation leaves the fingerprint unchanged and fails here.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EveryReachableWireDtoProperty_ChangesFingerprint()
    {
        foreach (var dtoType in DiscoverReachableDtoTypes())
        {
            var properties = dtoType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .ToArray();
            await Assert.That(properties).IsNotEmpty()
                .Because($"reachable wire DTO {dtoType.Name} must expose fingerprinted state.");

            foreach (var property in properties)
            {
                var baselineRoot = CreateFixture(dtoType);
                var changedRoot = CreateFixture(dtoType);
                var changedTarget = GetFixtureTarget(changedRoot, dtoType);
                property.SetValue(changedTarget, CreateDifferentValue(property, changedTarget));

                var baseline = WireToModelBridge.CreateWireStepFingerprint(baselineRoot);
                var changed = WireToModelBridge.CreateWireStepFingerprint(changedRoot);
                await Assert.That(changed).IsNotEqualTo(baseline)
                    .Because($"{dtoType.Name}.{property.Name} must participate in fork-echo identity.");
            }
        }
    }

    private static IReadOnlyList<Type> DiscoverReachableDtoTypes()
    {
        var discovered = new HashSet<Type>();
        var pending = new Queue<Type>();

        void Add(Type type)
        {
            if (discovered.Add(type))
            {
                pending.Enqueue(type);
            }
        }

        Add(typeof(StepDefinition));
        foreach (var arm in typeof(StepDefinition).Assembly
                     .GetTypes()
                     .Where(type => !type.IsAbstract && typeof(StepDefinition).IsAssignableFrom(type)))
        {
            Add(arm);
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var property in current.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var propertyType = UnwrapEnumerable(property.PropertyType);
                if (typeof(IWireContractDto).IsAssignableFrom(propertyType))
                {
                    Add(propertyType);
                }
            }
        }

        return discovered.OrderBy(static type => type.FullName, StringComparer.Ordinal).ToArray();
    }

    private static Type UnwrapEnumerable(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
            ? type.GetGenericArguments()[0]
            : type;

    private static StepDefinition CreateFixture(Type targetType)
    {
        StepDefinition root = targetType == typeof(HandlerStep)
            ? new HandlerStep { StepType = "AnalyzeStep" }
            : targetType == typeof(GateStep)
                ? new GateStep { StepType = "AnalyzeStep", GateId = "gate-a" }
                : targetType == typeof(DelegateStep)
                    ? new DelegateStep { Lambda = true }
                    : targetType == typeof(ApprovalStep)
                        ? new ApprovalStep { ApproverType = "Approver" }
                        : new SkillStep { StepType = "AnalyzeStep" };

        root.Kind = root switch
        {
            HandlerStep => "handler",
            GateStep => "gate",
            DelegateStep => "delegate",
            ApprovalStep => "approval",
            _ => "skill",
        };
        root.StepId = "step-a";
        root.StepName = "AnalyzeStep";
        root.InstanceName = "Analysis";
        root.IsTerminal = false;
        root.Runtime = "exarchos";
        root.Action = new ActionReferenceV1
        {
            DomainName = "orders",
            ObjectTypeName = "Order",
            ActionName = "analyze",
        };
        root.Configuration = new StepConfigurationDefinition
        {
            ConfidenceThreshold = 0.8,
            OnLowConfidence = new LowConfidenceHandlerDefinition
            {
                HandlerId = "risk-handler",
                HandlerSteps =
                [
                    new SkillStep
                    {
                        Kind = "skill",
                        StepId = "risk",
                        StepName = "RiskStep",
                        StepType = "RiskStep",
                        IsTerminal = true,
                    },
                ],
                IsTerminal = true,
                RejoinStepId = "join",
            },
            Compensation = new CompensationConfiguration
            {
                CompensationStepType = "CompensateStep",
                RequiredOnFailure = true,
                Timeout = "PT30S",
            },
            Retry = new RetryConfiguration
            {
                MaxAttempts = 3,
                InitialDelay = "PT1S",
                BackoffMultiplier = 2,
                MaxDelay = "PT5S",
                UseJitter = true,
            },
            Timeout = "PT10S",
            Validation = new ValidationDefinition
            {
                PredicateExpression = "state => state.IsValid",
                ErrorMessage = "invalid",
            },
        };
        return root;
    }

    private static object GetFixtureTarget(StepDefinition root, Type targetType)
    {
        if (targetType == typeof(StepDefinition) || targetType.IsInstanceOfType(root))
        {
            return root;
        }

        var configuration = root.Configuration!;
        if (targetType == typeof(ActionReferenceV1))
        {
            return root.Action!;
        }

        if (targetType == typeof(StepConfigurationDefinition))
        {
            return configuration;
        }

        if (targetType == typeof(LowConfidenceHandlerDefinition))
        {
            return configuration.OnLowConfidence!;
        }

        if (targetType == typeof(CompensationConfiguration))
        {
            return configuration.Compensation!;
        }

        if (targetType == typeof(RetryConfiguration))
        {
            return configuration.Retry!;
        }

        if (targetType == typeof(ValidationDefinition))
        {
            return configuration.Validation!;
        }

        throw new InvalidOperationException(
            $"Reachable wire DTO '{targetType.FullName}' needs a populated fingerprint fixture.");
    }

    private static object? CreateDifferentValue(PropertyInfo property, object target)
    {
        var current = property.GetValue(target);
        if (property.PropertyType == typeof(string))
        {
            return (current as string ?? "value") + "-changed";
        }

        if (property.PropertyType == typeof(bool))
        {
            return !(bool)current!;
        }

        if (property.PropertyType == typeof(bool?))
        {
            return current is true ? false : true;
        }

        if (property.PropertyType == typeof(int))
        {
            return (int)current! + 1;
        }

        if (property.PropertyType == typeof(double?))
        {
            return (double?)current + 0.125;
        }

        if (property.PropertyType == typeof(List<StepDefinition>))
        {
            var steps = new List<StepDefinition>((List<StepDefinition>)current!);
            steps.Add(new SkillStep
            {
                Kind = "skill",
                StepId = "extra",
                StepName = "ExtraStep",
                StepType = "ExtraStep",
            });
            return steps;
        }

        if (property.PropertyType == typeof(ActionReferenceV1))
        {
            return new ActionReferenceV1
            {
                DomainName = "inverse-domain",
                ObjectTypeName = "InverseObject",
                ActionName = "inverse-action",
            };
        }

        if (typeof(IWireContractDto).IsAssignableFrom(property.PropertyType) && current is not null)
        {
            return null;
        }

        throw new InvalidOperationException(
            $"Wire property '{property.DeclaringType?.Name}.{property.Name}' needs a fingerprint mutation value.");
    }
}
