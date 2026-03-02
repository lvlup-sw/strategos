// =============================================================================
// <copyright file="InterfacePropertyMapping.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Descriptors;

public sealed record InterfacePropertyMapping(
    string SourcePropertyName,
    string TargetPropertyName,
    string InterfaceName);
