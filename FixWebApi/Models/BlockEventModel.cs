using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class BlockEventModel :BaseModel
    {   
        public string EventId { get; set; }
        public int UserId { get; set;  }
        public bool EventFancy { get; set; }
        public bool EventRate { get; set; }
    }
}