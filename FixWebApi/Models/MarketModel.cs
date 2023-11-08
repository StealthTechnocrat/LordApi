using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class MarketModel :BaseModel
    {   
        public string EventId { get; set; }
        public string MarketId { get; set; }
        public string marketName { get; set; }
        public string Result { get; set; }
        public double Betdelay { get; set; }
        public double Fancydelay { get; set; }
        public double MaxStake { get; set; }
        public double MinStake { get; set; }
        public int ApiUrlType { get; set; } //1=diamond,2=betfair,3=bookmaker

    }
}