using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseApi.Model
{
    public class Template
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string?Id { get; set; }

        [BsonElement("name")]
        public string?Name { get; set; }

        [BsonElement("htmlContent")]
        public string?HtmlContent { get; set; }
    }
}
