using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models
{
    public class BannerModel:BaseModel
    {
        public int SportsId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string BannerType { get; set; }
    }
}