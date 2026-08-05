using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Numerics;
using System.Security.Claims;
using static ClinicManagementBusiness.clsAuditLog;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PatientsController : ControllerBase
    {
        [Authorize(Roles = "Admin, Receptionist, Assistant")]
        [HttpGet("AllActive", Name = "GetAllActivePatients")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllPatientsDTO>> GetAllActivePatients()
        {
            List<GetAllPatientsDTO> patientsList = clsPatient.GetAllActivePatients();
            return Ok(patientsList);
        }

        [Authorize(Roles ="Admin")]
        [HttpGet("AllDeleted", Name = "GetAllDeletedPatients")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllPatientsDTO>> GetAllDeletedPatients()
        {
            List<GetAllPatientsDTO> patientsList = clsPatient.GetAllDeletedPatients();
            return Ok(patientsList);
        }

        [Authorize(Roles = "Admin, Receptionist, Doctor")]
        [HttpGet("{patientID}", Name = "GetPatientInfoByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetPatientByIdDTO> GetPatientInfoByID(int patientID)
        {
            if (patientID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsPatient patient = clsPatient.FindByID(patientID);
            if (patient == null)
            {
                return NotFound($"Patient With ID {patientID} was Not Found.");
            }

            return Ok(patient.ToDto());
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPost("Add", Name = "AddPatient")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetPatientByIdDTO> AddPatient([FromBody] AddPatientDTO patientDto)
        {
            if (patientDto.PersonID <= 0 
                || patientDto == null
                )
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            patientDto.PatientCreatedByUserID = userId;

            clsPatient patient = new clsPatient(patientDto);

            try
            {
                if (!patient.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to add patient.");
                }

                patient = clsPatient.FindByID(patient.PatientID);

                if (patient == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Patient was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetPatientInfoByID",
                    new { patientID = patient.PatientID },
                    patient.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Person not found

                    50002 => NotFound(ex.Message),   // User not found

                    50003 => Conflict(ex.Message),   // Patient already exists

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

  
        [Authorize(Roles = "Admin, Receptionist")]
        [HttpDelete("{patientID}/Delete", Name = "DeletePatient")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Delete(int patientID)
        {
            if (patientID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsPatient.DeletePatient(patientID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete patient.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Delete.ToString(),
                    enAuditEntity.Patient.ToString(),
                    patientID,
                    ipAddress);

                return Ok("Patient deleted successfully.");

                return Ok("Patient deleted successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Patient not found

                    50002 => Conflict(ex.Message),   // Appointments exist

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPut("{patientID}/Restore", Name = "RestorePatient")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Restore(int patientID)
        {
            if (patientID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsPatient.RestorePatient(patientID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore patient.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Restore.ToString(),
                    enAuditEntity.Patient.ToString(),
                    patientID,
                    ipAddress);

                return Ok("Patient restored successfully.");

            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Patient not found

                    50002 => Conflict(ex.Message),   // Patient is not deleted

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
   
    }
}
