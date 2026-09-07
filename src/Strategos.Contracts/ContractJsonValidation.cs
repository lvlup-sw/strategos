// =============================================================================
// <copyright file="ContractJsonValidation.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts;

/// <summary>Shared AOT-safe guards called by generated JSON callbacks.</summary>
internal static class ContractJsonValidation
{
    internal static void RequireNotNull<T>(T? value, string propertyName)
        where T : class
    {
        if (value is null)
        {
            throw new JsonException($"Required contract property '{propertyName}' cannot be null.");
        }
    }

    internal static void RequireNonWhitespace(
        string? value,
        string propertyName,
        bool required)
    {
        if (value is null)
        {
            if (required)
            {
                throw new JsonException(
                    $"Required contract property '{propertyName}' cannot be null.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(
                $"Contract property '{propertyName}' must contain at least one non-whitespace character.");
        }
    }

    internal static void RequireEnumTypeName(
        bool isEnum,
        string? value,
        string propertyName)
    {
        if (isEnum && string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(
                $"Contract property '{propertyName}' is required and cannot be blank for an enum property.");
        }

        if (!isEnum && value is not null)
        {
            throw new JsonException(
                $"Contract property '{propertyName}' must be absent for a non-enum property.");
        }
    }

    internal static void RequireNoNullElements<T>(
        IReadOnlyList<T>? values,
        string propertyName)
        where T : class
    {
        if (values is null)
        {
            throw new JsonException($"Required contract property '{propertyName}' cannot be null.");
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is null)
            {
                throw new JsonException(
                    $"Contract property '{propertyName}' cannot contain null at index {index}.");
            }
        }
    }
}
