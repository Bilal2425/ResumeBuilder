using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseApi.Model
{
    [BsonIgnoreExtraElements]
    public class Resume
    {
        public string? Id { get; set; }  // MongoDB _id (user's email)
        public PersonalDetails? PersonalDetails { get; set; }
        public List<WorkExperiences>? WorkExperiences { get; set; }
        public List<EducationDetails>? Educations { get; set; }
        public List<Skill>? Skills { get; set; }
        public List<Certification>? Certifications { get; set; }
        public string? TemplateId { get; set; }
    }
}
