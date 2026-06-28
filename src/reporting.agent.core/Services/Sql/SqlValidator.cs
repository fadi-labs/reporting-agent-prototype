using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using reporting.agent.core.Models;

namespace reporting.agent.core.Services.Sql;

public sealed class SqlValidationOutput
{
    public ValidationResult Result { get; init; } = ValidationResult.Ok();
    public Statement.Select? Statement { get; init; }
}

/// Validates a raw SQL string against guardrails using SqlParserCS AST inspection.
/// MySQL dialect is used to match Python (sqlglot dialect="mysql") for correct
/// TIMESTAMPDIFF argument order.
public sealed class SqlValidator
{
    private static readonly HashSet<string> BlockedColumns = new(StringComparer.Ordinal)
    {
        "dataRelationId",
    };

    private static readonly HashSet<string> KeywordsAsColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DAY", "HOUR", "MINUTE", "SECOND", "MONTH", "YEAR", "WEEK", "QUARTER",
    };

    private static readonly MySqlDialect Dialect = new();

    private readonly ColumnRegistry _registry;

    public SqlValidator(ColumnRegistry registry)
    {
        _registry = registry;
    }

    public SqlValidationOutput Validate(string sql)
    {
        Sequence<Statement> parsed;
        try
        {
            parsed = new Parser().ParseSql(sql, Dialect);
        }
        catch (ParserException ex)
        {
            return new SqlValidationOutput
            {
                Result = ValidationResult.Fail(new ValidationError("PARSE_ERROR", $"SQL syntax error: {ex.Message}")),
            };
        }

        var statements = parsed.Where(s => s is not null).ToList();
        if (statements.Count != 1)
        {
            return new SqlValidationOutput
            {
                Result = ValidationResult.Fail(new ValidationError(
                    "MULTIPLE_STATEMENTS",
                    $"Only a single SQL statement is allowed. Found {statements.Count} statements.")),
            };
        }

        if (statements[0] is not Statement.Select sel)
        {
            return new SqlValidationOutput
            {
                Result = ValidationResult.Fail(new ValidationError(
                    "NON_SELECT",
                    $"Only SELECT queries are allowed. Got: {statements[0].GetType().Name}.",
                    "Rewrite as a SELECT query.")),
            };
        }

        var errors = new List<ValidationError>();
        var query = sel.Query;

        // 4. No CTEs
        if (query.With is not null)
        {
            errors.Add(new ValidationError(
                "CTE_NOT_ALLOWED",
                "CTEs (WITH clauses) are not supported.",
                "Rewrite using subqueries in FROM or WHERE instead."));
        }

        // 5. Only Reporting table
        var invalidTables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tableName in WalkTables(query))
        {
            if (!string.Equals(tableName, "REPORTING", StringComparison.OrdinalIgnoreCase))
            {
                invalidTables.Add(tableName);
            }
        }
        if (invalidTables.Count > 0)
        {
            errors.Add(new ValidationError(
                "INVALID_TABLE",
                $"Unknown table(s): [{string.Join(", ", invalidTables.OrderBy(t => t, StringComparer.Ordinal))}]. The only available table is 'Reporting'."));
        }

        // 6. No SELECT *
        if (query.Body is SetExpression.SelectExpression se)
        {
            foreach (var item in se.Select.Projection)
            {
                if (item is SelectItem.Wildcard or SelectItem.QualifiedWildcard)
                {
                    errors.Add(new ValidationError(
                        "SELECT_STAR_NOT_ALLOWED",
                        "SELECT * is not allowed. Specify columns explicitly.",
                        "Use get_fields to discover valid column IDs."));
                    break;
                }
            }
        }

        // 7. Blocked internal columns
        var blockedFound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in WalkColumnNames(query))
        {
            if (BlockedColumns.Contains(col)) blockedFound.Add(col);
        }
        if (blockedFound.Count > 0)
        {
            errors.Add(new ValidationError(
                "BLOCKED_COLUMN",
                $"Column(s) [{string.Join(", ", blockedFound.OrderBy(c => c, StringComparer.Ordinal))}] are internal and cannot be used directly.",
                "Use shipperBookingNumber or another identifier for counting unique shipments."));
        }

        // 9. Column whitelist (alias-aware)
        var aliases = CollectAliases(query);
        var invalidColumns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in WalkColumnNames(query))
        {
            if (aliases.Contains(name)) continue;
            if (BlockedColumns.Contains(name)) continue;
            if (KeywordsAsColumns.Contains(name)) continue;
            if (!_registry.IsValidColumn(name)) invalidColumns.Add(name);
        }
        if (invalidColumns.Count > 0)
        {
            errors.Add(new ValidationError(
                "INVALID_COLUMNS",
                $"Unknown column(s): [{string.Join(", ", invalidColumns.OrderBy(c => c, StringComparer.Ordinal))}].",
                "Call get_fields to discover valid column IDs."));
        }

        return new SqlValidationOutput
        {
            Result = errors.Count == 0
                ? ValidationResult.Ok()
                : new ValidationResult { Valid = false, Errors = errors },
            Statement = sel,
        };
    }

    private static IEnumerable<string> WalkTables(Query query)
    {
        foreach (var node in WalkNodes(query))
        {
            if (node is TableFactor.Table tbl)
            {
                var name = tbl.Name?.Values.LastOrDefault()?.Value;
                if (!string.IsNullOrEmpty(name)) yield return name;
            }
        }
    }

    private static IEnumerable<string> WalkColumnNames(Query query)
    {
        foreach (var node in WalkNodes(query))
        {
            switch (node)
            {
                case Expression.Identifier id:
                    yield return id.Ident.Value;
                    break;
                case Expression.CompoundIdentifier ci:
                    // Last segment is the column name.
                    if (ci.Idents.Count > 0)
                    {
                        yield return ci.Idents[^1].Value;
                    }
                    break;
            }
        }
    }

    private static HashSet<string> CollectAliases(Query query)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        if (query.Body is SetExpression.SelectExpression se)
        {
            foreach (var item in se.Select.Projection)
            {
                if (item is SelectItem.ExpressionWithAlias ewa)
                {
                    aliases.Add(ewa.Alias.Value);
                }
            }
        }
        foreach (var node in WalkNodes(query))
        {
            if (node is TableFactor.Derived d && d.Alias is not null)
            {
                aliases.Add(d.Alias.Name.Value);
            }
        }
        return aliases;
    }

    private static IEnumerable<object> WalkNodes(object root)
    {
        var stack = new Stack<object>();
        stack.Push(root);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is null || !visited.Add(current)) continue;
            yield return current;

            var type = current.GetType();
            // Skip primitives, strings, system types.
            if (type.IsPrimitive || current is string) continue;

            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                object? value;
                try { value = prop.GetValue(current); }
                catch { continue; }
                if (value is null) continue;
                if (value is string) continue;
                if (value is System.Collections.IEnumerable seq && value is not string)
                {
                    foreach (var item in seq)
                    {
                        if (item is null) continue;
                        if (IsAstCandidate(item)) stack.Push(item);
                    }
                    continue;
                }
                if (IsAstCandidate(value)) stack.Push(value);
            }
        }
    }

    private static bool IsAstCandidate(object value)
    {
        var ns = value.GetType().Namespace;
        return ns is not null && (ns.StartsWith("SqlParser.Ast", StringComparison.Ordinal) || ns == "SqlParser");
    }
}

