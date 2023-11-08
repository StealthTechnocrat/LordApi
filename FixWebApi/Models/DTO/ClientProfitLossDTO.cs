using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class ClientProfitLossDTO
    {

        public List<ClientProfitLossDTOList> clientProfitLossDTOList { get; set; }
        public ClientProfitLossDTOTotal clientProfitLossDTOTotal { get; set; }
        public List<ClientDLProfitLossDTO> clientDLProfitLossDTOList { get; set; }
    }
    public class ClientProfitLossDTOList
    {
        public string EventId { get; set; }
        public string MATCH { get; set; }
        public double ODDS { get; set; }
        public double SESSION { get; set; }
        public double TOSS { get; set; }
        public double MATCHCOMM { get; set; }
        public double SESSIONCOMM { get; set; }
        public double COMMTOTAL { get; set; }
        public double NETAMOUNT { get; set; }
        public double DL { get; set; }
        public double MDL { get; set; }
        public DateTime CreatedOn { get; set; }
    }
    public class ClientProfitLossDTOTotal
    {
      
        public double ODDSTOTAL { get; set; }
        public double SESSIONTOTAL { get; set; }
        public double TOSSTOTAL { get; set; }
        public double MATCHCOMMTOTAL { get; set; }
        public double SESSIONCOMMTOTAL { get; set; }
        public double COMMTOTAL { get; set; }
        public double NETAMOUNTTOTAL { get; set; }
        public double DLTOTAL { get; set; }
        public double MDLTOTAL { get; set; }
        public double TOTAL { get; set; }
        public double MDLMATCHCOMTOTAL { get; set; }
        public double MDLSESSIONCOMTOTAL { get; set; }
        public double DLMATCHCOMTOTAL { get; set; }
        public double DLSESSIONCOMTOTAL { get; set; }
        public double FINALTOTAL { get; set; }
        public double MDLCOMMTOTAL { get; set; }
    }

    public class ClientDLProfitLossDTO
    {
        public string NAME { get; set; }
        public double TOTAL { get; set; }
        public double MATCHCOMM_DL { get; set; }
        public double SESSIONCOMM_DL { get; set; }
        public double TOTALCOM_DL { get; set; }
        public double MATCHCOMM_MDL { get; set; }
        public double SESSIONCOMM_MDL { get; set; }
        public double TOTALCOM_MDL { get; set; }
        public double NETAMOUNT { get; set; }
        public double SHRAMT_MDL { get; set; }
        public double SHRAMT_DL { get; set; }
        public double FINAL { get; set; }
        public double ODDS { get; set; }
        public double SESSION { get; set; }
        public double TOSS { get; set; }
    }

}