using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static ClinicManagementBusiness.clsAuditLog;
using System.Security.Claims;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ClinicsController : ControllerBase
    {
        [HttpGet("AllActive", Name = "GetAllActiveClinics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllClinicsDTO>> GetAllActiveClinics()
        {
            List<GetAllClinicsDTO> clinicList = clsClinic.GetActiveClinics();
            return Ok(clinicList);
        }

        [HttpGet("{clinicID}", Name = "GetClinicInfoByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetClinicByIdDTO> GetClinicInfoByID(int clinicID)
        {
            if (clinicID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsClinic clinic = clsClinic.FindClinicByID(clinicID);
            if (clinic == null)
            {
                return NotFound($"Clinic With ID {clinicID} was Not Found.");
            }

            return Ok(clinic.ToDto());
        }

        [Authorize(Roles ="Admin")]
        [HttpPost("Add", Name = "AddNewClinic")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetClinicByIdDTO> AddNewClinic(AddClinicDTO clinicDto)
        {
            if (clinicDto == null ||
                string.IsNullOrWhiteSpace(clinicDto.ClinicName) ||
                clinicDto.CreatedByUserID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsClinic clinic = new clsClinic(clinicDto);

            try
            {
                if (!clinic.Save())
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create clinic.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),   // Clinic name is required

                    50002 => NotFound(ex.Message),     // User not found

                    50003 => Conflict(ex.Message),     // Clinic already exists

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }

            clinic = clsClinic.FindClinicByID(clinic.ClinicID);

            if (clinic == null)
                return NotFound("Clinic was not found after add.");

            return CreatedAtRoute(
                "GetClinicInfoByID",
                new { clinicID = clinic.ClinicID },
                clinic.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{clinicID}/Update", Name = "UpdateClinic")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetClinicByIdDTO> UpdateClinic(
          int clinicID,
          UpdateClinicDTO clinicDto)
        {
            if (clinicID <= 0)
                return BadRequest("Invalid Clinic ID.");

            if (clinicDto == null ||
                string.IsNullOrWhiteSpace(clinicDto.ClinicName))
            {
                return BadRequest("Invalid Data.");
            }

            clinicDto.ClinicName = clinicDto.ClinicName.Trim();

            clsClinic clinic = clsClinic.FindClinicByID(clinicID);

            if (clinic == null)
                return NotFound($"Clinic with ID {clinicID} was not found.");

            if (clinic.ClinicName.Equals(
                    clinicDto.ClinicName,
                    StringComparison.Ordinal))
            {
                return Conflict("No changes were detected.");
            }

            clinic.ClinicName = clinicDto.ClinicName;

            try
            {
                if (!clinic.Save())
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update clinic.");

                clinic = clsClinic.FindClinicByID(clinic.ClinicID);

                if (clinic == null)
                    return NotFound("Clinic was not found after update.");

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Update.ToString(),
                    enAuditEntity.Clinic.ToString(),
                    clinic.ClinicID,
                    ipAddress);

                return Ok(clinic.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),   // Clinic name is required

                    50002 => NotFound(ex.Message),     // Clinic not found

                    50003 => Conflict(ex.Message),     // Clinic already exists

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }

           
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{clinicID}/Delete", Name = "DeleteClinic")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Delete(int clinicID)
        {
            if (clinicID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsClinic.DeleteClinic(clinicID))
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete clinic.");

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Delete.ToString(),
                    enAuditEntity.Clinic.ToString(),
                    clinicID,
                    ipAddress);

                return Ok("Clinic deleted successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),

                    50002 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{clinicID}/Restore", Name = "RestoreClinic")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Restore(int clinicID)
        {
            if (clinicID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsClinic.RestoreClinic(clinicID))
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore clinic.");

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Restore.ToString(),
                    enAuditEntity.Clinic.ToString(),
                    clinicID,
                    ipAddress);

                return Ok("Clinic restored successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),

                    50002 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
    }
}
