namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init8 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.EventLastDigitModels", "Inning", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.EventLastDigitModels", "Inning");
        }
    }
}
