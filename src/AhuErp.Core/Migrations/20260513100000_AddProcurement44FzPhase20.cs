namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 20 / Improvement #13 — закупки 44-ФЗ.
    /// <list type="bullet">
    ///   <item><description><c>ProcurementPlans</c> — план-график муниципальных
    ///     закупок на финансовый год (ст. 16 44-ФЗ). Статусы Draft → Approved
    ///     → Published → Closed.</description></item>
    ///   <item><description><c>ProcurementPlanItems</c> — позиции плана
    ///     (ОКПД2, НМЦК, способ определения поставщика, плановый квартал).</description></item>
    ///   <item><description><c>ProcurementProcedures</c> — процедуры (электронный
    ///     аукцион / запрос котировок / единственный поставщик и т.д.) и их
    ///     результаты (победитель, цена контракта).</description></item>
    ///   <item><description>TPH-дискриминатор <c>Contract</c> на таблице
    ///     <c>Documents</c> — муниципальный контракт со ссылкой на процедуру.</description></item>
    ///   <item><description><c>ContractMilestones</c> — этапы исполнения
    ///     контракта (плановая/фактическая дата, сумма, статус).</description></item>
    /// </list>
    /// Схема создаётся внешним <c>scripts/create-db.sql</c>; миграция документирует
    /// изменения и поддерживает совместимость с EF6 Add-Migration.
    /// </summary>
    /// <remarks>
    /// Снимок модели в .resx сгенерирован вручную как заглушка. После
    /// возврата в среду VS / EF6 PowerShell миграцию следует пересобрать
    /// командой <c>Add-Migration AddProcurement44FzPhase20 -Force</c>,
    /// чтобы зафиксировать корректный EDM-снимок для последующих миграций.
    /// </remarks>
    public partial class AddProcurement44FzPhase20 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ProcurementPlans",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    Year = c.Int(nullable: false, defaultValue: 0),
                    Title = c.String(nullable: false, maxLength: 256),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    CreatedAt = c.DateTime(nullable: false),
                    ApprovedAt = c.DateTime(),
                    ApprovedByEmployeeId = c.Int(),
                    PublishedAt = c.DateTime(),
                    EisRegistrationNumber = c.String(maxLength: 64),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.ApprovedByEmployeeId)
                .Index(t => t.Year, unique: true)
                .Index(t => t.Status)
                .Index(t => t.ApprovedByEmployeeId);

            CreateTable(
                "dbo.ProcurementPlanItems",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ProcurementPlanId = c.Int(nullable: false),
                    LineNumber = c.Int(nullable: false, defaultValue: 0),
                    Okpd2Code = c.String(nullable: false, maxLength: 32),
                    Subject = c.String(nullable: false, maxLength: 512),
                    InitialMaxPrice = c.Decimal(nullable: false, precision: 18, scale: 2, defaultValue: 0m),
                    Method = c.Int(nullable: false, defaultValue: 0),
                    PlannedQuarter = c.Int(nullable: false, defaultValue: 1),
                    FundingSource = c.String(maxLength: 128),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ProcurementPlans", t => t.ProcurementPlanId, cascadeDelete: true)
                .Index(t => t.ProcurementPlanId)
                .Index(t => t.Okpd2Code);

            CreateTable(
                "dbo.ProcurementProcedures",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ProcurementPlanItemId = c.Int(nullable: false),
                    EisNoticeNumber = c.String(maxLength: 64),
                    Method = c.Int(nullable: false, defaultValue: 0),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    AnnouncedAt = c.DateTime(),
                    BidsDeadline = c.DateTime(),
                    AwardDecisionAt = c.DateTime(),
                    AwardedSupplierInn = c.String(maxLength: 32),
                    AwardedSupplierName = c.String(maxLength: 512),
                    AwardedPrice = c.Decimal(precision: 18, scale: 2),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ProcurementPlanItems", t => t.ProcurementPlanItemId)
                .Index(t => t.ProcurementPlanItemId)
                .Index(t => t.Status)
                .Index(t => t.AwardedSupplierInn);

            // Поля контракта добавляются на таблицу dbo.Documents (TPH).
            AddColumn("dbo.Documents", "ProcurementProcedureId", c => c.Int());
            AddColumn("dbo.Documents", "SupplierName", c => c.String(maxLength: 512));
            AddColumn("dbo.Documents", "SupplierInn", c => c.String(maxLength: 32));
            AddColumn("dbo.Documents", "SupplierKpp", c => c.String(maxLength: 32));
            AddColumn("dbo.Documents", "ContractAmount", c => c.Decimal(precision: 18, scale: 2));
            AddColumn("dbo.Documents", "FundingSource", c => c.String(maxLength: 128));
            AddColumn("dbo.Documents", "ContractStartDate", c => c.DateTime());
            AddColumn("dbo.Documents", "ContractEndDate", c => c.DateTime());
            AddColumn("dbo.Documents", "ContractStatus", c => c.Int());
            AddColumn("dbo.Documents", "SignedAt", c => c.DateTime());
            AddColumn("dbo.Documents", "ExecutedAt", c => c.DateTime());

            AddForeignKey("dbo.Documents", "ProcurementProcedureId",
                "dbo.ProcurementProcedures", "Id");
            CreateIndex("dbo.Documents", "ProcurementProcedureId");

            CreateTable(
                "dbo.ContractMilestones",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ContractId = c.Int(nullable: false),
                    SequenceNumber = c.Int(nullable: false, defaultValue: 0),
                    Title = c.String(nullable: false, maxLength: 512),
                    PlannedDate = c.DateTime(nullable: false),
                    ActualDate = c.DateTime(),
                    Amount = c.Decimal(nullable: false, precision: 18, scale: 2, defaultValue: 0m),
                    Status = c.Int(nullable: false, defaultValue: 0),
                    DeadlineReminderSentAt = c.DateTime(),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.ContractId, cascadeDelete: true)
                .Index(t => t.ContractId)
                .Index(t => t.Status)
                .Index(t => t.PlannedDate);
        }

        public override void Down()
        {
            DropForeignKey("dbo.ContractMilestones", "ContractId", "dbo.Documents");
            DropIndex("dbo.ContractMilestones", new[] { "PlannedDate" });
            DropIndex("dbo.ContractMilestones", new[] { "Status" });
            DropIndex("dbo.ContractMilestones", new[] { "ContractId" });
            DropTable("dbo.ContractMilestones");

            DropForeignKey("dbo.Documents", "ProcurementProcedureId", "dbo.ProcurementProcedures");
            DropIndex("dbo.Documents", new[] { "ProcurementProcedureId" });
            DropColumn("dbo.Documents", "ExecutedAt");
            DropColumn("dbo.Documents", "SignedAt");
            DropColumn("dbo.Documents", "ContractStatus");
            DropColumn("dbo.Documents", "ContractEndDate");
            DropColumn("dbo.Documents", "ContractStartDate");
            DropColumn("dbo.Documents", "FundingSource");
            DropColumn("dbo.Documents", "ContractAmount");
            DropColumn("dbo.Documents", "SupplierKpp");
            DropColumn("dbo.Documents", "SupplierInn");
            DropColumn("dbo.Documents", "SupplierName");
            DropColumn("dbo.Documents", "ProcurementProcedureId");

            DropForeignKey("dbo.ProcurementProcedures", "ProcurementPlanItemId", "dbo.ProcurementPlanItems");
            DropIndex("dbo.ProcurementProcedures", new[] { "AwardedSupplierInn" });
            DropIndex("dbo.ProcurementProcedures", new[] { "Status" });
            DropIndex("dbo.ProcurementProcedures", new[] { "ProcurementPlanItemId" });
            DropTable("dbo.ProcurementProcedures");

            DropForeignKey("dbo.ProcurementPlanItems", "ProcurementPlanId", "dbo.ProcurementPlans");
            DropIndex("dbo.ProcurementPlanItems", new[] { "Okpd2Code" });
            DropIndex("dbo.ProcurementPlanItems", new[] { "ProcurementPlanId" });
            DropTable("dbo.ProcurementPlanItems");

            DropForeignKey("dbo.ProcurementPlans", "ApprovedByEmployeeId", "dbo.Employees");
            DropIndex("dbo.ProcurementPlans", new[] { "ApprovedByEmployeeId" });
            DropIndex("dbo.ProcurementPlans", new[] { "Status" });
            DropIndex("dbo.ProcurementPlans", new[] { "Year" });
            DropTable("dbo.ProcurementPlans");
        }
    }
}
