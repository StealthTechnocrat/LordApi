using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.BetFairClasses
{
    public class GameDetailDTO
    {
        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        public string gameCode { get; set; }
        public string gameName { get; set; }
        public string gameDescription { get; set; }
    }
}