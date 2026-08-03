using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Numerics;
using System.Security.Claims;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MedicalRecordsController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;

        public MedicalRecordsController(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        [Authorize(Roles ="Admin")]
        [HttpGet("All", Name = "GetAllMedicalRecords")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllMedicalRecordsDTO>> GetAllMedicalRecords()
        {
            List<GetAllMedicalRecordsDTO> medicalList = clsMedicalRecord.GetAllMedicalRecords();
            return Ok(medicalList);
        }

        [HttpGet("{medicalID}", Name = "GetMedicalRecordByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task <ActionResult<GetMedicalRecordByIDDTO>> GetMedicalRecordByID(int medicalID)
        {
            if (medicalID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsMedicalRecord medicalRecord = clsMedicalRecord.FindByID(medicalID);

            if (medicalRecord == null)
            {
                return NotFound($"MedicalRecord With ID {medicalID} was Not Found.");
            }
           
            var medicalRecordOwnerUser = 
                clsUser.FindByPersonID
                (medicalRecord.Appointment.DoctorRoomSchedul.
                Doctor.PersonID);

            if (medicalRecordOwnerUser == null)
                return StatusCode(500);

            int ownerUserId = 
                medicalRecordOwnerUser.UserID;

            var authResult = await _authorizationService.AuthorizeAsync(
               User,
               ownerUserId,
               "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            return Ok(medicalRecord.ToDto());
        }

        [Authorize(Roles = "Admin, Doctor")]
        [HttpPost("Add", Name = "AddMedicalRecord")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task <ActionResult<GetMedicalRecordByIDDTO>> AddMedicalRecord(
           [FromBody] AddMedicalRecordDTO medicalRecordDto)
        {
            if (medicalRecordDto == null ||
                medicalRecordDto.AppointmentID <= 0 ||
                (medicalRecordDto.OperationID != null &&
                 medicalRecordDto.OperationID <= 0))
            {
                return BadRequest("Invalid data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            medicalRecordDto.CreatedByUserID = userId;

            var appointment = clsAppointment.FindByID(medicalRecordDto.AppointmentID);
            if (appointment == null)
                return StatusCode(500);

            var userOwner = clsUser.FindByPersonID(appointment.DoctorRoomSchedul.Doctor.PersonID);
            if (userOwner == null)
                return StatusCode(500);

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              userOwner.UserID,
              "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            clsMedicalRecord medicalRecord =
                new clsMedicalRecord(medicalRecordDto);

            try
            {
                if (!medicalRecord.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create medical record.");
                }

                medicalRecord =
                    clsMedicalRecord.FindByID(medicalRecord.MedicalID);

                if (medicalRecord == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Medical record was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetMedicalRecordByID",
                    new { medicalID = medicalRecord.MedicalID },
                    medicalRecord.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),
                    50002 => NotFound(ex.Message),
                    50003 => NotFound(ex.Message),
                    50004 => BadRequest(ex.Message),
                    50005 => BadRequest(ex.Message),
                    50006 => BadRequest(ex.Message),
                    50007 => Conflict(ex.Message),
                    50008 => BadRequest(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin, Doctor")]
        [HttpPut("{medicalID}/Update", Name = "UpdateMedicalRecord")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task <ActionResult<GetMedicalRecordByIDDTO>> UpdateMedicalRecord(
           int medicalID,
           [FromBody] UpdateMedicalRecordDTO medicalRecordDto)
        {
            if (medicalID <= 0 ||
                medicalRecordDto == null)
            {
                return BadRequest("Invalid Data.");
            }

            clsMedicalRecord? medicalRecord =
                clsMedicalRecord.FindByID(medicalID);

            if (medicalRecord == null)
            {
                return NotFound(
                    $"Medical Record With ID '{medicalID}' Was Not Found.");
            }          

            var userOwener = clsUser.FindByPersonID(medicalRecord.Appointment.DoctorRoomSchedul.Doctor.PersonID);

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              userOwener.UserID,
              "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403 

            medicalRecord.Diagnosis = medicalRecordDto.Diagnosis;
            medicalRecord.FollowUpDate = medicalRecordDto.FollowUpDate;
            medicalRecord.FollowUpType =
                medicalRecordDto.FollowUpDate.HasValue
                    ? (clsMedicalRecord.enFollowUpType?)medicalRecordDto.FollowUpType
                    : null;

            medicalRecord.AdditionalNotes = medicalRecordDto.AdditionalNotes;
            medicalRecord.OperationID = medicalRecordDto.OperationID;

            try
            {
                if (!medicalRecord.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update medical record.");
                }

                medicalRecord =
                    clsMedicalRecord.FindByID(medicalRecord.MedicalID);

                if (medicalRecord == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Medical record was updated but could not be retrieved.");
                }

                return Ok(medicalRecord.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),
                    50002 => BadRequest(ex.Message),
                    50003 => BadRequest(ex.Message),
                    50004 => BadRequest(ex.Message),
                    50005 => NotFound(ex.Message),
                    50006 => NotFound(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

    }
}
