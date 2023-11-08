namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class videourl : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.TableGamesModels", "VedioUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.TableGamesModels", "VedioUrl");
        }
    }
}
