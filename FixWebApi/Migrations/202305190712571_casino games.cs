namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class casinogames : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.LiveCasinoGamesModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        SystemId = c.String(),
                        PageCode = c.String(),
                        MerchantName = c.String(),
                        GameName = c.String(),
                        TableId = c.String(),
                        ImagePath = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "dbo.LiveCasinoProvidersModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        SystemId = c.String(),
                        ProviderName = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.LiveCasinoProvidersModels");
            DropTable("dbo.LiveCasinoGamesModels");
        }
    }
}
