namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init6 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.EventLastDigitModels", "RunnerName", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.EventLastDigitModels", "RunnerName");
        }
    }
}
