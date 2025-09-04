// Fájl: Controllers/MetricController.cs
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Core.Helpers;
using Core.Models;

namespace Core.Controllers
{
    [Authorize]
    public class MetricController : Controller
    {
        private readonly string _connectionString;
        private readonly ITemplateProcessor _templateProcessor;
        private readonly IDataService _dataService;
        private readonly IChartRenderer _chartRenderer; // Renderer a szűrőkhöz

        public MetricController()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            _dataService = new SqlDataService(_connectionString);
            _templateProcessor = new TemplateProcessor();
            _chartRenderer = new ChartRenderer(); // Példányosítás
        }

        public ActionResult Library()
        {
            var metrics = GetMetricsWithMetadata();
            return View(metrics);
        }

        // --- JAVÍTVA: A MyMetrics metódus logikája ---
        public ActionResult MyMetrics()
        {
            var favoriteMetrics = GetMetricsWithMetadata().Where(m => m.IsFavorite).ToList();
            var filterValues = GetFilterValuesFromRequest();

            // 1. Összegyűjtjük az összes egyedi szűrőt a kedvenc metrikákból
            var uniqueFilters = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var instructionParser = new InstructionParser();
            var tokenRegex = new Regex(@"\{\{((?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!)))\}\}");

            foreach (var metric in favoriteMetrics)
            {
                string templatePath = Server.MapPath($"~/Views/Metrics/Templates/{metric.FileName}.thtml");
                if (System.IO.File.Exists(templatePath))
                {
                    string templateContent = System.IO.File.ReadAllText(templatePath);
                    foreach (Match match in tokenRegex.Matches(templateContent))
                    {
                        var instructions = instructionParser.ParseInstructions(match.Groups[1].Value);
                        if (instructions.TryGetValue("representation", out var rep) && rep.Equals("filter", StringComparison.OrdinalIgnoreCase))
                        {
                            if (instructions.TryGetValue("param", out var paramName) && !uniqueFilters.ContainsKey(paramName))
                            {
                                uniqueFilters[paramName] = instructions;
                            }
                        }
                    }
                }
            }

            // 2. Legeneráljuk a közös szűrőpanel HTML kódját
            string filterPanelHtml = "";
            if (uniqueFilters.Any())
            {
                var filterControls = new List<string>();
                foreach (var instructions in uniqueFilters.Values)
                {
                    // Beállítjuk a szűrő aktuális értékét a requestből
                    if (instructions.TryGetValue("param", out var paramName) && filterValues.ContainsKey(paramName))
                    {
                        instructions["value"] = filterValues[paramName];
                    }
                    // T�lts�k fel az opci�kat a filter query-j�b�l, ha van, �s adjuk �t a request param�tereket is
                    System.Data.DataTable __data = new System.Data.DataTable();
                    if (instructions.TryGetValue("query", out var __q) || instructions.TryGetValue("dataSource", out __q))
                    {
                        if (!string.IsNullOrWhiteSpace(__q))
                        {
                            var __names = System.Text.RegularExpressions.Regex.Matches(__q, @"@(\w+)")
                                .Cast<System.Text.RegularExpressions.Match>()
                                .Select(m => m.Groups[1].Value)
                                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            var __params = new System.Collections.Generic.Dictionary<string, object>();
                            foreach (var __p in __names)
                                __params[__p] = (filterValues.TryGetValue(__p, out var __v) && !string.IsNullOrEmpty(__v)) ? (object)__v : System.DBNull.Value;
                            __data = _dataService.ExecuteQuery(__q, __params);
                        }
                    }
                    var __html = _chartRenderer.RenderFilterComponent(__data, instructions, filterValues);
                    filterControls.Add($"<div class='col-md-3'>{__html}</div>");
                }
                filterPanelHtml = $@"<div class='card mb-4 filter-panel'><div class='card-header d-flex justify-content-between align-items-center bg-light'><h3 class='mb-0'>Global Filters</h3><button type='submit' form='myMetricsForm' class='btn btn-primary'>Apply</button></div><div class='card-body'><div class='row'>{string.Join("", filterControls)}</div></div></div>";
            }
            ViewBag.FilterPanel = new MvcHtmlString(filterPanelHtml);

            // 3. Rendereljük a metrikákat a szűrőértékekkel
            var renderedMetrics = new Dictionary<MetricTile, MvcHtmlString>();
            foreach (var metric in favoriteMetrics)
            {
                string templatePath = Server.MapPath($"~/Views/Metrics/Templates/{metric.FileName}.thtml");
                if (System.IO.File.Exists(templatePath))
                {
                    string templateContent = System.IO.File.ReadAllText(templatePath);
                    string renderedHtml = _templateProcessor.ProcessTemplate(templateContent, _dataService, filterValues);
                    renderedMetrics.Add(metric, new MvcHtmlString(CleanupReportContent(renderedHtml))); // Tisztítás, hogy a szűrők ne jelenjenek meg a metrikán belül
                }
            }

            return View(renderedMetrics);
        }

        public ActionResult Display(string metricName)
        {
            if (string.IsNullOrEmpty(metricName))
            {
                return new HttpNotFoundResult("Metric name cannot be empty.");
            }

            string templatePath = Server.MapPath($"~/Views/Metrics/Templates/{metricName}.thtml");
            if (!System.IO.File.Exists(templatePath))
            {
                return new HttpNotFoundResult($"Metric template not found: {metricName}.thtml");
            }

            string templateContent = System.IO.File.ReadAllText(templatePath);
            var filterValues = GetFilterValuesFromRequest();

            ViewBag.MetricName = ParseMetaTag(templateContent, "meta-name");
            ViewBag.MetricDescription = ParseMetaTag(templateContent, "meta-description");
            ViewBag.MetricOwner = ParseMetaTag(templateContent, "meta-owner");
            ViewBag.FileName = metricName;

            string renderedContent = _templateProcessor.ProcessTemplate(templateContent, _dataService, filterValues);
            ViewBag.RenderedMetric = WrapMetricWithForm(renderedContent, metricName);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleFavorite(string metricName)
        {
            string userName = User.Identity.Name;
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    string checkSql = "SELECT COUNT(*) FROM UserMetricFavorites WHERE UserName = @UserName AND MetricName = @MetricName";
                    using (var checkCmd = new SqlCommand(checkSql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@UserName", userName);
                        checkCmd.Parameters.AddWithValue("@MetricName", metricName);
                        bool isFavorite = (int)checkCmd.ExecuteScalar() > 0;

                        if (isFavorite)
                        {
                            string deleteSql = "DELETE FROM UserMetricFavorites WHERE UserName = @UserName AND MetricName = @MetricName";
                            using (var cmd = new SqlCommand(deleteSql, connection))
                            {
                                cmd.Parameters.AddWithValue("@UserName", userName);
                                cmd.Parameters.AddWithValue("@MetricName", metricName);
                                cmd.ExecuteNonQuery();
                            }
                            return Json(new { success = true, isFavorite = false });
                        }
                        else
                        {
                            string insertSql = "INSERT INTO UserMetricFavorites (UserName, MetricName) VALUES (@UserName, @MetricName)";
                            using (var cmd = new SqlCommand(insertSql, connection))
                            {
                                cmd.Parameters.AddWithValue("@UserName", userName);
                                cmd.Parameters.AddWithValue("@MetricName", metricName);
                                cmd.ExecuteNonQuery();
                            }
                            return Json(new { success = true, isFavorite = true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods
        private List<MetricTile> GetMetricsWithMetadata()
        {
            string userName = User.Identity.Name;
            var favoriteMetrics = GetUserFavorites(userName);
            var metricFiles = GetMetricTemplates();
            var metrics = new List<MetricTile>();

            foreach (var fileName in metricFiles)
            {
                string filePath = Server.MapPath($"~/Views/Metrics/Templates/{fileName}.thtml");
                string content = System.IO.File.ReadAllText(filePath);

                metrics.Add(new MetricTile
                {
                    FileName = fileName,
                    Name = ParseMetaTag(content, "meta-name"),
                    Description = ParseMetaTag(content, "meta-description"),
                    Owner = ParseMetaTag(content, "meta-owner"),
                    IsFavorite = favoriteMetrics.Contains(fileName)
                });
            }
            return metrics.OrderBy(m => m.Name).ToList();
        }

        private string ParseMetaTag(string fileContent, string tagName)
        {
            var match = Regex.Match(fileContent, $@"<!--\s*{tagName}:\s*(.*?)\s*-->");
            return match.Success ? match.Groups[1].Value.Trim() : $"({tagName} not found)";
        }

        private HashSet<string> GetUserFavorites(string userName)
        {
            var favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT MetricName FROM UserMetricFavorites WHERE UserName = @UserName";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserName", userName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) { favorites.Add(reader["MetricName"].ToString()); }
                    }
                }
            }
            return favorites;
        }

        public static List<string> GetMetricTemplates()
        {
            string templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/Metrics/Templates");
            if (!Directory.Exists(templatePath)) return new List<string>();
            return Directory.GetFiles(templatePath, "*.thtml").Select(Path.GetFileNameWithoutExtension).ToList();
        }

        private Dictionary<string, string> GetFilterValuesFromRequest()
        {
            var filterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allKeys = Request.QueryString.AllKeys.Concat(Request.Form.AllKeys).Distinct();
            foreach (string key in allKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    filterValues[key] = Request[key];
                }
            }
            return filterValues;
        }

        private MvcHtmlString WrapMetricWithForm(string content, string metricName)
        {
            var filterControls = ExtractFilterControls(content);
            if (!filterControls.Any())
            {
                return new MvcHtmlString(content);
            }

            string hiddenInput = $"<input type='hidden' name='metricName' value='{metricName}' />";
            string filterPanel = $@"<div class='card mb-4 filter-panel'><div class='card-header d-flex justify-content-between align-items-center bg-light'><h3 class='mb-0'>Filter Options</h3></div><div class='card-body'><div class='row'>{string.Join("", filterControls)}</div></div></div>";
            string cleanedContent = CleanupReportContent(content);
            string fullHtml = $"<form id='metricForm' action='{Url.Action("Display", "Metric")}' method='get'>{hiddenInput}{filterPanel}<div class='metric-content-full'>{cleanedContent}</div></form>";
            return new MvcHtmlString(fullHtml);
        }

        private List<string> ExtractFilterControls(string reportContent)
        {
            var filters = new List<string>();
            string pattern = @"<div\s+class\s*=\s*['""]filter-component.*?</div>";
            foreach (Match match in Regex.Matches(reportContent, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                filters.Add($"<div class='col-md-3'>{match.Value}</div>");
            }
            return filters;
        }

        private string CleanupReportContent(string reportContent)
        {
            string pattern = @"<div\s+class\s*=\s*['""]filter-component.*?</div>";
            return Regex.Replace(reportContent, pattern, "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        }
        #endregion
    }
}
