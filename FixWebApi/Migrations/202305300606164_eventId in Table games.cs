namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class eventIdinTablegames : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.TableGamesModels", "EventId", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.TableGamesModels", "EventId");
        }
    }
}
