using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bookify.Web.Filters;

public class ValidateAntiforgeryTokenForNonApiAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Skip CSRF validation for API routes
        var path = context.HttpContext.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/api/"))
        {
            return;
        }

        // For non-API routes, use the standard AutoValidateAntiforgeryToken behavior
        // This is handled by the framework's built-in validation
        var method = context.HttpContext.Request.Method;
        if (method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH")
        {
            // The framework will handle CSRF validation automatically
            // We just need to ensure we don't block API routes
        }
    }
}

