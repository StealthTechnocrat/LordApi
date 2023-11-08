namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init1 : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.SignUpModels", "MarketCommission");
        }
        
        public override void Down()
        {
            AddColumn("dbo.SignUpModels", "MarketCommission", c => c.Double(nullable: false));
        }
    }
}
