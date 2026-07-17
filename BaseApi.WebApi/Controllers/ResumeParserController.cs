namespace BaseApi.WebApi.Controllers
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using BaseApi.Business;
    using BaseApi.Model;
    using DocumentFormat.OpenXml.Packaging;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using UglyToad.PdfPig;
    using UglyToad.PdfPig.Content;

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ResumeParserController : ControllerBase
    {
        private readonly ResumeParserService _parserService;

        public ResumeParserController(ResumeParserService parserService)
        {
            _parserService = parserService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadAndParseResume(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".docx")
                return BadRequest(new { error = "Only PDF and DOCX files are supported." });

            string extractedText;

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                if (extension == ".pdf")
                    extractedText = ExtractTextFromPdf(stream);
                else
                    extractedText = ExtractTextFromDocx(stream);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to extract text from file: {ex.Message}" });
            }

            if (string.IsNullOrWhiteSpace(extractedText))
                return BadRequest(new { error = "Could not extract readable text from the uploaded file." });

            try
            {
                var parsedResume = await _parserService.ParseResumeTextAsync(extractedText);
                return Ok(parsedResume);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"AI parsing failed: {ex.Message}" });
            }
        }

        private static string ExtractTextFromPdf(Stream stream)
        {
            var sb = new StringBuilder();
            using var pdf = PdfDocument.Open(stream);
            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString();
        }

        private static string ExtractTextFromDocx(Stream stream)
        {
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            return body?.InnerText ?? string.Empty;
        }
    }
}
