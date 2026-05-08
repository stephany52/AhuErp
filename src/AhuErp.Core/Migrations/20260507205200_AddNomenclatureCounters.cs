namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddNomenclatureCounters : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.NomenclatureCounters",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TypeCode = c.String(nullable: false, maxLength: 16),
                        Year = c.Int(nullable: false),
                        LastNumber = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => new { t.TypeCode, t.Year }, unique: true, name: "UX_NomenclatureCounter_TypeCode_Year");
        }

        public override void Down()
        {
            DropIndex("dbo.NomenclatureCounters", "UX_NomenclatureCounter_TypeCode_Year");
            DropTable("dbo.NomenclatureCounters");
        }
    }
}
