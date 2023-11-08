namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init2 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.SignUpModels", "MarketCommission", c => c.Double(nullable: false));
            AddColumn("dbo.SignUpModels", "SessionCommission", c => c.Double(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.SignUpModels", "SessionCommission");
            DropColumn("dbo.SignUpModels", "MarketCommission");
        }
    }
}
