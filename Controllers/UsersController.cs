using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using static ClinicManagementBusiness.clsAuditLog;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;

        public UsersController(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("All", Name = "GetAllUsers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllUsersDTO>> GetAllUsers()
        {
            List<GetAllUsersDTO> usersList = clsUser.GetAllUsers();
            return Ok(usersList);
        }

        [HttpGet("{id}", Name = "GetUserInfoByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task <ActionResult<GetUserByIdDTO>> GetUserInfoByID(int id)
        {
            if (id <= 0)
            {
                return BadRequest($"User id must be greater than zero.");
            }

            clsUser user = clsUser.FindByUserID(id);
            if (user == null)
            {
                return NotFound($"User with id {id} was not found.");
            }

            var authResult = await _authorizationService.AuthorizeAsync(
               User,
               user.UserID,
               "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            return Ok(user.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost(Name = "AddNewUser")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetUserByIdDTO> AddNewUser([FromBody] AddUserDTO newUserData)
        {
            if (newUserData == null ||
                newUserData.PersonID <= 0 ||
                newUserData.RoleID <= 0 ||
                string.IsNullOrWhiteSpace(newUserData.UserName) ||
                string.IsNullOrWhiteSpace(newUserData.Password))
            {
                return BadRequest("Invalid User Data.");
            }

            newUserData.UserName = newUserData.UserName.Trim();
            newUserData.Password = newUserData.Password.Trim();

            clsUser createdUser = new clsUser(newUserData);

            try
            {
                if (!createdUser.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create user.");
                }

                createdUser = clsUser.FindByUserID(createdUser.UserID);

                if (createdUser == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "User was created but could not be retrieved.");
                }

                var adminUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    adminUserId,
                    enAuditAction.Create.ToString(),
                    enAuditEntity.User.ToString(),
                    createdUser.UserID,
                    ipAddress);

                return CreatedAtRoute(
                    "GetUserInfoByID",
                    new { id = createdUser.UserID },
                    createdUser.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    // BadRequest (400)
                    50003 => BadRequest(ex.Message),
                    50004 => BadRequest(ex.Message),

                    // NotFound (404)
                    50001 => NotFound(ex.Message),
                    50002 => NotFound(ex.Message),

                    // Conflict (409)
                    50005 => Conflict(ex.Message),

                    // Unexpected Error (500)
                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{userId}/Role", Name = "UpdateUserRole")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetUserByIdDTO> UpdateUserRole(
           int userId,
           [FromBody] UpdateUserRoleDTO dto)
        {
            if (userId <= 0 ||
                dto == null ||
                dto.RoleID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsUser user = clsUser.FindByUserID(userId);

            if (user == null)
                return NotFound($"User with ID {userId} was not found.");

            if (user.RoleID == dto.RoleID)
            {
                return Conflict("User already has this role.");
            }

            user.RoleID = dto.RoleID;

            try
            {
                if (!user.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update user role.");
                }

                user = clsUser.FindByUserID(userId);

                if (user == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "User was not found after update.");
                }

                var adminUserId = int.Parse(
                   User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    adminUserId,
                    enAuditAction.AssignRole.ToString(),
                    enAuditEntity.User.ToString(),
                    user.UserID,
                    ipAddress);

                return Ok(user.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),
                    50002 => NotFound(ex.Message),
                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [HttpPut("ChangeUsernameAndPassword", Name = "ChangeUsernameAndPassword")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task <ActionResult> ChangeUsernameAndPassword(
           UpdateUsernameAndPasswordDTO updateDTO)
        {
            if (updateDTO == null ||
                updateDTO.UserID <= 0 ||
                string.IsNullOrWhiteSpace(updateDTO.UserName) ||
                string.IsNullOrWhiteSpace(updateDTO.Password))
            {
                return BadRequest("Invalid user data.");
            }

            updateDTO.UserName = updateDTO.UserName.Trim();
            updateDTO.Password = updateDTO.Password.Trim();

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              updateDTO.UserID,
              "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            clsUser? user = clsUser.FindByUserID(updateDTO.UserID);

            if (user == null)
                return NotFound($"User with ID {updateDTO.UserID} was not found.");

            bool isSameUsername =
                user.UserName.Equals(
                updateDTO.UserName,
                StringComparison.OrdinalIgnoreCase);

            bool isSamePassword =
                BCrypt.Net.BCrypt.Verify(
                    updateDTO.Password,
                    user.Password);

            if (isSameUsername && isSamePassword)
            {
                return Conflict("No changes were detected.");
            }

            try
            {
                if (!clsUser.ChangeUserNameAndPassword(updateDTO))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Username and password were not updated.");
                }

                var actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.ChangeCredentials.ToString(),
                    enAuditEntity.User.ToString(),
                    updateDTO.UserID,
                    ipAddress);

                return Ok("Username and password updated successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),
                    50002 => BadRequest(ex.Message),
                    50003 => BadRequest(ex.Message),
                    50004 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{userId}/Activate", Name = "ActivateUser")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult ActivateUser(int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsUser.ActivateUser(userId))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to activate user.");
                }

                var actorUserId = int.Parse(
                  User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Activate.ToString(),
                    enAuditEntity.User.ToString(),
                    userId,
                    ipAddress);

                return Ok("User activated successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),
                    50002 => BadRequest(ex.Message),
                    _ => StatusCode(
                            StatusCodes.Status500InternalServerError,
                            ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{userId}/Deactivate", Name = "DeactivateUser")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult DeactivateUser(int userId)
        {
            if (userId <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsUser.DeactivateUser(userId))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to deactivate user.");
                }

                var actorUserId = int.Parse(
                 User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Deactivate.ToString(),
                    enAuditEntity.User.ToString(),
                    userId,
                    ipAddress);

                return Ok("User deactivated successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),
                    50002 => BadRequest(ex.Message),
                    _ => StatusCode(
                            StatusCodes.Status500InternalServerError,
                            ex.Message)
                };
            }
        }
    }
}
