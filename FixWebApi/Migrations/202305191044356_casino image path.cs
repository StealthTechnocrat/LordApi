namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class casinoimagepath : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.LiveCasinoProvidersModels", "ImagePath", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.LiveCasinoProvidersModels", "ImagePath");
        }
    }
}

