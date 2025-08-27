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

        public MetricController()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            _dataService = new SqlDataService(_connectionString);
            _templateProcessor = new TemplateProcessor();
        }

        // --- Metric Library Page ---
        public ActionResult Library()
        {
            var metrics = GetMetricsWithMetadata();
            return View(metrics);
        }

        // --- My Metrics Page ---
        public ActionResult MyMetrics()
        {
            var favoriteMetrics = GetMetricsWithMetadata().Where(m => m.IsFavorite).ToList();
            var renderedMetrics = new Dictionary<MetricTile, MvcHtmlString>();

            foreach (var metric in favoriteMetrics)
            {
                string templatePath = Server.MapPath($"~/Views/Metrics/Templates/{metric.FileName}.thtml");
                if (System.IO.File.Exists(templatePath))
                {
                    string templateContent = System.IO.File.ReadAllText(templatePath);
                    // A szűrőket a jövőben a dashboardról kapja majd, egyelőre null-t adunk át
                    string renderedHtml = _templateProcessor.ProcessTemplate(templateContent, _dataService, null);
                    renderedMetrics.Add(metric, new MvcHtmlString(renderedHtml));
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

            // Metaadatok átadása a nézetnek
            ViewBag.MetricName = ParseMetaTag(templateContent, "meta-name");
            ViewBag.MetricDescription = ParseMetaTag(templateContent, "meta-description");
            ViewBag.MetricOwner = ParseMetaTag(templateContent, "meta-owner");

            // Metrika renderelése
            // A jövőben a dashboardról érkező szűrőket itt lehet majd átadni
            string renderedHtml = _templateProcessor.ProcessTemplate(templateContent, _dataService, null);
            ViewBag.RenderedMetric = new MvcHtmlString(renderedHtml);

            return View();
        }

        // --- API for Toggling Favorites ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleFavorite(string metricName)
        {
            // This logic is very similar to the ReportController's version
            // It operates on the UserMetricFavorites table instead
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

        // --- Helper Methods ---
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
    }
}
