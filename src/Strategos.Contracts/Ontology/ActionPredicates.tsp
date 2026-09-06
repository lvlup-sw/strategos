import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/** A non-empty segment in an ontology relation traversal path. */
@minLength(1)
@pattern(".*\\S.*")
@jsonSchema
scalar ActionLinkPathSegmentV1 extends string;

/** The comparison operators accepted by the closed action-predicate language. */
@jsonSchema
enum ActionComparisonOperatorV1 {
  Equal: "equal",
  NotEqual: "not-equal",
  LessThan: "less-than",
  LessThanOrEqual: "less-than-or-equal",
  GreaterThan: "greater-than",
  GreaterThanOrEqual: "greater-than-or-equal",
}

/** The scalar domain of a property compared by an action predicate. */
@jsonSchema
enum ActionPredicateScalarKindV1 {
  Boolean: "boolean",
  Integer: "integer",
  Decimal: "decimal",
  String: "string",
  Enum: "enum",
  Symbol: "symbol",
}

/** Whether a requirement blocks dispatch or is advisory. */
@jsonSchema
enum ActionRequirementStrengthV1 {
  Hard: "hard",
  Soft: "soft",
}

/** An explicit null literal. Missing data is not represented by this arm. */
model ActionNullLiteralV1 {
  kind: "null";
}

/** An exact Boolean literal. */
model ActionBooleanLiteralV1 {
  kind: "boolean";
  value: boolean;
}

/**
 * An arbitrary-precision integer encoded as a canonical base-10 string.
 * JSON numbers are deliberately excluded because their precision is transport dependent.
 */
model ActionIntegerLiteralV1 {
  kind: "integer";
  @pattern("^(0|-?[1-9][0-9]*)$")
  value: string;
}

/**
 * An exact decimal encoded as a canonical base-10 string without exponent notation.
 */
model ActionDecimalLiteralV1 {
  kind: "decimal";
  @pattern("^(0|-?[1-9][0-9]*|-?(0|[1-9][0-9]*)\\.[0-9]*[1-9])$")
  value: string;
}

/** An ordinal string literal. */
model ActionStringLiteralV1 {
  kind: "string";
  value: string;
}

/** A member of a named ontology enum. */
model ActionEnumLiteralV1 {
  kind: "enum";
  @minLength(1)
  typeName: string;
  @minLength(1)
  memberName: string;
}

/** A symbolic identifier whose identity is its ordinal token. */
model ActionSymbolLiteralV1 {
  kind: "symbol";
  @minLength(1)
  value: string;
}

/**
 * Version 1 of the closed literal vocabulary used by action predicates.
 * Floating-point values are intentionally not part of this union.
 */
@jsonSchema
union ActionLiteralV1 {
  null: ActionNullLiteralV1,
  boolean: ActionBooleanLiteralV1,
  integer: ActionIntegerLiteralV1,
  decimal: ActionDecimalLiteralV1,
  string: ActionStringLiteralV1,
  enumLiteral: ActionEnumLiteralV1,
  symbol: ActionSymbolLiteralV1,
}

/** A property resource declared in a custom predicate's read set. */
model ActionPropertyResourceV1 {
  kind: "property";
  @minLength(1)
  name: string;
}

/** A link resource declared in a custom predicate's read set. */
model ActionLinkResourceV1 {
  kind: "link";
  @minLength(1)
  name: string;
}

/** An event resource declared in a custom predicate's read set. */
model ActionEventResourceV1 {
  kind: "event";
  @minLength(1)
  name: string;
}

/** An external resource declared in a custom predicate's read set. */
model ActionExternalResourceV1 {
  kind: "external";
  @minLength(1)
  name: string;
}

/** Closed resource vocabulary for a custom predicate's declared read set. */
@jsonSchema
union ActionResourceV1 {
  property: ActionPropertyResourceV1,
  link: ActionLinkResourceV1,
  event: ActionEventResourceV1,
  external: ActionExternalResourceV1,
}

/** Predicate that is satisfied by every state. */
model ActionTruePredicateV1 {
  kind: "true";
}

/** Predicate that is satisfied by no state. */
model ActionFalsePredicateV1 {
  kind: "false";
}

/** Compare one ontology property with a typed literal. */
model ActionPropertyReferenceV1 {
  @minLength(1)
  name: string;
  scalarKind: ActionPredicateScalarKindV1;
  isNullable: boolean;
  /** Required for enum properties; absent for every other scalar kind. */
  enumTypeName?: string;
}

/** Compare one ontology property with a typed literal. */
model ActionPropertyComparisonPredicateV1 {
  kind: "property-comparison";
  property: ActionPropertyReferenceV1;
  operator: ActionComparisonOperatorV1;
  value: ActionLiteralV1;
}

/** Assert that a named ontology link is present. */
model ActionLinkExistsPredicateV1 {
  kind: "link-exists";
  @minLength(1)
  linkName: string;
}

/** Assert that a named relation holds after following an ontology link path. */
model ActionRelationHoldsPredicateV1 {
  kind: "relation-holds";
  @minLength(1)
  relationName: string;
  linkPath: ActionLinkPathSegmentV1[];
}

/** Conjunction. Empty input canonicalizes to true in the runtime model. */
model ActionAllPredicateV1 {
  kind: "all";
  predicates: ActionPredicateV1[];
}

/** Disjunction. Empty input canonicalizes to false in the runtime model. */
model ActionAnyPredicateV1 {
  kind: "any";
  predicates: ActionPredicateV1[];
}

/** Negation of one closed predicate. */
model ActionNotPredicateV1 {
  kind: "not";
  predicate: ActionPredicateV1;
}

/**
 * An explicitly opaque predicate evaluated by a stable registered key.
 * Description text is deliberately absent: it is presentation metadata on the
 * surrounding requirement or guarantee and is not part of semantic identity.
 */
model ActionCustomPredicateV1 {
  kind: "custom";
  @minLength(1)
  evaluatorKey: string;
  arguments: ActionLiteralV1[];
  readSet: ActionResourceV1[];
}

/**
 * Version 1 of the immutable, closed action-predicate language. Unknown kinds
 * are invalid; consumers must never reinterpret them as custom or true.
 */
@jsonSchema
union ActionPredicateV1 {
  alwaysTrue: ActionTruePredicateV1,
  alwaysFalse: ActionFalsePredicateV1,
  propertyComparison: ActionPropertyComparisonPredicateV1,
  linkExists: ActionLinkExistsPredicateV1,
  relationHolds: ActionRelationHoldsPredicateV1,
  all: ActionAllPredicateV1,
  any: ActionAnyPredicateV1,
  not: ActionNotPredicateV1,
  custom: ActionCustomPredicateV1,
}

/** One typed hard or soft action requirement carried in schema metadata. */
model ActionRequirementV1 {
  predicate: ActionPredicateV1;
  /** Canonical display projection; consumers never parse this field. */
  expression: string;
  strength: ActionRequirementStrengthV1;
  description?: string;
}

/** One typed post-state guarantee carried in schema metadata. */
model ActionGuaranteeV1 {
  predicate: ActionPredicateV1;
  /** Canonical display projection; consumers never parse this field. */
  expression: string;
  description?: string;
}
