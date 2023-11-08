namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class tablegames : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.TableGamesModels",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        GameName = c.String(),
                        ImagePath = c.String(),
                        SportsId = c.Int(nullable: false),
                        APIUrl = c.String(),
                        ResultAPIUrl = c.String(),
                        deleted = c.Boolean(nullable: false),
                        status = c.Boolean(nullable: false),
                        createdOn = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.TableGamesModels");
        }
    }
}
