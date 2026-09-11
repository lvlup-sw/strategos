import { createTypeSpecLibrary, getDoc, getTypeName, paramMessage } from "@typespec/compiler";
import { setExtension } from "@typespec/json-schema";

export const namespace = "LevelUp.Strategos.Ontology";

export const $lib = createTypeSpecLibrary({
  name: "@lvlup-sw/strategos-contracts",
  diagnostics: {
    "contract-action-requires-model": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operation '${"operationName"}' must accept or return a named model.`,
      },
    },
    "contract-action-shared-model": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operations '${"operationName"}' and '${"existingOperationName"}' cannot share the same metadata model.`,
      },
    },
    "contract-action-relation-path-segment": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operation '${"operationName"}' relation link-path segment ${"segmentIndex"} must be non-blank.`,
      },
    },
    "contract-action-semantic-name": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operation '${"operationName"}' ${"fieldName"} must be non-blank.`,
      },
    },
    "contract-action-subject": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operation '${"operationName"}' has action metadata but no @objectKind subject.`,
      },
    },
    "contract-action-duplicate-identity": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operations '${"operationName"}' and '${"existingOperationName"}' declare the same action identity '${"actionIdentity"}'.`,
      },
    },
    "contract-references-collection": {
      severity: "error",
      messages: {
        default: paramMessage`Property '${"propertyName"}' declares @references into '${"collection"}', which is not a root-anchored JSON Pointer (for example '/gates').`,
      },
    },
    "contract-references-id-field": {
      severity: "error",
      messages: {
        default: paramMessage`Property '${"propertyName"}' declares @references with id field '${"idField"}', which is not a member name.`,
      },
    },
    "contract-references-target-type": {
      severity: "error",
      messages: {
        default: paramMessage`Property '${"propertyName"}' declares @references but is typed '${"typeName"}'. A reference is a string moniker (INV-8).`,
      },
    },
    "contract-action-subject-kind": {
      severity: "error",
      messages: {
        default: paramMessage`Ontology operations '${"operationName"}' and '${"existingOperationName"}' declare conflicting object kinds '${"objectKind"}' and '${"existingObjectKind"}' for action subject '${"actionSubject"}'.`,
      },
    },
  },
});

const { reportDiagnostic } = $lib;
const metadataOwners = new WeakMap();
const metadataArrays = new WeakMap();
const actionMetadataByProgram = new WeakMap();
const actionIdentityOwnersByProgram = new WeakMap();
const actionSubjectKindsByProgram = new WeakMap();

function trackActionIdentity(context, operation, domainName, objectName) {
  let identities = actionIdentityOwnersByProgram.get(context.program);
  if (identities === undefined) {
    identities = new Map();
    actionIdentityOwnersByProgram.set(context.program, identities);
  }

  const identity = JSON.stringify([domainName, objectName, operation.name]);
  const existingOperation = identities.get(identity);
  if (existingOperation !== undefined && existingOperation !== operation) {
    reportDiagnostic(context.program, {
      code: "contract-action-duplicate-identity",
      format: {
        operationName: getTypeName(operation),
        existingOperationName: getTypeName(existingOperation),
        actionIdentity: `${domainName}/${objectName}.${operation.name}`,
      },
      target: operation,
    });
    return;
  }

  identities.set(identity, operation);
}

function trackActionSubjectKind(context, operation, domainName, objectName, objectKind) {
  let subjects = actionSubjectKindsByProgram.get(context.program);
  if (subjects === undefined) {
    subjects = new Map();
    actionSubjectKindsByProgram.set(context.program, subjects);
  }

  const subject = JSON.stringify([domainName, objectName]);
  const normalizedObjectKind = objectKind.toLowerCase();
  const existing = subjects.get(subject);
  if (existing !== undefined && existing.objectKind !== normalizedObjectKind) {
    reportDiagnostic(context.program, {
      code: "contract-action-subject-kind",
      format: {
        operationName: getTypeName(operation),
        existingOperationName: getTypeName(existing.operation),
        objectKind,
        existingObjectKind: existing.sourceObjectKind,
        actionSubject: `${domainName}/${objectName}`,
      },
      target: operation,
    });
    return;
  }

  subjects.set(subject, {
    operation,
    objectKind: normalizedObjectKind,
    sourceObjectKind: objectKind,
  });
}

function trackActionMetadata(program, operation, key) {
  let operations = actionMetadataByProgram.get(program);
  if (operations === undefined) {
    operations = new Map();
    actionMetadataByProgram.set(program, operations);
  }

  let keys = operations.get(operation);
  if (keys === undefined) {
    keys = new Set();
    operations.set(operation, keys);
  }

  keys.add(key);
}

function metadataTarget(context, operation) {
  for (const property of operation.parameters.properties.values()) {
    if (property.type.kind === "Model" && property.type.name) {
      return property.type;
    }
  }

  if (operation.returnType?.kind === "Model" && operation.returnType.name) {
    return operation.returnType;
  }

  reportDiagnostic(context.program, {
    code: "contract-action-requires-model",
    format: { operationName: operation.name },
    target: operation,
  });

  return undefined;
}

function extend(context, operation, key, value) {
  const target = metadataTarget(context, operation);
  if (target === undefined) {
    return;
  }

  trackActionMetadata(context.program, operation, key);

  const existingOperation = metadataOwners.get(target);
  if (existingOperation !== undefined && existingOperation !== operation) {
    reportDiagnostic(context.program, {
      code: "contract-action-shared-model",
      format: {
        operationName: getTypeName(operation),
        existingOperationName: getTypeName(existingOperation),
      },
      target: operation,
    });
    return;
  }

  metadataOwners.set(target, operation);
  setExtension(context.program, target, key, value);
}

function append(context, operation, key, value) {
  const target = metadataTarget(context, operation);
  if (target === undefined) {
    return;
  }

  trackActionMetadata(context.program, operation, key);

  const existingOperation = metadataOwners.get(target);
  if (existingOperation !== undefined && existingOperation !== operation) {
    reportDiagnostic(context.program, {
      code: "contract-action-shared-model",
      format: {
        operationName: getTypeName(operation),
        existingOperationName: getTypeName(existingOperation),
      },
      target: operation,
    });
    return;
  }

  metadataOwners.set(target, operation);
  let targetArrays = metadataArrays.get(target);
  if (targetArrays === undefined) {
    targetArrays = new Map();
    metadataArrays.set(target, targetArrays);
  }

  let values = targetArrays.get(key);
  if (values === undefined) {
    values = [];
    targetArrays.set(key, values);
    setExtension(context.program, target, key, values);
  }

  values.push(value);
}

function contractValue(value) {
  if (Array.isArray(value)) {
    return value.map(contractValue);
  }

  if (value?.valueKind === "EnumValue") {
    return value.value.value ?? value.value.name;
  }

  if (value?.kind === "EnumMember") {
    return value.value ?? value.name;
  }

  if (value !== null && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, member]) => [key, contractValue(member)]),
    );
  }

  return value;
}

const scalarKindIndex = new Map([
  ["boolean", 0],
  ["integer", 1],
  ["decimal", 2],
  ["string", 3],
  ["enum", 4],
  ["symbol", 5],
]);
const literalKindIndex = new Map([
  ["null", 0],
  ["boolean", 1],
  ["integer", 2],
  ["decimal", 3],
  ["string", 4],
  ["enum", 5],
  ["symbol", 6],
]);
const operatorIndex = new Map([
  ["equal", 0],
  ["not-equal", 1],
  ["less-than", 2],
  ["less-than-or-equal", 3],
  ["greater-than", 4],
  ["greater-than-or-equal", 5],
]);
const resourceKindIndex = new Map([
  ["property", 0],
  ["link", 1],
  ["event", 2],
  ["external", 3],
]);

function segment(value) {
  return `${value.length}:${value}`;
}

function compareOrdinal(left, right) {
  return left < right ? -1 : left > right ? 1 : 0;
}

// Keep the display projection byte-for-byte aligned with
// PredicateLiteral.Quote in the runtime model. This intentionally escapes
// only the five characters used by that canonical display format.
function quote(value) {
  let result = '"';
  for (const character of value) {
    switch (character) {
      case "\\": result += "\\\\"; break;
      case '"': result += '\\"'; break;
      case "\n": result += "\\n"; break;
      case "\r": result += "\\r"; break;
      case "\t": result += "\\t"; break;
      default: result += character; break;
    }
  }
  return `${result}"`;
}

function literalCanonicalValue(literal) {
  if (literal.kind === "null") {
    return "";
  }
  if (literal.kind === "boolean") {
    return literal.value ? "true" : "false";
  }
  if (literal.kind === "enum") {
    return literal.memberName;
  }
  return literal.value;
}

function literalToken(literal) {
  const typeName = literal.kind === "enum" ? literal.typeName : "";
  return `literal:${literalKindIndex.get(literal.kind)}:${segment(typeName)}:${segment(literalCanonicalValue(literal))}`;
}

function propertyToken(property) {
  return `property:${segment(property.name)}:${scalarKindIndex.get(property.scalarKind)}:${property.isNullable ? 1 : 0}:${segment(property.enumTypeName ?? "")}`;
}

function resourceToken(resource) {
  return `${resourceKindIndex.get(resource.kind)}:${segment(resource.name)}`;
}

function predicateToken(predicate) {
  switch (predicate.kind) {
    case "true": return "constant:1";
    case "false": return "constant:0";
    case "property-comparison":
      return `comparison:${segment(propertyToken(predicate.property))}:${operatorIndex.get(predicate.operator)}:${segment(literalToken(predicate.value))}`;
    case "link-exists": return `link-exists:${segment(predicate.linkName)}`;
    case "relation-holds":
      return `relation:${segment(predicate.relationName)}:${predicate.linkPath.map(segment).join("")}`;
    case "all":
    case "any":
      return `${predicate.kind}:${predicate.predicates.map((item) => segment(predicateToken(item))).join("")}`;
    case "not": return `not:${segment(predicateToken(predicate.predicate))}`;
    case "custom":
      return `custom:${segment(predicate.evaluatorKey)}:${predicate.arguments.map((item) => segment(literalToken(item))).join("")}:${predicate.readSet.map(resourceToken).join("")}`;
    default: throw new Error(`Unknown action predicate kind '${predicate.kind}'.`);
  }
}

function normalizePredicate(predicate) {
  if (predicate.kind === "not") {
    const operand = normalizePredicate(predicate.predicate);
    if (operand.kind === "true") return { kind: "false" };
    if (operand.kind === "false") return { kind: "true" };
    if (operand.kind === "not") return operand.predicate;
    return { kind: "not", predicate: operand };
  }

  if (predicate.kind === "all" || predicate.kind === "any") {
    const all = predicate.kind === "all";
    const flattened = [];
    for (const item of predicate.predicates.map(normalizePredicate)) {
      if ((all && item.kind === "false") || (!all && item.kind === "true")) {
        return { kind: all ? "false" : "true" };
      }
      if ((all && item.kind === "true") || (!all && item.kind === "false")) {
        continue;
      }
      if (item.kind === predicate.kind) flattened.push(...item.predicates);
      else flattened.push(item);
    }

    const unique = new Map(flattened.map((item) => [predicateToken(item), item]));
    const operands = [...unique.entries()]
      .sort(([left], [right]) => compareOrdinal(left, right))
      .map(([, item]) => item);
    if (operands.length === 0) return { kind: all ? "true" : "false" };
    if (operands.length === 1) return operands[0];
    return { kind: predicate.kind, predicates: operands };
  }

  if (predicate.kind === "custom") {
    const resources = new Map(predicate.readSet.map((item) => [resourceToken(item), item]));
    return {
      ...predicate,
      readSet: [...resources.entries()]
        .sort(([left], [right]) => compareOrdinal(left, right))
        .map(([, item]) => item),
    };
  }

  return predicate;
}

function literalExpression(literal) {
  switch (literal.kind) {
    case "null": return "null";
    case "boolean": return literal.value ? "true" : "false";
    case "integer":
    case "decimal": return literal.value;
    case "string": return quote(literal.value);
    case "enum": return `${literal.typeName}.${literal.memberName}`;
    case "symbol": return `symbol(${quote(literal.value)})`;
    default: throw new Error(`Unknown action literal kind '${literal.kind}'.`);
  }
}

function predicateExpression(predicate) {
  switch (predicate.kind) {
    case "true": return "true";
    case "false": return "false";
    case "property-comparison": {
      const operator = new Map([
        ["equal", "=="],
        ["not-equal", "!="],
        ["less-than", "<"],
        ["less-than-or-equal", "<="],
        ["greater-than", ">"],
        ["greater-than-or-equal", ">="],
      ]).get(predicate.operator);
      return `${predicate.property.name} ${operator} ${literalExpression(predicate.value)}`;
    }
    case "link-exists": return `link(${quote(predicate.linkName)}) exists`;
    case "relation-holds":
      return `principal -[${predicate.relationName}]-> ${predicate.linkPath.length === 0 ? "target" : predicate.linkPath.join("/")}`;
    case "all": return `(${predicate.predicates.map(predicateExpression).join(" && ")})`;
    case "any": return `(${predicate.predicates.map(predicateExpression).join(" || ")})`;
    case "not": return `!(${predicateExpression(predicate.predicate)})`;
    case "custom":
      return `custom(${quote(predicate.evaluatorKey)}${predicate.arguments.length === 0 ? "" : `, ${predicate.arguments.map(literalExpression).join(", ")}`})`;
    default: throw new Error(`Unknown action predicate kind '${predicate.kind}'.`);
  }
}

function validateSemanticName(context, operation, fieldName, value) {
  if (typeof value === "string" && value.trim().length > 0) {
    return true;
  }

  reportDiagnostic(context.program, {
    code: "contract-action-semantic-name",
    format: {
      operationName: operation.name,
      fieldName,
    },
    target: operation,
  });
  return false;
}

function validatePredicateSemanticNames(context, operation, predicate) {
  switch (predicate.kind) {
    case "property-comparison": {
      let valid = validateSemanticName(
        context,
        operation,
        "property name",
        predicate.property.name,
      );
      if (predicate.property.scalarKind === "enum") {
        valid = validateSemanticName(
          context,
          operation,
          "enum property type name",
          predicate.property.enumTypeName,
        ) && valid;
      }
      if (predicate.value.kind === "enum") {
        valid = validateSemanticName(
          context,
          operation,
          "enum literal type name",
          predicate.value.typeName,
        ) && valid;
        valid = validateSemanticName(
          context,
          operation,
          "enum literal member name",
          predicate.value.memberName,
        ) && valid;
      } else if (predicate.value.kind === "symbol") {
        valid = validateSemanticName(
          context,
          operation,
          "symbol literal",
          predicate.value.value,
        ) && valid;
      }
      return valid;
    }
    case "link-exists":
      return validateSemanticName(context, operation, "link name", predicate.linkName);
    case "relation-holds":
      return validateSemanticName(context, operation, "relation name", predicate.relationName);
    case "all":
    case "any":
      return predicate.predicates.every(
        (operand) => validatePredicateSemanticNames(context, operation, operand),
      );
    case "not":
      return validatePredicateSemanticNames(context, operation, predicate.predicate);
    case "custom": {
      let valid = validateSemanticName(
        context,
        operation,
        "custom evaluator key",
        predicate.evaluatorKey,
      );
      for (const resource of predicate.readSet) {
        valid = validateSemanticName(
          context,
          operation,
          `${resource.kind} resource name`,
          resource.name,
        ) && valid;
      }
      for (const argument of predicate.arguments) {
        if (argument.kind === "enum") {
          valid = validateSemanticName(
            context,
            operation,
            "enum argument type name",
            argument.typeName,
          ) && valid;
          valid = validateSemanticName(
            context,
            operation,
            "enum argument member name",
            argument.memberName,
          ) && valid;
        } else if (argument.kind === "symbol") {
          valid = validateSemanticName(
            context,
            operation,
            "symbol argument",
            argument.value,
          ) && valid;
        }
      }
      return valid;
    }
    default:
      return true;
  }
}

function validateRelationPathSegments(context, operation, predicate) {
  if (predicate.kind === "relation-holds") {
    const invalidSegmentIndex = predicate.linkPath.findIndex(
      (pathSegment) => typeof pathSegment !== "string" || pathSegment.trim().length === 0,
    );
    if (invalidSegmentIndex >= 0) {
      reportDiagnostic(context.program, {
        code: "contract-action-relation-path-segment",
        format: {
          operationName: operation.name,
          segmentIndex: invalidSegmentIndex.toString(),
        },
        target: operation,
      });
      return false;
    }
  }

  if (predicate.kind === "all" || predicate.kind === "any") {
    return predicate.predicates.every(
      (operand) => validateRelationPathSegments(context, operation, operand),
    );
  }

  if (predicate.kind === "not") {
    return validateRelationPathSegments(context, operation, predicate.predicate);
  }

  return true;
}

export function $objectKind(context, operation, domainName, objectName, kind) {
  let valid = validateSemanticName(context, operation, "domain name", domainName);
  valid = validateSemanticName(context, operation, "object name", objectName) && valid;
  valid = validateSemanticName(context, operation, "object kind", kind) && valid;
  if (!valid) {
    return;
  }

  trackActionSubjectKind(context, operation, domainName, objectName, kind);
  trackActionIdentity(context, operation, domainName, objectName);
  extend(context, operation, "x-strategos-action-name", operation.name);
  extend(
    context,
    operation,
    "x-strategos-action-description",
    getDoc(context.program, operation) ?? operation.name,
  );
  extend(context, operation, "x-strategos-domain", domainName);
  extend(context, operation, "x-strategos-object", objectName);
  extend(context, operation, "x-strategos-object-kind", kind);
}

export function $authority(context, operation, name) {
  if (!validateSemanticName(context, operation, "authority name", name)) {
    return;
  }

  extend(context, operation, "x-strategos-authority", name);
}

export function $relation(context, operation, name, ...linkPath) {
  const predicate = {
    kind: "relation-holds",
    relationName: name,
    linkPath,
  };
  if (!validateSemanticName(context, operation, "relation name", name)
      || !validateRelationPathSegments(context, operation, predicate)) {
    return;
  }

  const normalized = normalizePredicate(predicate);
  append(context, operation, "x-strategos-requires-v1", {
    strength: "hard",
    description: `Requires relation '${name}'.`,
    predicate: normalized,
    expression: predicateExpression(normalized),
  });
}

export function $requires(context, operation, predicate, strength, description) {
  const value = contractValue(predicate);
  if (!validatePredicateSemanticNames(context, operation, value)
      || !validateRelationPathSegments(context, operation, value)) {
    return;
  }

  const normalized = normalizePredicate(value);
  const requirement = {
    predicate: normalized,
    expression: predicateExpression(normalized),
    strength: strength === undefined ? "hard" : contractValue(strength),
  };
  if (description !== undefined) {
    requirement.description = description;
  }

  append(context, operation, "x-strategos-requires-v1", requirement);
}

export function $ensures(context, operation, predicate, description) {
  const value = contractValue(predicate);
  if (!validatePredicateSemanticNames(context, operation, value)
      || !validateRelationPathSegments(context, operation, value)) {
    return;
  }

  const normalized = normalizePredicate(value);
  const guarantee = {
    predicate: normalized,
    expression: predicateExpression(normalized),
  };
  if (description !== undefined) {
    guarantee.description = description;
  }

  append(context, operation, "x-strategos-ensures-v1", guarantee);
}

export function $clients(context, operation, ...names) {
  extend(context, operation, "x-strategos-clients", names);
}

export function $confirm(context, operation, required) {
  extend(context, operation, "x-strategos-confirm", required);
}

export function $readOnly(context, operation) {
  extend(context, operation, "x-strategos-read-only", true);
}

export function $idempotent(context, operation) {
  extend(context, operation, "x-strategos-idempotent", true);
}

// =============================================================================
// @references (#219) — the referential rule core JSON Schema cannot express.
//
// Declared in Workflow/references.tsp, bound to LevelUp.Strategos.Contracts (the
// namespace the workflow models live in) through the $decorators export below,
// so a workflow model reaches it without a `using` clause.
//
// The decorator does two things: it rejects bad authoring here, at tsp compile
// time, and it emits `x-strategos-references-v1` onto the PROPERTY schema for
// the Zod emitter to lower. It deliberately does not weaken the emitted JSON
// Schema: an `x-strategos-*` key is inert to a validator that does not read it.
// =============================================================================

// A root-anchored JSON Pointer over member names: `/gates`, `/steps`,
// `/spec/tasks`. Not a relative pointer, not an array index, not a blank
// segment — the emitter resolves it against a document root.
const REFERENCE_COLLECTION_POINTER = /^(?:\/[A-Za-z_][A-Za-z0-9_]*)+$/;
const REFERENCE_MEMBER_NAME = /^[A-Za-z_][A-Za-z0-9_]*$/;

/** True when `type` is `string` or a scalar deriving from it. */
function isStringScalar(type) {
  let current = type;
  while (current !== undefined && current !== null && current.kind === "Scalar") {
    if (current.name === "string") {
      return true;
    }
    current = current.baseScalar;
  }
  return false;
}

function referencesDecorator(context, property, collection, idField) {
  let valid = true;

  if (typeof collection !== "string" || !REFERENCE_COLLECTION_POINTER.test(collection)) {
    reportDiagnostic(context.program, {
      code: "contract-references-collection",
      format: {
        propertyName: property.name,
        collection: typeof collection === "string" ? collection : String(collection),
      },
      target: property,
    });
    valid = false;
  }

  if (typeof idField !== "string" || !REFERENCE_MEMBER_NAME.test(idField)) {
    reportDiagnostic(context.program, {
      code: "contract-references-id-field",
      format: {
        propertyName: property.name,
        idField: typeof idField === "string" ? idField : String(idField),
      },
      target: property,
    });
    valid = false;
  }

  // A reference is a string moniker, never a typed handle (INV-8). Rejecting the
  // type here is what keeps the emitter total: it lowers one shape, and cannot
  // meet a reference it has no lowering for.
  if (!isStringScalar(property.type)) {
    reportDiagnostic(context.program, {
      code: "contract-references-target-type",
      format: {
        propertyName: property.name,
        typeName: getTypeName(property.type),
      },
      target: property,
    });
    valid = false;
  }

  if (!valid) {
    return;
  }

  setExtension(context.program, property, "x-strategos-references-v1", {
    collection,
    idField,
  });
}

export const $decorators = {
  "LevelUp.Strategos.Contracts": {
    references: referencesDecorator,
  },
};

export function $onValidate(program) {
  const operations = actionMetadataByProgram.get(program);
  if (operations === undefined) {
    return;
  }

  for (const [operation, keys] of operations) {
    if (!keys.has("x-strategos-action-name")) {
      reportDiagnostic(program, {
        code: "contract-action-subject",
        format: { operationName: getTypeName(operation) },
        target: operation,
      });
    }
  }
}
