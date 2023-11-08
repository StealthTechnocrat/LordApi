namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class apiurltypeinmarket : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.MarketModels", "ApiUrlType", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.MarketModels", "ApiUrlType");
        }
    }
}
