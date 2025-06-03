using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ChatBot.Models
{
    [BsonIgnoreExtraElements]
    public class ErrorLog
    {
        //[BsonId]
        //[BsonRepresentation(BsonType.ObjectId)]
        //public string Id { get; set; }
        public string OrderID { get; set; } //order Id
        public string Station {  get; set; } // Station
        public DateTime ErrorStart { get; set; }
        public Double DurationSec { get; set; }
    }
}
