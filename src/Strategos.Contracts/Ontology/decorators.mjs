import { getDoc } from "@typespec/compiler";
import { setExtension } from "@typespec/json-schema";

export const namespace = "LevelUp.Strategos.Ontology";

function metadataTarget(context, operation) {
  for (const property of operation.parameters.properties.values()) {
    if (property.type.kind === "Model" && property.type.name) {
      return property.type;
    }
  }

  if (operation.returnType?.kind === "Model" && operation.returnType.name) {
    return operation.returnType;
  }

  context.program.reportDiagnostic({
    code: "contract-action-requires-model",
    severity: "error",
    message: `Ontology operation '${operation.name}' must accept or return a named model.`,
    target: operation,
  });

  return operation.parameters;
}

function extend(context, operation, key, value) {
  setExtension(context.program, metadataTarget(context, operation), key, value);
}

export function $objectKind(context, operation, domainName, objectName, kind) {
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
  extend(context, operation, "x-strategos-authority", name);
}

export function $relation(context, operation, name, ...linkPath) {
  extend(context, operation, "x-strategos-relation", name);
  extend(context, operation, "x-strategos-link-path", linkPath);
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
