namespace AhuErp.Core.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddInventoryItemUnitAndMinimumBalance : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.InventoryItems", "Unit", c => c.String(maxLength: 32));
            AddColumn("dbo.InventoryItems", "MinimumBalance", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.InventoryItems", "MinimumBalance");
            DropColumn("dbo.InventoryItems", "Unit");
        }
    }
}
