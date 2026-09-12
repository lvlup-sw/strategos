// =============================================================================
// Type-level assertions over the emitted Zod projection (#223).
//
// `tsc --project tsconfig.json` checks this file alongside Generated/zod, so a
// regression in the type graph fails `npm run check:zod` and the CI job that
// runs it. These are compile-time assertions with no runtime: the file exports
// types only, and nothing imports it.
//
// The property under test is not "the emitter ran". It is "a consumer that
// parses a workflow gets a STATIC type that says what the steps are". Before
// #223 every schema in a reference cycle carried `z.ZodType<unknown>`, so
// runtime validation was completely correct and `steps` inferred as `unknown[]`.
// A corpus of valid fixtures cannot detect that, because nothing about parsing
// is wrong — only the type is empty.
// =============================================================================

import type { ActionPredicateV1 } from "../Generated/zod/ActionPredicateV1.js";
import type { CheckNode } from "../Generated/zod/CheckNode.js";
import type { StepDefinition } from "../Generated/zod/StepDefinition.js";
import type { WorkflowDefinitionV1 } from "../Generated/zod/WorkflowDefinitionV1.js";

// Every helper below yields `false` on failure, never `never`. That distinction
// is the whole reason these assertions bite: an unused type alias that evaluates
// to `never` is not an error, and `never extends true` HOLDS, so the obvious
// `Assert<T extends true>` over a `never`-on-failure helper passes vacuously.
// `false extends true` does not hold, so a wrong answer is a compile error.

/** The constraint that turns a computed `false` into a build failure. */
type Assert<T extends true> = T;

/** `true` when `T` and `U` are mutually assignable, `false` otherwise. */
type MutuallyAssignable<T, U> = [T] extends [U] ? ([U] extends [T] ? true : false) : false;

/** `false` for `unknown`, so an assertion against it cannot pass vacuously. */
type NotUnknown<T> = unknown extends T ? false : true;

type ElementOf<T> = NonNullable<T> extends ReadonlyArray<infer E> ? E : never;

// --- The headline: a workflow's steps carry the five-arm union, not `unknown`.
type Steps = ElementOf<WorkflowDefinitionV1["steps"]>;
export type StepsAreNotUnknown = Assert<NotUnknown<Steps>>;
export type StepsAreTheStepUnion = Assert<MutuallyAssignable<Steps, StepDefinition>>;

// --- The discriminant is reachable, so a consumer can narrow on it.
export type StepKindsAreExhaustive = Assert<
  MutuallyAssignable<Steps["kind"], "skill" | "handler" | "gate" | "delegate" | "approval">
>;

// --- The other two cycles resolve to real types as well.
export type PredicateIsNotUnknown = Assert<NotUnknown<ActionPredicateV1>>;
export type CheckNodeIsNotUnknown = Assert<NotUnknown<CheckNode>>;

// --- A cyclic member reached THROUGH a non-cyclic document keeps its type.
type Configuration = NonNullable<Extract<StepDefinition, { kind: "skill" }>["configuration"]>;
export type NestedConfigurationIsNotUnknown = Assert<NotUnknown<Configuration>>;

// Key-keyed checks keep both recursive payloads and scoped restrictions visible.
export type CheckConjunctionChildren = Assert<MutuallyAssignable<
  Extract<CheckNode, { "all-of": unknown }>["all-of"][number], CheckNode
>>;
export type CheckScopeChild = Assert<MutuallyAssignable<
  Extract<CheckNode, { scope: unknown }>["node"], CheckNode
>>;
export type CheckScopeGlob = Assert<MutuallyAssignable<
  Extract<CheckNode, { scope: unknown }>["scope"]["file-glob"], string | undefined
>>;
export type CheckLeafKinds = Assert<MutuallyAssignable<
  Extract<CheckNode, { kind: unknown }>["kind"], "grep" | "structural" | "heuristic"
>>;
