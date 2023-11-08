namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init5 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.EventLastDigitModels", "LaySize", c => c.Double(nullable: false));
            AddColumn("dbo.EventLastDigitModels", "LayPrice", c => c.Double(nullable: false));
            AddColumn("dbo.EventLastDigitModels", "BackPrice", c => c.Double(nullable: false));
            AddColumn("dbo.EventLastDigitModels", "BackSize", c => c.Double(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.EventLastDigitModels", "BackSize");
            DropColumn("dbo.EventLastDigitModels", "BackPrice");
            DropColumn("dbo.EventLastDigitModels", "LayPrice");
            DropColumn("dbo.EventLastDigitModels", "LaySize");
        }
    }
}
