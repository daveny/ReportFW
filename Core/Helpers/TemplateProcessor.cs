using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Core.Helpers
{
    public class TemplateProcessor : ITemplateProcessor
    {
        private readonly IInstructionParser _instructionParser;
        private readonly IChartRenderer _chartRenderer;

        public TemplateProcessor()
        {
            _instructionParser = new InstructionParser();
            _chartRenderer = new ChartRenderer();
        }

        public string ProcessTemplate(string templateContent, IDataService dataService, Dictionary<string, string> requestParameters = null)
        {
            if (requestParameters == null) requestParameters = new Dictionary<string, string>();

            string pattern = @"\{\{((?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!)))\}\}";

            return Regex.Replace(templateContent, pattern, match =>
            {
                string instructionContent = match.Groups[1].Value.Trim();
                var instructions = _instructionParser.ParseInstructions(instructionContent);
                string renderedComponent;

                try
                {
                    DataTable data = new DataTable();
                    string query = null;

                    if (instructions.TryGetValue("query", out string queryValue) || instructions.TryGetValue("dataSource", out queryValue))
                    {
                        if (!string.IsNullOrWhiteSpace(queryValue))
                        {
                            query = queryValue;
                        }
                    }

                    if (!string.IsNullOrEmpty(query))
                    {
                        var queryParamNames = Regex.Matches(query, @"@(\w+)")
                                                   .Cast<Match>()
                                                   .Select(m => m.Groups[1].Value)
                                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                                   .ToList();

                        var sqlParameters = new Dictionary<string, object>();
                        foreach (var paramName in queryParamNames)
                        {
                            // JAVÍTVA: Biztonságos paraméter-hozzárendelés
                            if (requestParameters.TryGetValue(paramName, out string value) && !string.IsNullOrEmpty(value))
                            {
                                sqlParameters[paramName] = value;
                            }
                            else
                            {
                                sqlParameters[paramName] = DBNull.Value;
                            }
                        }

                        data = dataService.ExecuteQuery(query, sqlParameters);
                    }

                    renderedComponent = _chartRenderer.RenderChart(data, instructions, requestParameters);
                }
                catch (Exception ex)
                {
                    renderedComponent = $"<div class='alert alert-danger'><strong>Error processing component:</strong><br><pre>{HttpUtility.HtmlEncode(ex.Message)}</pre></div>";
                }
                return renderedComponent;
            });
        }
    }
}

