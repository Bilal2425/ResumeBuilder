using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseApi.Model
{
    [BsonIgnoreExtraElements]
    public class PersonalDetails
    {
        //public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Location { get; set; }
        public string? LinkedIn { get; set; }
        public string? GitHub { get; set; }
        public string? Portfolio { get; set; }
    }
}
