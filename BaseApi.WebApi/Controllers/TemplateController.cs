namespace BaseApi.WebApi.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using BaseApi.Data.Contexts;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Driver;

    [Route("api/[controller]")]
    [ApiController]
    public class TemplateController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TemplateController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTemplates()
        {
            try
            {
                var templates = await _context.Templates
                    .Find(_ => true)
                    .ToListAsync();

                var result = templates.Select(t => new { t.Id, t.Name });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTemplate(string id)
        {
            try
            {
                var template = await _context.Templates
                    .Find(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (template == null)
                    return NotFound(new { error = $"Template '{id}' not found." });

                return Ok(template);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
