using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class CardDetails:BaseModel
    {
        public string MarketId { get; set; }
        public string CardNames { get; set; }
        public string MarketName { get; set; }
    }
}