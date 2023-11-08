using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class BlockMarketModel :BaseModel
    {
        public string EventId { get; set; }
        public string MarketId { get; set; }
        public int UserId { get; set; }
    }
}