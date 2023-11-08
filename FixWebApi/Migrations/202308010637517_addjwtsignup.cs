namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class addjwtsignup : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.SignUpModels", "jwtToken", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.SignUpModels", "jwtToken");
        }
    }
}
