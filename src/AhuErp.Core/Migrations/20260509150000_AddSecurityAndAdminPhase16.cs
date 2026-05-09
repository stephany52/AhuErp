namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 16 / Bug #8 + Improvement #17 — админ-панель и безопасность:
    /// журнал попыток входа, история паролей, настройки учреждения
    /// (singleton с ключом шифрования и параметрами политики), а также
    /// расширение <see cref="Models.Employee"/> полями LastPasswordChangeAt
    /// и LockedUntil для контроля срока пароля и lockout.
    /// </summary>
    /// <remarks>
    /// Снимок модели в .resx сгенерирован вручную как заглушка. После
    /// возврата в среду VS / EF6 PowerShell миграцию следует пересобрать
    /// командой <c>Add-Migration AddSecurityAndAdminPhase16 -Force</c>,
    /// чтобы зафиксировать корректный EDM-снимок для последующих миграций.
    /// </remarks>
    public partial class AddSecurityAndAdminPhase16 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.LoginAttempts",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    EmployeeId = c.Int(),
                    AttemptedFullName = c.String(maxLength: 256),
                    Timestamp = c.DateTime(nullable: false),
                    IpAddress = c.String(maxLength: 64),
                    Success = c.Boolean(nullable: false),
                    FailureReason = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.EmployeeId)
                .Index(t => t.EmployeeId)
                .Index(t => t.Timestamp);

            CreateTable(
                "dbo.EmployeePasswordHistories",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    EmployeeId = c.Int(nullable: false),
                    PasswordHash = c.String(nullable: false, maxLength: 512),
                    SetAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.EmployeeId, cascadeDelete: true)
                .Index(t => t.EmployeeId);

            CreateTable(
                "dbo.OrganizationSettings",
                c => new
                {
                    Id = c.Int(nullable: false),
                    EncryptionKey = c.String(maxLength: 128),
                    EncryptionKeyGeneratedAt = c.DateTime(),
                    PasswordMinLength = c.Int(nullable: false),
                    PasswordExpiryDays = c.Int(nullable: false),
                    PasswordHistoryDepth = c.Int(nullable: false),
                    LockoutFailureThreshold = c.Int(nullable: false),
                    LockoutWindowMinutes = c.Int(nullable: false),
                    LockoutDurationMinutes = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id);

            AddColumn("dbo.Employees", "LastPasswordChangeAt", c => c.DateTime());
            AddColumn("dbo.Employees", "LockedUntil", c => c.DateTime());

            // Сидим singleton-настройки с дефолтами политики (8/1 цифра/1 заглавная,
            // 90 дней, 5 паролей в истории, 5 неудач за 10 мин → 30 мин lockout).
            // Шифр-ключ остаётся NULL — администратор задаёт его вручную в админ-панели,
            // чтобы не хранить «нулевой» ключ в production.
            Sql(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationSettings WHERE Id = 1)
                BEGIN
                    INSERT INTO dbo.OrganizationSettings
                        (Id, EncryptionKey, EncryptionKeyGeneratedAt,
                         PasswordMinLength, PasswordExpiryDays, PasswordHistoryDepth,
                         LockoutFailureThreshold, LockoutWindowMinutes, LockoutDurationMinutes)
                    VALUES
                        (1, NULL, NULL, 8, 90, 5, 5, 10, 30);
                END
            ");
        }

        public override void Down()
        {
            DropColumn("dbo.Employees", "LockedUntil");
            DropColumn("dbo.Employees", "LastPasswordChangeAt");

            DropTable("dbo.OrganizationSettings");

            DropForeignKey("dbo.EmployeePasswordHistories", "EmployeeId", "dbo.Employees");
            DropIndex("dbo.EmployeePasswordHistories", new[] { "EmployeeId" });
            DropTable("dbo.EmployeePasswordHistories");

            DropForeignKey("dbo.LoginAttempts", "EmployeeId", "dbo.Employees");
            DropIndex("dbo.LoginAttempts", new[] { "Timestamp" });
            DropIndex("dbo.LoginAttempts", new[] { "EmployeeId" });
            DropTable("dbo.LoginAttempts");
        }
    }
}
