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
    public class UserClinicsController : ControllerBase
    {
        [HttpGet("AllActive", Name = "GetAllActivatedUserClinics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllUserClinicsDTO>> GetAllActivatedUserClinics()
        {
            List<GetAllUserClinicsDTO> activeList = clsUserClinic.GetAllActivatedUserClinics();
            return Ok(activeList);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("AllDeActivate", Name = "GetAllDeActivatedUserClinics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllUserClinicsDTO>> GetAllDeActivatedUserClinics()
        {
            List<GetAllUserClinicsDTO> deactiveList = clsUserClinic.GetAllDeActivatedUserClinics();
            return Ok(deactiveList);
        }

        [HttpGet("{userClinicID}", Name = "GetUserClinicInfoByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetUserClinicByIdDTO> GetUserClinicInfoByID(int userClinicID)
        {
            if (userClinicID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsUserClinic userClinic = clsUserClinic.FindByID(userClinicID);
            if (userClinic == null)
            {
                return NotFound($"UserClinic With ID {userClinicID} was Not Found.");
            }

            return Ok(userClinic.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Add", Name = "AddUserClinic")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetUserClinicByIdDTO> AddUserClinic(
            [FromBody] AddUserClinicDTO userClinicDto)
        {
            if (userClinicDto == null ||
                userClinicDto.UserID <= 0 ||
                userClinicDto.ClinicID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            userClinicDto.CreatedByUserID = userId;

            clsUserClinic userClinic = new clsUserClinic(userClinicDto);

            try
            {
                
                if (!userClinic.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to add user clinic.");
                }

                userClinic = clsUserClinic.FindByID(userClinic.UserClinicID);

                if (userClinic == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "User clinic was not found after add.");
                }

                return CreatedAtRoute(
                    "GetUserClinicInfoByID",
                    new { userClinicID = userClinic.UserClinicID },
                    userClinic.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),
                    50002 => NotFound(ex.Message),
                    50003 => NotFound(ex.Message),
                    50004 => Conflict(ex.Message),

                    _ => StatusCode(
                            StatusCodes.Status500InternalServerError,
                            ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{userClinicID}/Update", Name = "UpdateUserClinic")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetUserClinicByIdDTO> UpdateUserClinic(
            int userClinicID,
            [FromBody] UpdateUserClinicDTO userClinicDto)
        {
            if (userClinicID <= 0 || userClinicDto == null ||
                userClinicDto.UserID <= 0 ||
                userClinicDto.ClinicID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsUserClinic userClinic = clsUserClinic.FindByID(userClinicID);

            if (userClinic == null)
                return NotFound($"User clinic with ID {userClinicID} was not found.");

            bool noChanges =
                userClinic.UserID 
                == userClinicDto.UserID &&
                userClinic.ClinicID 
                == userClinicDto.ClinicID;

            if (noChanges)
            {
                return Conflict("No changes were detected.");
            }

            userClinic.UserID = userClinicDto.UserID;
            userClinic.ClinicID = userClinicDto.ClinicID;

            try
            {
                if (!userClinic.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update user clinic.");
                }

                userClinic = clsUserClinic.FindByID(userClinic.UserClinicID);

                if (userClinic == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "User clinic was not found after update.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Update.ToString(),
                    enAuditEntity.UserClinic.ToString(),
                    userClinic.UserClinicID,
                    ipAddress);

                return Ok(userClinic.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 or 50002 or 50003 => NotFound(ex.Message),
                    50004 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

    }
        
}
