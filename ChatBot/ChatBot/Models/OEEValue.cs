using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ChatBot.Models
{
    public class OEEStation
    {
        //public string OrderID { get; set; }
        public string Station {  get; set; } // Blowmolder, Washer, Filler,...., Packer
        //public string Line {  get; set; }   //Line1 or Line2
        public int GoodCount { get; set; }
        public int ScrapCount { get; set; }
        public TimeSpan RunTime { get; set; }
        public TimeSpan Planned { get; set; }
        public TimeSpan CycleTime { get; set; }
        public double Availability { get; set; }
        public double Performance { get; set; }
        public double Quality { get; set; }
        public double OEE { get; set; }
        public DateTime TimeStamp { get; set; }
    }

    public class OEEValue
    {
        [BsonId]
        public string OrderID { get; set; }
        public string Line {  get; set; }
        public List<OEEStation> Stations { get; set; }
        public DateTime TimeStamp { get; set; }
    }

}
