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
            if (requestParameters == null)
            {
                requestParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                requestParameters = new Dictionary<string, string>(requestParameters, StringComparer.OrdinalIgnoreCase);
            }

            string pattern = @"\{\{((?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!)))\}\}";

            var managedFilterDefinitions = ExtractManagedFilterDefinitions(templateContent, pattern);
            var managedSegmentInfo = managedFilterDefinitions.Count > 0 ? ManagedSegmentHelper.GetManagedSegmentInfo() : null;

            return Regex.Replace(templateContent, pattern, match =>
            {
                string instructionContent = match.Groups[1].Value.Trim();
                var instructionsRaw = _instructionParser.ParseInstructions(instructionContent);
                var instructions = new Dictionary<string, string>(instructionsRaw, StringComparer.OrdinalIgnoreCase);
                string renderedComponent;

                try
                {
                    instructions.TryGetValue("representation", out var representationValue);
                    var representationLower = representationValue?.ToLowerInvariant();

                    DataTable data = new DataTable();
                    string query = null;

                    string paramName = null;
                    if (instructions.TryGetValue("param", out var paramValue) && !string.IsNullOrWhiteSpace(paramValue))
                    {
                        paramName = paramValue;
                    }
                    else if (instructions.TryGetValue("id", out var idValue) && !string.IsNullOrWhiteSpace(idValue))
                    {
                        paramName = idValue;
                    }

                    bool isFilter = string.Equals(representationLower, "filter", StringComparison.OrdinalIgnoreCase);
                    bool isManagedFilter = isFilter && instructions.TryGetValue("managedSegment", out var managedFlag) && ManagedSegmentHelper.IsTruthy(managedFlag);

                    if (isManagedFilter)
                    {
                        ManagedSegmentHelper.PrepareInstructionsForManagedFilter(instructions);
                        if (!string.IsNullOrWhiteSpace(paramName))
                        {
                            if (managedFilterDefinitions.TryGetValue(paramName, out var definition))
                            {
                                ManagedSegmentHelper.ResolveValue(requestParameters, paramName, definition.DefaultValue, managedSegmentInfo);
                            }
                            else
                            {
                                ManagedSegmentHelper.ResolveValue(requestParameters, paramName, null, managedSegmentInfo);
                            }
                        }

                        data = ManagedSegmentHelper.BuildOptionsTable(managedSegmentInfo);
                    }
                    else
                    {
                        if (instructions.TryGetValue("query", out var queryValue) || instructions.TryGetValue("dataSource", out queryValue))
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

                            if (queryParamNames.Count > 0 && managedFilterDefinitions.Count > 0)
                            {
                                var resolvedManaged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var name in queryParamNames)
                                {
                                    if (managedFilterDefinitions.TryGetValue(name, out var definition))
                                    {
                                        if (resolvedManaged.Add(name))
                                        {
                                            ManagedSegmentHelper.ResolveValue(requestParameters, name, definition.DefaultValue, managedSegmentInfo);
                                        }
                                    }
                                    else if (name.EndsWith("_admin", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var baseName = name.Substring(0, name.Length - "_admin".Length);
                                        if (managedFilterDefinitions.TryGetValue(baseName, out var baseDefinition) && resolvedManaged.Add(baseName))
                                        {
                                            ManagedSegmentHelper.ResolveValue(requestParameters, baseName, baseDefinition.DefaultValue, managedSegmentInfo);
                                        }
                                    }
                                }
                            }

                            var sqlParameters = new Dictionary<string, object>();
                            foreach (var name in queryParamNames)
                            {
                                if (requestParameters.TryGetValue(name, out string value) && !string.IsNullOrEmpty(value))
                                {
                                    sqlParameters[name] = value;
                                }
                                else
                                {
                                    sqlParameters[name] = DBNull.Value;
                                }
                            }

                            data = dataService.ExecuteQuery(query, sqlParameters);
                            try
                            {
                                var cols = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                                DebugHelper.Log($"Component rep='{representationValue}' rows={data.Rows.Count} cols=[{cols}] queryLen={(query?.Length ?? 0)}");
                            }
                            catch { /* logging best-effort */ }
                        }
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

        private Dictionary<string, ManagedFilterDefinition> ExtractManagedFilterDefinitions(string templateContent, string pattern)
        {
            var result = new Dictionary<string, ManagedFilterDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(templateContent, pattern))
            {
                var instructions = _instructionParser.ParseInstructions(match.Groups[1].Value.Trim());
                if (!instructions.TryGetValue("representation", out var representation) || !representation.Equals("filter", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!instructions.TryGetValue("managedSegment", out var managedFlag) || !ManagedSegmentHelper.IsTruthy(managedFlag))
                    continue;

                string paramName = null;
                if (instructions.TryGetValue("param", out var paramValue) && !string.IsNullOrWhiteSpace(paramValue))
                {
                    paramName = paramValue;
                }
                else if (instructions.TryGetValue("id", out var idValue) && !string.IsNullOrWhiteSpace(idValue))
                {
                    paramName = idValue;
                }

                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                instructions.TryGetValue("default", out var defaultValue);
                result[paramName] = new ManagedFilterDefinition
                {
                    ParamName = paramName,
                    DefaultValue = defaultValue
                };
            }
            return result;
        }

        private sealed class ManagedFilterDefinition
        {
            public string ParamName { get; set; }
            public string DefaultValue { get; set; }
        }
    }
}

