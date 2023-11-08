using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class DepositeWithDrawDTO
    {
        public int UserId { get; set; }
        public double Amount { get; set; }
        public string Type { get; set; }
        public string Remarks { get; set; }
    }
}