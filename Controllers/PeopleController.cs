using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ClinicManagementApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PeopleController : ControllerBase
    {
        [Authorize(Roles = "Admin, Receptionist")]
        [HttpGet("AllActive", Name = "GetAllActivePeople")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllPeopleDTO>> GetAllActivePeople()
        {
            List<GetAllPeopleDTO> activeList = clsPerson.GetAllActivePeople();
            return Ok(activeList);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("AllDeleted", Name = "GetAllDeletedPeople")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<GetAllPeopleDTO>> GetAllDeletedPeople()
        {
            List<GetAllPeopleDTO> deletedList = clsPerson.GetAllDeletedPeople();
            return Ok(deletedList);
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpGet("{personID}", Name = "GetPersonInfoByID")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<GetPersonByIdDTO> GetPersonInfoByID(int personID)
        {
            if (personID <= 0)
                return BadRequest("Invalid Data.");

            clsPerson person = clsPerson.FindByID(personID);
            if (person == null)
                return NotFound($"Person With ID {personID} was Not Found.");

            return Ok(person.ToDto());
        }

        [Authorize(Roles = "Admin, Receptionist")]
        [HttpPost("Add", Name = "AddPerson")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetPersonByIdDTO> AddPerson([FromBody] AddPersonDTO newPersonDto)
        {
            if (string.IsNullOrWhiteSpace(newPersonDto.FirstName) ||
                string.IsNullOrWhiteSpace(newPersonDto.SecondName) ||
                string.IsNullOrWhiteSpace(newPersonDto.ThirdName) ||
                string.IsNullOrWhiteSpace(newPersonDto.LastName) ||
                string.IsNullOrWhiteSpace(newPersonDto.Phone) ||
                string.IsNullOrWhiteSpace(newPersonDto.Email) ||
                string.IsNullOrWhiteSpace(newPersonDto.Address) ||
                newPersonDto.CreatedByUserID <= 0)
            {
                return BadRequest("Invalid Data.");
            }

            newPersonDto.FirstName = newPersonDto.FirstName.Trim();
            newPersonDto.SecondName = newPersonDto.SecondName.Trim();
            newPersonDto.ThirdName = newPersonDto.ThirdName.Trim();
            newPersonDto.LastName = newPersonDto.LastName.Trim();
            newPersonDto.Phone = newPersonDto.Phone.Trim();
            newPersonDto.Email = newPersonDto.Email.Trim();
            newPersonDto.Address = newPersonDto.Address.Trim();

            if (newPersonDto.Gender is not (0 or 1))
                return BadRequest("Invalid Gender.");

            if (newPersonDto.DateOfBirth >
                DateOnly.FromDateTime(DateTime.Today).AddYears(-18))
            {
                return BadRequest("Person must be at least 18 years old.");
            }

            clsPerson person = new clsPerson(newPersonDto);

            try
            {
                if (!person.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to add person.");
                }

                person = clsPerson.FindByID(person.PersonID);

                if (person == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Person was created but could not be retrieved.");
                }

                return CreatedAtRoute(
                    "GetPersonInfoByID",
                    new { personID = person.PersonID },
                    person.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),    // User not found

                    50004 => Conflict(ex.Message),    // Person already exists

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{personID}", Name = "UpdatePerson")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<GetPersonByIdDTO> UpdatePerson(
           int personID,
           [FromBody] UpdatePersonDTO personDto)
        {
            if (personID <= 0 ||
                string.IsNullOrWhiteSpace(personDto.FirstName) ||
                string.IsNullOrWhiteSpace(personDto.SecondName) ||
                string.IsNullOrWhiteSpace(personDto.ThirdName) ||
                string.IsNullOrWhiteSpace(personDto.LastName) ||
                string.IsNullOrWhiteSpace(personDto.Phone) ||
                string.IsNullOrWhiteSpace(personDto.Email) ||
                string.IsNullOrWhiteSpace(personDto.Address))
            {
                return BadRequest("Invalid Data.");
            }

            personDto.FirstName = personDto.FirstName.Trim();
            personDto.SecondName = personDto.SecondName.Trim();
            personDto.ThirdName = personDto.ThirdName.Trim();
            personDto.LastName = personDto.LastName.Trim();
            personDto.Phone = personDto.Phone.Trim();
            personDto.Email = personDto.Email.Trim();
            personDto.Address = personDto.Address.Trim();

            if (personDto.Gender is not (0 or 1))
                return BadRequest("Invalid Gender.");

            if (personDto.DateOfBirth >
                DateOnly.FromDateTime(DateTime.Today).AddYears(-18))
            {
                return BadRequest("Person must be at least 18 years old.");
            }

            clsPerson person = clsPerson.FindByID(personID);

            if (person == null)
            {
                return NotFound(
                    $"Person with ID '{personID}' was not found.");
            }

            person.FirstName = personDto.FirstName;
            person.SecondName = personDto.SecondName;
            person.ThirdName = personDto.ThirdName;
            person.LastName = personDto.LastName;
            person.DateOfBirth = personDto.DateOfBirth;
            person.Gender = personDto.Gender;
            person.Phone = personDto.Phone;
            person.Email = personDto.Email;
            person.Address = personDto.Address;

            try
            {
                if (!person.Save())
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to update person.");
                }

                person = clsPerson.FindByID(person.PersonID);

                if (person == null)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Person was updated but could not be retrieved.");
                }

                return Ok(person.ToDto());
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),

                    50004 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{personID}", Name = "DeletePerson")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult DeletePerson(int personID)
        {
            if (personID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsPerson.DeletePerson(personID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to delete person.");
                }

                return Ok("Data Deleted Successfully.");
            }
            catch (SqlException ex)
            {
                return ex.Number switch
                {
                    50001 => NotFound(ex.Message),

                    50002 => Conflict(ex.Message),

                    50003 => Conflict(ex.Message),

                    50004 => Conflict(ex.Message),

                    _ => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ex.Message)
                };
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{personID}/Restore", Name = "RestorePerson")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult RestorePerson(int personID)
        {
            if (personID <= 0)
                return BadRequest("Invalid Data.");

            try
            {
                if (!clsPerson.RestorePerson(personID))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Failed to restore person.");
                }

                return Ok("Data Restored Successfully.");
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
