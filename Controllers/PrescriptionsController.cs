using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PrescriptionsController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;

        public PrescriptionsController(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        [Authorize(Roles = "Admin, Doctor")]
        [HttpGet("All", Name = "GetAllPrescriptions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllPrescriptionsDTO>> GetAllPrescriptions()
        {
            List<GetAllPrescriptionsDTO> PrescriptionsList = clsPrescription.GetAllPrescriptions();
            return Ok(PrescriptionsList);
        }

        [Authorize(Roles = "Admin, Doctor")]
        [HttpGet("{prescriptionID}", Name = "GetPrescriptionByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task <ActionResult<GetPrescriptionByIDDTO>> GetPrescriptionByID(int prescriptionID)
        {
            if (prescriptionID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsPrescription prescription = clsPrescription.FindByID(prescriptionID);
            if (prescription == null)
            {
                return NotFound($"Prescription With ID {prescriptionID} was Not Found.");
            }

            var prescriptionOwner = 
                clsUser.FindByPersonID
                (prescription.MedicalRecord
                .Appointment.DoctorRoomSchedul.
                Doctor.PersonID);

            if (prescriptionOwner == null)
                return StatusCode(500);

            int authId = prescriptionOwner.UserID;

            var authResult = await _authorizationService.AuthorizeAsync(
             User,
             authId,
             "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            return Ok(prescription.ToDto());
        }

        [Authorize(Roles = "Admin, Doctor")]
        [HttpPost("Add", Name = "AddPrescription")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task <ActionResult<GetPrescriptionByIDDTO>> AddPrescription(
           [FromBody] AddPrescriptionDTO prescriptionDto)
        {
            if (prescriptionDto == null || prescriptionDto.MedicalID <= 0 ||
                string.IsNullOrWhiteSpace(prescriptionDto.MedicineName) ||
                string.IsNullOrWhiteSpace(prescriptionDto.Dosage) ||
                string.IsNullOrWhiteSpace(prescriptionDto.Frequency))
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId =
                int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var medicalRecord = clsMedicalRecord.FindByID(prescriptionDto.MedicalID);
            if (medicalRecord == null)
                return StatusCode(500);
            
            var userOwner =
                           clsUser.FindByPersonID
                           (medicalRecord
                           .Appointment.DoctorRoomSchedul.
                           Doctor.PersonID);

            if (userOwner == null)
                return StatusCode(500);

            int authId = userOwner.UserID;

            var authResult = await _authorizationService.AuthorizeAsync(
             User,
             authId,
             "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403

            // لا تعتمد على القيمة القادمة من العميل
            prescriptionDto.CreatedByUserID = userId;

            prescriptionDto.MedicineName =
                prescriptionDto.MedicineName.Trim();

            prescriptionDto.Dosage =
                prescriptionDto.Dosage.Trim();

            prescriptionDto.Frequency =
                prescriptionDto.Frequency.Trim();

            clsPrescription prescription =
                new clsPrescription(prescriptionDto);

            try
            {
                if (!prescription.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to add prescription.");
                }

                prescription =
                    clsPrescription.FindByID(
                        prescription.PrescriptionID);

                if (prescription == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Prescription was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetPrescriptionByID",
                    new { prescriptionID = prescription.PrescriptionID },
                    prescription.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50006 => NotFound(ex.Message),
                    50007 => NotFound(ex.Message),
                    50008 => Conflict(ex.Message),
                    50001 => BadRequest(ex.Message),
                    50002 => BadRequest(ex.Message),
                    50003 => BadRequest(ex.Message),
                    50004 => BadRequest(ex.Message),
                    50005 => BadRequest(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin, Doctor")]
        [HttpPut("{prescriptionID}/Update", Name = "UpdatePrescription")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task <ActionResult<GetPrescriptionByIDDTO>> UpdatePrescription(
           int prescriptionID,
           [FromBody] UpdatePrescriptionDTO prescriptionDto)
        {
            if (prescriptionID <= 0 || prescriptionDto == null ||
                string.IsNullOrWhiteSpace(prescriptionDto.MedicineName) ||
                string.IsNullOrWhiteSpace(prescriptionDto.Dosage) ||
                string.IsNullOrWhiteSpace(prescriptionDto.Frequency))
            {
                return BadRequest("Invalid Data.");
            }

            //Rename CreatedByUserID
            //→ UpdatedByUserID
            //أو فصلهما إلى
            //CreatedByUserID وUpdatedByUserID.
            prescriptionDto.CreatedByUserID = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            prescriptionDto.MedicineName = prescriptionDto.MedicineName.Trim();
            prescriptionDto.Dosage = prescriptionDto.Dosage.Trim();
            prescriptionDto.Frequency = prescriptionDto.Frequency.Trim();

            if (!string.IsNullOrWhiteSpace(prescriptionDto.SpecialInstructions))
            {
                prescriptionDto.SpecialInstructions =
                    prescriptionDto.SpecialInstructions.Trim();
            }

            clsPrescription? prescription =
                clsPrescription.FindByID(prescriptionID);

            if (prescription == null)
                return NotFound($"Prescription with ID {prescriptionID} was not found.");

            var prescriptionOwner =
                clsUser.FindByPersonID
                (prescription.MedicalRecord
                .Appointment.DoctorRoomSchedul.
                Doctor.PersonID);

            if (prescriptionOwner == null)
                return StatusCode(500);

            int authId = prescriptionOwner.UserID;

            var authResult = await _authorizationService.AuthorizeAsync(
             User,
             authId,
             "UserOwnerOrAdmin");

            if (!authResult.Succeeded)
                return Forbid(); // 403


            prescription.MedicineName = prescriptionDto.MedicineName;
            prescription.Dosage = prescriptionDto.Dosage;
            prescription.Frequency = prescriptionDto.Frequency;
            prescription.StartDate = prescriptionDto.StartDate;
            prescription.EndDate = prescriptionDto.EndDate;
            prescription.SpecialInstructions = prescriptionDto.SpecialInstructions;
            prescription.CreatedByUserID = prescriptionDto.CreatedByUserID;
           
            try
            {
                if (!prescription.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update prescription.");
                }

                prescription =
                    clsPrescription.FindByID(prescription.PrescriptionID);

                if (prescription == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Prescription was updated but could not be retrieved.");
                }

                return Ok(prescription.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    // BadRequest
                    50002 => BadRequest(ex.Message),
                    50003 => BadRequest(ex.Message),
                    50004 => BadRequest(ex.Message),

                    // NotFound
                    50001 => NotFound(ex.Message),
                    50005 => NotFound(ex.Message),

                    // Conflict
                    50006 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
   
    }
}
