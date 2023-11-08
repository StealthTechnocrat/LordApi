namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class logintoken : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.SignUpModels", "LoginToken", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.SignUpModels", "LoginToken");
        }
    }
}
