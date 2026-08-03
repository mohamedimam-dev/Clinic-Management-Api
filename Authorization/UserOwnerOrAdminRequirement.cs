using Microsoft.AspNetCore.Authorization;

namespace ClinicManagementApi.Authorization
{
    public class UserOwnerOrAdminRequirement : IAuthorizationRequirement
    {
    }
}
