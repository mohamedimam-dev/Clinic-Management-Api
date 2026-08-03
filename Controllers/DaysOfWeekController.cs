using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DaysOfWeekController : ControllerBase
    {
        [HttpGet("AllDays", Name = "GetAllDaysOfWeek")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllDaysOfWeekDTO>> GetAllDaysOfWeek()
        {
            List<GetAllDaysOfWeekDTO> listDays = clsDayOfWeek.GetAllDaysOfWeek();
            return Ok(listDays);
        }
    }
}
