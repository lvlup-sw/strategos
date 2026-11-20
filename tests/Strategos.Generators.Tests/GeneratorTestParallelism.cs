// -----------------------------------------------------------------------
// <copyright file="GeneratorTestParallelism.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Strategos.Generators.Tests.GeneratorTestParallelism>]

namespace Strategos.Generators.Tests;

/// <summary>
/// Limits simultaneous Roslyn compilations during instrumented test runs.
/// </summary>
public sealed class GeneratorTestParallelism : IParallelLimit
{
    /// <inheritdoc />
    public int Limit => 8;
}
