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

                if (!string.IsNullOrWhiteSpace(pivotRow) && !string.IsNullOrWhiteSpace(pivotCol))
                {
                    pivotAgg = string.IsNullOrWhiteSpace(pivotAgg) ? "sum" : pivotAgg;
                    try
                    {
                        data = PivotHelper.Pivot(data, pivotRow, pivotCol, pivotValue, pivotAgg);
                    }
                    catch (Exception ex)
                    {
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
