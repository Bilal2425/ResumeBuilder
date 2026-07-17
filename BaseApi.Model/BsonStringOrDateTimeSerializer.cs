using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace BaseApi.Model
{
    public class BsonStringOrDateTimeSerializer : SerializerBase<string?>
    {
        public override string? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonType = context.Reader.CurrentBsonType;
            if (bsonType == BsonType.DateTime)
            {
                var milliseconds = context.Reader.ReadDateTime();
                var dateTime = DateTime.SpecifyKind(new DateTime(1970, 1, 1).AddMilliseconds(milliseconds), DateTimeKind.Utc);
                return dateTime.ToString("yyyy-MM-dd");
            }
            else if (bsonType == BsonType.String)
            {
                return context.Reader.ReadString();
            }
            else if (bsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }
            else
            {
                context.Reader.SkipValue();
                return null;
            }
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string? value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(value);
            }
        }
    }
}
