namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init4 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.EventLastDigitModels", "Min", c => c.Double(nullable: false));
            AddColumn("dbo.EventLastDigitModels", "Max", c => c.Double(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.EventLastDigitModels", "Max");
            DropColumn("dbo.EventLastDigitModels", "Min");
        }
    }
}
