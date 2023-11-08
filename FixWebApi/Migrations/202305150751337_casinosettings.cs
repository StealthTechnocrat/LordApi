namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class casinosettings : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CasinoSettingsModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        Url = c.String(),
                        Currency = c.String(),
                        ExtraParameter = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.CasinoSettingsModels");
        }
    }
}
