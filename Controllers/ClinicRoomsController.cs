using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Security.Claims;
using static ClinicManagementBusiness.clsAuditLog;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ClinicRoomsController : ControllerBase
    {
        [HttpGet("AllActive", Name = "GetAllActiveClinicRooms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllClinicRoomsDTO>> GetAllActiveClinicRooms()
        {
            List<GetAllClinicRoomsDTO> activeList = clsClinicRoom.GetAllActiveClinicRooms();
            return Ok(activeList);
        }

        [HttpGet("AllDeleted", Name = "GetAllDeletedClinicRooms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllClinicRoomsDTO>> GetAllDeletedClinicRooms()
        {
            List<GetAllClinicRoomsDTO> deletedList = clsClinicRoom.GetAllDeletedClinicRooms();
            return Ok(deletedList);
        }

        [HttpGet("{clinicRoomID}", Name = "GetClinicRoomInfoByID")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<GetClinicRoomByIdDTO> GetClinicRoomInfoByID(int clinicRoomID)
        {
            if (clinicRoomID <= 0)
                return BadRequest("Invalid Data.");

            clsClinicRoom clinicRoom = clsClinicRoom.FindByID(clinicRoomID);
            if (clinicRoom == null)
                return NotFound($"clinicRoom With ID {clinicRoomID} was Not Found.");

            return Ok(clinicRoom.ToDto());
        }

        [Authorize(Roles ="Admin")]
        [HttpPost("Add", Name = "AddClinicRoom")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetClinicRoomByIdDTO> AddClinicRoom(
          AddClinicRoomDTO newClinicRoomDto)
        {
            if (newClinicRoomDto == null ||
                string.IsNullOrWhiteSpace(newClinicRoomDto.RoomName) ||
                string.IsNullOrWhiteSpace(newClinicRoomDto.RoomLocation) ||
                newClinicRoomDto.CreatedByUserID <= 0)
            {
                return BadRequest("Invalid data.");
            }

            newClinicRoomDto.CreatedByUserID =
             int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            clsClinicRoom clinicRoom = new clsClinicRoom(newClinicRoomDto);

            try
            {
                if (!clinicRoom.Save())
                    return BadRequest("Failed to create clinic room.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message), // Room name is required

                    50002 => BadRequest(ex.Message), // Room location is required

                    50003 => NotFound(ex.Message),   // User not found

                    50004 => Conflict(ex.Message),   // Clinic room already exists

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }

            clinicRoom = clsClinicRoom.FindByID(clinicRoom.ClinicRoomID);

            if (clinicRoom == null)
                return NotFound("Clinic room was not found after add.");

            return CreatedAtRoute(
                "GetClinicRoomInfoByID",
                new { clinicRoomID = clinicRoom.ClinicRoomID },
                clinicRoom.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{clinicRoomID}", Name = "UpdateClinicRoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetClinicRoomByIdDTO> UpdateClinicRoom(
          int clinicRoomID,
          UpdateClinicRoomDTO dto)
        {
            if (clinicRoomID <= 0)
                return BadRequest("Invalid Clinic Room ID.");

            clsClinicRoom clinicRoom = clsClinicRoom.FindByID(clinicRoomID);

            if (clinicRoom == null)
                return NotFound($"Clinic room with ID {clinicRoomID} was not found.");

            clinicRoom.RoomName = dto.RoomName;
            clinicRoom.RoomLocation = dto.RoomLocation;
            clinicRoom.CreatedByUserID =
              int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            try
            {
                if (!clinicRoom.Save())
                    return BadRequest("Data was not saved.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 or 50002 => BadRequest(ex.Message),   // Required fields
                    50003 => NotFound(ex.Message),              // Room not found
                    50004 or 50005 => Conflict(ex.Message),     // Business conflict
                    _ => StatusCode(
                            StatusCodes.Status500InternalServerError,
                            ex.Message)
                };
            }

            clinicRoom = clsClinicRoom.FindByID(clinicRoom.ClinicRoomID);

            if (clinicRoom == null)
                return NotFound("Clinic room was not found after update.");

            return Ok(clinicRoom.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{clinicRoomID}/Delete", Name = "DeleteClinicRoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Delete(int clinicRoomID)
        {
            if (clinicRoomID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsClinicRoom.DeleteClinicRoom(clinicRoomID))
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete clinic room.");

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Delete.ToString(),
                    enAuditEntity.ClinicRoom.ToString(),
                    clinicRoomID,
                    ipAddress);


                return Ok("Clinic room deleted successfully.");
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
        [HttpPut("{clinicRoomID}/Restore", Name = "RestoreClinicRoom")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult Restore(int clinicRoomID)
        {
            if (clinicRoomID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsClinicRoom.RestoreClinicRoom(clinicRoomID))
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore clinic room.");

                int actorUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                string ipAddress =
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                clsAuditLog.LogAction(
                    actorUserId,
                    enAuditAction.Restore.ToString(),
                    enAuditEntity.ClinicRoom.ToString(),
                    clinicRoomID,
                    ipAddress);

                return Ok("Clinic room restored successfully.");
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
