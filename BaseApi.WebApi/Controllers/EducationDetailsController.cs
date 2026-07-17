

namespace BaseApi.WebApi.Controllers
{
    using System.Security.Claims;
    using BaseApi.Data.Contexts;
    using BaseApi.Model;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Driver;

    [Route("api/[controller]")]
    [ApiController]
    public class EducationDetailsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EducationDetailsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SaveEducationDetails([FromBody] List<EducationDetails> educations)
        {
            try
            {


                if (educations == null || !educations.Any())
                {
                    return BadRequest("Error while saving education details");
                }

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
                    resume = new Resume
                    {
                        Id = resumeId,
                        Educations = new List<EducationDetails>(),
                    };


                }

                if (resume.Educations == null)
                {
                    resume.Educations = new List<EducationDetails>();
                }

                resume.Educations.AddRange(educations);

                await _context.Resumes.ReplaceOneAsync(
                    r => r.Id == resumeId,
                    resume,
                    new ReplaceOptions { IsUpsert = true });

                return Ok(new { status = "success", message = "Education details saved successfully" });
            }
            catch (Exception ex) 
            {
                throw ex;
            }

        }
    }
}
