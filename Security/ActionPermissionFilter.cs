using System.Security.Claims;
using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LMS.Security;

public sealed class ActionPermissionFilter(PermissionService permissions) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || user.IsInRole(Roles.Admin))
            return;

        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        var requiredCode = await permissions.GetCodeForActionAsync(controller, action);
        if (requiredCode is not null && !user.HasClaim(PermissionCodes.ClaimType, requiredCode))
            context.Result = new ForbidResult();
    }
}
