namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BetModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        ParentId = c.Int(nullable: false),
                        MasterId = c.Int(nullable: false),
                        AdminId = c.Int(nullable: false),
                        SuperId = c.Int(nullable: false),
                        UserName = c.String(),
                        SportsId = c.Int(nullable: false),
                        EventId = c.String(),
                        EventName = c.String(),
                        MarketId = c.String(),
                        MarketName = c.String(),
                        RunnerId = c.String(),
                        RunnerName = c.String(),
                        BetType = c.String(),
                        Odds = c.Double(nullable: false),
                        Price = c.Double(nullable: false),
                        Stake = c.Double(nullable: false),
                        Exposure = c.Double(nullable: false),
                        Profit = c.Double(nullable: false),
                        BetStatus = c.String(),
                        Result = c.String(),
                        IpAddress = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.BlockEventModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        EventId = c.String(),
                        UserId = c.Int(nullable: false),
                        EventFancy = c.Boolean(nullable: false),
                        EventRate = c.Boolean(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.BlockMarketModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        EventId = c.String(),
                        MarketId = c.String(),
                        UserId = c.Int(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.CardDetails",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        MarketId = c.String(),
                        CardNames = c.String(),
                        MarketName = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.ChipModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        ChipName = c.Double(nullable: false),
                        ChipValue = c.Double(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.EventModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        EventId = c.String(),
                        EventName = c.String(),
                        Runner1 = c.String(),
                        Runner2 = c.String(),
                        EventTime = c.DateTime(nullable: false),
                        EventFancy = c.Boolean(nullable: false),
                        SportsId = c.Int(nullable: false),
                        SportsName = c.String(),
                        SeriesId = c.String(),
                        SeriesName = c.String(),
                        IsFav = c.Boolean(nullable: false),
                        Betdelay = c.Double(nullable: false),
                        Fancydelay = c.Double(nullable: false),
                        MaxStake = c.Double(nullable: false),
                        MinStake = c.Double(nullable: false),
                        MaxProfit = c.Double(nullable: false),
                        Back1 = c.String(),
                        Lay1 = c.String(),
                        Back2 = c.String(),
                        Lay2 = c.String(),
                        Back3 = c.String(),
                        Lay3 = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.ExposureModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        ParentId = c.Int(nullable: false),
                        MasterId = c.Int(nullable: false),
                        AdminId = c.Int(nullable: false),
                        SuperId = c.Int(nullable: false),
                        EventId = c.String(),
                        MarketId = c.String(),
                        RunnerId = c.String(),
                        MarketName = c.String(),
                        Exposure = c.Double(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.MarketModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        EventId = c.String(),
                        MarketId = c.String(),
                        marketName = c.String(),
                        Result = c.String(),
                        Betdelay = c.Double(nullable: false),
                        Fancydelay = c.Double(nullable: false),
                        MaxStake = c.Double(nullable: false),
                        MinStake = c.Double(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.NewsModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        News = c.String(nullable: false, unicode: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.OfferModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        Offer = c.String(unicode: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.RunnerModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        EventId = c.String(),
                        MarketId = c.String(),
                        RunnerId = c.Int(nullable: false),
                        RunnerName = c.String(),
                        Book = c.Double(nullable: false),
                        MarketName = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.SignUpModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.String(),
                        ParentId = c.Int(nullable: false),
                        MasterId = c.Int(nullable: false),
                        AdminId = c.Int(nullable: false),
                        SuperId = c.Int(nullable: false),
                        UserName = c.String(),
                        Role = c.String(),
                        BetStatus = c.Boolean(nullable: false),
                        FancyBetStatus = c.Boolean(nullable: false),
                        CasinoStatus = c.Boolean(nullable: false),
                        TableStatus = c.Boolean(nullable: false),
                        ExposureLimit = c.Double(nullable: false),
                        IpAddress = c.String(),
                        Balance = c.Double(nullable: false),
                        Exposure = c.Double(nullable: false),
                        ProfitLoss = c.Double(nullable: false),
                        CreditLimit = c.Double(nullable: false),
                        Share = c.Double(nullable: false),
                        Password = c.String(),
                        MobileNumber = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.SuperNowaTransModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        discription = c.String(),
                        transId = c.String(),
                        refId = c.String(),
                        amount = c.Double(nullable: false),
                        userId = c.Int(nullable: false),
                        userName = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.TakeRecords",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        Records = c.Int(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.TransactionModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        UserName = c.String(),
                        SportsId = c.Int(nullable: false),
                        EventId = c.String(),
                        MarketId = c.String(),
                        SelectionId = c.String(),
                        Discription = c.String(),
                        MarketName = c.String(),
                        Remark = c.String(),
                        Amount = c.Double(nullable: false),
                        Commission = c.Double(nullable: false),
                        Balance = c.Double(nullable: false),
                        ParentId = c.Int(nullable: false),
                        MasterId = c.Int(nullable: false),
                        AdminId = c.Int(nullable: false),
                        SuperId = c.Int(nullable: false),
                        Parent = c.Int(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.UserSettingModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        SportsId = c.Int(nullable: false),
                        SportsName = c.String(),
                        MaxStake = c.Double(nullable: false),
                        MinStake = c.Double(nullable: false),
                        MaxOdds = c.Double(nullable: false),
                        BetDelay = c.Double(nullable: false),
                        FancyDelay = c.Double(nullable: false),
                        MaxProfit = c.Double(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.UserSettingModels");
            DropTable("dbo.TransactionModels");
            DropTable("dbo.TakeRecords");
            DropTable("dbo.SuperNowaTransModels");
            DropTable("dbo.SignUpModels");
            DropTable("dbo.RunnerModels");
            DropTable("dbo.OfferModels");
            DropTable("dbo.NewsModels");
            DropTable("dbo.MarketModels");
            DropTable("dbo.ExposureModels");
            DropTable("dbo.EventModels");
            DropTable("dbo.ChipModels");
            DropTable("dbo.CardDetails");
            DropTable("dbo.BlockMarketModels");
            DropTable("dbo.BlockEventModels");
            DropTable("dbo.BetModels");
        }
    }
}
