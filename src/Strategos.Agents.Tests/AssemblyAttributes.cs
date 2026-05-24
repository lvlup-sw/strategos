// =============================================================================
// <copyright file="AssemblyAttributes.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

// Required so Castle DynamicProxy (used by NSubstitute) can subclass internal
// generic delegates that reference internal nested test types (e.g. TestState,
// TestDto). Without this, dynamic-proxy generation throws TypeAccessException
// when the substitute is invoked with internal type arguments.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DynamicProxyGenAssembly2")]
