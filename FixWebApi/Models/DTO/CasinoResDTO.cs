using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class CasinoResDTO
    {
        public string partnerKey { get; set; }
        public string userId { get; set; }
        public double balance { get; set; }
        public string timestamp { get; set; }
        public status status { get; set; }
    }
    public class status
    {
        public string code { get; set; }
        public string message { get; set; }
    }
}