using System;
using System.Web;
using System.Web.Mvc;
using Core.Helpers;

namespace Core.Controllers
{
    [Authorize]
    public class ManagedSegmentController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetViewAs(string viewAs, string returnUrl, string clear)
        {
            if (!ManagedSegmentHelper.IsAdmin(User))
                return new HttpStatusCodeResult(403, "You are not authorised to use view-as.");

            var targetUrl = ResolveReturnUrl(returnUrl);

            if (!string.IsNullOrWhiteSpace(clear) || string.IsNullOrWhiteSpace(viewAs))
            {
                ManagedSegmentHelper.ClearViewAsOverride();
                TempData["ManagedSegmentViewAsMessage"] = "Impersonation cleared; using signed-in account.";
                return Redirect(targetUrl);
            }

            var trimmed = viewAs.Trim();
            var groups = ManagedSegmentHelper.GetUserGroupNames(trimmed, out var lookupError);
            if (!string.IsNullOrWhiteSpace(lookupError))
            {
                TempData["ManagedSegmentViewAsError"] = lookupError;
                return Redirect(targetUrl);
            }

            ManagedSegmentHelper.SetViewAsOverride(trimmed, groups);

            if (groups.Count == 0)
            {
                TempData["ManagedSegmentViewAsMessage"] = $"Viewing as {trimmed}, no matching managed segment groups found.";
            }
            else
            {
                TempData["ManagedSegmentViewAsMessage"] = $"Now viewing as {trimmed}.";
            }

            return Redirect(targetUrl);
        }

        private string ResolveReturnUrl(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return returnUrl;

            if (Request?.UrlReferrer != null)
            {
                var referrerPath = Request.UrlReferrer.PathAndQuery;
                if (Url.IsLocalUrl(referrerPath))
                    return referrerPath;
            }

            return Url.Action("Index", "Home");
        }
    }
}
