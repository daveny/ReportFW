using System.Collections.Generic;
using System.Data;

namespace Core.Helpers
{
    public interface IDataService
    {
        DataTable ExecuteQuery(string query, Dictionary<string, object> parameters = null);
    }

    public interface ITemplateProcessor
    {
        string ProcessTemplate(string templateContent, IDataService dataService, Dictionary<string, string> parameters = null);
    }

    public interface IChartRenderer
    {
        string RenderChart(DataTable data, Dictionary<string, string> instructions, Dictionary<string, string> requestParameters = null);
        string RenderDataTable(DataTable data, Dictionary<string, string> instructions);
        string RenderBarChart(DataTable data, Dictionary<string, string> instructions);
        string RenderLineChart(DataTable data, Dictionary<string, string> instructions);
        string RenderPieChart(DataTable data, Dictionary<string, string> instructions);
        string RenderFilterComponent(Dictionary<string, string> instructions);

        // Hozzáadva a hiányzó túlterhelés
        string RenderFilterComponent(DataTable data, Dictionary<string, string> instructions, Dictionary<string, string> requestParameters = null);
    }

    public interface IInstructionParser
    {
        Dictionary<string, string> ParseInstructions(string instructionContent);
    }
}

