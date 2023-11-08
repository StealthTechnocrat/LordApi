using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class BalanceDetailDTO
    {
        public double DownLineBal { get; set; }
        public double DownLineExp { get; set; }
        public double DownLineAvailBal { get; set; }
        public double OwnBal { get; set; }
        public double TotalBal { get; set; }
        public double CreditLimit { get; set; }
        public double Chips { get; set; }
        public int Parent { get; set; }
        public string Role { get; set; }
        public double ProfitLoss { get; set; }
        public dynamic usrObj { get; set; }
        
    }
   
}