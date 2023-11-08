using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class CasinoParamDTO
    {
        public string UserName { get; set; }
        public string Discription { get; set; }
        public string TransType { get; set; }
        public string SystemId { get; set; }
        public string PageCode { get; set; }
        public string RoundId { get; set; }
        public string PageName { get; set; }
        public string Merchant { get; set; }
        public double Amount { get; set; }
        public string SecurityToken { get; set; }
    }
}