using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingTimesController : ControllerBase
    {
        [HttpGet("All", Name = "GetAllBookingTimes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllBookingTimesDTO>> GetAllBookingTimes()
        {
            List<GetAllBookingTimesDTO> BookingTimesList = clsBookingTime.GetAll();
            return Ok(BookingTimesList);
        }
    }
}
