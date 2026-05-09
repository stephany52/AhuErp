namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 15 / Improvement #12 — журналы регистрации.
    /// <list type="bullet">
    ///   <item><description><c>SafetyBriefings</c> — журнал инструктажей по ОТ/ПБ
    ///     (вид инструктажа, инструктируемый, инструктор, тема, подпись).</description></item>
    ///   <item><description><c>Inventarizations</c> + <c>InventarizationDiscrepancies</c>
    ///     — инвентаризационные описи (даты, состав комиссии, расхождения).</description></item>
    ///   <item><description><c>ArchiveTransfers</c> — журнал передачи дел в архив
    ///     (номенклатурное дело, дата, архивист, акт, шифр, срок хранения).</description></item>
    ///   <item><description>Расширения <c>Vehicles</c> / <c>VehicleTrips</c> для
    ///     журнала ГСМ (тип топлива, норма расхода, одометр, маршрут, пассажиры).</description></item>
    /// </list>
    /// Схема создаётся внешним <c>scripts/create-db.sql</c>; миграция документирует
    /// изменения и поддерживает совместимость с EF6 Add-Migration.
    /// </summary>
    public partial class AddRegistrationJournalsPhase15 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SafetyBriefings",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    BriefingDate = c.DateTime(nullable: false),
                    Kind = c.Int(nullable: false),
                    Topic = c.String(nullable: false, maxLength: 256),
                    TraineeEmployeeId = c.Int(nullable: false),
                    InstructorEmployeeId = c.Int(nullable: false),
                    SignatureConfirmed = c.Boolean(nullable: false),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.TraineeEmployeeId, cascadeDelete: false)
                .ForeignKey("dbo.Employees", t => t.InstructorEmployeeId, cascadeDelete: false)
                .Index(t => t.TraineeEmployeeId)
                .Index(t => t.InstructorEmployeeId)
                .Index(t => new { t.BriefingDate, t.Kind }, name: "IX_SafetyBriefings_Date_Kind");

            CreateTable(
                "dbo.Inventarizations",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    StartDate = c.DateTime(nullable: false),
                    EndDate = c.DateTime(),
                    Scope = c.Int(nullable: false),
                    ScopeDescription = c.String(nullable: false, maxLength: 256),
                    CommissionMembers = c.String(maxLength: 2048),
                    ChairmanId = c.Int(),
                    ResultDocumentId = c.Int(),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.ChairmanId, cascadeDelete: false)
                .ForeignKey("dbo.Documents", t => t.ResultDocumentId, cascadeDelete: false)
                .Index(t => t.ChairmanId)
                .Index(t => t.ResultDocumentId)
                .Index(t => new { t.StartDate, t.Scope }, name: "IX_Inventarizations_Date_Scope");

            CreateTable(
                "dbo.InventarizationDiscrepancies",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    InventarizationId = c.Int(nullable: false),
                    ItemName = c.String(nullable: false, maxLength: 256),
                    ExpectedQuantity = c.Decimal(nullable: false, precision: 18, scale: 3),
                    ActualQuantity = c.Decimal(nullable: false, precision: 18, scale: 3),
                    Reason = c.String(maxLength: 512),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Inventarizations", t => t.InventarizationId, cascadeDelete: true)
                .Index(t => t.InventarizationId);

            CreateTable(
                "dbo.ArchiveTransfers",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    NomenclatureCaseId = c.Int(nullable: false),
                    TransferDate = c.DateTime(nullable: false),
                    TransferredById = c.Int(),
                    AcceptedById = c.Int(),
                    ActDocumentId = c.Int(),
                    ArchiveCode = c.String(maxLength: 64),
                    RetentionYears = c.Int(nullable: false),
                    Notes = c.String(maxLength: 2048),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.NomenclatureCases", t => t.NomenclatureCaseId, cascadeDelete: false)
                .ForeignKey("dbo.Employees", t => t.TransferredById, cascadeDelete: false)
                .ForeignKey("dbo.Employees", t => t.AcceptedById, cascadeDelete: false)
                .ForeignKey("dbo.Documents", t => t.ActDocumentId, cascadeDelete: false)
                .Index(t => t.NomenclatureCaseId)
                .Index(t => t.TransferredById)
                .Index(t => t.AcceptedById)
                .Index(t => t.ActDocumentId)
                .Index(t => t.TransferDate, name: "IX_ArchiveTransfers_Date");

            // Vehicle / VehicleTrip — поля журнала ГСМ.
            AddColumn("dbo.Vehicles", "FuelType", c => c.Int(nullable: false));
            AddColumn("dbo.Vehicles", "FuelConsumptionPer100Km", c => c.Decimal(nullable: false, precision: 7, scale: 2));
            AddColumn("dbo.VehicleTrips", "OdometerStart", c => c.Int());
            AddColumn("dbo.VehicleTrips", "OdometerEnd", c => c.Int());
            AddColumn("dbo.VehicleTrips", "FuelIssuedLiters", c => c.Decimal(precision: 9, scale: 2));
            AddColumn("dbo.VehicleTrips", "Route", c => c.String(maxLength: 512));
            AddColumn("dbo.VehicleTrips", "PassengerNames", c => c.String(maxLength: 1024));
            AddColumn("dbo.VehicleTrips", "ActualStart", c => c.DateTime());
            AddColumn("dbo.VehicleTrips", "ActualEnd", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.VehicleTrips", "ActualEnd");
            DropColumn("dbo.VehicleTrips", "ActualStart");
            DropColumn("dbo.VehicleTrips", "PassengerNames");
            DropColumn("dbo.VehicleTrips", "Route");
            DropColumn("dbo.VehicleTrips", "FuelIssuedLiters");
            DropColumn("dbo.VehicleTrips", "OdometerEnd");
            DropColumn("dbo.VehicleTrips", "OdometerStart");
            DropColumn("dbo.Vehicles", "FuelConsumptionPer100Km");
            DropColumn("dbo.Vehicles", "FuelType");

            DropIndex("dbo.ArchiveTransfers", "IX_ArchiveTransfers_Date");
            DropIndex("dbo.ArchiveTransfers", new[] { "ActDocumentId" });
            DropIndex("dbo.ArchiveTransfers", new[] { "AcceptedById" });
            DropIndex("dbo.ArchiveTransfers", new[] { "TransferredById" });
            DropIndex("dbo.ArchiveTransfers", new[] { "NomenclatureCaseId" });
            DropForeignKey("dbo.ArchiveTransfers", "ActDocumentId", "dbo.Documents");
            DropForeignKey("dbo.ArchiveTransfers", "AcceptedById", "dbo.Employees");
            DropForeignKey("dbo.ArchiveTransfers", "TransferredById", "dbo.Employees");
            DropForeignKey("dbo.ArchiveTransfers", "NomenclatureCaseId", "dbo.NomenclatureCases");
            DropTable("dbo.ArchiveTransfers");

            DropIndex("dbo.InventarizationDiscrepancies", new[] { "InventarizationId" });
            DropForeignKey("dbo.InventarizationDiscrepancies", "InventarizationId", "dbo.Inventarizations");
            DropTable("dbo.InventarizationDiscrepancies");

            DropIndex("dbo.Inventarizations", "IX_Inventarizations_Date_Scope");
            DropIndex("dbo.Inventarizations", new[] { "ResultDocumentId" });
            DropIndex("dbo.Inventarizations", new[] { "ChairmanId" });
            DropForeignKey("dbo.Inventarizations", "ResultDocumentId", "dbo.Documents");
            DropForeignKey("dbo.Inventarizations", "ChairmanId", "dbo.Employees");
            DropTable("dbo.Inventarizations");

            DropIndex("dbo.SafetyBriefings", "IX_SafetyBriefings_Date_Kind");
            DropIndex("dbo.SafetyBriefings", new[] { "InstructorEmployeeId" });
            DropIndex("dbo.SafetyBriefings", new[] { "TraineeEmployeeId" });
            DropForeignKey("dbo.SafetyBriefings", "InstructorEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.SafetyBriefings", "TraineeEmployeeId", "dbo.Employees");
            DropTable("dbo.SafetyBriefings");
        }
    }
}
