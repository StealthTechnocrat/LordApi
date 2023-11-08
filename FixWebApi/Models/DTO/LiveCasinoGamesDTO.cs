using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class LiveCasinoGamesDTO
    {
        public string ID { get; set; }
        public string System { get; set; }
        public string SubSystem { get; set; }
        public string PageCode { get; set; }
        public string LowRtpUrl { get; set; }
        public string LowRtpMobileUrl { get; set; }
        public string LowRtpUrlExternal { get; set; }
        public string LowRtpMobileUrlExternal { get; set; }
        public string MobilePageCode { get; set; }
        public string MobileAndroidPageCode { get; set; }
        public string MobileWindowsPageCode { get; set; }
        public Trans Trans { get; set; }
        public Description Description { get; set; }
        public string CustomSortType { get; set; }
        public string ImageURL { get; set; }
        public string Branded { get; set; }
        public string SuperBranded { get; set; }
        public string HasDemo { get; set; }
        public string GSort { get; set; }
        public string GSubSort { get; set; }
        public string Status { get; set; }
        public string GameStatus { get; set; }
        public List<string> Categories { get; set; }
        public SortPerCategory SortPerCategory { get; set; }
        public string ExternalCode { get; set; }
        public string MobileExternalCode { get; set; }
        public string AR { get; set; }
        public string IDCountryRestriction { get; set; }
        public string MerchantName { get; set; }
        public string MinBetDefault { get; set; }
        public string MaxBetDefault { get; set; }
        public string MaxMultiplier { get; set; }
        public string SubMerchantName { get; set; }
        public string IsVirtual { get; set; }
        public object WorkingHours { get; set; }
        public string TableID { get; set; }
        public string RTP { get; set; }
        public string BrandedNew { get; set; }
        public List<object> CustomSort { get; set; }
        public string ImageFullPath { get; set; }
        public int BonusBuy { get; set; }
        public int Megaways { get; set; }
        public int Freespins { get; set; }
        public string Freeround { get; set; }
    }
    // Root myDeserializedClass = JsonConvert.DeserializeObject<List<Root>>(myJsonResponse);
    public class Description
    {
        public string en { get; set; }
        public string cs { get; set; }
        public string de { get; set; }
        public string es { get; set; }
        public string it { get; set; }
        public string lt { get; set; }
        public string hu { get; set; }
        public string pl { get; set; }
        public string pt { get; set; }
        public string sk { get; set; }
        public string ru { get; set; }

        [JsonProperty("zh-hans")]
        public string zhhans { get; set; }
        public string ko { get; set; }
    }

    public class SortPerCategory
    {
        [JsonProperty("3604")]
        public int _3604 { get; set; }

        [JsonProperty("16")]
        public int? _16 { get; set; }

        [JsonProperty("13")]
        public int? _13 { get; set; }

        [JsonProperty("4")]
        public int? _4 { get; set; }

        [JsonProperty("35")]
        public int? _35 { get; set; }

        [JsonProperty("37")]
        public int? _37 { get; set; }

        [JsonProperty("41")]
        public int? _41 { get; set; }

        [JsonProperty("22")]
        public int? _22 { get; set; }

        [JsonProperty("1366")]
        public int? _1366 { get; set; }

        [JsonProperty("84")]
        public int? _84 { get; set; }

        [JsonProperty("1364")]
        public int? _1364 { get; set; }

        [JsonProperty("7")]
        public int? _7 { get; set; }

        [JsonProperty("1368")]
        public int? _1368 { get; set; }

        [JsonProperty("19")]
        public int? _19 { get; set; }

        [JsonProperty("10")]
        public int? _10 { get; set; }
    }

    public class Trans
    {
        public string en { get; set; }
        public string id { get; set; }
        public string de { get; set; }
        public string es { get; set; }
        public string fr { get; set; }
        public string it { get; set; }
        public string nl { get; set; }
        public string no { get; set; }
        public string pt { get; set; }
        public string fi { get; set; }
        public string sv { get; set; }
        public string vi { get; set; }
        public string tr { get; set; }
        public string bg { get; set; }
        public string ru { get; set; }
        public string uk { get; set; }
        public string th { get; set; }
        public string zh { get; set; }
        public string ja { get; set; }

        [JsonProperty("zh-hant")]
        public string zhhant { get; set; }
        public string ko { get; set; }
        public string gr { get; set; }

        [JsonProperty("zh-hans")]
        public string zhhans { get; set; }
    }


}