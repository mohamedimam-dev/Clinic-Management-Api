using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentTypesController : ControllerBase
    {
        [HttpGet("All", Name = "GetAllAppointmentTypes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllAppointmentTypesDTO>> GetAllAppointmentTypes()
        {
            List<GetAllAppointmentTypesDTO> List = clsAppointmentType.GetAllAppointmentTypes();
            return Ok(List);
        }
    }
}
