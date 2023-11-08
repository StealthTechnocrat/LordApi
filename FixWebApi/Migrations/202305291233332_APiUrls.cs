namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class APiUrls : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ThirdPartyApiModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        SportsId = c.Int(nullable: false),
                        BetfairUrl = c.String(),
                        DaimondUrl = c.String(),
                        FancyUrl = c.String(),
                        BookMakerUrl = c.String(),
                        ScoreUrl = c.String(),
                        TvUrl = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.ThirdPartyApiModels");
        }
    }
}
