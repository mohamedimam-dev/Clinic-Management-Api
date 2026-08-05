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
    public class OperationsController : ControllerBase
    {
        [HttpGet("AllActive", Name = "GetAllActiveOperations")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllOperationsDTO>> GetAllActiveOperations()
        {
            List<GetAllOperationsDTO> activeList = clsOperation.GetAllActive();
            return Ok(activeList);
        }

        [Authorize(Roles ="Admin")]
        [HttpGet("AllDeleted", Name = "GetAllDeletedOperations")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllOperationsDTO>> GetAllDeletedOperations()
        {
            List<GetAllOperationsDTO> deactiveList = clsOperation.GetAllDeleted();
            return Ok(deactiveList);
        }

        [HttpGet("{operationID}", Name = "GetOperationInfoByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetOperationByIdDTO> GetOperationInfoByID(int operationID)
        {
            if (operationID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsOperation operation = clsOperation.FindByID(operationID);
            if (operation == null)
            {
                return NotFound($"Operation With ID {operationID} was Not Found.");
            }

            return Ok(operation.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Add", Name = "AddOperation")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetOperationByIdDTO> AddOperation(
            [FromBody] AddOperationDTO operationDto)
        {
            if (operationDto == null ||
                string.IsNullOrWhiteSpace(operationDto.OperationName) ||
                operationDto.OperationFees < 0)
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            operationDto.CreatedByUserID = userId;

            operationDto.OperationName = operationDto.OperationName.Trim();

            clsOperation operation = new clsOperation(operationDto);

            try
            {
                if (!operation.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create operation.");
                }

                operation = clsOperation.FindByID(operation.OperationID);

                if (operation == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Operation was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetOperationInfoByID",
                    new { operationID = operation.OperationID },
                    operation.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message),   // Operation name is required

                    50002 => BadRequest(ex.Message),   // Negative fees

                    50003 => NotFound(ex.Message),     // User not found

                    50004 => Conflict(ex.Message),     // Duplicate operation

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{operationID}/Update", Name = "UpdateOperation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetOperationByIdDTO> UpdateOperation(
            int operationID,
            [FromBody] UpdateOperationDTO operationDto)
        {
            if (operationID <= 0 || operationDto == null ||
                string.IsNullOrWhiteSpace(operationDto.OperationName) ||
                operationDto.OperationFees < 0)
            {
                return BadRequest("Invalid Data.");
            }

            operationDto.OperationName = operationDto.OperationName.Trim();

            clsOperation operation = clsOperation.FindByID(operationID);

            if (operation == null)
            {
                return NotFound($"Operation with ID {operationID} was not found.");
            }

            bool noChanges =
                operation.OperationName 
                == operationDto.OperationName &&
                operation.OperationFees 
                == operationDto.OperationFees;

            if (noChanges)
            {
                return Conflict("No changes were detected.");
            }

            operation.OperationName = operationDto.OperationName;
            operation.OperationFees = operationDto.OperationFees;

            try
            {
                if (!operation.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update operation.");
                }

                operation = clsOperation.FindByID(operation.OperationID);

                if (operation == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Operation was updated but could not be retrieved.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Update.ToString(),
                    enAuditEntity.Operation.ToString(),
                    operation.OperationID,
                    ipAddress);

                return Ok(operation.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),     // Operation not found

                    50002 => BadRequest(ex.Message),   // Operation name is required

                    50003 => BadRequest(ex.Message),   // Negative fees

                    50004 => Conflict(ex.Message),     // Cannot change name because bookings exist

                    50005 => Conflict(ex.Message),     // Duplicate operation name

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{operationID}/Delete", Name = "DeleteOperation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]      
        public ActionResult DeleteOperation(int operationID)
        {
            if (operationID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsOperation.Delete(operationID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete operation.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Delete.ToString(),
                    enAuditEntity.Operation.ToString(),
                    operationID,
                    ipAddress);

                return Ok("Operation deleted successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Operation not found or already deleted

                    50002 => Conflict(ex.Message),   // Operation has bookings

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{operationID}/Restore", Name = "RestoreOperation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult RestoreOperation(int operationID)
        {
            if (operationID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsOperation.Restore(operationID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore operation.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Restore.ToString(),
                    enAuditEntity.Operation.ToString(),
                    operationID,
                    ipAddress);

                return Ok("Operation restored successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Operation not found

                    50002 => Conflict(ex.Message),   // Already active

                    50003 => Conflict(ex.Message),   // Duplicate active operation name

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
   
    }
}
