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
    public class WorkExperiencesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WorkExperiencesController(ApplicationDbContext context)
        {
            _context = context;
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SaveWorkExperienceDetails([FromBody] List<WorkExperiences> experiences)
        {
            try
            {
                if (experiences == null || !experiences.Any())
                {
                    return BadRequest("Error while saving work details");

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
                        WorkExperiences = new List<WorkExperiences>(),
                    };

                }

                if (resume.WorkExperiences == null)
                {
                    resume.WorkExperiences = new List<WorkExperiences>();
                }

                resume.WorkExperiences.AddRange(experiences);

                await _context.Resumes.ReplaceOneAsync(
                    r => r.Id == resumeId,
                    resume,
                    new ReplaceOptions { IsUpsert = true });

                return Ok(new { status = "success", message = "Work Experience details saved successfully" });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
            
    }
}
