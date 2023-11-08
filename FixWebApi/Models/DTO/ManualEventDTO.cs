using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class ManualEventDTO
    {
        public int SportsId { get; set; }
        public string SeriesId { get; set; }
        public string SeriesName { get; set; }
        public string EventId { get; set; }
        public string EventName { get; set; }
        public string EventTime { get; set; }
        public string MarketId { get; set; }
        public string MarketName { get; set; }
        public int RunnerId1 { get; set; }
        public int RunnerId2 { get; set; }
        public string RunnerName1 { get; set; }
        public string RunnerName2 { get; set; }
    }
}