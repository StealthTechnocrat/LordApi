using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class WebSiteLogoModel : BaseModel
    {
        public string Type { get; set; }
        public string LogoPath { get; set; }
    }
}