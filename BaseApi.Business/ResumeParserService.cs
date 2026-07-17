using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BaseApi.Model;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using Microsoft.Extensions.Logging;

namespace BaseApi.Business
{
    public class ResumeParserService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResumeParserService> _logger;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        public ResumeParserService(IConfiguration configuration, ILogger<ResumeParserService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Main entry point
        //  Strategy: Run 5 focused sequential calls instead of parallel WhenAll.
        //  Sequential calls prevent Jetson Orin GPU from getting overloaded,
        //  avoiding 524 gateway timeouts, and keeping RAM usage perfectly stable.
        // ─────────────────────────────────────────────────────────────────────
        public async Task<Resume> ParseResumeTextAsync(string extractedText)
        {
            var endpoint  = _configuration["OllamaSettings:Endpoint"] ?? "https://ai.bilal-shaik.com/api/generate";
            var modelName = _configuration["OllamaSettings:Model"]    ?? "gemma2:2b";

            _logger.LogInformation($"[ResumeParser] Using model '{modelName}' at '{endpoint}'");
            _logger.LogInformation($"[ResumeParser] Text length: {extractedText.Length} characters");

            // Run sequentially for stability on edge hardware
            // Only auto-populate what the model reliably extracts:
            // Personal Details, Work Experience, Education, and Certification names+org
            // Skills are left for manual entry (user adds them)
            var personalDetails = await ExtractPersonalDetails(endpoint, modelName, extractedText);
            var workExperiences = await ExtractWorkExperiences(endpoint, modelName, extractedText);
            var educations      = await ExtractEducations(endpoint, modelName, extractedText);
            var certifications  = await ExtractCertifications(endpoint, modelName, extractedText);

            var resume = new Resume
            {
                PersonalDetails  = personalDetails,
                WorkExperiences  = workExperiences,
                Educations       = educations,
                Skills           = new List<Skill>(), // User adds skills manually
                Certifications   = certifications
            };

            _logger.LogInformation("[ResumeParser] Parse complete (4 API calls).");
            _logger.LogInformation($"  PersonalDetails : {(resume.PersonalDetails  != null ? "OK" : "NULL")}");
            _logger.LogInformation($"  WorkExperiences : {resume.WorkExperiences?.Count ?? 0} entries");
            _logger.LogInformation($"  Educations      : {resume.Educations?.Count      ?? 0} entries");
            _logger.LogInformation($"  Certifications  : {resume.Certifications?.Count  ?? 0} entries");
            _logger.LogInformation("  Skills          : (manual — not auto-populated)");

            return resume;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Ollama API caller
        // ─────────────────────────────────────────────────────────────────────
        private async Task<string?> CallOllama(string endpoint, string modelName, string prompt, int numPredict = 1024)
        {
            var payload = new
            {
                model   = modelName,
                prompt  = prompt,
                stream  = false,
                format  = "json",
                options = new { temperature = 0, num_ctx = 4096, num_predict = numPredict }
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var httpResponse = await _httpClient.PostAsync(endpoint, content);
                httpResponse.EnsureSuccessStatusCode();

                var raw = await httpResponse.Content.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeAnonymousType(raw, new { response = "" });
                var responseText = parsed?.response ?? "";

                _logger.LogInformation($"[Ollama] Raw response ({responseText.Length} chars): {responseText[..Math.Min(200, responseText.Length)]}...");
                return responseText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Ollama] Call failed: {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Section 1: Personal Details
        // ─────────────────────────────────────────────────────────────────────
        private async Task<PersonalDetails?> ExtractPersonalDetails(string endpoint, string model, string text)
        {
            var prompt = $@"Extract contact details from the following resume text.
Return ONLY a valid JSON object matching this schema. Do not add any conversational text.

{{
  ""name"": ""<full name e.g. John Doe>"",
  ""email"": ""<email address>"",
  ""phoneNumber"": ""<phone number>"",
  ""location"": ""<city, state or address>""
}}

Resume text:
{text}";

            var raw = await CallOllama(endpoint, model, prompt);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw).RootElement;
                var fullName = GetString(doc, "name", "Name", "fullName", "FullName");
                var firstName = "";
                var lastName = "";

                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        firstName = parts[0];
                        lastName = string.Join(" ", parts.Skip(1));
                    }
                }

                return new PersonalDetails
                {
                    FirstName   = firstName,
                    LastName    = lastName,
                    Email       = GetString(doc, "email", "Email", "emailAddress"),
                    PhoneNumber = GetString(doc, "phoneNumber", "PhoneNumber", "phone"),
                    Location    = GetString(doc, "location", "Location", "address")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonalDetails] Parse error: {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Section 2: Work Experiences
        // ─────────────────────────────────────────────────────────────────────
        private async Task<List<WorkExperiences>> ExtractWorkExperiences(string endpoint, string model, string text)
        {
            var prompt = $@"Extract all work experience entries from the following resume text.
Return ONLY a valid JSON object matching this schema. Each description must be an array of individual bullet points. Do not add any conversational text.

{{
  ""workExperiences"": [
    {{
      ""companyName"": ""<company name>"",
      ""position"": ""<job title>"",
      ""startDate"": ""<start month/year>"",
      ""endDate"": ""<end month/year or Present>"",
      ""location"": ""<city, state or remote>"",
      ""description"": [
        ""<bullet point responsibility 1>"",
        ""<bullet point responsibility 2>""
      ]
    }}
  ]
}}

Resume text:
{text}";

            var raw = await CallOllama(endpoint, model, prompt);
            if (string.IsNullOrWhiteSpace(raw)) return new List<WorkExperiences>();

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw).RootElement;
                var arr = GetArray(doc, "workExperiences", "WorkExperiences");
                var list = new List<WorkExperiences>();
                foreach (var item in arr)
                {
                    var startDate = GetString(item, "startDate", "StartDate");
                    var endDate = GetString(item, "endDate", "EndDate");

                    CleanAndSplitDates(ref startDate, ref endDate);

                    var desc = "";
                    if (item.TryGetProperty("description", out var descProp) || item.TryGetProperty("Description", out descProp))
                    {
                        if (descProp.ValueKind == JsonValueKind.Array)
                        {
                            var bullets = descProp.EnumerateArray()
                                .Select(b => b.GetString() ?? "")
                                .Where(b => !string.IsNullOrWhiteSpace(b))
                                .Select(b => b.Trim('-', '•', ' ').Trim());
                            desc = string.Join("\n", bullets);
                        }
                        else
                        {
                            desc = descProp.GetString() ?? "";
                        }
                    }

                    list.Add(new WorkExperiences
                    {
                        CompanyName = GetString(item, "companyName", "CompanyName", "company"),
                        Position    = GetString(item, "position", "Position", "jobTitle"),
                        StartDate   = startDate,
                        EndDate     = endDate,
                        Location    = GetString(item, "location", "Location"),
                        Description = CleanValue(desc)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkExperiences] Parse error: {ex.Message}");
                return new List<WorkExperiences>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Section 3: Educations
        // ─────────────────────────────────────────────────────────────────────
        private async Task<List<EducationDetails>> ExtractEducations(string endpoint, string model, string text)
        {
            var prompt = $@"Extract all educational history entries from the following resume text.
Return ONLY a valid JSON object matching this schema. Do not add any conversational text.

{{
  ""educations"": [
    {{
      ""collegeName"": ""<university or school name>"",
      ""degree"": ""<degree type e.g. Master of Science>"",
      ""major"": ""<field of study e.g. Computer Science>"",
      ""startDate"": ""<start year>"",
      ""endDate"": ""<end year or Expected>"",
      ""location"": ""<city, state or country>"",
      ""gpa"": ""<GPA if listed>""
    }}
  ]
}}

Resume text:
{text}";

            var raw = await CallOllama(endpoint, model, prompt);
            if (string.IsNullOrWhiteSpace(raw)) return new List<EducationDetails>();

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw).RootElement;
                var arr = GetArray(doc, "educations", "Educations", "educationDetails");
                var list = new List<EducationDetails>();
                foreach (var item in arr)
                {
                    var startDate = GetString(item, "startDate", "StartDate");
                    var endDate = GetString(item, "endDate", "EndDate");

                    CleanAndSplitDates(ref startDate, ref endDate);

                    list.Add(new EducationDetails
                    {
                        CollegeName = GetString(item, "collegeName", "CollegeName", "school", "university"),
                        Degree      = GetString(item, "degree", "Degree"),
                        Major       = GetString(item, "major", "Major", "fieldOfStudy"),
                        StartDate   = startDate,
                        EndDate     = endDate,
                        Location    = GetString(item, "location", "Location"),
                        Gpa         = GetString(item, "gpa", "Gpa")
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Educations] Parse error: {ex.Message}");
                return new List<EducationDetails>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Section 4: Skills
        // ─────────────────────────────────────────────────────────────────────
        private async Task<List<Skill>> ExtractSkills(string endpoint, string model, string text)
        {
            // Simplified prompt — gemma2:2b responds reliably to direct, short instructions
            var prompt = $@"List all technical skills and technologies from the resume below.
Return ONLY a JSON object in this exact format. Put each skill as a separate object.

{{""skills"": [{{""name"": ""skill1""}}, {{""name"": ""skill2""}}]}}

Resume:
{text}";

            // Skills lists can be long — give the model more tokens to work with
            var raw = await CallOllama(endpoint, model, prompt, numPredict: 2048);
            Console.WriteLine($"[Skills] Full raw response: {raw}");

            if (string.IsNullOrWhiteSpace(raw)) return new List<Skill>();

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw).RootElement;
                var arr = GetArray(doc, "skills", "Skills", "technicalSkills", "TechnicalSkills");
                var list = new List<Skill>();

                if (arr.Length > 0)
                {
                    foreach (var item in arr)
                    {
                        // Each item may be an object { name: "..." } or a plain string
                        if (item.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            var name = GetString(item, "name", "Name", "skill", "Skill", "technology");
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                foreach (var part in SplitSkillString(name))
                                    list.Add(new Skill { Name = part });
                            }
                        }
                        else if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var name = item.GetString() ?? "";
                            foreach (var part in SplitSkillString(name))
                                list.Add(new Skill { Name = part });
                        }
                    }
                }
                else
                {
                    // Fallback: maybe the model returned a flat string or comma list somewhere
                    var flatSkills = GetString(doc, "skills", "Skills", "technicalSkills");
                    if (!string.IsNullOrWhiteSpace(flatSkills))
                    {
                        foreach (var part in SplitSkillString(flatSkills))
                            list.Add(new Skill { Name = part });
                    }

                    // Last-resort fallback: regex-extract all "name": "value" pairs from raw text
                    // This handles the case where gemma2:2b outputs a flat object with duplicate keys
                    // e.g. {"name": "C#", "name": "Java"} which is invalid JSON but parseable via regex
                    if (list.Count == 0 && raw != null)
                    {
                        var matches = Regex.Matches(raw, @"""name""\s*:\s*""([^""]+)""");
                        foreach (Match match in matches)
                        {
                            var skillName = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(skillName) && skillName.Length < 60)
                                list.Add(new Skill { Name = skillName });
                        }
                        Console.WriteLine($"[Skills] Regex fallback extracted {list.Count} skills from flat response");
                    }
                }

                Console.WriteLine($"[Skills] Extracted {list.Count} skills");
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Skills] Parse error: {ex.Message}. Raw: {raw}");
                return new List<Skill>();
            }
        }

        // Split a possibly comma/semicolon-separated skill string into individual items
        private static List<string> SplitSkillString(string raw)
        {
            var result = new List<string>();
            var separators = new[] { ',', ';', '|', '\n', '\r' };
            foreach (var part in raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var cleaned = part.Trim('-', '•', '*', ' ').Trim();
                if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length < 60)
                    result.Add(cleaned);
            }
            return result.Count > 0 ? result : new List<string> { raw.Trim() };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Section 5: Certifications
        // ─────────────────────────────────────────────────────────────────────
        private async Task<List<Certification>> ExtractCertifications(string endpoint, string model, string text)
        {
            // Simplified prompt: only extract name and issuing org
            // Dates and URLs are often hallucinated by small models — user fills those manually
            var prompt = $@"List all certifications from the resume below.
Return ONLY a JSON object. Do not add any conversational text.

{{""certifications"": [{{""name"": ""cert name"", ""issuingOrganization"": ""org name""}}]}}

Resume:
{text}";

            var raw = await CallOllama(endpoint, model, prompt);
            if (string.IsNullOrWhiteSpace(raw)) return new List<Certification>();

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw).RootElement;
                var arr = GetArray(doc, "certifications", "Certifications", "certificates");
                var list = new List<Certification>();
                foreach (var item in arr)
                {
                    list.Add(new Certification
                    {
                        Name                 = GetString(item, "name", "Name"),
                        IssuingOrganization  = GetString(item, "issuingOrganization", "IssuingOrganization", "organization"),
                        IssueDate            = GetString(item, "issueDate", "IssueDate"),
                        VerificationUrl      = GetString(item, "verificationUrl", "VerificationUrl", "url")
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Certifications] Parse error: {ex.Message}");
                return new List<Certification>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helper utilities
        // ─────────────────────────────────────────────────────────────────────
        private static string GetString(System.Text.Json.JsonElement el, params string[] alternateKeys)
        {
            foreach (var key in alternateKeys)
            {
                if (el.TryGetProperty(key, out var prop))
                {
                    return CleanValue(prop.GetString() ?? "");
                }
            }

            var normalizedKeys = alternateKeys.Select(NormalizeKey).ToHashSet();
            foreach (var prop in el.EnumerateObject())
            {
                if (normalizedKeys.Contains(NormalizeKey(prop.Name)))
                {
                    return CleanValue(prop.Value.GetString() ?? "");
                }
            }

            return "";
        }

        private static System.Text.Json.JsonElement[] GetArray(System.Text.Json.JsonElement el, params string[] alternateKeys)
        {
            foreach (var key in alternateKeys)
            {
                if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Array)
                {
                    return prop.EnumerateArray().ToArray();
                }
            }

            var normalizedKeys = alternateKeys.Select(NormalizeKey).ToHashSet();
            foreach (var prop in el.EnumerateObject())
            {
                if (normalizedKeys.Contains(NormalizeKey(prop.Name)) && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    return prop.Value.EnumerateArray().ToArray();
                }
            }

            if (el.ValueKind == JsonValueKind.Array)
            {
                return el.EnumerateArray().ToArray();
            }

            return Array.Empty<System.Text.Json.JsonElement>();
        }

        private static string NormalizeKey(string key)
        {
            return key.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        }

        private static string CleanValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            var cleaned = val.Trim();
            cleaned = cleaned.Trim('-', '–', '—', ' ').Trim();
            return cleaned;
        }

        private static void CleanAndSplitDates(ref string startDate, ref string endDate)
        {
            startDate = CleanValue(startDate);
            endDate = CleanValue(endDate);

            if (string.IsNullOrEmpty(endDate) && !string.IsNullOrEmpty(startDate))
            {
                var splitters = new[] { " - ", " – ", " — ", " to ", "-" };
                foreach (var splitter in splitters)
                {
                    var idx = startDate.IndexOf(splitter, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var end = startDate[(idx + splitter.Length)..].Trim();
                        var start = startDate[..idx].Trim();
                        startDate = CleanValue(start);
                        endDate = CleanValue(end);
                        break;
                    }
                }
            }
        }
    }
}
