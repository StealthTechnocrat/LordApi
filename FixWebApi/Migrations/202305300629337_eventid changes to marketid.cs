namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class eventidchangestomarketid : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.TableGamesModels", "MarketId", c => c.String());
            DropColumn("dbo.TableGamesModels", "EventId");
        }
        
        public override void Down()
        {
            AddColumn("dbo.TableGamesModels", "EventId", c => c.String());
            DropColumn("dbo.TableGamesModels", "MarketId");
        }
    }
}
