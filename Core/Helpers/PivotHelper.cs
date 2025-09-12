using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Core.Helpers
{
    public static class PivotHelper
    {
        public static DataTable Pivot(DataTable source, string rowField, string colField, string valueField, string agg)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.Columns.Contains(rowField)) throw new ArgumentException("Row field not found: " + rowField);
            if (!source.Columns.Contains(colField)) throw new ArgumentException("Column field not found: " + colField);
            if (string.IsNullOrWhiteSpace(valueField) && !string.Equals(agg, "count", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Value field must be specified for aggregator: " + agg);
            if (!string.IsNullOrWhiteSpace(valueField) && !source.Columns.Contains(valueField))
                throw new ArgumentException("Value field not found: " + valueField);

            var aggNorm = (agg ?? "sum").Trim().ToLowerInvariant();

            // Distinct row keys and column keys
            var rowKeys = source.AsEnumerable()
                                .Select(r => r[rowField]?.ToString() ?? string.Empty)
                                .Distinct()
                                .OrderBy(s => s)
                                .ToList();

            var colKeys = source.AsEnumerable()
                                .Select(r => r[colField]?.ToString() ?? string.Empty)
                                .Distinct()
                                .OrderBy(s => s)
                                .ToList();

            // Prepare result table
            var result = new DataTable();
            result.Columns.Add(rowField, typeof(string));
            foreach (var ck in colKeys)
            {
                result.Columns.Add(ck, typeof(double));
            }

            // Index data by row/col
            var lookup = source.AsEnumerable().GroupBy(r => new
            {
                Row = r[rowField]?.ToString() ?? string.Empty,
                Col = r[colField]?.ToString() ?? string.Empty
            });

            // Helper to safely convert to double
            Func<object, double?> asDouble = o =>
            {
                if (o == null || o == DBNull.Value) return null;
                double d; if (double.TryParse(o.ToString(), out d)) return d; return null;
            };

            // Build rows
            foreach (var rk in rowKeys)
            {
                var row = result.NewRow();
                row[rowField] = rk;

                foreach (var ck in colKeys)
                {
                    var group = lookup.Where(g => g.Key.Row == rk && g.Key.Col == ck)
                                      .SelectMany(g => g);

                    double cell = 0.0;
                    switch (aggNorm)
                    {
                        case "count":
                            cell = group.Count();
                            break;
                        case "avg":
                        case "average":
                            {
                                var vals = group.Select(r => asDouble(string.IsNullOrWhiteSpace(valueField) ? null : r[valueField]))
                                                .Where(v => v.HasValue)
                                                .Select(v => v.Value)
                                                .ToList();
                                cell = vals.Count > 0 ? vals.Average() : 0.0;
                            }
                            break;
                        case "min":
                            {
                                var vals = group.Select(r => asDouble(string.IsNullOrWhiteSpace(valueField) ? null : r[valueField]))
                                                .Where(v => v.HasValue)
                                                .Select(v => v.Value)
                                                .ToList();
                                cell = vals.Count > 0 ? vals.Min() : 0.0;
                            }
                            break;
                        case "max":
                            {
                                var vals = group.Select(r => asDouble(string.IsNullOrWhiteSpace(valueField) ? null : r[valueField]))
                                                .Where(v => v.HasValue)
                                                .Select(v => v.Value)
                                                .ToList();
                                cell = vals.Count > 0 ? vals.Max() : 0.0;
                            }
                            break;
                        case "sum":
                        default:
                            {
                                var vals = group.Select(r => asDouble(string.IsNullOrWhiteSpace(valueField) ? null : r[valueField]))
                                                .Where(v => v.HasValue)
                                                .Select(v => v.Value);
                                cell = vals.Sum();
                            }
                            break;
                    }

                    row[ck] = cell;
                }

                result.Rows.Add(row);
            }

            return result;
        }
    }
}

