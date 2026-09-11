# Strategos.Identity.Abstractions

The identity seam for Strategos. It holds the identity types and the ports, and no implementation.

An application references this package to supply or to read an identity. It does not take the runtime packages to do so. That is the purpose of the seam: a host chooses its own identity source, and Strategos depends only on the port.

## Installation

```bash
dotnet add package LevelUp.Strategos.Identity.Abstractions
```

## What the package holds

| Type | Purpose |
|---|---|
| `WorkflowIdentity` | The identity of one workflow instance |
| `AgentIdentity` | The identity of one agent |
| `IAgentIdentityProvider` | The port a host implements to supply an agent identity |
| `IAgentIdentityAccessor` | The port that reads the identity of the current request |
| `IPhaseAwareSaga` | The port a saga implements to expose its phase |
| `StrategosHeaders` | The message-header names that carry identity |
| `IdentityValueValidator` | The check both identity records apply to their values |

## How identity travels

Wolverine carries the identity in two envelope headers:

```text
x-strategos-workflow-identity
x-strategos-agent-identity
```

Use the constants in `StrategosHeaders`. Do not write the header names as literals.

## Supply an identity

Implement `IAgentIdentityProvider` in the host. Register it before the workflow registrations.

```csharp
public sealed class ClaimsAgentIdentityProvider : IAgentIdentityProvider
{
    public AgentIdentity GetAgentIdentity() => new(agentId, agentType);
}

services.AddSingleton<IAgentIdentityProvider, ClaimsAgentIdentityProvider>();
```

## Read an identity

Inject `IAgentIdentityAccessor` into a step. The accessor reads the identity of the message in flight.

```csharp
public sealed class AuditStep(IAgentIdentityAccessor accessor) : IWorkflowStep<OrderState>
{
    public Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, CancellationToken ct)
    {
        var agent = accessor.CurrentAgentIdentity;
        return Task.FromResult(StepResult<OrderState>.FromState(state));
    }
}
```
