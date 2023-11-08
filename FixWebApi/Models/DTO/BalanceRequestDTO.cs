using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class BalanceRequestDTO
    {
        public string UserId { get; set; }
        public string SecurityToken { get; set; }
    }
}