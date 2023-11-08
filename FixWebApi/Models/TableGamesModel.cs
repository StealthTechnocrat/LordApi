using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class TableGamesModel:BaseModel
    {
        public string GameName { get; set; }
        public string ImagePath { get; set; }
        public int SportsId { get; set; }
        public string APIUrl { get; set; }
        public string ResultAPIUrl { get; set; }
        public string VedioUrl { get; set; }
        public string MarketId { get; set; }
    }
}