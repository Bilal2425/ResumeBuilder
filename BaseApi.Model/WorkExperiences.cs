using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseApi.Model
{
    [BsonIgnoreExtraElements]
    public class WorkExperiences
    {
        [BsonElement("Company")]
        public string? CompanyName { get; set; }
        public string? Position { get; set; }
        [BsonSerializer(typeof(BsonStringOrDateTimeSerializer))]
        public string? StartDate { get; set; }
        [BsonSerializer(typeof(BsonStringOrDateTimeSerializer))]
        public string? EndDate { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
    }
}
