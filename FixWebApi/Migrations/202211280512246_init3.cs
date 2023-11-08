namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init3 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.EventLastDigitModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        EventId = c.String(),
                        MarketId = c.String(),
                        MarketName = c.String(),
                        Over = c.Int(nullable: false),
                        result = c.Int(nullable: false),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            AddColumn("dbo.SignUpModels", "MatchCommission", c => c.Double(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.SignUpModels", "MatchCommission");
            DropTable("dbo.EventLastDigitModels");
        }
    }
}
