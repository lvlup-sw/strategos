// -----------------------------------------------------------------------
// <copyright file="MinimalJsonReader.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

namespace Strategos.Generators.Import;

// =============================================================================
// DR-12 (#100) — vendored, dependency-free JSON reader for the import front-end.
//
// Strategos.Generators is an isolated netstandard2.0 analyzer with ZERO analyzer
// package dependencies: it cannot use System.Text.Json (net-current) nor take a
// Newtonsoft.Json package reference. This is a small, self-contained
// recursive-descent JSON parser producing a generic node tree
// (<see cref="JsonValue"/>), plus a binder (<see cref="WireWorkflowReader"/>)
// that maps that tree onto the hand-authored wire-DTO twins in WireDtos.cs.
//
// It is intentionally minimal: it parses the JSON grammar (RFC 8259 objects,
// arrays, strings with escapes, numbers, booleans, null) and binds the
// WorkflowDefinitionV1 shape. Semantic validation (schema conformance, moniker
// resolution, DR-14 rejection) is the bridge's job, not the reader's — malformed
// syntax surfaces as a <see cref="JsonParseException"/> the bridge turns into a
// stable build diagnostic (never a generator crash).
// =============================================================================

/// <summary>The kind of a parsed JSON node.</summary>
internal enum JsonKind
{
    /// <summary>A JSON object (<c>{ ... }</c>).</summary>
    Object,

    /// <summary>A JSON array (<c>[ ... ]</c>).</summary>
    Array,

    /// <summary>A JSON string.</summary>
    String,

    /// <summary>A JSON number.</summary>
    Number,

    /// <summary>A JSON boolean.</summary>
    Boolean,

    /// <summary>A JSON <c>null</c>.</summary>
    Null,
}

/// <summary>
/// Thrown when the input is not well-formed JSON. The import bridge catches this
/// and reports a stable build diagnostic rather than letting the generator crash.
/// </summary>
[Serializable]
internal sealed class JsonParseException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="JsonParseException"/> class.</summary>
    /// <param name="message">The parse-failure message.</param>
    public JsonParseException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A parsed JSON node. Immutable; produced by <see cref="MinimalJsonReader.Parse"/>.
/// </summary>
internal sealed class JsonValue
{
    private static readonly IReadOnlyDictionary<string, JsonValue> EmptyMembers =
        new Dictionary<string, JsonValue>(0);

    private static readonly IReadOnlyList<JsonValue> EmptyItems = new List<JsonValue>(0);

    private readonly Dictionary<string, JsonValue>? _members;
    private readonly List<JsonValue>? _items;
    private readonly string? _text;
    private readonly bool _boolean;

    private JsonValue(
        JsonKind kind,
        Dictionary<string, JsonValue>? members = null,
        List<JsonValue>? items = null,
        string? text = null,
        bool boolean = false)
    {
        this.Kind = kind;
        this._members = members;
        this._items = items;
        this._text = text;
        this._boolean = boolean;
    }

    /// <summary>Gets the kind of this node.</summary>
    public JsonKind Kind { get; }

    /// <summary>Gets the object members (empty when this node is not an object).</summary>
    public IReadOnlyDictionary<string, JsonValue> Members => this._members ?? EmptyMembers;

    /// <summary>Gets the array items (empty when this node is not an array).</summary>
    public IReadOnlyList<JsonValue> Items => this._items ?? EmptyItems;

    /// <summary>Creates an object node.</summary>
    /// <param name="members">The object members.</param>
    /// <returns>An object <see cref="JsonValue"/>.</returns>
    public static JsonValue NewObject(Dictionary<string, JsonValue> members) =>
        new JsonValue(JsonKind.Object, members: members);

    /// <summary>Creates an array node.</summary>
    /// <param name="items">The array items.</param>
    /// <returns>An array <see cref="JsonValue"/>.</returns>
    public static JsonValue NewArray(List<JsonValue> items) =>
        new JsonValue(JsonKind.Array, items: items);

    /// <summary>Creates a string node.</summary>
    /// <param name="value">The string value.</param>
    /// <returns>A string <see cref="JsonValue"/>.</returns>
    public static JsonValue NewString(string value) =>
        new JsonValue(JsonKind.String, text: value);

    /// <summary>Creates a number node from its raw lexeme.</summary>
    /// <param name="raw">The raw number lexeme.</param>
    /// <returns>A number <see cref="JsonValue"/>.</returns>
    public static JsonValue NewNumber(string raw) =>
        new JsonValue(JsonKind.Number, text: raw);

    /// <summary>Creates a boolean node.</summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>A boolean <see cref="JsonValue"/>.</returns>
    public static JsonValue NewBoolean(bool value) =>
        new JsonValue(JsonKind.Boolean, boolean: value);

    /// <summary>Creates a null node.</summary>
    /// <returns>A null <see cref="JsonValue"/>.</returns>
    public static JsonValue NewNull() => new JsonValue(JsonKind.Null);

    /// <summary>Attempts to get a member of an object node.</summary>
    /// <param name="name">The member name.</param>
    /// <param name="value">The member value, when present.</param>
    /// <returns><see langword="true"/> when the member exists.</returns>
    public bool TryGetMember(string name, out JsonValue value)
    {
        if (this._members is not null && this._members.TryGetValue(name, out var found))
        {
            value = found;
            return true;
        }

        value = NewNull();
        return false;
    }

    /// <summary>Gets the string payload, or <see langword="null"/> when this node is not a string.</summary>
    /// <returns>The string value or <see langword="null"/>.</returns>
    public string? AsStringOrNull() => this.Kind == JsonKind.String ? this._text : null;

    /// <summary>Gets the boolean payload, or <see langword="null"/> when this node is not a boolean.</summary>
    /// <returns>The boolean value or <see langword="null"/>.</returns>
    public bool? AsBoolOrNull() => this.Kind == JsonKind.Boolean ? this._boolean : (bool?)null;

    /// <summary>Gets the value as an <see cref="int"/>, or <see langword="null"/> when this node is not a number.</summary>
    /// <returns>The integer value or <see langword="null"/>.</returns>
    public int? AsIntOrNull()
    {
        if (this.Kind != JsonKind.Number || this._text is null)
        {
            return null;
        }

        if (int.TryParse(this._text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            return i;
        }

        if (long.TryParse(this._text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return unchecked((int)l);
        }

        // Tolerate a fractional/exponential lexeme in an integer slot (e.g. "5.0").
        if (double.TryParse(this._text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return unchecked((int)d);
        }

        return null;
    }

    /// <summary>Gets the value as a <see cref="double"/>, or <see langword="null"/> when this node is not a number.</summary>
    /// <returns>The double value or <see langword="null"/>.</returns>
    public double? AsDoubleOrNull()
    {
        if (this.Kind != JsonKind.Number || this._text is null)
        {
            return null;
        }

        return double.TryParse(this._text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : (double?)null;
    }
}

/// <summary>
/// A minimal, dependency-free JSON parser (RFC 8259) producing a
/// <see cref="JsonValue"/> tree. No System.Text.Json, no Newtonsoft — the isolated
/// netstandard2.0 analyzer carries zero package dependencies.
/// </summary>
internal static class MinimalJsonReader
{
    /// <summary>Parses a JSON document into a <see cref="JsonValue"/> tree.</summary>
    /// <param name="text">The JSON text.</param>
    /// <returns>The parsed root node.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="text"/> is null.</exception>
    /// <exception cref="JsonParseException">When the text is not well-formed JSON.</exception>
    public static JsonValue Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var cursor = new Cursor(text);
        cursor.SkipWhitespace();
        var value = ParseValue(ref cursor);
        cursor.SkipWhitespace();
        if (!cursor.AtEnd)
        {
            throw cursor.Error("unexpected trailing content after the JSON value");
        }

        return value;
    }

    private static JsonValue ParseValue(ref Cursor cursor)
    {
        if (cursor.AtEnd)
        {
            throw cursor.Error("unexpected end of input");
        }

        var c = cursor.Peek();
        switch (c)
        {
            case '{':
                return ParseObject(ref cursor);
            case '[':
                return ParseArray(ref cursor);
            case '"':
                return JsonValue.NewString(ParseString(ref cursor));
            case 't':
            case 'f':
                return ParseBoolean(ref cursor);
            case 'n':
                cursor.Expect("null");
                return JsonValue.NewNull();
            default:
                if (c == '-' || (c >= '0' && c <= '9'))
                {
                    return ParseNumber(ref cursor);
                }

                throw cursor.Error($"unexpected character '{c}'");
        }
    }

    private static JsonValue ParseObject(ref Cursor cursor)
    {
        cursor.Advance(); // consume '{'
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        cursor.SkipWhitespace();
        if (cursor.TryConsume('}'))
        {
            return JsonValue.NewObject(members);
        }

        while (true)
        {
            cursor.SkipWhitespace();
            if (cursor.AtEnd || cursor.Peek() != '"')
            {
                throw cursor.Error("expected a string property name");
            }

            var name = ParseString(ref cursor);
            cursor.SkipWhitespace();
            if (!cursor.TryConsume(':'))
            {
                throw cursor.Error("expected ':' after property name");
            }

            cursor.SkipWhitespace();
            members[name] = ParseValue(ref cursor); // last-writer-wins on duplicate keys
            cursor.SkipWhitespace();

            if (cursor.TryConsume(','))
            {
                continue;
            }

            if (cursor.TryConsume('}'))
            {
                break;
            }

            throw cursor.Error("expected ',' or '}' in object");
        }

        return JsonValue.NewObject(members);
    }

    private static JsonValue ParseArray(ref Cursor cursor)
    {
        cursor.Advance(); // consume '['
        var items = new List<JsonValue>();
        cursor.SkipWhitespace();
        if (cursor.TryConsume(']'))
        {
            return JsonValue.NewArray(items);
        }

        while (true)
        {
            cursor.SkipWhitespace();
            items.Add(ParseValue(ref cursor));
            cursor.SkipWhitespace();

            if (cursor.TryConsume(','))
            {
                continue;
            }

            if (cursor.TryConsume(']'))
            {
                break;
            }

            throw cursor.Error("expected ',' or ']' in array");
        }

        return JsonValue.NewArray(items);
    }

    private static JsonValue ParseBoolean(ref Cursor cursor)
    {
        if (cursor.Peek() == 't')
        {
            cursor.Expect("true");
            return JsonValue.NewBoolean(true);
        }

        cursor.Expect("false");
        return JsonValue.NewBoolean(false);
    }

    private static JsonValue ParseNumber(ref Cursor cursor)
    {
        var start = cursor.Position;

        cursor.TryConsume('-');

        if (!ConsumeDigits(ref cursor))
        {
            throw cursor.Error("expected a digit in number");
        }

        if (cursor.TryConsume('.'))
        {
            if (!ConsumeDigits(ref cursor))
            {
                throw cursor.Error("expected a digit after '.' in number");
            }
        }

        if (!cursor.AtEnd && (cursor.Peek() == 'e' || cursor.Peek() == 'E'))
        {
            cursor.Advance();
            if (!cursor.AtEnd && (cursor.Peek() == '+' || cursor.Peek() == '-'))
            {
                cursor.Advance();
            }

            if (!ConsumeDigits(ref cursor))
            {
                throw cursor.Error("expected a digit in number exponent");
            }
        }

        return JsonValue.NewNumber(cursor.Slice(start));
    }

    private static bool ConsumeDigits(ref Cursor cursor)
    {
        var any = false;
        while (!cursor.AtEnd)
        {
            var c = cursor.Peek();
            if (c < '0' || c > '9')
            {
                break;
            }

            cursor.Advance();
            any = true;
        }

        return any;
    }

    private static string ParseString(ref Cursor cursor)
    {
        cursor.Advance(); // consume opening quote
        var sb = new StringBuilder();
        while (true)
        {
            if (cursor.AtEnd)
            {
                throw cursor.Error("unterminated string");
            }

            var c = cursor.Next();
            if (c == '"')
            {
                break;
            }

            if (c == '\\')
            {
                sb.Append(ParseEscape(ref cursor));
                continue;
            }

            if (c < ' ')
            {
                throw cursor.Error("unescaped control character in string");
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static char ParseEscape(ref Cursor cursor)
    {
        if (cursor.AtEnd)
        {
            throw cursor.Error("unterminated escape sequence");
        }

        var c = cursor.Next();
        switch (c)
        {
            case '"':
                return '"';
            case '\\':
                return '\\';
            case '/':
                return '/';
            case 'b':
                return '\b';
            case 'f':
                return '\f';
            case 'n':
                return '\n';
            case 'r':
                return '\r';
            case 't':
                return '\t';
            case 'u':
                return ParseUnicodeEscape(ref cursor);
            default:
                throw cursor.Error($"invalid escape '\\{c}'");
        }
    }

    private static char ParseUnicodeEscape(ref Cursor cursor)
    {
        var code = 0;
        for (var i = 0; i < 4; i++)
        {
            if (cursor.AtEnd)
            {
                throw cursor.Error("truncated \\u escape");
            }

            var c = cursor.Next();
            int digit;
            if (c >= '0' && c <= '9')
            {
                digit = c - '0';
            }
            else if (c >= 'a' && c <= 'f')
            {
                digit = (c - 'a') + 10;
            }
            else if (c >= 'A' && c <= 'F')
            {
                digit = (c - 'A') + 10;
            }
            else
            {
                throw cursor.Error($"invalid hex digit '{c}' in \\u escape");
            }

            code = (code << 4) + digit;
        }

        return (char)code;
    }

    /// <summary>A forward-only string cursor for the parser.</summary>
    private struct Cursor
    {
        private readonly string _text;
        private int _pos;

        public Cursor(string text)
        {
            this._text = text;
            this._pos = 0;
        }

        public bool AtEnd => this._pos >= this._text.Length;

        public int Position => this._pos;

        public char Peek() => this._text[this._pos];

        public char Next() => this._text[this._pos++];

        public void Advance() => this._pos++;

        public string Slice(int start) => this._text.Substring(start, this._pos - start);

        public void SkipWhitespace()
        {
            while (this._pos < this._text.Length)
            {
                var c = this._text[this._pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    this._pos++;
                }
                else
                {
                    break;
                }
            }
        }

        public bool TryConsume(char c)
        {
            if (this._pos < this._text.Length && this._text[this._pos] == c)
            {
                this._pos++;
                return true;
            }

            return false;
        }

        public void Expect(string literal)
        {
            if (this._pos + literal.Length > this._text.Length
                || string.CompareOrdinal(this._text, this._pos, literal, 0, literal.Length) != 0)
            {
                throw this.Error($"expected '{literal}'");
            }

            this._pos += literal.Length;
        }

        public JsonParseException Error(string message) =>
            new JsonParseException($"{message} (at offset {this._pos})");
    }
}

/// <summary>
/// Binds a parsed <see cref="JsonValue"/> tree (via <see cref="MinimalJsonReader"/>)
/// onto the wire-DTO twins. This is the reader half of the DR-12 import front-end;
/// the bridge (WorkflowModel rehydration) and DR-14 rejection consume the twins it
/// produces.
/// </summary>
internal static class WireWorkflowReader
{
    /// <summary>Reads a <c>WorkflowDefinitionV1</c> JSON document into its DTO twin.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The populated <see cref="WorkflowDefinitionV1"/> twin.</returns>
    /// <exception cref="JsonParseException">When the text is malformed or the root is not an object.</exception>
    public static WorkflowDefinitionV1 Read(string json)
    {
        var root = MinimalJsonReader.Parse(json);
        if (root.Kind != JsonKind.Object)
        {
            throw new JsonParseException("the workflow document root must be a JSON object");
        }

        return new WorkflowDefinitionV1
        {
            SchemaVersion = GetString(root, "schemaVersion"),
            Name = GetString(root, "name"),
            EntryStepId = GetString(root, "entryStepId"),
            TerminalStepId = GetString(root, "terminalStepId"),
            Steps = ReadList(root, "steps", ReadStep),
            Transitions = ReadList(root, "transitions", ReadTransition),
            BranchPoints = ReadList(root, "branchPoints", ReadBranchPoint),
            Loops = ReadList(root, "loops", ReadLoop),
            ForkPoints = ReadList(root, "forkPoints", ReadForkPoint),
            FailureHandlers = ReadList(root, "failureHandlers", ReadFailureHandler),
            ApprovalPoints = ReadList(root, "approvalPoints", ReadApproval),
            Gates = ReadList(root, "gates", ReadGate),
            DiagnosticForks = ReadList(root, "diagnosticForks", ReadDiagnosticFork),
        };
    }

    private static StepDefinition ReadStep(JsonValue node)
    {
        var kind = GetString(node, "kind");
        StepDefinition step = kind switch
        {
            "skill" => new SkillStep { StepType = GetString(node, "stepType") },
            "handler" => new HandlerStep { StepType = GetString(node, "stepType") },
            "gate" => new GateStep { StepType = GetString(node, "stepType"), GateId = GetString(node, "gateId") },
            "delegate" => new DelegateStep { Lambda = GetBool(node, "lambda") ?? false },
            "approval" => new ApprovalStep { ApproverType = GetString(node, "approverType") },
            null => throw new JsonParseException("a step is missing the required 'kind' discriminator"),
            _ => throw new JsonParseException($"unknown step kind '{kind}'"),
        };

        step.Kind = kind;
        step.StepId = GetString(node, "stepId");
        step.StepName = GetString(node, "stepName");
        step.InstanceName = GetString(node, "instanceName");
        step.IsTerminal = GetBool(node, "isTerminal") ?? false;
        step.Runtime = GetString(node, "runtime");
        step.Configuration = ReadObject(node, "configuration", ReadStepConfiguration);
        return step;
    }

    private static StepConfigurationDefinition ReadStepConfiguration(JsonValue node) =>
        new StepConfigurationDefinition
        {
            ConfidenceThreshold = GetDouble(node, "confidenceThreshold"),
            OnLowConfidence = ReadObject(node, "onLowConfidence", ReadLowConfidenceHandler),
            Compensation = ReadObject(node, "compensation", ReadCompensation),
            Retry = ReadObject(node, "retry", ReadRetry),
            Timeout = GetString(node, "timeout"),
            Validation = ReadObject(node, "validation", ReadValidation),
        };

    private static LowConfidenceHandlerDefinition ReadLowConfidenceHandler(JsonValue node) =>
        new LowConfidenceHandlerDefinition
        {
            HandlerId = GetString(node, "handlerId"),
            HandlerSteps = ReadList(node, "handlerSteps", ReadStep),
            IsTerminal = GetBool(node, "isTerminal") ?? false,
            RejoinStepId = GetString(node, "rejoinStepId"),
        };

    private static CompensationConfiguration ReadCompensation(JsonValue node) =>
        new CompensationConfiguration
        {
            CompensationStepType = GetString(node, "compensationStepType"),
            RequiredOnFailure = GetBool(node, "requiredOnFailure"),
            Timeout = GetString(node, "timeout"),
        };

    private static RetryConfiguration ReadRetry(JsonValue node) =>
        new RetryConfiguration
        {
            MaxAttempts = GetInt(node, "maxAttempts") ?? 0,
            InitialDelay = GetString(node, "initialDelay"),
            BackoffMultiplier = GetDouble(node, "backoffMultiplier"),
            MaxDelay = GetString(node, "maxDelay"),
            UseJitter = GetBool(node, "useJitter"),
        };

    private static ValidationDefinition ReadValidation(JsonValue node) =>
        new ValidationDefinition
        {
            PredicateExpression = GetString(node, "predicateExpression"),
            ErrorMessage = GetString(node, "errorMessage"),
        };

    private static TransitionDefinition ReadTransition(JsonValue node) =>
        new TransitionDefinition
        {
            TransitionId = GetString(node, "transitionId"),
            FromStepId = GetString(node, "fromStepId"),
            ToStepId = GetString(node, "toStepId"),
            IsDefault = GetBool(node, "isDefault") ?? false,
        };

    private static BranchPointDefinition ReadBranchPoint(JsonValue node) =>
        new BranchPointDefinition
        {
            BranchPointId = GetString(node, "branchPointId"),
            FromStepId = GetString(node, "fromStepId"),
            Paths = ReadList(node, "paths", ReadBranchPath),
            RejoinStepId = GetString(node, "rejoinStepId"),
        };

    private static BranchPathDefinition ReadBranchPath(JsonValue node) =>
        new BranchPathDefinition
        {
            PathId = GetString(node, "pathId"),
            ConditionDescription = GetString(node, "conditionDescription"),
            Steps = ReadList(node, "steps", ReadStep),
            IsTerminal = GetBool(node, "isTerminal") ?? false,
            Approval = ReadObject(node, "approval", ReadApproval),
        };

    private static LoopDefinition ReadLoop(JsonValue node) =>
        new LoopDefinition
        {
            LoopId = GetString(node, "loopId"),
            LoopName = GetString(node, "loopName"),
            FromStepId = GetString(node, "fromStepId"),
            MaxIterations = GetInt(node, "maxIterations") ?? 0,
            BodySteps = ReadList(node, "bodySteps", ReadStep),
            ContinuationStepId = GetString(node, "continuationStepId"),
        };

    private static ForkPointDefinition ReadForkPoint(JsonValue node) =>
        new ForkPointDefinition
        {
            ForkPointId = GetString(node, "forkPointId"),
            FromStepId = GetString(node, "fromStepId"),
            Paths = ReadList(node, "paths", ReadForkPath),
            JoinStepId = GetString(node, "joinStepId"),
        };

    private static ForkPathDefinition ReadForkPath(JsonValue node) =>
        new ForkPathDefinition
        {
            PathId = GetString(node, "pathId"),
            PathIndex = GetInt(node, "pathIndex") ?? 0,
            Steps = ReadList(node, "steps", ReadStep),
            FailureHandler = ReadObject(node, "failureHandler", ReadFailureHandler),
        };

    private static FailureHandlerDefinition ReadFailureHandler(JsonValue node) =>
        new FailureHandlerDefinition
        {
            HandlerId = GetString(node, "handlerId"),
            Scope = GetString(node, "scope"),
            TriggerStepId = GetString(node, "triggerStepId"),
            Steps = ReadList(node, "steps", ReadStep),
            IsTerminal = GetBool(node, "isTerminal") ?? false,
        };

    private static ApprovalDefinition ReadApproval(JsonValue node) =>
        new ApprovalDefinition
        {
            ApprovalPointId = GetString(node, "approvalPointId"),
            ApproverType = GetString(node, "approverType"),
            PrecedingStepId = GetString(node, "precedingStepId"),
            EscalationHandler = ReadObject(node, "escalationHandler", ReadEscalation),
            RejectionHandler = ReadObject(node, "rejectionHandler", ReadRejection),
            HasContext = GetBool(node, "hasContext") ?? false,
        };

    private static ApprovalEscalationDefinition ReadEscalation(JsonValue node) =>
        new ApprovalEscalationDefinition
        {
            EscalationId = GetString(node, "escalationId"),
            Steps = ReadList(node, "steps", ReadStep),
            NestedApprovals = ReadList(node, "nestedApprovals", ReadApproval),
            IsTerminal = GetBool(node, "isTerminal") ?? false,
        };

    private static ApprovalRejectionDefinition ReadRejection(JsonValue node) =>
        new ApprovalRejectionDefinition
        {
            RejectionHandlerId = GetString(node, "rejectionHandlerId"),
            Steps = ReadList(node, "steps", ReadStep),
            IsTerminal = GetBool(node, "isTerminal") ?? false,
        };

    private static GateDeclaration ReadGate(JsonValue node) =>
        new GateDeclaration
        {
            Class = GetString(node, "class"),
            Id = GetString(node, "id"),
            Reliability = ReadObject(node, "reliability", ReadGateReliability),
        };

    private static GateReliability ReadGateReliability(JsonValue node) =>
        new GateReliability
        {
            Fpr = GetDouble(node, "fpr") ?? 0,
            SampleSize = GetInt(node, "sampleSize") ?? 0,
            AsOf = GetString(node, "asOf"),
            Source = GetString(node, "source"),
        };

    private static DiagnosticForkDefinition ReadDiagnosticFork(JsonValue node) =>
        new DiagnosticForkDefinition
        {
            AnchorStepIds = ReadStringList(node, "anchorStepIds"),
            PermittedTriggers = ReadList(node, "permittedTriggers", ReadPermittedTrigger),
            MaxForks = GetInt(node, "maxForks") ?? 0,
            CompensationSeed = GetString(node, "compensationSeed"),
        };

    private static PermittedForkTrigger ReadPermittedTrigger(JsonValue node) =>
        new PermittedForkTrigger
        {
            Trigger = GetString(node, "trigger"),
            RequiredEvidenceFields = ReadStringList(node, "requiredEvidenceFields"),
        };

    // ---- primitive/collection accessors -------------------------------------

    private static string? GetString(JsonValue obj, string name) =>
        obj.TryGetMember(name, out var v) ? v.AsStringOrNull() : null;

    private static bool? GetBool(JsonValue obj, string name) =>
        obj.TryGetMember(name, out var v) ? v.AsBoolOrNull() : null;

    private static int? GetInt(JsonValue obj, string name) =>
        obj.TryGetMember(name, out var v) ? v.AsIntOrNull() : null;

    private static double? GetDouble(JsonValue obj, string name) =>
        obj.TryGetMember(name, out var v) ? v.AsDoubleOrNull() : null;

    private static List<T> ReadList<T>(JsonValue obj, string name, Func<JsonValue, T> map)
    {
        var result = new List<T>();
        if (obj.TryGetMember(name, out var array) && array.Kind == JsonKind.Array)
        {
            foreach (var item in array.Items)
            {
                if (item.Kind == JsonKind.Object)
                {
                    result.Add(map(item));
                }
            }
        }

        return result;
    }

    private static List<string> ReadStringList(JsonValue obj, string name)
    {
        var result = new List<string>();
        if (obj.TryGetMember(name, out var array) && array.Kind == JsonKind.Array)
        {
            foreach (var item in array.Items)
            {
                var s = item.AsStringOrNull();
                if (s is not null)
                {
                    result.Add(s);
                }
            }
        }

        return result;
    }

    private static T? ReadObject<T>(JsonValue obj, string name, Func<JsonValue, T> map)
        where T : class =>
        obj.TryGetMember(name, out var child) && child.Kind == JsonKind.Object ? map(child) : null;
}
