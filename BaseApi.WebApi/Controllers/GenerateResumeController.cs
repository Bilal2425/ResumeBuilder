namespace BaseApi.WebApi.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using BaseApi.Business;
    using BaseApi.Data.Contexts;
    using BaseApi.Model;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Driver;

    [Route("api/[controller]")]
    [ApiController]
    public class GenerateResumeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PdfService _pdfService;

        public GenerateResumeController(ApplicationDbContext context, PdfService pdfService)
        {
            _context = context;
            _pdfService = pdfService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> GeneratePdf()
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                    return Unauthorized("Email not found in token.");

                var resume = await _context.Resumes
                    .Find(r => r.Id == email)
                    .FirstOrDefaultAsync();

                if (resume == null)
                    return NotFound("No resume details found for the logged-in user.");

                var templateId = resume.TemplateId ?? "classic";
                var htmlContent = BuildResumeHtml(resume, templateId);
                var pdfBytes = await _pdfService.GeneratePdfFromHtmlAsync(htmlContent);

                return File(pdfBytes, "application/pdf", "resume.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private static string ParseMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            var result = text;
            int boldIndex;
            bool isBoldOpen = false;
            while ((boldIndex = result.IndexOf("**")) != -1)
            {
                if (!isBoldOpen)
                {
                    result = result.Remove(boldIndex, 2).Insert(boldIndex, "<strong>");
                    isBoldOpen = true;
                }
                else
                {
                    result = result.Remove(boldIndex, 2).Insert(boldIndex, "</strong>");
                    isBoldOpen = false;
                }
            }
            
            int italicIndex;
            bool isItalicOpen = false;
            while ((italicIndex = result.IndexOf("*")) != -1)
            {
                if (!isItalicOpen)
                {
                    result = result.Remove(italicIndex, 1).Insert(italicIndex, "<em>");
                    isItalicOpen = true;
                }
                else
                {
                    result = result.Remove(italicIndex, 1).Insert(italicIndex, "</em>");
                    isItalicOpen = false;
                }
            }
            
            return result;
        }

        private static string FormatBullets(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var liItems = lines.Select(line =>
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("• ")) trimmed = trimmed.Substring(2).Trim();
                else if (trimmed.StartsWith("•")) trimmed = trimmed.Substring(1).Trim();
                else if (trimmed.StartsWith("- ")) trimmed = trimmed.Substring(2).Trim();
                else if (trimmed.StartsWith("* ")) trimmed = trimmed.Substring(2).Trim();
                
                trimmed = ParseMarkdown(trimmed);
                return $"<li>{trimmed}</li>";
            });
            
            return $"<ul>{string.Join("", liItems)}</ul>";
        }

        private static string EnsureExternalUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            var trimmed = url.Trim();
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "https://" + trimmed;
            }
            return trimmed;
        }

        private string BuildResumeHtml(Resume resume, string templateId)
        {
            var fullName = $"{resume.PersonalDetails?.FirstName} {resume.PersonalDetails?.LastName}".Trim();
            var email = resume.PersonalDetails?.Email ?? "";
            var phone = resume.PersonalDetails?.PhoneNumber ?? "";
            var location = resume.PersonalDetails?.Location ?? "";

            // CLASSIC ATS TEMPLATE (Match Screenshot precisely)
            if (templateId == "classic")
            {
                var workHtml = "";
                if (resume.WorkExperiences != null && resume.WorkExperiences.Any())
                {
                    workHtml = string.Join("", resume.WorkExperiences.Select(w =>
                        $"<div class='item'>" +
                        $"  <div class='item-header'>" +
                        $"    <div><strong>{w.Position}</strong>, <em>{w.CompanyName}</em></div>" +
                        $"    <div class='date'>{w.StartDate} &ndash; {w.EndDate}</div>" +
                        $"  </div>" +
                        $"  {(string.IsNullOrEmpty(w.Location) ? "" : $"<div class='loc-text'>{w.Location}</div>")}" +
                        $"  <div class='desc-bullets'>{FormatBullets(w.Description)}</div>" +
                        $"</div>"));
                }

                var eduHtml = "";
                if (resume.Educations != null && resume.Educations.Any())
                {
                    eduHtml = string.Join("", resume.Educations.Select(e =>
                        $"<div class='item'>" +
                        $"  <div class='item-header'>" +
                        $"    <div><strong>{e.Degree}</strong>, <em>{e.CollegeName}</em></div>" +
                        $"    <div class='date'>{e.StartDate} &ndash; {e.EndDate}</div>" +
                        $"  </div>" +
                        $"  <div class='major-text'>{e.Major}</div>" +
                        $"  {(string.IsNullOrEmpty(e.Location) && string.IsNullOrEmpty(e.Gpa) ? "" : $"<div class='loc-text'>{e.Location}{(string.IsNullOrEmpty(e.Gpa) ? "" : $" | GPA: {e.Gpa}")}</div>")}" +
                        $"</div>"));
                }

                var skillsHtml = "";
                if (resume.Skills != null && resume.Skills.Any())
                {
                    skillsHtml = "<ul>" + string.Join("", resume.Skills.Select(s =>
                        $"<li>{s.Name}</li>")) + "</ul>";
                }

                var certHtml = "";
                if (resume.Certifications != null && resume.Certifications.Any())
                {
                    var certs = resume.Certifications.ToList();
                    certHtml = "<div class='cert-grid'>";
                    for (int i = 0; i < certs.Count; i++)
                    {
                        var c = certs[i];
                        var nameHtml = c.Name;
                        if (!string.IsNullOrEmpty(c.VerificationUrl))
                        {
                            nameHtml = $"<a href='{EnsureExternalUrl(c.VerificationUrl)}' target='_blank' class='cert-link'>{c.Name}</a> <a href='{EnsureExternalUrl(c.VerificationUrl)}' target='_blank' class='verify-link'>(Verify)</a>";
                        }
                        certHtml += $"<div class='cert-col'><span class='cert-bullet'>&bull;</span><div>{nameHtml}</div></div>";
                    }
                    certHtml += "</div>";
                }

                var cssContent = """
                  * { margin: 0; padding: 0; box-sizing: border-box; }
                  body { font-family: 'Inter', 'Segoe UI', Arial, sans-serif; font-size: 10pt; color: #000000; background: white; padding: 30px 45px; line-height: 1.35; word-wrap: break-word; overflow-wrap: break-word; }
                  
                  .header { text-align: center; margin-bottom: 12px; }
                  .name { font-size: 24pt; font-weight: bold; color: #000000; margin-bottom: 6px; }
                  .contact { display: flex; justify-content: center; align-items: center; gap: 20px; flex-wrap: wrap; }
                  .contact-item { display: inline-flex; align-items: center; font-size: 9.5pt; color: #000000; text-decoration: none; }
                  .contact-item svg { margin-right: 4px; }
                  
                  .section { border-top: 1.5px solid #000000; margin-top: 15px; padding-top: 8px; }
                  .section-title { font-size: 11.5pt; font-weight: bold; margin-bottom: 6px; color: #000000; }
                  
                  .profile-text { font-size: 10pt; text-align: justify; line-height: 1.35; color: #000000; }
                  
                  .item { margin-bottom: 10px; }
                  .item:last-child { margin-bottom: 0; }
                  .item-header { display: flex; justify-content: space-between; align-items: baseline; font-size: 10pt; }
                  .date { font-weight: normal; color: #000000; font-size: 10pt; text-align: right; }
                  .loc-text { font-size: 9.5pt; color: #000000; font-style: italic; margin-top: 1px; }
                  .major-text { font-size: 10pt; font-weight: normal; color: #000000; margin-top: 2px; }
                  
                  ul { padding-left: 18px; margin-top: 4px; margin-bottom: 4px; list-style-type: disc; }
                  li { font-size: 10pt; margin-bottom: 3px; line-height: 1.35; color: #000000; }
                  
                  .cert-grid { display: flex; flex-wrap: wrap; margin-top: 4px; }
                  .cert-col { width: 50%; font-size: 10pt; color: #000000; margin-bottom: 4px; display: flex; align-items: baseline; }
                  .cert-bullet { margin-right: 6px; font-size: 10pt; }
                  .cert-link { color: #000000; text-decoration: none; }
                  .cert-link:hover { text-decoration: underline; }
                  .verify-link { font-size: 8pt; color: #4f46e5; margin-left: 4px; text-decoration: none; }
                  .verify-link:hover { text-decoration: underline; }
                """;

                var envelopeIcon = "<svg viewBox=\"0 0 24 24\" width=\"11\" height=\"11\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z\"></path><polyline points=\"22,6 12,13 2,6\"></polyline></svg>";
                var phoneIcon = "<svg viewBox=\"0 0 24 24\" width=\"11\" height=\"11\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z\"></path></svg>";
                var pinIcon = "<svg viewBox=\"0 0 24 24\" width=\"11\" height=\"11\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z\"></path><circle cx=\"12\" cy=\"10\" r=\"3\"></circle></svg>";

                var contactHtml = "";
                if (!string.IsNullOrEmpty(email)) contactHtml += $"<span class='contact-item'>{envelopeIcon}{email}</span>";
                if (!string.IsNullOrEmpty(phone)) contactHtml += $"<span class='contact-item'>{phoneIcon}{phone}</span>";
                if (!string.IsNullOrEmpty(location)) contactHtml += $"<span class='contact-item'>{pinIcon}{location}</span>";

                var profileHtml = "";
                if (resume.PersonalDetails != null && !string.IsNullOrEmpty(resume.PersonalDetails.Location) && resume.PersonalDetails.Location.Length > 50)
                {
                    profileHtml = $"<div class='section'><div class='section-title'>Profile</div><div class='profile-text'>{resume.PersonalDetails.Location}</div></div>";
                }

                var eduSectionHtml = string.IsNullOrEmpty(eduHtml) ? "" : $"<div class='section'><div class='section-title'>Education</div>{eduHtml}</div>";
                var workSectionHtml = string.IsNullOrEmpty(workHtml) ? "" : $"<div class='section'><div class='section-title'>Professional Experience</div>{workHtml}</div>";
                var skillsSectionHtml = string.IsNullOrEmpty(skillsHtml) ? "" : $"<div class='section'><div class='section-title'>Skills</div>{skillsHtml}</div>";
                var certSectionHtml = string.IsNullOrEmpty(certHtml) ? "" : $"<div class='section'><div class='section-title'>Certificates</div>{certHtml}</div>";

                return $"""
<!DOCTYPE html>
<html>
<head>
<meta charset="UTF-8">
<style>
{cssContent}
</style>
</head>
<body>
  <div class="header">
    <div class="name">{fullName}</div>
    <div class="contact">{contactHtml}</div>
  </div>

  {profileHtml}

  {eduSectionHtml}

  {workSectionHtml}

  {skillsSectionHtml}

  {certSectionHtml}
</body>
</html>
""";
            }

            // DEFAULT TEMPLATE (Fallbacks for other templates: modern, tech, executive)
            // They can use this basic structured style with respective styling
            var defaultWorkHtml = string.Join("", resume.WorkExperiences.Select(w =>
                $"<div class='exp-item'><div class='exp-header'><strong>{w.Position}</strong> &mdash; {w.CompanyName}</div>" +
                $"<div class='exp-meta'>{w.Location} | {w.StartDate} &ndash; {w.EndDate}</div>" +
                $"<div class='exp-desc'>{w.Description}</div></div>"));

            var defaultEduHtml = string.Join("", resume.Educations.Select(e =>
                $"<div class='exp-item'><div class='exp-header'><strong>{e.Degree}</strong> in {e.Major}</div>" +
                $"<div class='exp-meta'>{e.CollegeName} &mdash; {e.Location}</div>" +
                $"<div class='exp-meta'>{e.StartDate} &ndash; {e.EndDate}{(string.IsNullOrEmpty(e.Gpa) ? "" : $" | GPA: {e.Gpa}")}</div></div>"));

            var defaultSkillsHtml = string.Join("", resume.Skills.Select(s =>
                $"<span class='skill-tag'>{s.Name}</span>"));

            var defaultCertHtml = string.Join("", resume.Certifications.Select(c =>
                $"<div class='exp-item'><strong>{c.Name}</strong> &mdash; {c.IssuingOrganization}" +
                $"{(string.IsNullOrEmpty(c.IssueDate) ? "" : $" ({c.IssueDate})")}" +
                $"{(string.IsNullOrEmpty(c.VerificationUrl) ? "" : $" <a href='{EnsureExternalUrl(c.VerificationUrl)}'>Verify</a>")}</div>"));

            var defaultCss = """
              * { margin: 0; padding: 0; box-sizing: border-box; }
              body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11pt; color: #1e293b; background: white; padding: 40px; }
              .header { border-bottom: 3px solid #4f46e5; padding-bottom: 16px; margin-bottom: 20px; }
              .name { font-size: 26pt; font-weight: 700; color: #1e293b; }
              .contact { font-size: 9pt; color: #64748b; margin-top: 6px; }
              .section { margin-bottom: 20px; }
              .section-title { font-size: 12pt; font-weight: 700; text-transform: uppercase; letter-spacing: 1px; color: #4f46e5; border-bottom: 1px solid #e2e8f0; padding-bottom: 4px; margin-bottom: 12px; }
              .exp-item { margin-bottom: 12px; }
              .exp-header { font-size: 11pt; font-weight: 600; }
              .exp-meta { font-size: 9pt; color: #64748b; margin: 2px 0; }
              .exp-desc { font-size: 10pt; margin-top: 4px; color: #374151; }
              .skill-tag { display: inline-block; background: #f1f5f9; border: 1px solid #e2e8f0; border-radius: 4px; padding: 2px 8px; margin: 2px; font-size: 9pt; }
            """;

            return $"""
<!DOCTYPE html>
<html>
<head>
<meta charset="UTF-8">
<style>
{defaultCss}
</style>
</head>
<body>
  <div class="header">
    <div class="name">{fullName}</div>
    <div class="contact">{email} &bull; {phone} &bull; {location}</div>
  </div>

  {defaultWorkHtml}

  {defaultEduHtml}

  {defaultSkillsHtml}

  {defaultCertHtml}
</body>
</html>
""";
        }
    }
}
