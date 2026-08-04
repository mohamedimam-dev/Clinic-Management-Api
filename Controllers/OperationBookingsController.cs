using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Logging;
using System.Security.Claims;
using static ClinicManagementBusiness.clsAuditLog;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class OperationBookingsController : ControllerBase
    {
        [Authorize(Roles = "Admin, Receptionist")]
        [HttpGet("All", Name = "GetAllOperationBookings")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllOperationBookingsDTO>> GetAllOperationBookings()
        {
            List<GetAllOperationBookingsDTO> OperationBookingsList = clsOperationBooking.GetAllOperationBookings();
            return Ok(OperationBookingsList);
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpGet("{operationBookingID}", Name = "GetOperationBookingByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]       
        public ActionResult<GetOperationBookingByIdDTO> GetOperationBookingByID(int operationBookingID)
        {
            if (operationBookingID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsOperationBooking operationBooking = clsOperationBooking.FindByID(operationBookingID);

            if (operationBooking == null)
            {
                return NotFound($"Operation Booking With ID {operationBookingID} was Not Found.");
            }

            return Ok(operationBooking.ToDto());
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPost("Add", Name = "AddOperationBooking")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetOperationBookingByIdDTO> AddOperationBooking(
          AddOperationBookingDTO dto)
        {
            if (dto.MedicalID <= 0 ||
                dto.OperationRoomID <= 0 ||
                dto.BookingTimeID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            dto.CreatedByUserID = userId;

            if (dto.OperationDate.Date < DateTime.Today)
                return BadRequest("Operation date cannot be in the past.");

            clsOperationBooking booking = new clsOperationBooking(dto);

            try
            {
                if (!booking.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create operation booking.");
                }

                booking = clsOperationBooking.FindByID(booking.OperationBookingID);

                if (booking == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Operation booking was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetOperationBookingByID",
                    new { operationBookingID = booking.OperationBookingID },
                    booking.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),

                    50002 => NotFound(ex.Message),

                    50003 => NotFound(ex.Message),

                    50004 => NotFound(ex.Message),

                    50005 => NotFound(ex.Message),

                    50006 => Conflict(ex.Message),

                    50007 => Conflict(ex.Message),

                    50008 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin, Assistant")]
        [HttpPut("{operationBookingID}/MarkAsInProgress", Name = "MarkOperationBookingAsInProgress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult MarkOperationBookingAsInProgress(int operationBookingID)
        {
            if (operationBookingID <= 0)
                return BadRequest("Invalid Operation Booking ID.");

            clsOperationBooking? operationBooking =
                clsOperationBooking.FindByID(operationBookingID);

            if (operationBooking == null)
            {
                return NotFound(
                    $"Operation Booking with ID '{operationBookingID}' was not found.");
            }

            if (operationBooking.OperationBookingStatus !=
                clsOperationBooking.enOperationBookingStatus.WaitingList)
            {
                return BadRequest(
                    "Only waiting operation bookings can be marked as In Progress.");
            }

            int userId =
                int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            string role =
                User.FindFirstValue(ClaimTypes.Role)!;

            if (role == "Assistant")
            {
                int? clinicID =
                    clsOperationBooking.GetClinicIDByOperationBookingID(
                        operationBookingID);

                if (clinicID == null)
                    return NotFound("Clinic not found.");

                if (!clsUserClinic.IsExist(userId, clinicID.Value))
                    return Forbid();
            }

            if (!clsOperationBooking.MarkOperationBookingAsInProgress(operationBookingID))
            {
                return BadRequest("Unable to start operation booking.");
            }

            string ipAddress =
                HttpContext.Connection
                .RemoteIpAddress?
                .ToString() ?? "Unknown";

            clsAuditLog.LogAction(
                userId,
                enAuditAction.MarkAsInProgress.ToString(),
                enAuditEntity.OperationBooking.ToString(),
                operationBookingID,
                ipAddress);

            return Ok("Operation booking has been marked as In Progress.");
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpDelete("{operationBookingID}/Cancel", Name = "CancelOperationBooking")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult CancelOperationBooking(int operationBookingID)
        {
            if (operationBookingID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsOperationBooking.Cancel(operationBookingID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to cancel operation booking.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Cancel.ToString(),
                    enAuditEntity.OperationBooking.ToString(),
                    operationBookingID,
                    ipAddress);

                return Ok("Operation booking cancelled successfully.");
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
