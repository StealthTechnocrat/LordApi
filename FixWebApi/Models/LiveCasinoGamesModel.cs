using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class LiveCasinoGamesModel:BaseModel
    {
        public string SystemId { get; set; }
        public string PageCode { get; set; }
        public string MerchantName { get; set; }
        public string GameName { get; set; }
        public string TableId { get; set; }
        public string ImagePath { get; set; }
    }
}