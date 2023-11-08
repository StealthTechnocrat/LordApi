using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class EventLastDigitModel : BaseModel
    {
        public string EventId { get; set; }
        public string MarketId { get; set; }
        public string MarketName { get; set; }
        public int Over { get; set; }
        public string RunnerName { get; set; }
        public int Result { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double LaySize { get; set; }
        public double LayPrice { get; set; }
        public double BackPrice { get; set; }
        public double BackSize { get; set; }
        public int Inning { get; set; }
    }
}