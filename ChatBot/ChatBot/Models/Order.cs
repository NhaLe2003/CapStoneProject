using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;


namespace ChatBot.Models
{
    public class Order
    {
        [BsonId]
        public string OrderID { get; set; }
        public string FinalMaterial { get; set; }
        public int PlannedQTY { get; set; }
        public string UoM { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
