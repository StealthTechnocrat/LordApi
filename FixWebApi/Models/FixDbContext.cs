using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;

namespace FixWebApi.Models
{
    public class FixDbContext : DbContext
    {
        public FixDbContext() : base("FixDbContext")
        {
        }
        public DbSet<SignUpModel> SignUp { get; set; }
        public DbSet<EventModel> Event { get; set; }
        public DbSet<MarketModel> Market { get; set; }
        public DbSet<RunnerModel> Runner { get; set; }
        public DbSet<BlockEventModel> BlockEvent { get; set; }
        public DbSet<BlockMarketModel> BlockMarket { get; set; }
        public DbSet<UserSettingModel> UserSetting { get; set; }
        public DbSet<BetModel> Bet { get; set; }
        public DbSet<ExposureModel> Exposure { get; set; }
        public DbSet<ChipModel> Chip { get; set; }
        public DbSet<NewsModel> News { get; set; }
        public DbSet<OfferModel> Offer { get; set; }
        public DbSet<TakeRecord> TakeRecord { get; set; }
        public DbSet<TransactionModel> Transaction { get; set; }
        public DbSet<CardDetails> Cards { get; set; }
        public DbSet<EventLastDigitModel> LastDigit { get; set; }
        public DbSet<SuperNowaTransModel> SuperNowaTransModels { get; set; }
        public DbSet<CasinoSettingsModel> CasinoSettingsModel { get; set; }
        public DbSet<BannerModel> BannerModel { get; set; }
        public DbSet<LiveCasinoGamesModel> LiveCasinoGamesModel { get; set; }
        public DbSet<LiveCasinoProvidersModel> LiveCasinoProvidersModel { get; set; }
        public DbSet<TableGamesModel> TableGamesModel { get; set; }
        public DbSet<ThirdPartyApiModel> ThirdPartyApiModel { get; set; }
        public DbSet<WebSiteLogoModel> LogoModel { get; set; }

    }
}