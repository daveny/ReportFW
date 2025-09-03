using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Core.Helpers;

namespace Core.Controllers
{
    [Authorize]
    public class EditorController : Controller
    {
        private readonly ITemplateProcessor _templateProcessor;
        private readonly IDataService _dataService;

        // Paraméter nélküli ctor (MVC tudja példányosítani DI nélkül is)
        public EditorController()
        {
            var cs = ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString ?? "";
            _dataService = new SqlDataService(cs);
            _templateProcessor = new TemplateProcessor();
        }

        // Opcionális DI-ctor
        public EditorController(ITemplateProcessor templateProcessor, IDataService dataService)
        {
            _templateProcessor = templateProcessor ?? new TemplateProcessor();
            _dataService = dataService ?? new SqlDataService(ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString ?? "");
        }

        [HttpGet]
        public ActionResult Index() => View();

        private string MetricsRoot
        {
            get
            {
                var root = Server.MapPath("~/Views/Metrics/Templates");
                Directory.CreateDirectory(root);
                return root;
            }
        }

        [HttpGet]
        public ActionResult LoadMetric(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return new HttpStatusCodeResult(400, "Missing fileName");

                var safe = Path.GetFileName(fileName);
                var path = Path.Combine(MetricsRoot, safe);

                if (!System.IO.File.Exists(path))
                    return HttpNotFound("File not found");

                var txt = System.IO.File.ReadAllText(path);
                return Content(txt, "text/plain");
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"LoadMetric ERROR: {ex}");
                return new HttpStatusCodeResult(500, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] // a .thtml tartalomban lehet HTML-szerű szöveg
        public JsonResult SaveMetric(string fileName, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return Json(new { success = false, message = "fileName is required" });

                if (!fileName.EndsWith(".thtml", StringComparison.OrdinalIgnoreCase))
                    fileName += ".thtml";

                var safe = Path.GetFileName(fileName);
                var path = Path.Combine(MetricsRoot, safe);

                System.IO.File.WriteAllText(path, content ?? string.Empty);
                DebugHelper.Log($"SaveMetric OK: {path} (len={content?.Length ?? 0})");

                return Json(new { success = true, message = $"Metric saved: {safe}" });
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"SaveMetric ERROR: {ex}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ContentResult RenderPreview(string thtmlCode)
        {
            try
            {
                DebugHelper.Log("--- RenderPreview START ---");

                // Paraméterek összegyűjtése string->string dictionary-be
                var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "__RequestVerificationToken", "thtmlCode" };

                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var k in Request.QueryString.AllKeys.Where(k => k != null))
                    if (!exclude.Contains(k)) parameters[k] = Request.QueryString[k];

                foreach (var k in Request.Form.AllKeys.Where(k => k != null))
                    if (!exclude.Contains(k)) parameters[k] = Request.Form[k];

                var html = _templateProcessor.ProcessTemplate(thtmlCode ?? string.Empty, _dataService, parameters);

                DebugHelper.Log("--- RenderPreview END (Success) ---");
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                DebugHelper.Log($"RenderPreview ERROR: {ex.Message}\n{ex.StackTrace}");
                DebugHelper.Log("--- RenderPreview END (Error) ---");
                var safe = System.Web.HttpUtility.HtmlEncode(ex.Message);
                return Content($"<div class='alert alert-danger'>Error rendering preview: {safe}</div>", "text/html");
            }
        }
    }
}