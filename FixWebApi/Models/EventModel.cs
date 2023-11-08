using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class EventModel : BaseModel
    {
        public string EventId { get; set; }
        public string EventName { get; set; }
        public string Runner1 { get; set; }
        public string Runner2 { get; set; }
        public DateTime EventTime { get; set; }
        public bool EventFancy { get; set; }
        public int SportsId { get; set; }
        public string SportsName { get; set; }
        public string SeriesId { get; set; }
        public string ScoreId { get; set; }
        public string SeriesName { get; set; }
        public bool IsFav { get; set; }
        public bool IsLastDigit { get; set; }
        public double Betdelay { get; set; }
        public double Fancydelay { get; set; }
        public double MaxStake { get; set; }
        public double MinStake { get; set; }
        public double MaxProfit { get; set; }
        public string Back1 { get; set; }
        public string Lay1 { get; set; }
        public string Back2 { get; set; }
        public string Lay2 { get; set; }
        public string Back3 { get; set; }
        public string Lay3 { get; set; }
    }
}