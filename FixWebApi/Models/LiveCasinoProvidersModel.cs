using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class LiveCasinoProvidersModel:BaseModel
    {
        public string SystemId { get; set; }
        public string ProviderName { get; set; }
        public string ImagePath { get; set; }
    }
}