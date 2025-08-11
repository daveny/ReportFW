using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace Core.Helpers
{
    /// <summary>
    /// Feldolgozza a sablonfájl tartalmát, biztonságosan végrehajtja a beágyazott lekérdezéseket
    /// és behelyettesíti a riport-komponensek generált HTML kódját.
    /// Ez az osztály kiváltja a sebezhető FilterProcessor-t.
    /// </summary>
    public class TemplateProcessor : ITemplateProcessor
    {
        private readonly IInstructionParser _instructionParser;
        private readonly IChartRenderer _chartRenderer;

        // Regex a {{...}} tokenek megtalálására.
        private static readonly Regex TokenRegex = new Regex(@"\{\{((?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!)))\}\}", RegexOptions.Compiled);

        // Regex a @paraméter nevek kinyerésére a query stringből.
        private static readonly Regex ParamRegex = new Regex(@"@(\w+)", RegexOptions.Compiled);

        public TemplateProcessor()
        {
            _instructionParser = new InstructionParser();
            _chartRenderer = new ChartRenderer();
        }

        public string ProcessTemplate(string templateContent, IDataService dataService, Dictionary<string, string> requestParameters = null)
        {
            if (requestParameters == null)
            {
                requestParameters = new Dictionary<string, string>();
            }

            // A feldolgozás során a MatchEvaluator-t használjuk, ami minden egyes tokenre lefut.
            string processedContent = TokenRegex.Replace(templateContent, match =>
            {
                string instructionContent = match.Groups[1].Value.Trim();
                var instructions = _instructionParser.ParseInstructions(instructionContent);

                try
                {
                    // Ha a token egy szűrő definíciója, akkor azt rendereljük.
                    if (instructions.ContainsKey("representation") && instructions["representation"].Equals("filter", StringComparison.OrdinalIgnoreCase))
                    {
                        // A szűrő rendereléséhez átadjuk a requestből érkező értékeket, hogy a megfelelő
                        // opciók legyenek kiválasztva (pl. egy legördülő menüben).
                        if (instructions.ContainsKey("param") && requestParameters.ContainsKey(instructions["param"]))
                        {
                            instructions["value"] = requestParameters[instructions["param"]];
                        }
                        return _chartRenderer.RenderFilterComponent(instructions);
                    }

                    // Ha a token egy adat-komponens (tábla, diagram), akkor lekérdezést hajtunk végre.
                    if (instructions.ContainsKey("query"))
                    {
                        string query = instructions["query"];

                        // 1. Kinyerjük a paraméterneveket a lekérdezésből.
                        var queryParamNames = ParamRegex.Matches(query)
                            .Cast<Match>()
                            .Select(m => m.Groups[1].Value)
                            .Distinct()
                            .ToList();

                        // 2. Összegyűjtjük a paraméterek értékeit a requestből.
                        var sqlParameters = new Dictionary<string, object>();
                        foreach (var paramName in queryParamNames)
                        {
                            if (requestParameters.TryGetValue(paramName, out var value) && !string.IsNullOrEmpty(value))
                            {
                                sqlParameters[paramName] = value;
                            }
                            else
                            {
                                sqlParameters[paramName] = DBNull.Value;
                            }
                        }

                        // 3. Biztonságos, paraméterezett lekérdezés végrehajtása.
                        DataTable data = dataService.ExecuteQuery(query, sqlParameters);

                        // 4. A komponens renderelése a kapott adatokkal.
                        return _chartRenderer.RenderChart(data, instructions);
                    }
                }
                catch (Exception ex)
                {
                    // Hiba esetén egyértelmű hibaüzenetet jelenítünk meg a riportban.
                    DebugHelper.Log($"Error processing token: {match.Value}\n{ex.Message}\n{ex.StackTrace}");
                    return $"<div class='alert alert-danger'><strong>Error processing component:</strong><br><pre>{ex.Message}</pre></div>";
                }

                // Ha a token nem felismerhető, változatlanul hagyjuk.
                return match.Value;
            });

            return processedContent;
        }

private string RenderFilterComponent(Dictionary<string, string> instructions)
        {
            // Get filter properties
            string filterId = instructions.ContainsKey("id")
                ? instructions["id"]
                : "filter_" + Guid.NewGuid().ToString("N");

            string filterLabel = instructions.ContainsKey("label")
                ? instructions["label"]
                : "Select Value";

            string filterName = instructions.ContainsKey("name")
                ? instructions["name"]
                : "Value";

            string filterValue = instructions.ContainsKey("value")
                ? instructions["value"]
                : "";

            // Check if we need to get values from a query
            List<string> options = new List<string>();
            if (instructions.ContainsKey("options"))
            {
                // Parse static options
                string optionsStr = instructions["options"];
                if (optionsStr.StartsWith("[") && optionsStr.EndsWith("]"))
                {
                    optionsStr = optionsStr.Substring(1, optionsStr.Length - 2);
                    options = optionsStr.Split(',')
                        .Select(o => o.Trim().Trim('\'', '"'))
                        .ToList();
                }
            }

            // Render the filter HTML
            string html = $@"
    <div class='filter-component'>
        <label for='{filterId}'>{filterLabel}</label>
        <input type='text' id='{filterId}' name='{filterName}' value='{filterValue}' class='form-control' />
    </div>
    <script>
        $(document).ready(function() {{
            $('#{filterId}').on('change', function() {{
                // Submit the form when filter changes
                $(this).closest('form').submit();
            }});
        }});
    </script>";

            return html;
        }

        private string AddFilterSupportScript()
        {
            return @"
<script>
    $(document).ready(function() {
        // Handle dropdown filters
        $('.filter-dropdown').on('change', function() {
            var filter = $(this);
            var paramName = filter.data('param-name');
            var value = filter.val();
            
            // Update URL with the filter value
            updateUrlParameter(paramName, value);
            
            // Check if this filter affects any chart placeholders
            refreshAffectedCharts(paramName, value);
        });
        
        // Handle button group filters
        $('.filter-button').on('click', function() {
            var button = $(this);
            var paramName = button.data('param-name');
            var value = button.data('value');
            
            // Update active state in button group
            button.siblings().removeClass('btn-primary').addClass('btn-secondary');
            button.removeClass('btn-secondary').addClass('btn-primary');
            
            // Update URL with the filter value
            updateUrlParameter(paramName, value);
            
            // Check if this filter affects any chart placeholders
            refreshAffectedCharts(paramName, value);
        });
        
        // Handle text filters with Apply button
        $('.filter-apply-btn').on('click', function() {
            var input = $(this).closest('.input-group').find('.filter-text');
            var paramName = input.data('param-name');
            var value = input.val();
            
            // Update URL with the filter value
            updateUrlParameter(paramName, value);
            
            // Check if this filter affects any chart placeholders
            refreshAffectedCharts(paramName, value);
        });
        
        // Handle calendar filters
        $('.datepicker').on('changeDate', function() {
            var calendar = $(this);
            var paramName = calendar.data('param-name');
            var value = calendar.val();
            
            // Update URL with the filter value
            updateUrlParameter(paramName, value);
            
            // Check if this filter affects any chart placeholders
            refreshAffectedCharts(paramName, value);
        });
        
        // Helper function to update URL with filter parameter
        function updateUrlParameter(key, value) {
            var url = window.location.href;
            var re = new RegExp('([?&])' + key + '=.*?(&|$)', 'i');
            var separator = url.indexOf('?') !== -1 ? '&' : '?';
            
            if (url.match(re)) {
                url = url.replace(re, '$1' + key + '=' + value + '$2');
            } else {
                url = url + separator + key + '=' + value;
            }
            
            // Update the URL without reloading the page
            window.history.pushState({path: url}, '', url);
        }
        
        // Helper function to check and refresh charts that depend on a filter
        function refreshAffectedCharts(paramName, value) {
            // Check all placeholders to see if they need this parameter
            $('.chart-placeholder').each(function() {
                var placeholder = $(this);
                var neededParams = placeholder.data('needs-params').split(',');
                
                // If this placeholder needs the parameter that changed
                if (neededParams.includes(paramName)) {
                    // Remove this parameter from the needed list
                    neededParams = neededParams.filter(p => p !== paramName);
                    
                    // Update the placeholder's data attribute
                    placeholder.data('needs-params', neededParams.join(','));
                    
                    // If no more parameters are needed, we can load the chart
                    if (neededParams.length === 0) {
                        loadChartWithFilters(placeholder);
                    }
                }
            });
        }
        
        // Function to load a chart with all filter values
        function loadChartWithFilters(placeholder) {
            // Get the token (chart instructions)
            var token = placeholder.data('token');
            
            // Collect all current filter values
            var filterValues = {};
            $('.filter-dropdown, .filter-text, .datepicker').each(function() {
                var filter = $(this);
                filterValues[filter.data('param-name')] = filter.val();
            });
            
            // Add values from button groups
            $('.btn-group').each(function() {
                var activeButton = $(this).find('.btn-primary');
                if (activeButton.length) {
                    filterValues[activeButton.data('param-name')] = activeButton.data('value');
                }
            });
            
            // Send AJAX request to update the chart
            $.ajax({
                url: '/Report/UpdateChart',
                type: 'POST',
                data: {
                    token: token,
                    filterValues: filterValues
                },
                success: function(response) {
                    // Replace the placeholder with the chart
                    placeholder.replaceWith(response);
                },
                error: function(xhr, status, error) {
                    placeholder.html('<div class=""alert alert-danger"">Error loading chart: ' + error + '</div>');
                }
            });
        }
    });
</script>";
        }
    }
}