namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 9 — таблицы in-app/e-mail уведомлений.
    /// Добавляет:
    /// <list type="bullet">
    ///   <item><description><c>Notifications</c> — журнал отправленных
    ///     in-app уведомлений с привязкой к получателю и (опционально) к
    ///     документу/поручению/согласованию.</description></item>
    ///   <item><description><c>NotificationPreferences</c> — настройка
    ///     канала (InApp/Email/Both) и активности по
    ///     <c>NotificationKind</c>.</description></item>
    /// </list>
    /// Поле <c>Employees.Email</c> уже создано миграцией Phase 11
    /// (<c>AddOrgAndSubstitution</c>) — повторно не добавляем.
    /// </summary>
    public partial class AddNotifications : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Notifications",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    RecipientId = c.Int(nullable: false),
                    Kind = c.Int(nullable: false),
                    Title = c.String(maxLength: 512),
                    Body = c.String(maxLength: 2048),
                    RelatedDocumentId = c.Int(),
                    RelatedTaskId = c.Int(),
                    RelatedApprovalId = c.Int(),
                    CreatedAt = c.DateTime(nullable: false),
                    ReadAt = c.DateTime(),
                    Channel = c.Int(nullable: false),
                    SentToEmailAt = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.RecipientId, cascadeDelete: false)
                .Index(t => t.RecipientId)
                .Index(t => new { t.RecipientId, t.ReadAt }, name: "IX_Notifications_Recipient_Unread")
                .Index(t => new { t.RelatedTaskId, t.Kind }, name: "IX_Notifications_RelatedTask_Kind");

            CreateTable(
                "dbo.NotificationPreferences",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    EmployeeId = c.Int(nullable: false),
                    Kind = c.Int(nullable: false),
                    Channel = c.Int(nullable: false),
                    IsEnabled = c.Boolean(nullable: false),
                    EmailOverride = c.String(maxLength: 256),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.EmployeeId, cascadeDelete: false)
                .Index(t => new { t.EmployeeId, t.Kind }, unique: true, name: "IX_NotificationPreferences_Employee_Kind");
        }

        public override void Down()
        {
            DropIndex("dbo.NotificationPreferences", "IX_NotificationPreferences_Employee_Kind");
            DropForeignKey("dbo.NotificationPreferences", "EmployeeId", "dbo.Employees");
            DropTable("dbo.NotificationPreferences");

            DropIndex("dbo.Notifications", "IX_Notifications_RelatedTask_Kind");
            DropIndex("dbo.Notifications", "IX_Notifications_Recipient_Unread");
            DropIndex("dbo.Notifications", new[] { "RecipientId" });
            DropForeignKey("dbo.Notifications", "RecipientId", "dbo.Employees");
            DropTable("dbo.Notifications");
        }
    }
}
