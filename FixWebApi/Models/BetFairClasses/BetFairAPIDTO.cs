using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class BetFairAPIDTO
    {
        public List<Runner> runners { get; set; }
        public object totalAvailable { get; set; }
        public object crossMatching { get; set; }
        public object runnersVoidable { get; set; }
        public object version { get; set; }
        public string marketId { get; set; }
        public double totalMatched { get; set; }
        public object numberOfWinners { get; set; }
        public bool inplay { get; set; }
        public object lastMatchTime { get; set; }
        public object betDelay { get; set; }
        public object isMarketDataDelayed { get; set; }
        public object complete { get; set; }
        public object bspReconciled { get; set; }
        public object numberOfActiveRunners { get; set; }
        public string status { get; set; }
        public object numberOfRunners { get; set; }
    }
    public class AvailableToBack
    {
        public double? size { get; set; }
        public double? price { get; set; }
    }

    public class AvailableToLay
    {
        public double? size { get; set; }
        public double? price { get; set; }
    }

    public class Runner
    {
        public double totalMatched { get; set; }
        public double lastPriceTraded { get; set; }
        public int selectionId { get; set; }
        public int handicap { get; set; }
        public string status { get; set; }
        public List<AvailableToBack> availableToBack { get; set; }
        public List<AvailableToLay> availableToLay { get; set; }
    }
    public class ApiData
    {
        public BetFairAPIDTO betFair { get; set; }
        public CricketAPIDTO CricFair { get; set; }
    }
}