using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class ThirdPartyApiModel:BaseModel
    {
        public int SportsId { get; set; }
        public string BetfairUrl { get; set; }
        public string DaimondUrl { get; set; }
        public string FancyUrl { get; set; }
        public string BookMakerUrl { get; set; }
        public string ScoreUrl { get; set; }
        public string TvUrl { get; set; }
    }
}