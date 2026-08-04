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
    public class OperationRoomsController : ControllerBase
    {
        [HttpGet("AllActive", Name = "GetAllActiveOperationRooms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllOperationRoomsDTO>> GetAllActiveOperationRooms()
        {
            List<GetAllOperationRoomsDTO> activeList = clsOperationRoom.GetAllActiveOperationRooms();
            return Ok(activeList);
        }

        [Authorize(Roles ="Admin")]
        [HttpGet("AllDeleted", Name = "GetAllDeletedOperationRooms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllOperationRoomsDTO>> GetAllDeletedOperationRooms()
        {
            List<GetAllOperationRoomsDTO> deletedList = clsOperationRoom.GetAllDeletedOperationRooms();
            return Ok(deletedList);
        }

        [HttpGet("{operationRoomID}", Name = "GetOperationRoomInfoByID")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<GetOperationRoomByIdDTO> GetOperationRoomInfoByID(int operationRoomID)
        {
            if (operationRoomID <= 0)
                return BadRequest("Invalid Data.");

            clsOperationRoom operationRoom = clsOperationRoom.FindByID(operationRoomID);

            if (operationRoom == null)
                return NotFound($"OperationRoom With ID {operationRoomID} was Not Found.");

            return Ok(operationRoom.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Add", Name = "AddOperationRoom")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetOperationRoomByIdDTO> AddOperationRoom(
           AddOperationRoomDTO operationRoomDto)
        {
            if (operationRoomDto == null ||
                string.IsNullOrWhiteSpace(operationRoomDto.RoomName) ||
                string.IsNullOrWhiteSpace(operationRoomDto.Location)
                )
            {
                return BadRequest("Invalid Data.");
            }

            // بيانات المستخدم الحالي من الـ JWT
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // لا تعتمد على القيمة القادمة من العميل
            operationRoomDto.CreatedByUserID = userId;

            operationRoomDto.RoomName = operationRoomDto.RoomName.Trim();
            operationRoomDto.Location = operationRoomDto.Location.Trim();

            clsOperationRoom operationRoom = new clsOperationRoom(operationRoomDto);

            try
            {
                if (!operationRoom.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create operation room.");
                }

                operationRoom = clsOperationRoom.FindByID(operationRoom.OperationRoomID);

                if (operationRoom == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Operation room was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetOperationRoomInfoByID",
                    new { operationRoomID = operationRoom.OperationRoomID },
                    operationRoom.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message), // Room name is required

                    50002 => BadRequest(ex.Message), // Location is required

                    50003 => NotFound(ex.Message),   // User not found

                    50004 => Conflict(ex.Message),   // Operation room already exists

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{operationRoomID}/Update", Name = "UpdateOperationRoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetOperationRoomByIdDTO> UpdateOperationRoom(
           int operationRoomID,
           UpdateOperationRoomDTO operationRoomDto)
        {
            if (operationRoomID <= 0 || operationRoomDto == null ||
                string.IsNullOrWhiteSpace(operationRoomDto.RoomName) ||
                string.IsNullOrWhiteSpace(operationRoomDto.Location))
            {
                return BadRequest("Invalid Data.");
            }

            operationRoomDto.RoomName = operationRoomDto.RoomName.Trim();
            operationRoomDto.Location = operationRoomDto.Location.Trim();

            clsOperationRoom operationRoom =
                clsOperationRoom.FindByID(operationRoomID);

            if (operationRoom == null)
            {
                return NotFound($"Operation room with ID {operationRoomID} was not found.");
            }

            bool noChanges =
                operationRoom.RoomName 
                == operationRoomDto.RoomName &&
                operationRoom.Location 
                == operationRoomDto.Location;

            if (noChanges)
            {
                return Conflict("No changes were detected.");
            }

            operationRoom.RoomName = operationRoomDto.RoomName;
            operationRoom.Location = operationRoomDto.Location;

            try
            {
                if (!operationRoom.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update operation room.");
                }

                operationRoom =
                    clsOperationRoom.FindByID(operationRoom.OperationRoomID);

                if (operationRoom == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Operation room was updated but could not be retrieved.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Update.ToString(),
                    enAuditEntity.OperationRoom.ToString(),
                    operationRoom.OperationRoomID,
                    ipAddress);

                return Ok(operationRoom.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message), // Room name is required

                    50002 => BadRequest(ex.Message), // Location is required

                    50003 => NotFound(ex.Message),   // Room not found

                    50004 => Conflict(ex.Message),   // Cannot change room name because bookings exist

                    50005 => Conflict(ex.Message),   // Duplicate room name

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{operationRoomID}/Delete", Name = "DeleteOperationRoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Delete(int operationRoomID)
        {
            if (operationRoomID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsOperationRoom.DeleteOperationRoom(operationRoomID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete operation room.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Delete.ToString(),
                    enAuditEntity.OperationRoom.ToString(),
                    operationRoomID,
                    ipAddress);

                return Ok("Operation room deleted successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Operation room not found

                    50002 => Conflict(ex.Message),   // Operation bookings exist

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{operationRoomID}/Restore", Name = "RestoreOperationRoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Restore(int operationRoomID)
        {
            if (operationRoomID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsOperationRoom.RestoreOperationRoom(operationRoomID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore operation room.");
                }

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Restore.ToString(),
                    enAuditEntity.OperationRoom.ToString(),
                    operationRoomID,
                    ipAddress);

                return Ok("Operation room restored successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),   // Operation room not found or already restored

                    50002 => Conflict(ex.Message),   // Duplicate room name

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
   
    }
}
