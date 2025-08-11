using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Web.Mvc;
using System.Linq;
using Core.Helpers;
using System.Text.RegularExpressions;

namespace Core.Controllers
{
    public class ReportController : Controller
    {
        private readonly IDataService _dataService;
        private readonly ITemplateProcessor _templateProcessor;

        // A konstruktorban már az új, biztonságos TemplateProcessor-t példányosítjuk.
        public ReportController()
            : this(new SqlDataService(System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString),
                  new TemplateProcessor())
        {
        }

        // DI-kompatibilis konstruktor.
        public ReportController(IDataService dataService, ITemplateProcessor templateProcessor)
        {
            _dataService = dataService;
            _templateProcessor = templateProcessor;
        }

        /// <summary>
        /// Riport renderelése sablonból, szűrők támogatásával.
        /// </summary>
        public ActionResult RenderReport(string templateName)
        {
            try
            {
                DebugHelper.Initialize(Server);
                DebugHelper.Log($"Starting RenderReport for template: {templateName}");

                // 1. Szűrőértékek összegyűjtése a requestből (QueryString vagy Form).
                var filterValues = GetFilterValuesFromRequest();
                DebugHelper.Log($"Rendering report with {filterValues.Count} filter values.");

                // 2. THTML sablon betöltése.
                string templatePath = Server.MapPath($"~/Views/Reports/Templates/{templateName}.thtml");
                if (!System.IO.File.Exists(templatePath))
                {
                    DebugHelper.Log($"Template file not found: {templatePath}");
                    return Content($"<div class='alert alert-danger'>Template '{templateName}' not found.</div>");
                }
                string templateContent = System.IO.File.ReadAllText(templatePath);

                // HTML kommentek eltávolítása, hogy a kikommentelt tokenek ne okozzanak hibát.
                templateContent = Regex.Replace(templateContent, @"<!--[\s\S]*?-->", string.Empty, RegexOptions.Singleline);

                // 3. JAVÍTOTT RÉSZ: A sablon feldolgozása az új, biztonságos TemplateProcessor-ral.
                // A FilterProcessorra már nincs szükség.
                string renderedContent = _templateProcessor.ProcessTemplate(templateContent, _dataService, filterValues);

                // 4. Tartalom és szűrőértékek átadása a View-nak.
                ViewBag.ReportContent = WrapReportWithForm(renderedContent, templateName);
                ViewBag.ReportName = templateName;
                ViewBag.FilterValues = filterValues;

                return View();
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error in RenderReport: {ex.Message}\n{ex.StackTrace}");
                return Content($"<div class='alert alert-danger'><h4>Error Rendering Report</h4><p>{ex.Message}</p><details><summary>Stack Trace</summary><pre>{ex.StackTrace}</pre></details></div>");
            }
        }

        /// <summary>
        /// Fejlesztői verzió a riport rendereléséhez.
        /// </summary>
        public ActionResult RenderReportDEV(string templateName)
        {
            try
            {
                DebugHelper.Initialize(Server);
                DebugHelper.Log($"Starting RenderReportDEV for template: {templateName}");

                var filterValues = GetFilterValuesFromRequest();
                DebugHelper.Log($"Rendering DEV report with {filterValues.Count} filter values.");

                string templatePath = Server.MapPath($"~/Views/Reports/TemplatesDEV/{templateName}.thtml");
                if (!System.IO.File.Exists(templatePath))
                {
                    DebugHelper.Log($"Template DEV file not found: {templatePath}");
                    return Content($"<div class='alert alert-danger'>DEV Template '{templateName}' not found.</div>");
                }
                string templateContent = System.IO.File.ReadAllText(templatePath);
                templateContent = Regex.Replace(templateContent, @"<!--[\s\S]*?-->", string.Empty, RegexOptions.Singleline);

                // JAVÍTOTT RÉSZ: Itt is az új processzort használjuk.
                string renderedContent = _templateProcessor.ProcessTemplate(templateContent, _dataService, filterValues);

                ViewBag.ReportContent = WrapReportWithForm(renderedContent, templateName, true);
                ViewBag.ReportName = templateName + " (DEV)";
                ViewBag.FilterValues = filterValues;
                ViewBag.IsDev = true;

                return View("RenderReport");
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error in RenderReportDEV: {ex.Message}\n{ex.StackTrace}");
                return Content($"<div class='alert alert-danger'><h4>Error Rendering DEV Report</h4><p>{ex.Message}</p><details><summary>Stack Trace</summary><pre>{ex.StackTrace}</pre></details></div>");
            }
        }

        /// <summary>
        /// AJAX végpont a szűrők legördülő listáinak feltöltéséhez.
        /// </summary>
        [HttpGet]
        public ActionResult GetFilterData(string query, string valueField, string textField)
        {
            try
            {
                DebugHelper.Log($"GetFilterData called with query: {query}");
                DataTable data = _dataService.ExecuteQuery(query, null);

                var result = data.AsEnumerable().Select(row => new {
                    value = row[valueField].ToString(),
                    text = string.IsNullOrEmpty(textField) ? row[valueField].ToString() : row[textField].ToString()
                }).ToList();

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error in GetFilterData: {ex.Message}");
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// AJAX végpont egy riport komponens (pl. diagram) frissítéséhez a szűrőértékek alapján.
        /// </summary>
        [HttpPost]
        public ActionResult UpdateChart()
        {
            try
            {
                string token = Request.Form["token"];
                if (string.IsNullOrEmpty(token))
                {
                    return Content("<div class='alert alert-danger'>Error: Missing chart token</div>");
                }

                var filterValues = GetFilterValuesFromRequest();
                string chartHtml = _templateProcessor.ProcessTemplate(token, _dataService, filterValues);

                return Content(chartHtml);
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error in UpdateChart: {ex.Message}\n{ex.StackTrace}");
                return Content($"<div class='alert alert-danger'>Error updating chart: {ex.Message}</div>");
            }
        }

        #region Helper Methods

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

        private string WrapReportWithForm(string reportContent, string templateName, bool isDev = false)
        {
            var filterControls = ExtractFilterControls(reportContent);
            if (!filterControls.Any())
            {
                return reportContent;
            }

            string actionUrl = Url.Action(isDev ? "RenderReportDEV" : "RenderReport", "Report", new { templateName });
            string filterPanel = $@"
<div class='card mb-4 filter-panel'>
    <div class='card-header d-flex justify-content-between align-items-center bg-light' data-bs-toggle='collapse' data-bs-target='#filterCollapse' style='cursor: pointer;'>
        <h3 class='mb-0'>Filter Options <span class='filter-count badge bg-primary'>{filterControls.Count}</span></h3>
        <button type='submit' form='reportForm' class='btn btn-primary'>Apply Filters</button>
    </div>
    <div id='filterCollapse' class='collapse show'>
        <div class='card-body'><div class='row'>{string.Join("", filterControls)}</div></div>
    </div>
</div>";

            string cleanedReportContent = CleanupReportContent(reportContent);
            return $"<form id='reportForm' action='{actionUrl}' method='get'>{filterPanel}<div class='report-content'>{cleanedReportContent}</div></form>";
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

        public static List<string> GetReportTemplates()
        {
            try
            {
                string templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/Reports/Templates");
                if (!Directory.Exists(templatePath)) return new List<string>();

                return Directory.GetFiles(templatePath, "*.thtml")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(name => name)
                    .ToList();
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error in GetReportTemplates: {ex.Message}");
                return new List<string>();
            }
        }

        public static List<string> GetReportTemplatesDEV()
        {
            try
            {
                string templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/Reports/TemplatesDEV");
                if (!Directory.Exists(templatePath)) return new List<string>();

                return Directory.GetFiles(templatePath, "*.thtml")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(name => name)
                    .ToList();
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error in GetReportTemplatesDEV: {ex.Message}");
                return new List<string>();
            }
        }
        #endregion
    }
}
