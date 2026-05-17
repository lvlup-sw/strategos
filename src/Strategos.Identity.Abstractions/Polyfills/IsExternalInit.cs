// -----------------------------------------------------------------------
// <copyright file="IsExternalInit.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for the init-only-setter marker. Required for C# records on netstandard2.0.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
