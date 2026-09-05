using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace reporting.agent.core.Services.Sql;

/// Transforms a validated SELECT statement: ensures SELECT DISTINCT, injects
/// the dedup + exclude-sold subquery into WHERE, and caps LIMIT.
/// Mirrors services/sql/transformer.py.
public sealed class SqlTransformer
{
    private const string ExcludeDeleted =
        "COALESCE(positionStatus, '') != 'SOLD'";
    private const int MaxLimit = 500;
    private static readonly MySqlDialect Dialect = new();

    public string Transform(Statement.Select stmt, int limit)
    {
        var query = stmt.Query;
        if (query.Body is not SetExpression.SelectExpression se)
        {
            throw new InvalidOperationException("Expected a SELECT expression as the query body.");
        }
        var select = se.Select;

        var projectionSql = string.Join(", ", select.Projection.Select(p => p.ToSql()));

        var fromSql = select.From is not null && select.From.Count > 0
            ? "FROM " + string.Join(", ", select.From.Select(f => f.ToSql()))
            : "FROM Reporting";

        var userWhereSql = select.Selection?.ToSql() ?? "1=1";

        var injectionSql =
            "(__time, dataRelationId) IN (" +
            "SELECT MAX(__time), dataRelationId FROM Reporting " +
            "WHERE dataRelationId IN (" +
            "SELECT DISTINCT dataRelationId FROM Reporting " +
            $"WHERE {userWhereSql} AND {ExcludeDeleted}" +
            ") " +
            "GROUP BY dataRelationId" +
            ")";

        var whereSql = select.Selection is not null
            ? $"WHERE {userWhereSql} AND {injectionSql} AND {ExcludeDeleted}"
            : $"WHERE {injectionSql} AND {ExcludeDeleted}";

        var groupBySql = ResolveGroupBy(select);
        var havingSql = select.Having is not null ? $"HAVING {select.Having.ToSql()}" : string.Empty;
        var orderBySql = query.OrderBy is { Expressions: { Count: > 0 } exprs }
            ? $"ORDER BY {string.Join(", ", exprs.Select(o => o.ToSql()))}"
            : string.Empty;

        var effectiveLimit = Math.Min(limit, MaxLimit);
        var finalLimit = effectiveLimit;
        if (query.Limit is not null && int.TryParse(query.Limit.ToSql(), out var existing))
        {
            finalLimit = Math.Min(existing, effectiveLimit);
        }

        var parts = new List<string>
        {
            "SELECT DISTINCT",
            projectionSql,
            fromSql,
            whereSql,
        };
        if (!string.IsNullOrEmpty(groupBySql)) parts.Add(groupBySql);
        if (!string.IsNullOrEmpty(havingSql)) parts.Add(havingSql);
        if (!string.IsNullOrEmpty(orderBySql)) parts.Add(orderBySql);
        parts.Add($"LIMIT {finalLimit}");

        return string.Join(' ', parts);
    }

    private static string ResolveGroupBy(Select select)
    {
        var group = select.GroupBy;
        if (group is null) return string.Empty;

        // Build alias -> expression map from SELECT list to resolve alias references.
        var aliasToExpr = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in select.Projection)
        {
            if (item is SelectItem.ExpressionWithAlias ewa)
            {
                aliasToExpr[ewa.Alias.Value] = ewa.Expression.ToSql();
            }
        }

        if (group is GroupByExpression.Expressions expr)
        {
            if (expr.ColumnNames.Count == 0) return string.Empty;
            var resolved = expr.ColumnNames.Select(e =>
            {
                var sql = e.ToSql();
                return aliasToExpr.TryGetValue(sql, out var underlying) ? underlying : sql;
            });
            return "GROUP BY " + string.Join(", ", resolved);
        }
        return group.ToSql();
    }
}

