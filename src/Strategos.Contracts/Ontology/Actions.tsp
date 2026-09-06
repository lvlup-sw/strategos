using LevelUp.Strategos.Ontology;

namespace LevelUp.Strategos.Contracts;

/** Input carried by the contract-authored position inspection action. */
model InspectPositionRequest {
  /** Stable position identifier. */
  positionId: string;
}

/** Projection returned by the contract-authored position inspection action. */
model InspectPositionResult {
  /** Stable position identifier. */
  positionId: string;

  /** Current position status. */
  status: string;
}

/** Inspect a position visible through its owning portfolio. */
@objectKind("Trading", "Position", "entity")
@authority("position.reader")
@relation("owner", "Portfolio")
@requires(
  #{
    kind: "property-comparison",
    property: #{ name: "status", scalarKind: ActionPredicateScalarKindV1.String, isNullable: false },
    operator: ActionComparisonOperatorV1.NotEqual,
    value: #{ kind: "string", value: "closed" }
  },
  ActionRequirementStrengthV1.Soft,
  "The position should not already be closed."
)
@ensures(
  #{ kind: "relation-holds", relationName: "owner", linkPath: #["Portfolio"] },
  "The ownership relation remains established."
)
@clients("mcp", "web")
@confirm(false)
@readOnly
@idempotent
op inspectPosition(input: InspectPositionRequest): InspectPositionResult;
