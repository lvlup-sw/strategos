// =============================================================================
// <copyright file="Program.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts.Codegen;

// Usage: ContractsCodegen <schemas-dir> <output-dir>
// Reads every *.json JSON Schema in <schemas-dir> and emits one sealed record
// per schema into <output-dir>, in the shape mandated by INV-6 (sealed record)
// and INV-7 ({ get; init; } + IReadOnlyList<T> collections).
//
// Emitter decision (T3): the native TypeSpec C# emitter
// (@typespec/http-client-csharp) is an HTTP *client* generator — it requires
// @typespec/http operation definitions and emits mutable model classes with
// { get; set; }. It cannot produce sealed/init-only/IReadOnlyList records from
// plain @jsonSchema data models. We therefore generate from the emitted JSON
// Schema using NJsonSchema (parsing + $ref resolution) with a template tuned
// to the exact INV-6/INV-7 shape.

if (args.Length != 2)
{
    await Console.Error.WriteLineAsync("usage: ContractsCodegen <schemas-dir> <output-dir>").ConfigureAwait(false);
    return 2;
}

return await RecordEmitter.RunAsync(schemasDir: args[0], outputDir: args[1]).ConfigureAwait(false);
