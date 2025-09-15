using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;

namespace Core.Helpers
{
    public class TableRenderer
    {
        public string RenderDataTable(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the table
            string tableId = "datatable_" + Guid.NewGuid().ToString("N");

            // Parse formatting instructions if available
            FormatOptions formatOptions = FormatParser.ParseFormattingOptions(
                instructions.ContainsKey("formatting") ? instructions["formatting"] : "");

            // Optional pivot transformation before rendering
            if (instructions != null && (instructions.ContainsKey("pivotRow") || instructions.ContainsKey("pivotCol")))
            {
                instructions.TryGetValue("pivotRow", out string pivotRow);
                instructions.TryGetValue("pivotCol", out string pivotCol);
                instructions.TryGetValue("pivotValue", out string pivotValue);
                instructions.TryGetValue("pivotAgg", out string pivotAgg);
                // Resolve columns case-insensitively to avoid typos in casing
                Func<string, string> resolve = (name) =>
                    string.IsNullOrWhiteSpace(name) ? name :
                    (data?.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase))?.ColumnName ?? name);
                pivotRow = resolve(pivotRow);
                pivotCol = resolve(pivotCol);
                if (!string.IsNullOrWhiteSpace(pivotValue)) pivotValue = resolve(pivotValue);
                // Fallbacks: if pivotRow/pivotValue not provided, use groupBy/series
                if (string.IsNullOrWhiteSpace(pivotRow))
                {
                    if (instructions.TryGetValue("groupBy", out string gb) && !string.IsNullOrWhiteSpace(gb))
                        pivotRow = resolve(gb);
                }
                if (string.IsNullOrWhiteSpace(pivotValue))
                {
                    if (!string.Equals((pivotAgg ?? "").Trim(), "count", StringComparison.OrdinalIgnoreCase))
                    {
                        if (instructions.TryGetValue("series", out string seriesVal) && !string.IsNullOrWhiteSpace(seriesVal))
                        {
                            var firstSeries = seriesVal.Split(',').Select(s => s.Trim()).FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(firstSeries)) pivotValue = resolve(firstSeries);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(pivotRow) && !string.IsNullOrWhiteSpace(pivotCol))
                {
                    pivotAgg = string.IsNullOrWhiteSpace(pivotAgg) ? "sum" : pivotAgg;
                    try
                    {
                        try
                        {
                            var beforeCols = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                            DebugHelper.Log($"[Pivot] row='{pivotRow}' col='{pivotCol}' val='{pivotValue}' agg='{pivotAgg}' cols=[{beforeCols}] rows={data.Rows.Count}");
                        }
                        catch { }
                        var cfg = new PivotConfig
                        {
                            ShowRowTotal = instructions.ContainsKey("pivotShowRowTotal"),
                            ShowColTotal = instructions.ContainsKey("pivotShowColTotal"),
                            ShowGrandTotal = instructions.ContainsKey("pivotShowGrandTotal"),
                            RowOrder = instructions.ContainsKey("pivotRowOrder") ? instructions["pivotRowOrder"].Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0).ToList() : null,
                            ColOrder = instructions.ContainsKey("pivotColOrder") ? instructions["pivotColOrder"].Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0).ToList() : null,
                            ColSortNumeric = instructions.ContainsKey("pivotColSort") && instructions["pivotColSort"].Equals("numeric", StringComparison.OrdinalIgnoreCase),
                            RowSortNumeric = instructions.ContainsKey("pivotRowSort") && instructions["pivotRowSort"].Equals("numeric", StringComparison.OrdinalIgnoreCase),
                            ColDesc = instructions.ContainsKey("pivotColDir") && instructions["pivotColDir"].Equals("desc", StringComparison.OrdinalIgnoreCase),
                            RowDesc = instructions.ContainsKey("pivotRowDir") && instructions["pivotRowDir"].Equals("desc", StringComparison.OrdinalIgnoreCase)
                        };
                        data = PivotHelper.Pivot(data, pivotRow, pivotCol, pivotValue, pivotAgg, cfg);
                        try
                        {
                            var afterCols = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                            DebugHelper.Log($"[Pivot OK] newCols=[{afterCols}] newRows={data.Rows.Count}");
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        try { DebugHelper.Log("[Pivot ERROR] " + ex.Message); } catch { }
                        return "<div class='alert alert-danger'>Pivot error: " + System.Web.HttpUtility.HtmlEncode(ex.Message) + "</div>";
                    }
                }
            }

            // Build the HTML for the table
            string html = $"<table id='{tableId}' class='display' style='width:100%'><thead><tr>";

            // Add table headers
            foreach (DataColumn column in data.Columns)
            {
                string headerStyle = "";
                if (formatOptions.ColumnPattern != null &&
                    column.ColumnName.Contains(formatOptions.ColumnPattern.NameContains))
                {
                    headerStyle = $" style='{formatOptions.ColumnPattern.Style}'";
                }

                html += $"<th{headerStyle}>{column.ColumnName}</th>";
            }
            html += "</tr></thead><tbody>";

            // Add table rows
            int rowIndex = 0;
            foreach (DataRow row in data.Rows)
            {
                rowIndex++;

                string rowStyle = "";
                if (formatOptions.RowPattern != null &&
                    rowIndex % formatOptions.RowPattern.Index == 0)
                {
                    rowStyle = $" style='{formatOptions.RowPattern.Style}'";
                }

                html += $"<tr{rowStyle}>";

                for (int i = 0; i < row.ItemArray.Length; i++)
                {
                    string cellStyle = "";
                    string columnName = data.Columns[i].ColumnName;

                    if (formatOptions.ColumnPattern != null &&
                        columnName.Contains(formatOptions.ColumnPattern.NameContains))
                    {
                        cellStyle = $" style='{formatOptions.ColumnPattern.Style}'";
                    }

                    html += $"<td{cellStyle}>{row.ItemArray[i]}</td>";
                }

                html += "</tr>";
            }
            html += "</tbody></table>";

            // Add DataTables initialization script with styling options
            html += $@"
<script>
    $(document).ready(function() {{
        $('#{tableId}').DataTable({{
            // Preserve any custom styling when DataTables renders
            'drawCallback': function() {{
                // Apply row styling based on pattern
                if ({(formatOptions.RowPattern != null ? "true" : "false")}) {{
                    $('#{tableId} tbody tr').each(function(index) {{
                        if (((index + 1) % {(formatOptions.RowPattern?.Index ?? 0)}) === 0) {{
                            $(this).attr('style', '{(formatOptions.RowPattern?.Style ?? "")}');
                        }}
                    }});
                }}
                
                // Apply column styling based on name pattern
                if ({(formatOptions.ColumnPattern != null ? "true" : "false")}) {{
                    $('#{tableId} thead th').each(function(index) {{
                        var columnText = $(this).text();
                        if (columnText.indexOf('{(formatOptions.ColumnPattern?.NameContains ?? "")}') !== -1) {{
                            $(this).attr('style', '{(formatOptions.ColumnPattern?.Style ?? "")}');
                            $('#{tableId} tbody tr td:nth-child(' + (index + 1) + ')').attr('style', '{(formatOptions.ColumnPattern?.Style ?? "")}');
                        }}
                    }});
                }}
            }}
        }});
    }});
</script>";

            return html;
        }
    }
}
