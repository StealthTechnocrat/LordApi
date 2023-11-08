using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class RunnerModel :BaseModel
    {
        public string EventId { get; set; }
        public string MarketId { get; set; }
        public int RunnerId { get; set;  }
        public string RunnerName { get; set; }
        public double Book { get; set; }
        public string MarketName { get; set; }

    }
   
}