namespace FixWebApi.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class init21 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BetModels", "SubAdminId", c => c.Int(nullable: false));
            AddColumn("dbo.BetModels", "SuperMasterId", c => c.Int(nullable: false));
            AddColumn("dbo.BetModels", "SuperAgentId", c => c.Int(nullable: false));
            AddColumn("dbo.BetModels", "AgentId", c => c.Int(nullable: false));
            AddColumn("dbo.ExposureModels", "SubAdminId", c => c.Int(nullable: false));
            AddColumn("dbo.ExposureModels", "SuperMasterId", c => c.Int(nullable: false));
            AddColumn("dbo.ExposureModels", "SuperAgentId", c => c.Int(nullable: false));
            AddColumn("dbo.ExposureModels", "AgentId", c => c.Int(nullable: false));
            AddColumn("dbo.TransactionModels", "SubAdminId", c => c.Int(nullable: false));
            AddColumn("dbo.TransactionModels", "SuperMasterId", c => c.Int(nullable: false));
            AddColumn("dbo.TransactionModels", "SuperAgentId", c => c.Int(nullable: false));
            AddColumn("dbo.TransactionModels", "AgentId", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.TransactionModels", "AgentId");
            DropColumn("dbo.TransactionModels", "SuperAgentId");
            DropColumn("dbo.TransactionModels", "SuperMasterId");
            DropColumn("dbo.TransactionModels", "SubAdminId");
            DropColumn("dbo.ExposureModels", "AgentId");
            DropColumn("dbo.ExposureModels", "SuperAgentId");
            DropColumn("dbo.ExposureModels", "SuperMasterId");
            DropColumn("dbo.ExposureModels", "SubAdminId");
            DropColumn("dbo.BetModels", "AgentId");
            DropColumn("dbo.BetModels", "SuperAgentId");
            DropColumn("dbo.BetModels", "SuperMasterId");
            DropColumn("dbo.BetModels", "SubAdminId");
        }
    }
}
