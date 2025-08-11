using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace IML
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Route for UpdateChart action
            routes.MapRoute(
                name: "UpdateChart",
                url: "Report/UpdateChart",
                defaults: new { controller = "Report", action = "UpdateChart" }
            );

            // Route for RenderReport action with templateName parameter
            routes.MapRoute(
                name: "RenderReport",
                url: "Report/RenderReport/{templateName}",
                defaults: new { controller = "Report", action = "RenderReport", templateName = UrlParameter.Optional }
            );

            // Default route
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
