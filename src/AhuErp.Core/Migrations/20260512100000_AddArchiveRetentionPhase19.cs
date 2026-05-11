namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 19 / Improvement #16 — архив и долговременное хранение.
    /// <list type="bullet">
    ///   <item><description><c>DestructionActs</c> — акты о выделении к уничтожению
    ///     архивных документов, не подлежащих хранению (Приказ Минкультуры
    ///     от 31.03.2015 № 526, приложение № 21; Приказ Росархива от 20.12.2019 № 236).
    ///     Жизненный цикл независим от <c>DocumentStatus</c>:
    ///     <c>Draft → Approved → Executed | Cancelled</c>.</description></item>
    ///   <item><description><c>DestructionActItems</c> — позиции акта (снимок
    ///     дел номенклатуры). Денормализованы: при удалении исходного
    ///     <c>NomenclatureCases</c>-дела позиция сохраняет исторический индекс,
    ///     заголовок, год, срок хранения и количество документов.</description></item>
    /// </list>
    /// Схема создаётся внешним <c>scripts/create-db.sql</c>; миграция документирует
    /// изменения и поддерживает совместимость с EF6 Add-Migration.
    /// </summary>
    /// <remarks>
    /// Снимок модели в .resx сгенерирован вручную как заглушка. После
    /// возврата в среду VS / EF6 PowerShell миграцию следует пересобрать
    /// командой <c>Add-Migration AddArchiveRetentionPhase19 -Force</c>,
    /// чтобы зафиксировать корректный EDM-снимок для последующих миграций.
    /// </remarks>
    public partial class AddArchiveRetentionPhase19 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DestructionActs",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ActNumber = c.String(nullable: false, maxLength: 64),
                    ActDate = c.DateTime(nullable: false),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    DraftedByEmployeeId = c.Int(nullable: false),
                    ApprovedByEmployeeId = c.Int(),
                    ApprovedAt = c.DateTime(),
                    ExecutedAt = c.DateTime(),
                    DestructionMethod = c.String(maxLength: 256),
                    Notes = c.String(maxLength: 4096),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.DraftedByEmployeeId)
                .ForeignKey("dbo.Employees", t => t.ApprovedByEmployeeId)
                .Index(t => t.ActNumber, unique: true)
                .Index(t => t.Status)
                .Index(t => t.ActDate)
                .Index(t => t.DraftedByEmployeeId)
                .Index(t => t.ApprovedByEmployeeId);

            CreateTable(
                "dbo.DestructionActItems",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    DestructionActId = c.Int(nullable: false),
                    NomenclatureCaseId = c.Int(),
                    CaseIndex = c.String(nullable: false, maxLength: 32),
                    CaseTitle = c.String(nullable: false, maxLength: 512),
                    CaseYear = c.Int(nullable: false, defaultValue: 0),
                    RetentionYears = c.Int(nullable: false, defaultValue: 0),
                    DocumentCount = c.Int(nullable: false, defaultValue: 0),
                    Article = c.String(maxLength: 64),
                    Notes = c.String(maxLength: 1024),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DestructionActs", t => t.DestructionActId, cascadeDelete: true)
                .ForeignKey("dbo.NomenclatureCases", t => t.NomenclatureCaseId)
                .Index(t => t.DestructionActId)
                .Index(t => t.NomenclatureCaseId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.DestructionActItems", "NomenclatureCaseId", "dbo.NomenclatureCases");
            DropForeignKey("dbo.DestructionActItems", "DestructionActId", "dbo.DestructionActs");
            DropIndex("dbo.DestructionActItems", new[] { "NomenclatureCaseId" });
            DropIndex("dbo.DestructionActItems", new[] { "DestructionActId" });
            DropTable("dbo.DestructionActItems");

            DropForeignKey("dbo.DestructionActs", "ApprovedByEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.DestructionActs", "DraftedByEmployeeId", "dbo.Employees");
            DropIndex("dbo.DestructionActs", new[] { "ApprovedByEmployeeId" });
            DropIndex("dbo.DestructionActs", new[] { "DraftedByEmployeeId" });
            DropIndex("dbo.DestructionActs", new[] { "ActDate" });
            DropIndex("dbo.DestructionActs", new[] { "Status" });
            DropIndex("dbo.DestructionActs", new[] { "ActNumber" });
            DropTable("dbo.DestructionActs");
        }
    }
}
