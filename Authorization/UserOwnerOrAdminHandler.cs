using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ClinicManagementApi.Authorization
{
    public class UserOwnerOrAdminHandler
        : AuthorizationHandler<UserOwnerOrAdminRequirement, int>
    {
        protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       UserOwnerOrAdminRequirement requirement,
       int ownerUserId)
        {
            // Admin override
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Ownership check
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(userId, out int authenticatedUserId) &&
                authenticatedUserId == ownerUserId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
