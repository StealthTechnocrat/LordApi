using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class CasinoSettingsModel: BaseModel
    {
        public string Url { get; set; }
        public string Currency { get; set; }
        public string ExtraParameter { get; set; }
        public string Environment { get; set; }
    }
}