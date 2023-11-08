using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class SuperNowaTransModel:BaseModel
    {
        public string discription { get; set; }
        public string transId { get; set; }
        public string refId { get; set; }
        public double amount { get; set; }
        public int userId { get; set; }
        public string userName { get; set; }
    }
}