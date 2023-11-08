using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class BetModel:BaseModel
    {
        public int UserId { get; set; }
        public int ParentId { get; set; }
        public int SuperId { get; set; }
        public int AdminId { get; set; }
        public int SubAdminId { get; set; }
        public int SuperMasterId { get; set; }
        public int MasterId { get; set; }
        public int SuperAgentId { get; set; }
        public int AgentId { get; set; }
        public string UserName { get; set; }
        public int SportsId { get; set; }
        public string EventId { get; set; }
        public string EventName { get; set; }
        public string MarketId { get; set; }
        public string MarketName { get; set; }
        public string RunnerId { get; set; }
        public string RunnerName { get; set; }
        public string BetType { get; set; }
        public double Odds { get; set; }
        public double Price { get; set; }
        public double Stake { get; set; }
        public double Exposure { get; set; }
        public double Profit { get; set; }
        public string BetStatus { get; set; }
        public string Result { get; set; }
        public string IpAddress { get; set; }
    }
}