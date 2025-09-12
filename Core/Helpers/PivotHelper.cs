using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Core.Helpers
{
    public class PivotConfig
    {
        public bool ShowRowTotal { get; set; }
        public bool ShowColTotal { get; set; }
        public bool ShowGrandTotal { get; set; }
        public List<string> RowOrder { get; set; }
        public List<string> ColOrder { get; set; }
        public bool ColSortNumeric { get; set; }
        public bool RowSortNumeric { get; set; }
        public bool ColDesc { get; set; }
        public bool RowDesc { get; set; }
    }

    public static class PivotHelper
    {
        public static DataTable Pivot(DataTable source, string rowField, string colField, string valueField, string agg)
            => Pivot(source, rowField, colField, valueField, agg, null);

        public static DataTable Pivot(DataTable source, string rowField, string colField, string valueField, string agg, PivotConfig config)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.Columns.Contains(rowField)) throw new ArgumentException("Row field not found: " + rowField);
            if (!source.Columns.Contains(colField)) throw new ArgumentException("Column field not found: " + colField);
            if (string.IsNullOrWhiteSpace(valueField) && !string.Equals(agg, "count", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Value field must be specified for aggregator: " + agg);
            if (!string.IsNullOrWhiteSpace(valueField) && !source.Columns.Contains(valueField))
                throw new ArgumentException("Value field not found: " + valueField);

            var aggNorm = (agg ?? "sum").Trim().ToLowerInvariant();

            config = config ?? new PivotConfig();

            // Distinct row keys and column keys
            var rowKeySet = source.AsEnumerable()
                                  .Select(r => r[rowField]?.ToString() ?? string.Empty)
                                  .Distinct()
                                  .ToList();

            var colKeySet = source.AsEnumerable()
                                  .Select(r => r[colField]?.ToString() ?? string.Empty)
                                  .Distinct()
                                  .ToList();

            Func<IEnumerable<string>, bool, bool, IEnumerable<string>> sortKeys = (keys, numeric, desc) =>
            {
                IEnumerable<string> ordered;
                if (numeric)
                {
                    ordered = keys.Select(k => new { k, n = ToDoubleOrNull(k) }).OrderBy(x => x.n ?? double.NaN).Select(x => x.k);
                }
                else
                {
                    ordered = keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
                }
                if (desc) ordered = ordered.Reverse();
                return ordered;
            };

            var rowKeys = (config.RowOrder != null && config.RowOrder.Count > 0)
                ? config.RowOrder.Concat(rowKeySet.Except(config.RowOrder)).ToList()
                : sortKeys(rowKeySet, config.RowSortNumeric, config.RowDesc).ToList();

            var colKeys = (config.ColOrder != null && config.ColOrder.Count > 0)
                ? config.ColOrder.Concat(colKeySet.Except(config.ColOrder)).ToList()
                : sortKeys(colKeySet, config.ColSortNumeric, config.ColDesc).ToList();

            // Prepare result table
            var result = new DataTable();
            result.Columns.Add(rowField, typeof(string));
            foreach (var ck in colKeys)
            {
                result.Columns.Add(ck, typeof(double));
            }
            if (config.ShowRowTotal)
            {
                var totalColName = GetUniqueColumnName(result, "Total");
                result.Columns.Add(totalColName, typeof(double));
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

                if (config.ShowRowTotal)
                {
                    double total = 0.0;
                    foreach (var ck in colKeys) total += SafeToDouble(row[ck]);
                    // last column is the added total column
                    row[result.Columns[result.Columns.Count - 1].ColumnName] = total;
                }

                result.Rows.Add(row);
            }

            if (config.ShowColTotal || config.ShowGrandTotal)
            {
                var totalRow = result.NewRow();
                totalRow[rowField] = "Total";
                foreach (var ck in colKeys)
                {
                    double colSum = 0.0;
                    foreach (DataRow r in result.Rows)
                    {
                        colSum += SafeToDouble(r[ck]);
                    }
                    totalRow[ck] = colSum;
                }
                if (config.ShowRowTotal)
                {
                    double grand = 0.0;
                    foreach (var ck in colKeys) grand += SafeToDouble(totalRow[ck]);
                    // last column is total
                    totalRow[result.Columns[result.Columns.Count - 1].ColumnName] = grand;
                }
                result.Rows.Add(totalRow);
            }

            return result;
        }

        private static double SafeToDouble(object obj)
        {
            if (obj == null || obj == DBNull.Value) return 0.0;
            double d; return double.TryParse(obj.ToString(), out d) ? d : 0.0;
        }

        private static double? ToDoubleOrNull(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            double d; if (double.TryParse(s, out d)) return d; return null;
        }

        private static string GetUniqueColumnName(DataTable table, string baseName)
        {
            string name = baseName; int i = 1;
            while (table.Columns.Contains(name)) { name = baseName + " " + (++i); }
            return name;
        }
    }
}
