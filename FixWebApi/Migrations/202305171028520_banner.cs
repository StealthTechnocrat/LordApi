namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class banner : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BannerModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        SportsId = c.Int(nullable: false),
                        FileName = c.String(),
                        FilePath = c.String(),
                        BannerType = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
            AddColumn("dbo.CasinoSettingsModels", "Environment", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.CasinoSettingsModels", "Environment");
            DropTable("dbo.BannerModels");
        }
    }
}
