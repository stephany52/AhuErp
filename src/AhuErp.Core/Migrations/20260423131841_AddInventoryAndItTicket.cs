namespace AhuErp.Core.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddInventoryAndItTicket : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.InventoryItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 256),
                        Category = c.Int(nullable: false),
                        TotalQuantity = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.InventoryTransactions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        InventoryItemId = c.Int(nullable: false),
                        DocumentId = c.Int(),
                        QuantityChanged = c.Int(nullable: false),
                        TransactionDate = c.DateTime(nullable: false),
                        InitiatorId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.DocumentId)
                .ForeignKey("dbo.Employees", t => t.InitiatorId)
                .ForeignKey("dbo.InventoryItems", t => t.InventoryItemId, cascadeDelete: true)
                .Index(t => t.InventoryItemId)
                .Index(t => t.DocumentId)
                .Index(t => t.InitiatorId);
            
            AddColumn("dbo.Documents", "AffectedEquipment", c => c.String(maxLength: 256));
            AddColumn("dbo.Documents", "ResolutionNotes", c => c.String(maxLength: 1024));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.InventoryTransactions", "InventoryItemId", "dbo.InventoryItems");
            DropForeignKey("dbo.InventoryTransactions", "InitiatorId", "dbo.Employees");
            DropForeignKey("dbo.InventoryTransactions", "DocumentId", "dbo.Documents");
            DropIndex("dbo.InventoryTransactions", new[] { "InitiatorId" });
            DropIndex("dbo.InventoryTransactions", new[] { "DocumentId" });
            DropIndex("dbo.InventoryTransactions", new[] { "InventoryItemId" });
            DropColumn("dbo.Documents", "ResolutionNotes");
            DropColumn("dbo.Documents", "AffectedEquipment");
            DropTable("dbo.InventoryTransactions");
            DropTable("dbo.InventoryItems");
        }
    }
}
