using BaseApi.Model;
using System.Collections.Generic;

namespace BaseApi.Model
{

    public class Resume
    {
        public string? Id { get; set; }  // MongoDB _id field
        public PersonalDetails? PersonalDetails { get; set; }
        public List<WorkExperience>? WorkExperiences { get; set; }
        public List<Education>? Educations { get; set; }
    }

}