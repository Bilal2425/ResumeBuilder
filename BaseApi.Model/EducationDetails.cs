using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseApi.Model
{
    [BsonIgnoreExtraElements]
    public class EducationDetails
    {
        public string? CollegeName { get; set; }
        public string? Degree { get; set; }
        public string? Major { get; set; }
        [BsonSerializer(typeof(BsonStringOrDateTimeSerializer))]
        public string? StartDate { get; set; }
        [BsonSerializer(typeof(BsonStringOrDateTimeSerializer))]
        public string? EndDate { get; set; }
        public string? Location { get; set; }
        public string? Gpa { get; set; }
    }
}
