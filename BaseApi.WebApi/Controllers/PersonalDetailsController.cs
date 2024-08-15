namespace BaseApi.WebApi.Controllers
{
    using BaseApi.Data.Contexts;
    using BaseApi.Model;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.HttpResults;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Driver;

    [Route("api/[controller]")]
    [ApiController]
    public class PersonalDetailsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PersonalDetailsController(ApplicationDbContext context)
        {
            _context = context; //Inject ApplicationDbContext

        }

        [HttpPost]
        public async Task<IActionResult> SavePersonalDetails(PersonalDetails personalDetails)
        {
            if (personalDetails == null)
            {
                return BadRequest("Error while saving personal details");
            }

            if(string.IsNullOrEmpty(personalDetails.Id))
            {
                personalDetails.Id = Guid.NewGuid().ToString(); //Generate a new unique Id if it is null
            }

            var resume = await _context.Resumes
                .Find(r => r.Id == personalDetails.Id)
                .FirstOrDefaultAsync();

            if (resume == null)
            {
                resume = new Resume { Id = personalDetails.Id };
            }

            resume.PersonalDetails = personalDetails;
            await _context.Resumes.ReplaceOneAsync(
                r => r.Id == personalDetails.Id, 
                resume, 
                new ReplaceOptions { IsUpsert = true });

            return Ok(new { status = "success", message = "Personal details saved successfully"});




        }
    }

   

}
