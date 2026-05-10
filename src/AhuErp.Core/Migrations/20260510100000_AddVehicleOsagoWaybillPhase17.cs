namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 17 / Improvement #14 — паспортные данные ТС, ОСАГО / ТО,
    /// печать путевого листа.
    /// <list type="bullet">
    ///   <item><description>Расширение <c>Vehicles</c>: <c>VehicleClass</c>,
    ///     <c>Make</c>, <c>Year</c>, <c>Vin</c>, <c>OdometerCurrent</c>,
    ///     <c>NextMaintenanceOdometer</c>, <c>OsagoExpiry</c>,
    ///     <c>TechInspectionExpiry</c>.</description></item>
    /// </list>
    /// Схема создаётся внешним <c>scripts/create-db.sql</c>; миграция документирует
    /// изменения и поддерживает совместимость с EF6 Add-Migration.
    /// </summary>
    public partial class AddVehicleOsagoWaybillPhase17 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Vehicles", "VehicleClass", c => c.Int(nullable: false, defaultValue: 0));
            AddColumn("dbo.Vehicles", "Make", c => c.String(maxLength: 64));
            AddColumn("dbo.Vehicles", "Year", c => c.Int(nullable: false, defaultValue: 0));
            AddColumn("dbo.Vehicles", "Vin", c => c.String(maxLength: 32));
            AddColumn("dbo.Vehicles", "OdometerCurrent", c => c.Int());
            AddColumn("dbo.Vehicles", "NextMaintenanceOdometer", c => c.Int());
            AddColumn("dbo.Vehicles", "OsagoExpiry", c => c.DateTime());
            AddColumn("dbo.Vehicles", "TechInspectionExpiry", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.Vehicles", "TechInspectionExpiry");
            DropColumn("dbo.Vehicles", "OsagoExpiry");
            DropColumn("dbo.Vehicles", "NextMaintenanceOdometer");
            DropColumn("dbo.Vehicles", "OdometerCurrent");
            DropColumn("dbo.Vehicles", "Vin");
            DropColumn("dbo.Vehicles", "Year");
            DropColumn("dbo.Vehicles", "Make");
            DropColumn("dbo.Vehicles", "VehicleClass");
        }
    }
}
