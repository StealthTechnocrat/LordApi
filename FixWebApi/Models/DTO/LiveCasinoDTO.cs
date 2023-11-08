using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class LiveCasinoDTO
    {
        public string SecurityToken { get; set; }
        public string UserId { get; set; }
        public double Amount { get; set; }
        public string TransactionType { get; set; }
        public string SystemId { get; set; }
        public string PageCode { get; set; }
        public string GameId { get; set; }
        public string ActionId { get; set; }
        public string TransactionId { get; set; }
    }
}