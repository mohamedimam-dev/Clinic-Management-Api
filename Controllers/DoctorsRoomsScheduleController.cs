using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Numerics;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorsRoomsScheduleController : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("AllActive", Name = "GetAllActive")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllDoctorRoomSchedulesDTO>> GetAllActive()
        {
            List<GetAllDoctorRoomSchedulesDTO> activeList = clsDoctorRoomSchedule.GetAllActive();
            return Ok(activeList);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("AllDeactive", Name = "GetAllDeActive")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllDoctorRoomSchedulesDTO>> GetAllDeActive()
        {
            List<GetAllDoctorRoomSchedulesDTO> deactiveList = clsDoctorRoomSchedule.GetAllDeActive();
            return Ok(deactiveList);
        }

        [HttpGet("{scheduleID}", Name = "GetDoctorRoomScheduleByID")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<GetDoctorRoomScheduleByIdDTO> GetDoctorRoomScheduleByID(int scheduleID)
        {
            if (scheduleID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            clsDoctorRoomSchedule schedule = clsDoctorRoomSchedule.FindByID(scheduleID);
            if (schedule == null)
            {
                return NotFound($"Schedule With ID {scheduleID} was Not Found.");
            }

            return Ok(schedule.ToDto());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Add", Name = "AddDoctorRoomSchedule")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetDoctorRoomScheduleByIdDTO> AddDoctorRoomSchedule(
          [FromBody] AddDoctorRoomScheduleDTO scheduleDto)
        {
            if (scheduleDto == null ||
                scheduleDto.DayID <= 0 ||
                scheduleDto.DoctorID <= 0 ||
                scheduleDto.ClinicRoomID <= 0 ||
                scheduleDto.CreatedByUserID <= 0)
            {
                return BadRequest("Invalid data.");
            }


            clsDoctorRoomSchedule schedule =
                new clsDoctorRoomSchedule(scheduleDto);


            try
            {
                if (!schedule.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to create doctor room schedule.");
                }


                schedule = clsDoctorRoomSchedule.FindByID(
                    schedule.DoctorRoomScheduleID);


                if (schedule == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Doctor room schedule was created but could not be retrieved.");
                }


                return CreatedAtRoute(
                    "GetDoctorRoomScheduleByID",
                    new { scheduleID = schedule.DoctorRoomScheduleID },
                    schedule.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => BadRequest(ex.Message), // Invalid day

                    50002 => BadRequest(ex.Message), // Invalid time

                    50003 => NotFound(ex.Message), // Doctor not found

                    50004 => NotFound(ex.Message), // Clinic room not found

                    50005 => NotFound(ex.Message), // User not found

                    50006 => Conflict(ex.Message), // Schedule already exists

                    50007 => Conflict(ex.Message), // Room conflict

                    50008 => Conflict(ex.Message), // Doctor conflict

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
            catch (Exception ex)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ex.Message);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{scheduleID}/Update", Name = "UpdateDoctorRoomSchedule")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetDoctorRoomScheduleByIdDTO> UpdateDoctorRoomSchedule(
          int scheduleID,
          [FromBody] UpdateDoctorRoomScheduleDTO scheduleDto)
        {
            if (scheduleID <= 0 ||
                scheduleDto == null ||
                scheduleDto.DayID <= 0 ||
                scheduleDto.DoctorID <= 0 ||
                scheduleDto.ClinicRoomID <= 0)
            {
                return BadRequest("Invalid data.");
            }


            clsDoctorRoomSchedule schedule =
                clsDoctorRoomSchedule.FindByID(scheduleID);


            if (schedule == null)
            {
                return NotFound(
                    $"Schedule with ID {scheduleID} was not found.");
            }


            schedule.DayID = scheduleDto.DayID;
            schedule.DoctorID = scheduleDto.DoctorID;
            schedule.ClinicRoomID = scheduleDto.ClinicRoomID;
            schedule.FromTime = scheduleDto.FromTime;
            schedule.ToTime = scheduleDto.ToTime;


            try
            {
                if (!schedule.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update doctor room schedule.");
                }


                schedule = clsDoctorRoomSchedule.FindByID(
                    schedule.DoctorRoomScheduleID);


                if (schedule == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Schedule was updated but could not be retrieved.");
                }


                return Ok(schedule.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message), // Schedule not found

                    50002 => BadRequest(ex.Message), // Invalid day

                    50003 => BadRequest(ex.Message), // Invalid time

                    50004 => NotFound(ex.Message), // Doctor not found

                    50005 => NotFound(ex.Message), // Clinic room not found

                    50006 => Conflict(ex.Message), // Has appointments

                    50007 => Conflict(ex.Message), // Duplicate schedule

                    50008 => Conflict(ex.Message), // Room conflict

                    50009 => Conflict(ex.Message), // Doctor conflict

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
           
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{scheduleID}/Deactivate", Name = "DeactivateDoctorRoomSchedule")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult DeactivateDoctorRoomSchedule(int scheduleID)
        {
            if (scheduleID <= 0)
                return BadRequest("Invalid data.");


            try
            {
                if (!clsDoctorRoomSchedule.Deactivate(scheduleID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to deactivate doctor room schedule.");
                }


                return Ok("Doctor room schedule deactivated successfully.");
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
        [HttpPut("{scheduleID}/Activate", Name = "ActivateDoctorRoomSchedule")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult ActivateDoctorRoomSchedule(int scheduleID)
        {
            if (scheduleID <= 0)
                return BadRequest("Invalid data.");


            try
            {
                if (!clsDoctorRoomSchedule.Activate(scheduleID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to activate doctor room schedule.");
                }


                return Ok("Doctor room schedule activated successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message), // Schedule not found

                    50002 => Conflict(ex.Message), // Already active

                    50003 => Conflict(ex.Message), // Room conflict

                    50004 => Conflict(ex.Message), // Doctor conflict

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }
    }
}
