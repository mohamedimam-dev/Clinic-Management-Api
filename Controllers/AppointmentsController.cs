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
    public class AppointmentsController : ControllerBase
    {
        [Authorize(Roles = "Admin")]
        [HttpGet("All", Name = "GetAllAppointments")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllAppointmentsDTO>> GetAllAppointments()
        {
            List<GetAllAppointmentsDTO> List = clsAppointment.GetAllAppointments();
            return Ok(List);
        }

        [Authorize(Roles = "Admin,Receptionist,Doctor,Assistant")]
        [HttpGet("{appointmentID}", Name = "GetAppointmentByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetAppointmentByIDDTO> GetAppointmentByID(int appointmentID)
        {
            if (appointmentID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsAppointment appointment = clsAppointment.FindByID(appointmentID);
            if (appointment == null)
            {
                return NotFound($"Appointment With ID {appointmentID} was Not Found.");
            }

            return Ok(appointment.ToDto());
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPost("Add", Name = "AddAppointment")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetAppointmentByIDDTO> AddAppointment(
         [FromBody] AddAppointmentDTO appointmentDto)
        {
            if (appointmentDto == null ||
                appointmentDto.PatientID <= 0 ||
                appointmentDto.DoctorRoomScheduleID <= 0 ||
                appointmentDto.AppointmentTypeID <= 0 ||
                appointmentDto.CreatedByUserID <= 0)
            {
                return BadRequest("Invalid data.");
            }

            var createdByUserId = int.Parse(
               User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            appointmentDto.CreatedByUserID = createdByUserId;

            if (appointmentDto.AppointmentDate.Date < DateTime.Today)
                return BadRequest("Appointment date cannot be in the past.");

            clsDoctorRoomSchedule schedule =
                clsDoctorRoomSchedule.FindByID(appointmentDto.DoctorRoomScheduleID);

            if (schedule != null &&
                clsAppointment.GetDayIDFromDate(appointmentDto.AppointmentDate) != schedule.DayID)
            {
                return BadRequest(
                    "The selected date does not match the doctor's working day.");
            }

            clsAppointment appointment = new clsAppointment(appointmentDto);

            try
            {
                if (!appointment.Save())
                    return BadRequest("Failed to create appointment.");

                appointment = clsAppointment.FindByID(appointment.AppointmentID);

                if (appointment == null)
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Appointment was created but could not be retrieved.");

                return CreatedAtRoute(
                    "GetAppointmentByID",
                    new { appointmentID = appointment.AppointmentID },
                    appointment.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),

                    50002 => Conflict(ex.Message),

                    50003 => NotFound(ex.Message),

                    50004 => NotFound(ex.Message),

                    50005 => NotFound(ex.Message),

                    50006 => NotFound(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin, Assistant, Doctor")]
        [HttpPut("{appointmentID}/MarkAsInProgress", Name = "MarkAsInProgress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult MarkAsInProgress(int appointmentID)
        {
            if (appointmentID <= 0)
                return BadRequest("Invalid Appointment ID.");

            clsAppointment? appointment = clsAppointment.FindByID(appointmentID);

            if (appointment == null)
                return NotFound($"Appointment with ID '{appointmentID}' was not found.");

            if (appointment.AppointmentStatus != clsAppointment.enAppointmentStatus.WaitingList)
                return BadRequest("Only waiting appointments can be started.");

            // بيانات المستخدم الحالي من الـ JWT
            //int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            //string role = User.FindFirst(ClaimTypes.Role)!.Value;

            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string role = User.FindFirstValue(ClaimTypes.Role)!;

            if (role == "Assistant")
            {
                int? clinicId = clsAppointment.GetClinicIDByAppointmentID(appointmentID);

                if (clinicId == null)
                    return NotFound("Clinic not found.");

                if (!clsUserClinic.IsExist(userId, clinicId.Value))
                    return Forbid();
            }
            else
            {
                if (!clsAppointment.HasAccessToAppointment(
                        appointmentID,
                        userId,
                        role))
                {
                    return Forbid();
                }
            }

            if (!clsAppointment.MarkAsInProgress(appointmentID))
                return BadRequest("Unable to start appointment.");

            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            clsAuditLog.LogAction(
                userId,
                enAuditAction.MarkAsInProgress.ToString(),
                enAuditEntity.Appointment.ToString(),
                appointmentID,
                ipAddress);

            return Ok("Appointment started successfully.");
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPut("{appointmentID}/Cancel", Name = "CancelAppointment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult CancelAppointment(int appointmentID)
        {
            if (appointmentID <= 0)
                return BadRequest("Invalid appointment ID.");

            try
            {
                if (!clsAppointment.CancelAppointment(appointmentID))
                    return BadRequest("Failed to cancel appointment.");

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Cancel.ToString(),
                    enAuditEntity.Appointment.ToString(),
                    appointmentID,
                    ipAddress);

                return Ok("Appointment cancelled successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Appointment not found

                    50002 => BadRequest(ex.Message), // Only scheduled appointments can be cancelled

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
    
    }
}
