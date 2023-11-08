using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class UserSettingModel : BaseModel
    {
        public int UserId { get; set; }
        public int SportsId { get; set; }
        public string SportsName { get; set; }
        public double MaxStake { get; set; }
        public double MinStake { get; set; }
        public double MaxOdds { get; set; }
        public double BetDelay { get; set; }
        public double FancyDelay { get; set; }
        public double MaxProfit { get; set; }
    }
}