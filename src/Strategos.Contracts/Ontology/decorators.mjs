import { createTypeSpecLibrary, getDoc, paramMessage } from "@typespec/compiler";
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
  },
});

const { reportDiagnostic } = $lib;
const metadataOwners = new WeakMap();

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

  const existingOperation = metadataOwners.get(target);
  if (existingOperation !== undefined && existingOperation !== operation.name) {
    reportDiagnostic(context.program, {
      code: "contract-action-shared-model",
      format: {
        operationName: operation.name,
        existingOperationName: existingOperation,
      },
      target: operation,
    });
    return;
  }

  metadataOwners.set(target, operation.name);
  setExtension(context.program, target, key, value);
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
