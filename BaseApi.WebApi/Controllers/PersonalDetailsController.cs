namespace BaseApi.WebApi.Controllers
{
    using System.Security.Claims;
    using BaseApi.Data.Contexts;
    using BaseApi.Model;
    using Microsoft.AspNetCore.Authorization;
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

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SavePersonalDetails(PersonalDetails personalDetails)
        {
            try
            {


                if (personalDetails == null)
                {
                    return BadRequest("Error while saving personal details");
                }

                //Getting emailId from claims pf the authenticated user.
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                {
                    return Unauthorized(email);
                }

                var resumeId = email;

               
                var resume = await _context.Resumes
                    .Find(r => r.Id == resumeId)
                    .FirstOrDefaultAsync();

                if (resume == null)
                {
                    resume = new Resume { Id = resumeId };
                }

                resume.PersonalDetails = personalDetails;
                await _context.Resumes.ReplaceOneAsync(
                    r => r.Id == resumeId,
                    resume,
                    new ReplaceOptions { IsUpsert = true });

                return Ok(new { status = "success", message = "Personal details saved successfully" });
            }
            catch (Exception ex)
            {
                throw ex;
            }





        }
    }

   

}
