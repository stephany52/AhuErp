namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 14 / Improvement #10 — расширение модуля ИТО:
    /// каталог оборудования, сегменты сети, журнал ВКС, журнал диагностики
    /// и поля передачи в сервис у ИТ-заявки.
    /// </summary>
    /// <remarks>
    /// Снимок модели в .resx сгенерирован вручную как заглушка. После
    /// возврата в среду VS / EF6 PowerShell миграцию следует пересобрать
    /// командой <c>Add-Migration AddItoExpansionPhase14 -Force</c>, чтобы
    /// зафиксировать корректный EDM-снимок для последующих миграций.
    /// </remarks>
    public partial class AddItoExpansionPhase14 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.NetworkSegments",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 128),
                    Vlan = c.String(maxLength: 16),
                    IpRange = c.String(maxLength: 32),
                    SubnetMask = c.String(maxLength: 32),
                    Gateway = c.String(maxLength: 32),
                    Dns = c.String(maxLength: 128),
                    Notes = c.String(maxLength: 512),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.Equipment",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    InventoryNumber = c.String(nullable: false, maxLength: 64),
                    Type = c.Int(nullable: false),
                    Model = c.String(maxLength: 256),
                    SerialNumber = c.String(maxLength: 64),
                    MacAddress = c.String(maxLength: 32),
                    IpAddress = c.String(maxLength: 32),
                    Room = c.String(maxLength: 64),
                    ResponsibleEmployeeId = c.Int(),
                    InServiceDate = c.DateTime(),
                    WarrantyExpiry = c.DateTime(),
                    Status = c.Int(nullable: false),
                    NetworkSegmentId = c.Int(),
                    Notes = c.String(maxLength: 1024),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.ResponsibleEmployeeId)
                .ForeignKey("dbo.NetworkSegments", t => t.NetworkSegmentId)
                .Index(t => t.ResponsibleEmployeeId)
                .Index(t => t.NetworkSegmentId);

            CreateTable(
                "dbo.ItTicketDiagnosticEntries",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TicketId = c.Int(nullable: false),
                    Timestamp = c.DateTime(nullable: false),
                    AuthorId = c.Int(nullable: false),
                    Action = c.String(nullable: false, maxLength: 1024),
                    Category = c.String(maxLength: 64),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.TicketId, cascadeDelete: true)
                .ForeignKey("dbo.Employees", t => t.AuthorId)
                .Index(t => t.TicketId)
                .Index(t => t.AuthorId);

            CreateTable(
                "dbo.VideoConferences",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TicketId = c.Int(),
                    Topic = c.String(nullable: false, maxLength: 256),
                    ScheduledAt = c.DateTime(nullable: false),
                    CompletedAt = c.DateTime(),
                    OrganizerId = c.Int(nullable: false),
                    Participants = c.String(maxLength: 2048),
                    Platform = c.Int(nullable: false),
                    MeetingUrl = c.String(maxLength: 1024),
                    Notes = c.String(maxLength: 512),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.TicketId)
                .ForeignKey("dbo.Employees", t => t.OrganizerId)
                .Index(t => t.TicketId)
                .Index(t => t.OrganizerId);

            // ItTicket — TPH discriminator на dbo.Documents.
            AddColumn("dbo.Documents", "AffectedEquipmentId", c => c.Int());
            AddColumn("dbo.Documents", "Kind", c => c.Int());
            AddColumn("dbo.Documents", "IsSentToVendor", c => c.Boolean());
            AddColumn("dbo.Documents", "VendorName", c => c.String(maxLength: 256));
            AddColumn("dbo.Documents", "VendorTicketNumber", c => c.String(maxLength: 64));
            AddColumn("dbo.Documents", "VendorReturnDeadline", c => c.DateTime());
            AddColumn("dbo.Documents", "CompletedAt", c => c.DateTime());

            CreateIndex("dbo.Documents", "AffectedEquipmentId");
            AddForeignKey("dbo.Documents", "AffectedEquipmentId", "dbo.Equipment", "Id");
        }

        public override void Down()
        {
            DropForeignKey("dbo.Documents", "AffectedEquipmentId", "dbo.Equipment");
            DropIndex("dbo.Documents", new[] { "AffectedEquipmentId" });

            DropColumn("dbo.Documents", "CompletedAt");
            DropColumn("dbo.Documents", "VendorReturnDeadline");
            DropColumn("dbo.Documents", "VendorTicketNumber");
            DropColumn("dbo.Documents", "VendorName");
            DropColumn("dbo.Documents", "IsSentToVendor");
            DropColumn("dbo.Documents", "Kind");
            DropColumn("dbo.Documents", "AffectedEquipmentId");

            DropForeignKey("dbo.VideoConferences", "OrganizerId", "dbo.Employees");
            DropForeignKey("dbo.VideoConferences", "TicketId", "dbo.Documents");
            DropForeignKey("dbo.ItTicketDiagnosticEntries", "AuthorId", "dbo.Employees");
            DropForeignKey("dbo.ItTicketDiagnosticEntries", "TicketId", "dbo.Documents");
            DropForeignKey("dbo.Equipment", "NetworkSegmentId", "dbo.NetworkSegments");
            DropForeignKey("dbo.Equipment", "ResponsibleEmployeeId", "dbo.Employees");

            DropIndex("dbo.VideoConferences", new[] { "OrganizerId" });
            DropIndex("dbo.VideoConferences", new[] { "TicketId" });
            DropIndex("dbo.ItTicketDiagnosticEntries", new[] { "AuthorId" });
            DropIndex("dbo.ItTicketDiagnosticEntries", new[] { "TicketId" });
            DropIndex("dbo.Equipment", new[] { "NetworkSegmentId" });
            DropIndex("dbo.Equipment", new[] { "ResponsibleEmployeeId" });

            DropTable("dbo.VideoConferences");
            DropTable("dbo.ItTicketDiagnosticEntries");
            DropTable("dbo.Equipment");
            DropTable("dbo.NetworkSegments");
        }
    }
}
