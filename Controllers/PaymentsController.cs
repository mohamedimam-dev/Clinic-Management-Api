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
    public class PaymentsController : ControllerBase
    {
        [Authorize(Roles = "Admin, Receptionist")]
        [HttpGet("All", Name = "GetAllPayments")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllPaymentsDTO>> GetAllPayments()
        {
            List<GetAllPaymentsDTO> List = clsPayment.GetAllPayments();
            return Ok(List);
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpGet("{paymentID}", Name = "GetPaymentByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetPaymentByIDDTO> GetPaymentByID(int paymentID)
        {
            if (paymentID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsPayment payment = clsPayment.FindByID(paymentID);
            if (payment == null)
            {
                return NotFound($"Payment With ID {paymentID} was Not Found.");
            }

            return Ok(payment.ToDto());
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPost("Add", Name = "AddPayment")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetPaymentByIDDTO> AddPayment([FromBody] AddPaymentDTO paymentDto)
        {
            if (paymentDto.AppointmentID <= 0 ||
                (paymentDto.OperationBookingID.HasValue &&
                 paymentDto.OperationBookingID.Value <= 0) ||
                (paymentDto.Notes != null &&
                 string.IsNullOrWhiteSpace(paymentDto.Notes)))
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            paymentDto.CreatedByUserID = userId;

            clsPayment payment = new clsPayment(paymentDto);

            try
            {
                if (!payment.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to add payment.");
                }

                payment = clsPayment.FindByID(payment.PaymentID);

                if (payment == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Payment was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetPaymentByID",
                    new { paymentID = payment.PaymentID },
                    payment.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),    // User not found

                    50002 => Conflict(ex.Message),    // Appointment already paid

                    50003 => Conflict(ex.Message),    // Appointment not found or cannot be paid

                    50004 => Conflict(ex.Message),    // Operation booking already paid

                    50005 => Conflict(ex.Message),    // Operation booking not found or cannot be paid

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpDelete("{paymentID}/Cancel", Name = "CancelPayment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Cancel(int paymentID)
        {
            if (paymentID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsPayment.Cancel(paymentID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to cancel payment.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Cancel.ToString(),
                    enAuditEntity.Payment.ToString(),
                    paymentID,
                    ipAddress);

                return Ok("Payment cancelled successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
    }
}
