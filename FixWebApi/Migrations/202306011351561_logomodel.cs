namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class logomodel : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.WebSiteLogoModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        Type = c.String(),
                        LogoPath = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.WebSiteLogoModels");
        }
    }
}
