namespace BaseApi.WebApi.Controllers
{
    using System;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using BaseApi.Data.Contexts;
    using BaseApi.Model;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Driver;

    [Route("api/[controller]")]
    [ApiController]
    public class ResumeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ResumeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SaveResume([FromBody] Resume resumeInput)
        {
            try
            {
                if (resumeInput == null)
                    return BadRequest("Error: Resume payload is empty.");

                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                    return Unauthorized("User email claim not found.");

                var resumeId = email;

                var existingResume = await _context.Resumes
                    .Find(r => r.Id == resumeId)
                    .FirstOrDefaultAsync();

                if (existingResume == null)
                    existingResume = new Resume { Id = resumeId };

                if (resumeInput.PersonalDetails != null)
                    existingResume.PersonalDetails = resumeInput.PersonalDetails;

                if (resumeInput.WorkExperiences != null)
                    existingResume.WorkExperiences = resumeInput.WorkExperiences;

                if (resumeInput.Educations != null)
                    existingResume.Educations = resumeInput.Educations;

                if (resumeInput.Skills != null)
                    existingResume.Skills = resumeInput.Skills;

                if (resumeInput.Certifications != null)
                    existingResume.Certifications = resumeInput.Certifications;

                if (!string.IsNullOrEmpty(resumeInput.TemplateId))
                    existingResume.TemplateId = resumeInput.TemplateId;

                await _context.Resumes.ReplaceOneAsync(
                    r => r.Id == resumeId,
                    existingResume,
                    new ReplaceOptions { IsUpsert = true });

                return Ok(new { status = "success", message = "Resume saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetResume()
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                    return Unauthorized("User email claim not found.");

                var resume = await _context.Resumes
                    .Find(r => r.Id == email)
                    .FirstOrDefaultAsync();

                if (resume == null)
                    return NotFound(new { status = "error", message = "Resume not found for the user." });

                return Ok(resume);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
