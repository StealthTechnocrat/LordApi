using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.BetFairClasses
{
    public class CasinoResult
    {
        public List<Datum> data { get; set; }
        public object graphdata { get; set; }
        public bool success { get; set; }
    }
    public class Datum
    {
        public string mid { get; set; }
        public string result { get; set; }
    }
}