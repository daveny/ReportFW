using System.Collections.Generic;
using System.Data;

namespace Core.Helpers
{
    public interface IDataService
    {
        /// <summary>
        /// Executes a SQL query securely with parameters to prevent SQL injection.
        /// </summary>
        /// <param name="query">The SQL query with parameter placeholders (e.g., @paramName).</param>
        /// <param name="parameters">A dictionary of parameters to pass to the query.</param>
        /// <returns>A DataTable containing the results of the query.</returns>
        DataTable ExecuteQuery(string query, Dictionary<string, object> parameters);
    }

    public interface ITemplateProcessor
    {
        string ProcessTemplate(string templateContent, IDataService dataService, Dictionary<string, string> parameters = null);
    }

    public interface IChartRenderer
    {
        string RenderChart(DataTable data, Dictionary<string, string> instructions);
        string RenderDataTable(DataTable data, Dictionary<string, string> instructions);
        string RenderBarChart(DataTable data, Dictionary<string, string> instructions);
        string RenderLineChart(DataTable data, Dictionary<string, string> instructions);
        string RenderPieChart(DataTable data, Dictionary<string, string> instructions);
        string RenderFilterComponent(Dictionary<string, string> instructions);
        string RenderFilterComponent(DataTable data, Dictionary<string, string> instructions);
    }

    public interface IInstructionParser
    {
        Dictionary<string, string> ParseInstructions(string instructionContent);
    }
}