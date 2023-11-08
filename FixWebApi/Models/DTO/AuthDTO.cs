using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class AuthDTO
    {
        public string partnerName { get; set; }
        public string partnerKey { get; set; }
        public string timestamp { get; set; }
        public game game { get; set; }
        public user user { get; set; }
    }
    public class SuperTranDTO
    {
        public string partnerKey { get; set; }
        public user user { get; set; }
        public gameData gameData { get; set; }
        public transactionData transactionData { get; set; }
        public string timestamp { get; set; }        
    }

    public class game
    {
        public string gameCode { get; set; }
        public string providerCode { get; set; }
    }
    public class user
    {
        public string id { get; set; }
        public string currency { get; set; }
    }
    public class gameData
    {
        public string providerCode { get; set; }
        public string providerTransactionId { get; set; }
        public string gameCode { get; set; }
        public string description { get; set; }
        public string providerRoundId { get; set; }
    }
    public class transactionData
    {
        public string id { get; set; }
        public double amount { get; set; }
        public string referenceId { get; set; }
    }
}