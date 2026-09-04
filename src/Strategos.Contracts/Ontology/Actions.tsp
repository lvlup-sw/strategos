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
@clients("mcp", "web")
@confirm(false)
@readOnly
@idempotent
op inspectPosition(input: InspectPositionRequest): InspectPositionResult;
