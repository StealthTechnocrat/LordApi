using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class EventMarketDTO
    {
        public string marketId { get; set; }
        public string marketName { get; set; }
        public double totalMatched { get; set; }
    }
    
}