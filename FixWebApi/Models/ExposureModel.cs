using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class ExposureModel:BaseModel
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
        public string EventId { get; set; }
        public string MarketId { get; set; }
        public string RunnerId { get; set; }
        public string MarketName { get; set; }
        public double Exposure { get; set; }
    }
}