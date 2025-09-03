using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Core.Helpers
{
    public class ChartRenderer : IChartRenderer
    {
        private readonly TableRenderer _tableRenderer;
        private readonly JsonSerializerSettings _jsonSettings;

        public ChartRenderer()
        {
            _tableRenderer = new TableRenderer();
            _jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public string RenderChart(DataTable data, Dictionary<string, string> instructions, Dictionary<string, string> requestParameters = null)
        {
            requestParameters = requestParameters ?? new Dictionary<string, string>();
            instructions.TryGetValue("representation", out string representation);
            representation = representation?.ToLower() ?? "table";

            if (representation == "filter")
            {
                return RenderFilterComponent(data, instructions, requestParameters);
            }

            switch (representation)
            {
                case "table": return RenderDataTable(data, instructions);
                case "barchart": return RenderBarChart(data, instructions);
                case "linechart": return RenderLineChart(data, instructions);
                case "piechart": return RenderPieChart(data, instructions);
                default: return $"<div class='alert alert-warning'>Unknown representation: '{representation}'</div>";
            }
        }

        public string RenderDataTable(DataTable data, Dictionary<string, string> instructions)
        {
            return _tableRenderer.RenderDataTable(data, instructions);
        }

        public string RenderBarChart(DataTable data, Dictionary<string, string> instructions)
        {
            var options = FormatParser.ParseBarChartOptions(instructions);
            string chartId = "chart_" + Guid.NewGuid().ToString("N");

            instructions.TryGetValue("legends", out string legendsColumn);
            if (string.IsNullOrEmpty(legendsColumn) && data.Columns.Count > 0) legendsColumn = data.Columns[0].ColumnName;

            instructions.TryGetValue("series", out string seriesValue);
            if (string.IsNullOrEmpty(seriesValue) && data.Columns.Count > 1) seriesValue = data.Columns[1].ColumnName;
            var seriesColumns = ChartHelpers.ParseArray(seriesValue);

            instructions.TryGetValue("groupBy", out string groupByColumn);

            if (data.Columns.Count == 0 || !data.Columns.Contains(legendsColumn) || seriesColumns.Any(s => !data.Columns.Contains(s)))
            {
                return "";
            }

            var labels = data.AsEnumerable().Select(r => r[legendsColumn].ToString()).Distinct().OrderBy(l => l).ToList();
            var datasets = new List<ChartDataset>();

            if (string.IsNullOrEmpty(groupByColumn))
            {
                int colorIndex = 0;
                foreach (var seriesName in seriesColumns)
                {
                    datasets.Add(new ChartDataset
                    {
                        Label = seriesName,
                        Data = data.AsEnumerable().Select(r => SafeConvertToDouble(r[seriesName])).ToList(),
                        BackgroundColor = options.ValueColors != null ? null : options.BackgroundColors[colorIndex % options.BackgroundColors.Length],
                        BorderColor = options.ValueColors != null ? null : options.BorderColors[colorIndex % options.BorderColors.Length],
                        BorderWidth = options.BorderWidth
                    });
                    colorIndex++;
                }
            }
            else
            {
                var groups = data.AsEnumerable().Select(r => r[groupByColumn].ToString()).Distinct().OrderBy(g => g).ToList();
                int colorIndex = 0;
                foreach (var group in groups)
                {
                    var groupData = data.AsEnumerable().Where(r => r[groupByColumn].ToString() == group);
                    datasets.Add(new ChartDataset
                    {
                        Label = group,
                        Data = labels.Select(label =>
                        {
                            var row = groupData.FirstOrDefault(r => r[legendsColumn].ToString() == label);
                            return row != null ? SafeConvertToDouble(row[seriesColumns[0]]) : 0;
                        }).ToList(),
                        BackgroundColor = options.ValueColors != null ? null : options.BackgroundColors[colorIndex % options.BackgroundColors.Length],
                        BorderColor = options.ValueColors != null ? null : options.BorderColors[colorIndex % options.BorderColors.Length],
                        BorderWidth = options.BorderWidth
                    });
                    colorIndex++;
                }
            }

            if (options.ValueColors != null && options.ValueColors.Any())
            {
                foreach (var ds in datasets)
                {
                    if (ds.Label != null && options.ValueColors.ContainsKey(ds.Label))
                    {
                        ds.BackgroundColor = options.ValueColors[ds.Label];
                        ds.BorderColor = options.ValueColors[ds.Label];
                    }
                }
            }

            string chartDataJson = JsonConvert.SerializeObject(new { labels, datasets }, _jsonSettings);

            return $@"
                <div style='height: 400px;'><canvas id='{chartId}'></canvas></div>
                <script>
                    (function() {{
                        var ctx = document.getElementById('{chartId}');
                        if (!ctx) return;
                        new Chart(ctx.getContext('2d'), {{
                            type: 'bar',
                            data: {chartDataJson},
                            options: {{
                                indexAxis: '{(options.Horizontal ? "y" : "x")}',
                                responsive: true,
                                maintainAspectRatio: false,
                                plugins: {{
                                    legend: {{ display: true }},
                                    title: {{ 
                                        display: true, 
                                        text: '{options.ChartTitle}' 
                                    }}
                                }},
                                scales: {{
                                    x: {{ 
                                        stacked: {options.Stacked.ToString().ToLower()} 
                                    }},
                                    y: {{ 
                                        stacked: {options.Stacked.ToString().ToLower()}, 
                                        beginAtZero: true 
                                    }}
                                }}
                            }}
                        }});
                    }})();
                </script>";
        }

        public string RenderLineChart(DataTable data, Dictionary<string, string> instructions)
        {
            var options = FormatParser.ParseLineChartOptions(instructions);
            string chartId = "chart_" + Guid.NewGuid().ToString("N");

            instructions.TryGetValue("legends", out string legendsColumn);
            if (string.IsNullOrEmpty(legendsColumn) && data.Columns.Count > 0) legendsColumn = data.Columns[0].ColumnName;

            instructions.TryGetValue("series", out string seriesValue);
            if (string.IsNullOrEmpty(seriesValue) && data.Columns.Count > 1) seriesValue = data.Columns[1].ColumnName;
            var seriesColumns = ChartHelpers.ParseArray(seriesValue);

            if (data.Columns.Count == 0 || !data.Columns.Contains(legendsColumn) || seriesColumns.Any(s => !data.Columns.Contains(s)))
            {
                return "";
            }

            var labels = data.AsEnumerable().Select(r => r[legendsColumn].ToString()).Distinct().OrderBy(l => l).ToList();
            var datasets = new List<object>();
            int colorIndex = 0;

            foreach (var seriesName in seriesColumns)
            {
                datasets.Add(new
                {
                    label = seriesName,
                    data = data.AsEnumerable().Select(r => SafeConvertToDouble(r[seriesName])).ToList(),
                    borderColor = options.Colors[colorIndex % options.Colors.Length],
                    fill = false,
                    tension = options.Tension / 100.0
                });
                colorIndex++;
            }

            string chartDataJson = JsonConvert.SerializeObject(new { labels, datasets }, _jsonSettings);

            return $@"
                <div style='height: 400px;'><canvas id='{chartId}'></canvas></div>
                <script>
                    (function() {{
                        var ctx = document.getElementById('{chartId}');
                        if (!ctx) return;
                        new Chart(ctx.getContext('2d'), {{
                            type: 'line',
                            data: {chartDataJson},
                            options: {{
                                responsive: true,
                                maintainAspectRatio: false,
                                plugins: {{
                                    title: {{ 
                                        display: true, 
                                        text: '{options.ChartTitle}' 
                                    }}
                                }},
                                scales: {{
                                    y: {{
                                        beginAtZero: true
                                    }}
                                }}
                            }}
                        }});
                    }})();
                </script>";
        }

        public string RenderPieChart(DataTable data, Dictionary<string, string> instructions)
        {
            var options = FormatParser.ParsePieChartOptions(instructions);
            string chartId = "chart_" + Guid.NewGuid().ToString("N");

            instructions.TryGetValue("legends", out string legendsColumn);
            if (string.IsNullOrEmpty(legendsColumn) && data.Columns.Count > 0) legendsColumn = data.Columns[0].ColumnName;

            instructions.TryGetValue("series", out string seriesColumn);
            if (string.IsNullOrEmpty(seriesColumn) && data.Columns.Count > 1) seriesColumn = data.Columns[1].ColumnName;


            if (data.Columns.Count == 0 || !data.Columns.Contains(legendsColumn) || !data.Columns.Contains(seriesColumn))
            {
                return "";
            }

            var labels = data.AsEnumerable().Select(r => r[legendsColumn].ToString()).ToList();
            var values = data.AsEnumerable().Select(r => SafeConvertToDouble(r[seriesColumn])).ToList();

            var backgroundColors = new List<string>();
            if (options.ValueColors != null && options.ValueColors.Any())
            {
                backgroundColors = labels.Select(l => options.ValueColors.ContainsKey(l) ? options.ValueColors[l] : "#cccccc").ToList();
            }
            else
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    backgroundColors.Add(options.BackgroundColors[i % options.BackgroundColors.Length]);
                }
            }

            var datasets = new[] { new { data = values, backgroundColor = backgroundColors } };
            string chartDataJson = JsonConvert.SerializeObject(new { labels, datasets }, _jsonSettings);

            return $@"
                <div style='height: 400px;'><canvas id='{chartId}'></canvas></div>
                <script>
                    (function() {{
                        var ctx = document.getElementById('{chartId}');
                        if (!ctx) return;
                        new Chart(ctx.getContext('2d'), {{
                            type: '{(options.IsDoughnut ? "doughnut" : "pie")}',
                            data: {chartDataJson},
                            options: {{
                                responsive: true,
                                maintainAspectRatio: false,
                                plugins: {{
                                    title: {{ 
                                        display: true, 
                                        text: '{options.ChartTitle}' 
                                    }},
                                    legend: {{ 
                                        display: {options.ShowLegend.ToString().ToLower()} 
                                    }}
                                }}
                            }}
                        }});
                    }})();
                </script>";
        }

        public string RenderFilterComponent(DataTable data, Dictionary<string, string> instructions, Dictionary<string, string> requestParameters = null)
        {
            requestParameters = requestParameters ?? new Dictionary<string, string>();

            instructions.TryGetValue("id", out string id);
            if (string.IsNullOrEmpty(id)) id = "filter_" + Guid.NewGuid().ToString("N");

            instructions.TryGetValue("param", out string paramName);
            if (string.IsNullOrEmpty(paramName)) paramName = id;

            instructions.TryGetValue("label", out string label);
            if (string.IsNullOrEmpty(label)) label = paramName;

            instructions.TryGetValue("default", out string defaultValue);

            instructions.TryGetValue("valueField", out string valueField);
            if (string.IsNullOrEmpty(valueField) && data.Columns.Count > 0) valueField = data.Columns[0].ColumnName;

            instructions.TryGetValue("textField", out string textField);
            if (string.IsNullOrEmpty(textField)) textField = valueField;


            requestParameters.TryGetValue(paramName, out string currentValue);
            if (string.IsNullOrEmpty(currentValue)) currentValue = defaultValue;


            if (data.Columns.Count > 0 && (!data.Columns.Contains(valueField) || !data.Columns.Contains(textField)))
            {
                return $"<div class='alert alert-danger'>Error: Invalid valueField ('{valueField}') or textField ('{textField}') for filter.</div>";
            }

            var optionsHtml = new System.Text.StringBuilder();
            if (!instructions.ContainsKey("required"))
            {
                string selected = string.IsNullOrEmpty(currentValue) ? " selected" : "";
                optionsHtml.Append($"<option value=''{selected}>-- Select --</option>");
            }

            foreach (DataRow row in data.Rows)
            {
                string value = row[valueField].ToString();
                string text = row[textField].ToString();
                string selected = (value == currentValue) ? " selected" : "";
                optionsHtml.Append($"<option value='{HttpUtility.HtmlEncode(value)}'{selected}>{HttpUtility.HtmlEncode(text)}</option>");
            }

            return $@"
                <div class='filter-component mb-3'>
                    <label for='{id}' class='form-label'>{label}</label>
                    <select id='{id}' name='{paramName}' class='form-select filter-dropdown'>
                        {optionsHtml}
                    </select>
                </div>";
        }

        public string RenderFilterComponent(Dictionary<string, string> instructions)
        {
            return RenderFilterComponent(new DataTable(), instructions, new Dictionary<string, string>());
        }

        private static double SafeConvertToDouble(object obj)
        {
            if (obj == null || obj == DBNull.Value) return 0;
            double.TryParse(obj.ToString(), out double result);
            return result;
        }
    }
}

