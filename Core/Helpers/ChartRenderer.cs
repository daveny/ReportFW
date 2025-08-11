using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace Core.Helpers
{
    public class ChartRenderer : IChartRenderer
    {
        private readonly TableRenderer _tableRenderer;

        public ChartRenderer()
        {
            _tableRenderer = new TableRenderer();
        }

        public string RenderChart(DataTable data, Dictionary<string, string> instructions)
        {
            if (instructions.ContainsKey("representation") &&
                instructions["representation"].ToLower() == "filter")
            {
                return RenderFilterComponent(instructions);
            }

            string representation = instructions.ContainsKey("representation")
                ? instructions["representation"].ToLower()
                : "table";

            switch (representation)
            {
                case "table":
                    return RenderDataTable(data, instructions);
                case "barchart":
                    return RenderBarChart(data, instructions);
                case "linechart":
                    return RenderLineChart(data, instructions);
                case "piechart":
                    return RenderPieChart(data, instructions);
                default:
                    return RenderDataTable(data, instructions);
            }
        }

        private double SafeConvertToDouble(object obj)
        {
            if (obj == null || obj == DBNull.Value) return 0;
            double result;
            double.TryParse(obj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            return result;
        }

        public string RenderDataTable(DataTable data, Dictionary<string, string> instructions)
        {
            return _tableRenderer.RenderDataTable(data, instructions);
        }

        public string RenderBarChart(DataTable data, Dictionary<string, string> instructions)
        {
            string chartId = "barchart_" + Guid.NewGuid().ToString("N");
            var options = FormatParser.ParseBarChartOptions(instructions);
            List<string> seriesColumns = instructions.ContainsKey("series") ? ChartHelpers.ParseArray(instructions["series"]) : new List<string> { data.Columns[1].ColumnName };
            string groupByColumn = instructions.ContainsKey("groupBy") ? instructions["groupBy"] : null;
            string legendsColumn = instructions.ContainsKey("legends") ? ChartHelpers.ParseArray(instructions["legends"]).FirstOrDefault() ?? data.Columns[0].ColumnName : data.Columns[0].ColumnName;

            var columnIndices = ChartHelpers.GetColumnIndices(data, data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList());
            int legendsColumnIndex = columnIndices.ContainsKey(legendsColumn) ? columnIndices[legendsColumn] : 0;
            int groupByColumnIndex = !string.IsNullOrEmpty(groupByColumn) && columnIndices.ContainsKey(groupByColumn) ? columnIndices[groupByColumn] : -1;

            List<string> labels = ChartHelpers.GetUniqueOrderedLabels(data, legendsColumnIndex);
            List<string> datasets = new List<string>();

            if (groupByColumnIndex != -1)
            {
                List<string> uniqueCategories = data.AsEnumerable().Select(row => row[groupByColumnIndex].ToString()).Distinct().OrderBy(c => c).ToList();
                int categoryIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!columnIndices.ContainsKey(seriesName)) continue;
                    int colIndex = columnIndices[seriesName];
                    foreach (string category in uniqueCategories)
                    {
                        Dictionary<string, double> valuesByLegend = labels.ToDictionary(l => l, l => 0.0);
                        foreach (DataRow row in data.Rows)
                        {
                            if (row[groupByColumnIndex].ToString() == category)
                            {
                                string legend = row[legendsColumnIndex].ToString();
                                if (valuesByLegend.ContainsKey(legend))
                                {
                                    valuesByLegend[legend] = SafeConvertToDouble(row[colIndex]);
                                }
                            }
                        }
                        List<double> values = labels.Select(l => valuesByLegend[l]).ToList();
                        string backgroundColor = options.BackgroundColors[categoryIndex % options.BackgroundColors.Length];
                        string borderColor = options.BorderColors[categoryIndex % options.BorderColors.Length];
                        string datasetLabel = seriesColumns.Count > 1 ? $"{seriesName} - {category}" : category;
                        datasets.Add($"{{ label: '{datasetLabel}', data: {JsonConvert.SerializeObject(values)}, backgroundColor: '{backgroundColor}', borderColor: '{borderColor}', borderWidth: {options.BorderWidth} }}");
                        categoryIndex++;
                    }
                }
            }
            else
            {
                int seriesIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!columnIndices.ContainsKey(seriesName)) continue;
                    int colIndex = columnIndices[seriesName];
                    List<double> values = data.AsEnumerable().Select(row => SafeConvertToDouble(row[colIndex])).ToList();
                    string backgroundColor = options.BackgroundColors[seriesIndex % options.BackgroundColors.Length];
                    string borderColor = options.BorderColors[seriesIndex % options.BorderColors.Length];
                    datasets.Add($"{{ label: '{seriesName}', data: {JsonConvert.SerializeObject(values)}, backgroundColor: '{backgroundColor}', borderColor: '{borderColor}', borderWidth: {options.BorderWidth} }}");
                    seriesIndex++;
                }
            }

            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";
            html += $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        var ctx = document.getElementById('{chartId}');
        if (!ctx) return;
        ctx = ctx.getContext('2d');
        var chartData = {{ labels: {JsonConvert.SerializeObject(labels)}, datasets: [{string.Join(",", datasets)}] }};
        {ChartHelpers.GetDefaultSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        new Chart(ctx, {{
            type: 'bar',
            data: chartData,
            options: {{
                indexAxis: '{(options.Horizontal ? "y" : "x")}',
                responsive: true, maintainAspectRatio: false,
                scales: {{ x: {{ stacked: {options.Stacked.ToString().ToLower()} }}, y: {{ stacked: {options.Stacked.ToString().ToLower()}, beginAtZero: true }} }},
                plugins: {{ title: {{ display: true, text: '{options.ChartTitle}' }} }}
            }}
        }});
    }});
</script>";
            return html;
        }

        public string RenderLineChart(DataTable data, Dictionary<string, string> instructions)
        {
            string chartId = "linechart_" + Guid.NewGuid().ToString("N");
            var options = FormatParser.ParseLineChartOptions(instructions);
            List<string> seriesColumns = instructions.ContainsKey("series") ? ChartHelpers.ParseArray(instructions["series"]) : new List<string> { data.Columns[1].ColumnName };
            string groupByColumn = instructions.ContainsKey("groupBy") ? instructions["groupBy"] : null;
            string legendsColumn = instructions.ContainsKey("legends") ? ChartHelpers.ParseArray(instructions["legends"]).FirstOrDefault() ?? data.Columns[0].ColumnName : data.Columns[0].ColumnName;

            var columnIndices = ChartHelpers.GetColumnIndices(data, data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList());
            int legendsColumnIndex = columnIndices.ContainsKey(legendsColumn) ? columnIndices[legendsColumn] : 0;
            int groupByColumnIndex = !string.IsNullOrEmpty(groupByColumn) && columnIndices.ContainsKey(groupByColumn) ? columnIndices[groupByColumn] : -1;

            List<string> labels = ChartHelpers.GetUniqueOrderedLabels(data, legendsColumnIndex);
            List<string> datasets = new List<string>();

            if (groupByColumnIndex != -1)
            {
                List<string> uniqueCategories = data.AsEnumerable().Select(row => row[groupByColumnIndex].ToString()).Distinct().OrderBy(c => c).ToList();
                int categoryIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!columnIndices.ContainsKey(seriesName)) continue;
                    int colIndex = columnIndices[seriesName];

                    foreach (string category in uniqueCategories)
                    {
                        Dictionary<string, double> valuesByLegend = labels.ToDictionary(l => l, l => 0.0);
                        foreach (DataRow row in data.Rows)
                        {
                            if (row[groupByColumnIndex].ToString() == category)
                            {
                                string legend = row[legendsColumnIndex].ToString();
                                if (valuesByLegend.ContainsKey(legend))
                                {
                                    valuesByLegend[legend] = SafeConvertToDouble(row[colIndex]);
                                }
                            }
                        }
                        List<double> values = labels.Select(l => valuesByLegend[l]).ToList();
                        string color = options.Colors[categoryIndex % options.Colors.Length];
                        string datasetLabel = seriesColumns.Count > 1 ? $"{seriesName} - {category}" : category;
                        datasets.Add($"{{ label: '{datasetLabel}', data: {JsonConvert.SerializeObject(values)}, borderColor: '{color}', backgroundColor: '{color}', fill: false, tension: {options.Tension / 100.0}, pointRadius: {(options.ShowPoints ? 3 : 0)} }}");
                        categoryIndex++;
                    }
                }
            }
            else
            {
                int seriesIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!columnIndices.ContainsKey(seriesName)) continue;
                    int colIndex = columnIndices[seriesName];
                    List<double> values = data.AsEnumerable().Select(row => SafeConvertToDouble(row[colIndex])).ToList();
                    string color = options.Colors[seriesIndex % options.Colors.Length];
                    datasets.Add($"{{ label: '{seriesName}', data: {JsonConvert.SerializeObject(values)}, borderColor: '{color}', backgroundColor: '{color}', fill: false, tension: {options.Tension / 100.0}, pointRadius: {(options.ShowPoints ? 3 : 0)} }}");
                    seriesIndex++;
                }
            }

            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";
            html += $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        var ctx = document.getElementById('{chartId}');
        if (!ctx) return;
        ctx = ctx.getContext('2d');
        var chartData = {{ labels: {JsonConvert.SerializeObject(labels)}, datasets: [{string.Join(",", datasets)}] }};
        {ChartHelpers.GetDefaultSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        new Chart(ctx, {{
            type: 'line',
            data: chartData,
            options: {{
                responsive: true, maintainAspectRatio: false,
                scales: {{ y: {{ beginAtZero: true }} }},
                plugins: {{ title: {{ display: true, text: '{options.ChartTitle}' }} }}
            }}
        }});
    }});
</script>";
            return html;
        }

        public string RenderPieChart(DataTable data, Dictionary<string, string> instructions)
        {
            string chartId = "piechart_" + Guid.NewGuid().ToString("N");
            var options = FormatParser.ParsePieChartOptions(instructions);
            string seriesColumn = instructions.ContainsKey("series") ? ChartHelpers.ParseArray(instructions["series"]).FirstOrDefault() ?? data.Columns[1].ColumnName : data.Columns[1].ColumnName;
            string legendsColumn = instructions.ContainsKey("legends") ? ChartHelpers.ParseArray(instructions["legends"]).FirstOrDefault() ?? data.Columns[0].ColumnName : data.Columns[0].ColumnName;
            string groupByColumn = instructions.ContainsKey("groupBy") ? instructions["groupBy"] : null;

            var columnIndices = ChartHelpers.GetColumnIndices(data, data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList());
            int legendsColumnIndex = columnIndices[legendsColumn];
            int seriesColumnIndex = columnIndices[seriesColumn];
            int groupByColumnIndex = !string.IsNullOrEmpty(groupByColumn) && columnIndices.ContainsKey(groupByColumn) ? columnIndices[groupByColumn] : -1;

            if (groupByColumnIndex != -1)
            {
                // VISSZAÁLLÍTVA: Logika több diagram rendereléséhez
                List<string> uniqueGroups = data.AsEnumerable().Select(row => row[groupByColumnIndex].ToString()).Distinct().OrderBy(g => g).ToList();
                string html = "<div style='width:100%; display:flex; flex-wrap:wrap; justify-content:center;'>";
                for (int i = 0; i < uniqueGroups.Count; i++)
                {
                    string group = uniqueGroups[i];
                    string groupChartId = $"{chartId}_{i}";
                    var groupData = data.AsEnumerable().Where(r => r[groupByColumnIndex].ToString() == group);

                    List<string> labels = groupData.Select(r => r[legendsColumnIndex].ToString()).ToList();
                    List<double> values = groupData.Select(r => SafeConvertToDouble(r[seriesColumnIndex])).ToList();
                    List<string> bgColors = Enumerable.Range(0, labels.Count).Select(n => options.BackgroundColors[n % options.BackgroundColors.Length]).ToList();
                    List<string> borderColors = Enumerable.Range(0, labels.Count).Select(n => options.BorderColors[n % options.BorderColors.Length]).ToList();

                    html += $"<div style='flex: 1; min-width: 300px; max-width: 500px; margin: 10px;'><h3 style='text-align: center;'>{group}</h3><div style='height: 300px;'><canvas id='{groupChartId}'></canvas></div></div>";
                    html += GetPieChartScript(groupChartId, options, labels, values, bgColors, borderColors, legendsColumn);
                }
                html += "</div>";
                return html;
            }
            else
            {
                // Eredeti logika egyetlen diagramhoz
                List<string> labels = data.AsEnumerable().Select(r => r[legendsColumnIndex].ToString()).ToList();
                List<double> values = data.AsEnumerable().Select(r => SafeConvertToDouble(r[seriesColumnIndex])).ToList();
                List<string> bgColors = Enumerable.Range(0, labels.Count).Select(i => options.BackgroundColors[i % options.BackgroundColors.Length]).ToList();
                List<string> borderColors = Enumerable.Range(0, labels.Count).Select(i => options.BorderColors[i % options.BorderColors.Length]).ToList();

                string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";
                html += GetPieChartScript(chartId, options, labels, values, bgColors, borderColors, legendsColumn);
                return html;
            }
        }

        private string GetPieChartScript(string chartId, PieChartOptions options, List<string> labels, List<double> values, List<string> bgColors, List<string> borderColors, string legendsColumn)
        {
            // VISSZAÁLLÍTVA: Részletes JS generálás a címkékhez
            return $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        var ctx = document.getElementById('{chartId}');
        if(!ctx) return;
        ctx = ctx.getContext('2d');
        var chartData = {{
            labels: {JsonConvert.SerializeObject(labels)},
            datasets: [{{ data: {JsonConvert.SerializeObject(values)}, backgroundColor: {JsonConvert.SerializeObject(bgColors)}, borderColor: {JsonConvert.SerializeObject(borderColors)}, borderWidth: 1 }}]
        }};
        {ChartHelpers.GetPieSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        new Chart(ctx, {{
            type: '{(options.IsDoughnut ? "doughnut" : "pie")}',
            data: chartData,
            options: {{
                responsive: true, maintainAspectRatio: false,
                plugins: {{
                    title: {{ display: true, text: '{options.ChartTitle}' }},
                    legend: {{ display: {options.ShowLegend.ToString().ToLower()}, position: 'right' }},
                    tooltip: {{
                        callbacks: {{
                            label: function(context) {{
                                var label = context.label || '';
                                var value = context.raw || 0;
                                var total = context.dataset.data.reduce((a, b) => a + b, 0);
                                var percentage = total > 0 ? Math.round((value / total) * 100) : 0;
                                return label + ': ' + value.toLocaleString() + ' (' + percentage + '%)';
                            }}
                        }}
                    }}
                }}
            }}
        }});
    }});
</script>";
        }

        public string RenderFilterComponent(Dictionary<string, string> instructions)
        {
            // VISSZAÁLLÍTVA: Teljes funkcionalitású szűrő renderelés
            string id = instructions.ContainsKey("id") ? instructions["id"] : "filter_" + Guid.NewGuid().ToString("N");
            string name = instructions.ContainsKey("param") ? instructions["param"] : id;
            string label = instructions.ContainsKey("label") ? instructions["label"] : name;
            string value = instructions.ContainsKey("value") ? instructions["value"] : (instructions.ContainsKey("default") ? instructions["default"] : "");
            string filterType = instructions.ContainsKey("type") ? instructions["type"].ToLower() : "dropdown";
            string affects = instructions.ContainsKey("affects") ? $"data-affects='{instructions["affects"]}'" : "";

            string html = $"<div class='filter-component'><label for='{id}'>{label}</label>";

            switch (filterType)
            {
                case "dropdown":
                    html += $"<select id='{id}' name='{name}' class='form-control filter-dropdown' {affects}>";
                    if (!(instructions.ContainsKey("required") && instructions["required"].ToLower() == "true"))
                        html += "<option value=''>-- Select --</option>";
                    if (instructions.ContainsKey("options"))
                    {
                        foreach (var option in instructions["options"].Split(',').Select(o => o.Trim()))
                        {
                            html += $"<option value='{HttpUtility.HtmlAttributeEncode(option)}'{(option == value ? " selected" : "")}>{HttpUtility.HtmlEncode(option)}</option>";
                        }
                    }
                    html += "</select>";
                    if (instructions.ContainsKey("dataSource"))
                    {
                        html += $@"<script>
                            document.addEventListener('DOMContentLoaded', function() {{
                                $.ajax({{
                                    url: '/Report/GetFilterData', type: 'GET',
                                    data: {{ query: '{HttpUtility.JavaScriptStringEncode(instructions["dataSource"])}', valueField: '{instructions["valueField"]}', textField: '{instructions["textField"]}' }},
                                    success: function(data) {{
                                        var select = $('#{id}');
                                        $.each(data, function(i, item) {{
                                            var option = $('<option>').val(item.value).text(item.text);
                                            if (item.value == '{HttpUtility.JavaScriptStringEncode(value)}') option.prop('selected', true);
                                            select.append(option);
                                        }});
                                    }}
                                }});
                            }});
                         </script>";
                    }
                    break;
                case "button":
                    var buttonValues = instructions.ContainsKey("options") ? instructions["options"].Split(',').Select(s => s.Trim()).ToList() : new List<string> { "Yes", "No" };
                    var buttonLabels = instructions.ContainsKey("labels") ? instructions["labels"].Split(',').Select(s => s.Trim()).ToList() : buttonValues;
                    html += $"<div class='btn-group filter-button-group' role='group' {affects}>";
                    for (int i = 0; i < buttonValues.Count; i++)
                    {
                        var btnValue = buttonValues[i];
                        var btnLabel = buttonLabels.Count > i ? buttonLabels[i] : btnValue;
                        bool isActive = btnValue == value;
                        html += $"<button type='button' class='btn {(isActive ? "btn-primary" : "btn-secondary")} filter-button' data-param-name='{name}' data-value='{btnValue}'>{btnLabel}</button>";
                    }
                    html += "</div>";
                    break;
                case "calendar":
                case "date":
                    html += $"<input type='text' id='{id}' name='{name}' value='{HttpUtility.HtmlAttributeEncode(value)}' class='form-control datepicker' {affects} />";
                    html += $"<script>document.addEventListener('DOMContentLoaded', function() {{ $('#{id}').datepicker({{ format: 'yyyy-mm-dd', autoclose: true }}); }});</script>";
                    break;
                case "number":
                    html += $"<input type='number' id='{id}' name='{name}' value='{HttpUtility.HtmlAttributeEncode(value)}' class='form-control filter-text' {affects} " +
                       $"{(instructions.ContainsKey("min") ? "min='" + instructions["min"] + "'" : "")} " +
                       $"{(instructions.ContainsKey("max") ? "max='" + instructions["max"] + "'" : "")} " +
                       $"{(instructions.ContainsKey("step") ? "step='" + instructions["step"] + "'" : "")} />";
                    break;
                default: // text
                    html += $"<div class='input-group'><input type='text' id='{id}' name='{name}' value='{HttpUtility.HtmlAttributeEncode(value)}' class='form-control filter-text' {affects} /><div class='input-group-append'><button class='btn btn-outline-secondary filter-apply-btn' type='button'>Apply</button></div></div>";
                    break;
            }
            html += "</div>";
            return html;
        }

        public string RenderFilterComponent(DataTable data, Dictionary<string, string> instructions)
        {
            // This overload is now fully replaced by the one above.
            // It can be kept for interface compatibility but shouldn't be called directly.
            return RenderFilterComponent(instructions);
        }
    }
}
