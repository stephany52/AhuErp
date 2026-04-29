namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 10 — полнотекстовый поиск по содержимому вложений
    /// и сохранённые фильтры (<see cref="Models.AttachmentTextIndex"/>,
    /// <see cref="Models.SavedSearch"/>).
    /// </summary>
    public partial class AddSearchIndex : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AttachmentTextIndices",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    AttachmentId = c.Int(nullable: false),
                    DocumentId = c.Int(nullable: false),
                    ExtractedText = c.String(),
                    IndexedAt = c.DateTime(nullable: false),
                    SourceContentHash = c.String(maxLength: 64),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DocumentAttachments", t => t.AttachmentId, cascadeDelete: true)
                .Index(t => t.AttachmentId, unique: true, name: "IX_AttachmentTextIndices_Attachment")
                .Index(t => t.DocumentId, name: "IX_AttachmentTextIndices_Document");

            CreateTable(
                "dbo.SavedSearches",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    OwnerId = c.Int(nullable: false),
                    Name = c.String(nullable: false, maxLength: 128),
                    FilterJson = c.String(),
                    IsShared = c.Boolean(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.OwnerId, cascadeDelete: false)
                .Index(t => t.OwnerId, name: "IX_SavedSearches_Owner");

            // Опциональный SQL Server FULLTEXT каталог: создаётся, если поддерживается.
            // На LocalDB / SQL Server Express без FT-сервиса этот блок завершится
            // ошибкой — обернём в TRY/CATCH, чтобы миграция не падала.
            Sql(@"
BEGIN TRY
    IF SERVERPROPERTY('IsFullTextInstalled') = 1
        AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'AhuErpFt')
    BEGIN
        EXEC('CREATE FULLTEXT CATALOG AhuErpFt');
        EXEC('CREATE FULLTEXT INDEX ON dbo.AttachmentTextIndices(ExtractedText)
              KEY INDEX [PK_dbo.AttachmentTextIndices] ON AhuErpFt');
    END
END TRY
BEGIN CATCH
    -- Full-text каталог недоступен — поиск идёт через LIKE/in-process.
END CATCH
");
        }

        public override void Down()
        {
            Sql(@"
BEGIN TRY
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes fi
               JOIN sys.objects o ON fi.object_id = o.object_id
               WHERE o.name = N'AttachmentTextIndices')
        EXEC('DROP FULLTEXT INDEX ON dbo.AttachmentTextIndices');
    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'AhuErpFt')
        EXEC('DROP FULLTEXT CATALOG AhuErpFt');
END TRY
BEGIN CATCH
END CATCH
");

            DropForeignKey("dbo.SavedSearches", "OwnerId", "dbo.Employees");
            DropForeignKey("dbo.AttachmentTextIndices", "AttachmentId", "dbo.DocumentAttachments");
            DropIndex("dbo.SavedSearches", "IX_SavedSearches_Owner");
            DropIndex("dbo.AttachmentTextIndices", "IX_AttachmentTextIndices_Document");
            DropIndex("dbo.AttachmentTextIndices", "IX_AttachmentTextIndices_Attachment");
            DropTable("dbo.SavedSearches");
            DropTable("dbo.AttachmentTextIndices");
        }
    }
}
