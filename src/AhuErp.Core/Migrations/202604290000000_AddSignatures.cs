namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 8 — электронная подпись и блокировка документа подписью.
    /// <list type="bullet">
    ///   <item><description>Таблица <c>DocumentSignatures</c> с FK на <c>Documents</c>,
    ///     <c>DocumentAttachments</c> и <c>Employees</c> (cascadeDelete: false).</description></item>
    ///   <item><description>Колонки <c>Documents.IsLocked</c> (bool, default 0) и
    ///     <c>Documents.CurrentVersionAttachmentId</c> (int?, FK на DocumentAttachments).</description></item>
    /// </list>
    /// </summary>
    public partial class AddSignatures : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DocumentSignatures",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    DocumentId = c.Int(nullable: false),
                    AttachmentId = c.Int(),
                    SignerId = c.Int(nullable: false),
                    Kind = c.Int(nullable: false),
                    SignedAt = c.DateTime(nullable: false),
                    SignedHash = c.String(maxLength: 128),
                    SignatureBlobBase64 = c.String(maxLength: 8000),
                    CertificateThumbprint = c.String(maxLength: 512),
                    CertificateSubject = c.String(maxLength: 256),
                    CertificateNotAfter = c.DateTime(),
                    Reason = c.String(maxLength: 1024),
                    IsRevoked = c.Boolean(nullable: false),
                    RevokedAt = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.DocumentId, cascadeDelete: false)
                .ForeignKey("dbo.DocumentAttachments", t => t.AttachmentId, cascadeDelete: false)
                .ForeignKey("dbo.Employees", t => t.SignerId, cascadeDelete: false)
                .Index(t => new { t.DocumentId, t.IsRevoked }, name: "IX_DocumentSignatures_Document_Active")
                .Index(t => t.AttachmentId, name: "IX_DocumentSignatures_Attachment")
                .Index(t => t.SignerId, name: "IX_DocumentSignatures_Signer");

            AddColumn("dbo.Documents", "IsLocked", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("dbo.Documents", "CurrentVersionAttachmentId", c => c.Int());
            CreateIndex("dbo.Documents", "CurrentVersionAttachmentId",
                name: "IX_Documents_CurrentVersionAttachment");
            AddForeignKey("dbo.Documents", "CurrentVersionAttachmentId",
                "dbo.DocumentAttachments", "Id", cascadeDelete: false);
        }

        public override void Down()
        {
            DropForeignKey("dbo.Documents", "CurrentVersionAttachmentId", "dbo.DocumentAttachments");
            DropIndex("dbo.Documents", "IX_Documents_CurrentVersionAttachment");
            DropColumn("dbo.Documents", "CurrentVersionAttachmentId");
            DropColumn("dbo.Documents", "IsLocked");

            DropForeignKey("dbo.DocumentSignatures", "SignerId", "dbo.Employees");
            DropForeignKey("dbo.DocumentSignatures", "AttachmentId", "dbo.DocumentAttachments");
            DropForeignKey("dbo.DocumentSignatures", "DocumentId", "dbo.Documents");
            DropIndex("dbo.DocumentSignatures", "IX_DocumentSignatures_Signer");
            DropIndex("dbo.DocumentSignatures", "IX_DocumentSignatures_Attachment");
            DropIndex("dbo.DocumentSignatures", "IX_DocumentSignatures_Document_Active");
            DropTable("dbo.DocumentSignatures");
        }
    }
}
