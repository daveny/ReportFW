using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Core.Helpers;
using Core.Models;
using System.Text.RegularExpressions;

namespace Core.Controllers
{
    public class ReportController : Controller
    {
        private readonly IDataService _dataService;
        private readonly ITemplateProcessor _templateProcessor;
        private readonly string _connectionString;

        public ReportController()
            : this(new SqlDataService(System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString),
                  new TemplateProcessor())
        {
        }

        public ReportController(IDataService dataService, ITemplateProcessor templateProcessor)
        {
            _dataService = dataService;
            _templateProcessor = templateProcessor;
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        [Authorize]
        public ActionResult List()
        {
            string userName = User.Identity.Name;
            var allTemplates = GetReportTemplates();
            var devTemplates = GetReportTemplatesDEV();

            var favoriteReports = GetUserFavorites(userName);

            var allReportTiles = allTemplates.Select(t => new ReportTile
            {
                Name = t,
                IsFavorite = favoriteReports.Contains(t)
            }).ToList();

            var devReportTiles = devTemplates.Select(t => new ReportTile
            {
                Name = t,
                IsFavorite = favoriteReports.Contains(t)
            }).ToList();

            ViewBag.AllReports = allReportTiles;
            ViewBag.DevReports = devReportTiles;

            return View();
        }

        [HttpPost]
        [Authorize]
        // [ValidateAntiForgeryToken] // <-- IDEIGLENESEN KIKOMMENTELVE A HIBAKERESÉSHEZ
        public JsonResult ToggleFavorite(string reportName)
        {
            // Biztosítjuk, hogy a naplózó inicializálva legyen
            DebugHelper.Initialize();
            DebugHelper.Log($"ToggleFavorite called for report: '{reportName}' by user: '{User.Identity.Name}'");

            if (string.IsNullOrEmpty(reportName))
            {
                return Json(new { success = false, message = "Report name is required." });
            }

            string userName = User.Identity.Name;
            bool isCurrentlyFavorite = false;

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    DebugHelper.Log("Database connection opened successfully for ToggleFavorite.");

                    string checkSql = "SELECT COUNT(*) FROM UserReportFavorites WHERE UserName = @UserName AND ReportName = @ReportName";
                    using (var checkCmd = new SqlCommand(checkSql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@UserName", userName);
                        checkCmd.Parameters.AddWithValue("@ReportName", reportName);
                        DebugHelper.Log("Executing check query to see if report is favorite...");
                        isCurrentlyFavorite = (int)checkCmd.ExecuteScalar() > 0;
                        DebugHelper.Log($"Report is currently favorite: {isCurrentlyFavorite}");
                    }

                    if (isCurrentlyFavorite)
                    {
                        string deleteSql = "DELETE FROM UserReportFavorites WHERE UserName = @UserName AND ReportName = @ReportName";
                        using (var deleteCmd = new SqlCommand(deleteSql, connection))
                        {
                            deleteCmd.Parameters.AddWithValue("@UserName", userName);
                            deleteCmd.Parameters.AddWithValue("@ReportName", reportName);
                            DebugHelper.Log("Executing delete command...");
                            deleteCmd.ExecuteNonQuery();
                            DebugHelper.Log("Delete command successful.");
                        }
                        return Json(new { success = true, isFavorite = false });
                    }
                    else
                    {
                        string insertSql = "INSERT INTO UserReportFavorites (UserName, ReportName) VALUES (@UserName, @ReportName)";
                        using (var insertCmd = new SqlCommand(insertSql, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@UserName", userName);
                            insertCmd.Parameters.AddWithValue("@ReportName", reportName);
                            DebugHelper.Log("Executing insert command...");
                            insertCmd.ExecuteNonQuery();
                            DebugHelper.Log("Insert command successful.");
                        }
                        return Json(new { success = true, isFavorite = true });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.Log("--- EXCEPTION IN ToggleFavorite ---");
                DebugHelper.Log($"Message: {ex.Message}");
                DebugHelper.Log($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    DebugHelper.Log($"Inner Exception: {ex.InnerException.Message}");
                }
                DebugHelper.Log("---------------------------------");

                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        private HashSet<string> GetUserFavorites(string userName)
        {
            var favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "SELECT ReportName FROM UserReportFavorites WHERE UserName = @UserName";
                    using (var cmd = new SqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserName", userName);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                favorites.Add(reader["ReportName"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"Error getting user favorites: {ex.Message}");
            }
            return favorites;
        }

        #region Existing Code
        public ActionResult RenderReport(string templateName)
        {
            try
            {
                DebugHelper.Initialize();
                DebugHelper.Log($"Starting RenderReport for template: {templateName}");
                var filterValues = GetFilterValuesFromRequest();
                string templatePath = Server.MapPath($"~/Views/Reports/Templates/{templateName}.thtml");
                if (!System.IO.File.Exists(templatePath))
                {
                    return Content($"<div class='alert alert-danger'>Template '{templateName}' not found.</div>");
                }
                string templateContent = System.IO.File.ReadAllText(templatePath);
                templateContent = Regex.Replace(templateContent, @"<!--[\s\S]*?-->", string.Empty, RegexOptions.Singleline);
                string renderedContent = _templateProcessor.ProcessTemplate(templateContent, _dataService, filterValues);
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
        public ActionResult RenderReportDEV(string templateName)
        {
            try
            {
                DebugHelper.Initialize();
                DebugHelper.Log($"Starting RenderReportDEV for template: {templateName}");
                var filterValues = GetFilterValuesFromRequest();
                string templatePath = Server.MapPath($"~/Views/Reports/TemplatesDEV/{templateName}.thtml");
                if (!System.IO.File.Exists(templatePath))
                {
                    return Content($"<div class='alert alert-danger'>DEV Template '{templateName}' not found.</div>");
                }
                string templateContent = System.IO.File.ReadAllText(templatePath);
                templateContent = Regex.Replace(templateContent, @"<!--[\s\S]*?-->", string.Empty, RegexOptions.Singleline);
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
            string filterPanel = $@"<div class='card mb-4 filter-panel'><div class='card-header d-flex justify-content-between align-items-center bg-light' data-bs-toggle='collapse' data-bs-target='#filterCollapse' style='cursor: pointer;'><h3 class='mb-0'>Filter Options <span class='filter-count badge bg-primary'>{filterControls.Count}</span></h3><button type='submit' form='reportForm' class='btn btn-primary'>Apply Filters</button></div><div id='filterCollapse' class='collapse show'><div class='card-body'><div class='row'>{string.Join("", filterControls)}</div></div></div></div>";
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
        public static List<string> GetReportTemplates(bool isDev = false)
        {
            string dir = isDev ? "TemplatesDEV" : "Templates";
            string templatePath = System.Web.Hosting.HostingEnvironment.MapPath($"~/Views/Reports/{dir}");
            if (!Directory.Exists(templatePath)) return new List<string>();
            return Directory.GetFiles(templatePath, "*.thtml").Select(Path.GetFileNameWithoutExtension).OrderBy(name => name).ToList();
        }
        public static List<string> GetReportTemplates() { return GetReportTemplates(false); }
        public static List<string> GetReportTemplatesDEV() { return GetReportTemplates(true); }
        #endregion
    }
}
