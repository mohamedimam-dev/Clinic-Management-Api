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
    public class DoctorsController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;

        public DoctorsController(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("AllActive", Name = "GetAllActiveDoctors")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllDoctorsDTO>> GetAllActiveDoctors()
        {
            List<GetAllDoctorsDTO> doctorsList = clsDoctor.GetAllActiveDoctors();
            return Ok(doctorsList);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("AllDeleted", Name = "GetAllDeletedDoctors")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllDoctorsDTO>> GetAllDeletedDoctors()
        {
            List<GetAllDoctorsDTO> doctorsList = clsDoctor.GetAllDeletedDoctors();
            return Ok(doctorsList);
        }

        [HttpGet("{doctorID}", Name = "GetDoctorInfoByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task <ActionResult<GetDoctorByIdDTO>> GetDoctorInfoByID(int doctorID)
        {
            if (doctorID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsDoctor doctor = clsDoctor.FindByID(doctorID);
            if (doctor == null)
            {
                return NotFound($"Doctor With ID {doctorID} was Not Found.");
            }

            var ownerUser = clsUser.FindByPersonID(doctor.PersonID);

            if (ownerUser == null)
                return StatusCode(500);

            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                ownerUser.UserID,
                "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            return Ok(doctor.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Add", Name = "AddDoctor")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetDoctorByIdDTO> AddDoctor(AddDoctorDTO doctorDto)
        {
            if (doctorDto.PersonID <= 0 ||
                doctorDto.ClinicID <= 0 ||
                doctorDto.DoctorFees < 0)
            {
                return BadRequest("Invalid Data.");
            }

            doctorDto.DoctorCreatedByUserID = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            clsDoctor doctor = new clsDoctor(doctorDto);

            try
            {
                if (!doctor.Save())
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create doctor.");

                doctor = clsDoctor.FindByID(doctor.DoctorID);

                if (doctor == null)
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Doctor was created but could not be retrieved.");

                return CreatedAtRoute(
                    "GetDoctorInfoByID",
                    new { doctorID = doctor.DoctorID },
                    doctor.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Person not found

                    50002 => NotFound(ex.Message),   // Clinic not found

                    50003 => NotFound(ex.Message),   // User not found

                    50004 => Conflict(ex.Message),   // Doctor already exists

                    50005 => BadRequest(ex.Message), // Invalid doctor fees

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{doctorID}/Update", Name = "UpdateDoctor")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetDoctorByIdDTO> UpdateDoctor(
          int doctorID,
          [FromBody] UpdateDoctorDTO doctorDto)
        {
            if (doctorID <= 0 ||
                doctorDto.PersonID <= 0 ||
                doctorDto.ClinicID <= 0 ||
                doctorDto.DoctorFees < 0)
            {
                return BadRequest("Invalid data.");
            }

            clsDoctor doctor = clsDoctor.FindByID(doctorID);

            if (doctor == null)
            {
                return NotFound($"Doctor with ID {doctorID} was not found.");
            }

            bool noChanges =
               doctor.PersonID == doctorDto.PersonID &&
               doctor.ClinicID == doctorDto.ClinicID &&
               doctor.DoctorFees == doctorDto.DoctorFees;

            if (noChanges)
            {
                return Conflict("No changes were detected.");
            }

            doctor.PersonID = doctorDto.PersonID;
            doctor.ClinicID = doctorDto.ClinicID;
            doctor.DoctorFees = doctorDto.DoctorFees;

            try
            {
                if (!doctor.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update doctor.");
                }

                doctor = clsDoctor.FindByID(doctor.DoctorID);

                if (doctor == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Doctor was updated but could not be retrieved.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Update.ToString(),
                    enAuditEntity.Doctor.ToString(),
                    doctor.DoctorID,
                    ipAddress);

                return Ok(doctor.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message), // Doctor not found

                    50002 => NotFound(ex.Message), // Person not found

                    50003 => NotFound(ex.Message), // Clinic not found

                    50004 => BadRequest(ex.Message), // Invalid fees

                    50005 => Conflict(ex.Message), // Doctor has appointments

                    50006 => Conflict(ex.Message), // Person already assigned

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{doctorID}", Name = "DeleteDoctor")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult DeleteDoctor(int doctorID)
        {
            if (doctorID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsDoctor.DeleteDoctor(doctorID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete doctor.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Delete.ToString(),
                    enAuditEntity.Doctor.ToString(),
                    doctorID,
                    ipAddress);

                return Ok("Doctor deleted successfully.");
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
        [HttpPut("{doctorID}/Restore", Name = "RestoreDoctor")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult RestoreDoctor(int doctorID)
        {
            if (doctorID <= 0)
                return BadRequest("Invalid data.");

            try
            {
                if (!clsDoctor.RestoreDoctor(doctorID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore doctor.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Restore.ToString(),
                    enAuditEntity.Doctor.ToString(),
                    doctorID,
                    ipAddress);

                return Ok("Doctor restored successfully.");

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
