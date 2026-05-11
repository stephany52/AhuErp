using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Data
{
    /// <summary>
    /// Корневой EF6 Code-First контекст системы АХУ.
    /// TPH-иерархия документов: <see cref="Document"/> → <see cref="ArchiveRequest"/>,
    /// <see cref="ItTicket"/>. Дискриминатор хранится в столбце
    /// <c>DocumentDiscriminator</c> (исторически назывался <c>DocumentKind</c>;
    /// миграция Phase 7 переименовала колонку, чтобы освободить имя <c>DocumentKind</c>
    /// для возможного будущего использования и подчеркнуть техническую природу TPH).
    /// </summary>
    public class AhuDbContext : DbContext
    {
        static AhuDbContext()
        {
            // Схема создаётся внешним скриптом scripts/create-db.sql, поэтому отключаем
            // встроенные инициализаторы EF6 (CreateDatabaseIfNotExists / DropCreate*),
            // чтобы не плодить параллельный механизм миграций и не падать на пустой
            // _MigrationHistory при первом подключении.
            Database.SetInitializer<AhuDbContext>(null);
        }

        public AhuDbContext()
            : base("name=AhuErpDb")
        {
        }

        public AhuDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }

        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Department> Departments { get; set; }
        public virtual DbSet<Document> Documents { get; set; }
        public virtual DbSet<ArchiveRequest> ArchiveRequests { get; set; }
        public virtual DbSet<ItTicket> ItTickets { get; set; }
        public virtual DbSet<Vehicle> Vehicles { get; set; }
        public virtual DbSet<VehicleTrip> VehicleTrips { get; set; }
        public virtual DbSet<InventoryItem> InventoryItems { get; set; }
        public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        public virtual DbSet<DocumentTypeRef> DocumentTypeRefs { get; set; }
        public virtual DbSet<NomenclatureCase> NomenclatureCases { get; set; }
        public virtual DbSet<NomenclatureCounter> NomenclatureCounters { get; set; }
        public virtual DbSet<DocumentCaseLink> DocumentCaseLinks { get; set; }
        public virtual DbSet<DocumentAttachment> DocumentAttachments { get; set; }
        public virtual DbSet<DocumentResolution> DocumentResolutions { get; set; }
        public virtual DbSet<DocumentTask> DocumentTasks { get; set; }
        public virtual DbSet<ApprovalRouteTemplate> ApprovalRouteTemplates { get; set; }
        public virtual DbSet<ApprovalStage> ApprovalStages { get; set; }
        public virtual DbSet<DocumentApproval> DocumentApprovals { get; set; }
        public virtual DbSet<DocumentSignature> DocumentSignatures { get; set; }
        public virtual DbSet<AuditLog> AuditLogs { get; set; }
        public virtual DbSet<AttachmentTextIndex> AttachmentTextIndices { get; set; }
        public virtual DbSet<SavedSearch> SavedSearches { get; set; }

        // Phase 11 — оргструктура и замещения.
        public virtual DbSet<Substitution> Substitutions { get; set; }
        public virtual DbSet<TaskDelegation> TaskDelegations { get; set; }

        // Phase 9 — уведомления и пользовательские предпочтения.
        public virtual DbSet<Notification> Notifications { get; set; }
        public virtual DbSet<NotificationPreference> NotificationPreferences { get; set; }

        // Phase 14 / Improvement #10 — каталог оборудования и журналы ИТО.
        public virtual DbSet<Equipment> Equipment { get; set; }
        public virtual DbSet<NetworkSegment> NetworkSegments { get; set; }
        public virtual DbSet<VideoConference> VideoConferences { get; set; }
        public virtual DbSet<ItTicketDiagnosticEntry> ItTicketDiagnosticEntries { get; set; }

        // Phase 15 / Improvement #12 — журналы регистрации (ОТ/ПБ, инвентаризации, передача в архив).
        public virtual DbSet<SafetyBriefing> SafetyBriefings { get; set; }
        public virtual DbSet<Inventarization> Inventarizations { get; set; }
        public virtual DbSet<InventarizationDiscrepancy> InventarizationDiscrepancies { get; set; }
        public virtual DbSet<ArchiveTransfer> ArchiveTransfers { get; set; }

        // Phase 16 / Improvement #17 + Bug #8 — журнал входов, история паролей,
        // настройки учреждения (singleton с ключом шифрования и параметрами политики).
        public virtual DbSet<LoginAttempt> LoginAttempts { get; set; }
        public virtual DbSet<EmployeePasswordHistory> EmployeePasswordHistories { get; set; }
        public virtual DbSet<OrganizationSettings> OrganizationSettings { get; set; }

        // Phase 18 / Improvement #15 — эксплуатация зданий и реестр основных средств.
        public virtual DbSet<Building> Buildings { get; set; }
        public virtual DbSet<Room> Rooms { get; set; }
        public virtual DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public virtual DbSet<FixedAsset> FixedAssets { get; set; }

        // Phase 19 / Improvement #16 — архив: акты о выделении к уничтожению.
        public virtual DbSet<DestructionAct> DestructionActs { get; set; }
        public virtual DbSet<DestructionActItem> DestructionActItems { get; set; }

        // Phase 20 / Improvement #13 — закупки 44-ФЗ: план-график, процедуры,
        // контракты и этапы исполнения.
        public virtual DbSet<ProcurementPlan> ProcurementPlans { get; set; }
        public virtual DbSet<ProcurementPlanItem> ProcurementPlanItems { get; set; }
        public virtual DbSet<ProcurementProcedure> ProcurementProcedures { get; set; }
        public virtual DbSet<Contract> Contracts { get; set; }
        public virtual DbSet<ContractMilestone> ContractMilestones { get; set; }

        public override int SaveChanges()
        {
            ValidateDocumentRegistrationNumbers();
            return base.SaveChanges();
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync()
        {
            ValidateDocumentRegistrationNumbers();
            return base.SaveChangesAsync();
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken)
        {
            ValidateDocumentRegistrationNumbers();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ValidateDocumentRegistrationNumbers()
        {
            foreach (var entry in ChangeTracker.Entries<Document>()
                         .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                Document.ValidateRegistrationNumber(entry.Entity.RegistrationNumber);
            }
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<Employee>().ToTable("Employees");
            modelBuilder.Entity<Department>().ToTable("Departments");

            modelBuilder.Entity<Document>()
                .Map<Document>(m =>
                {
                    m.Requires("DocumentDiscriminator").HasValue("Document");
                    m.ToTable("Documents");
                })
                .Map<ArchiveRequest>(m =>
                {
                    m.Requires("DocumentDiscriminator").HasValue("ArchiveRequest");
                    m.ToTable("Documents");
                })
                .Map<ItTicket>(m =>
                {
                    m.Requires("DocumentDiscriminator").HasValue("ItTicket");
                    m.ToTable("Documents");
                })
                .Map<Contract>(m =>
                {
                    m.Requires("DocumentDiscriminator").HasValue("Contract");
                    m.ToTable("Documents");
                });

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.AssignedEmployee)
                .WithMany(e => e.AssignedDocuments)
                .HasForeignKey(d => d.AssignedEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.Author)
                .WithMany()
                .HasForeignKey(d => d.AuthorId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.DocumentTypeRef)
                .WithMany(t => t.Documents)
                .HasForeignKey(d => d.DocumentTypeRefId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.NomenclatureCase)
                .WithMany()
                .HasForeignKey(d => d.NomenclatureCaseId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.BasisDocument)
                .WithMany()
                .HasForeignKey(d => d.BasisDocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ArchiveRequest>()
                .Property(r => r.RequestKind)
                .HasColumnName("ArchiveRequestKind");

            modelBuilder.Entity<Vehicle>().ToTable("Vehicles");
            modelBuilder.Entity<VehicleTrip>().ToTable("VehicleTrips");

            modelBuilder.Entity<VehicleTrip>()
                .HasRequired(t => t.Vehicle)
                .WithMany(v => v.Trips)
                .HasForeignKey(t => t.VehicleId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<VehicleTrip>()
                .HasOptional(t => t.Document)
                .WithMany(d => d.VehicleTrips)
                .HasForeignKey(t => t.DocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<VehicleTrip>()
                .HasOptional(t => t.BasisDocument)
                .WithMany()
                .HasForeignKey(t => t.BasisDocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryItem>().ToTable("InventoryItems");
            modelBuilder.Entity<InventoryTransaction>().ToTable("InventoryTransactions");

            modelBuilder.Entity<InventoryTransaction>()
                .HasRequired(t => t.InventoryItem)
                .WithMany(i => i.Transactions)
                .HasForeignKey(t => t.InventoryItemId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOptional(t => t.Document)
                .WithMany()
                .HasForeignKey(t => t.DocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOptional(t => t.BasisDocument)
                .WithMany()
                .HasForeignKey(t => t.BasisDocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryTransaction>()
                .HasRequired(t => t.Initiator)
                .WithMany()
                .HasForeignKey(t => t.InitiatorId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentTypeRef>().ToTable("DocumentTypeRefs");

            modelBuilder.Entity<NomenclatureCase>().ToTable("NomenclatureCases");

            modelBuilder.Entity<NomenclatureCounter>().ToTable("NomenclatureCounters");
            modelBuilder.Entity<NomenclatureCounter>()
                .Property(c => c.TypeCode)
                .IsRequired()
                .HasMaxLength(16);

            modelBuilder.Entity<NomenclatureCase>()
                .HasOptional(n => n.Department)
                .WithMany(d => d.NomenclatureCases)
                .HasForeignKey(n => n.DepartmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentCaseLink>().ToTable("DocumentCaseLinks");
            modelBuilder.Entity<DocumentCaseLink>()
                .HasRequired(l => l.Document)
                .WithMany(d => d.CaseLinks)
                .HasForeignKey(l => l.DocumentId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<DocumentCaseLink>()
                .HasRequired(l => l.NomenclatureCase)
                .WithMany(n => n.DocumentLinks)
                .HasForeignKey(l => l.NomenclatureCaseId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentCaseLink>()
                .HasOptional(l => l.LinkedBy)
                .WithMany()
                .HasForeignKey(l => l.LinkedById)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentAttachment>().ToTable("DocumentAttachments");
            modelBuilder.Entity<DocumentAttachment>()
                .HasRequired(a => a.Document)
                .WithMany(d => d.Attachments)
                .HasForeignKey(a => a.DocumentId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<DocumentAttachment>()
                .HasRequired(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentResolution>().ToTable("DocumentResolutions");
            modelBuilder.Entity<DocumentResolution>()
                .HasRequired(r => r.Document)
                .WithMany(d => d.Resolutions)
                .HasForeignKey(r => r.DocumentId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<DocumentResolution>()
                .HasRequired(r => r.Author)
                .WithMany()
                .HasForeignKey(r => r.AuthorId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentTask>().ToTable("DocumentTasks");
            modelBuilder.Entity<DocumentTask>()
                .HasRequired(t => t.Document)
                .WithMany(d => d.Tasks)
                .HasForeignKey(t => t.DocumentId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentTask>()
                .HasOptional(t => t.Resolution)
                .WithMany(r => r.Tasks)
                .HasForeignKey(t => t.ResolutionId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentTask>()
                .HasOptional(t => t.ParentTask)
                .WithMany(t => t.ChildTasks)
                .HasForeignKey(t => t.ParentTaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentTask>()
                .HasRequired(t => t.Author)
                .WithMany()
                .HasForeignKey(t => t.AuthorId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentTask>()
                .HasRequired(t => t.Executor)
                .WithMany()
                .HasForeignKey(t => t.ExecutorId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentTask>()
                .HasOptional(t => t.Controller)
                .WithMany()
                .HasForeignKey(t => t.ControllerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ApprovalRouteTemplate>().ToTable("ApprovalRouteTemplates");
            modelBuilder.Entity<ApprovalRouteTemplate>()
                .HasOptional(t => t.DocumentTypeRef)
                .WithMany()
                .HasForeignKey(t => t.DocumentTypeRefId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ApprovalStage>().ToTable("ApprovalStages");
            modelBuilder.Entity<ApprovalStage>()
                .HasRequired(s => s.RouteTemplate)
                .WithMany(t => t.Stages)
                .HasForeignKey(s => s.RouteTemplateId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<ApprovalStage>()
                .HasOptional(s => s.ApproverEmployee)
                .WithMany()
                .HasForeignKey(s => s.ApproverEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentApproval>().ToTable("DocumentApprovals");
            modelBuilder.Entity<DocumentApproval>()
                .HasRequired(a => a.Document)
                .WithMany(d => d.Approvals)
                .HasForeignKey(a => a.DocumentId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<DocumentApproval>()
                .HasOptional(a => a.Stage)
                .WithMany()
                .HasForeignKey(a => a.StageId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentApproval>()
                .HasRequired(a => a.Approver)
                .WithMany()
                .HasForeignKey(a => a.ApproverId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DocumentSignature>().ToTable("DocumentSignatures");
            modelBuilder.Entity<DocumentSignature>()
                .HasRequired(s => s.Document)
                .WithMany()
                .HasForeignKey(s => s.DocumentId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentSignature>()
                .HasOptional(s => s.Attachment)
                .WithMany()
                .HasForeignKey(s => s.AttachmentId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DocumentSignature>()
                .HasRequired(s => s.Signer)
                .WithMany()
                .HasForeignKey(s => s.SignerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.CurrentVersionAttachment)
                .WithMany()
                .HasForeignKey(d => d.CurrentVersionAttachmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
            modelBuilder.Entity<AuditLog>()
                .HasOptional(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .WillCascadeOnDelete(false);

            // ---- Phase 11: иерархия отделов + замещения + делегирования. ----
            modelBuilder.Entity<Department>()
                .HasOptional(d => d.ParentDepartment)
                .WithMany(d => d.ChildDepartments)
                .HasForeignKey(d => d.ParentDepartmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Department>()
                .HasOptional(d => d.HeadEmployee)
                .WithMany()
                .HasForeignKey(d => d.HeadEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Employee>()
                .HasOptional(e => e.Department)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Substitution>().ToTable("Substitutions");
            modelBuilder.Entity<Substitution>()
                .HasRequired(s => s.OriginalEmployee)
                .WithMany()
                .HasForeignKey(s => s.OriginalEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Substitution>()
                .HasRequired(s => s.SubstituteEmployee)
                .WithMany()
                .HasForeignKey(s => s.SubstituteEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskDelegation>().ToTable("TaskDelegations");
            modelBuilder.Entity<TaskDelegation>()
                .HasRequired(d => d.Task)
                .WithMany()
                .HasForeignKey(d => d.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskDelegation>()
                .HasRequired(d => d.FromEmployee)
                .WithMany()
                .HasForeignKey(d => d.FromEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskDelegation>()
                .HasRequired(d => d.ToEmployee)
                .WithMany()
                .HasForeignKey(d => d.ToEmployeeId)
                .WillCascadeOnDelete(false);

            // Phase 9 — Notifications.
            modelBuilder.Entity<Notification>().ToTable("Notifications");
            modelBuilder.Entity<Notification>()
                .Ignore(n => n.IsRead);
            modelBuilder.Entity<Notification>()
                .HasRequired(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<NotificationPreference>().ToTable("NotificationPreferences");
            modelBuilder.Entity<NotificationPreference>()
                .HasRequired(p => p.Employee)
                .WithMany()
                .HasForeignKey(p => p.EmployeeId)
                .WillCascadeOnDelete(false);

            // Phase 10 — полнотекстовый индекс и сохранённые поиски.
            modelBuilder.Entity<AttachmentTextIndex>().ToTable("AttachmentTextIndices");
            modelBuilder.Entity<AttachmentTextIndex>()
                .Property(x => x.ExtractedText)
                .HasColumnType("nvarchar(max)");
            // Каскадное удаление: Document → DocumentAttachment (cascade) →
            // AttachmentTextIndex (cascade). Иначе FK на индексе блокирует
            // удаление документа с проиндексированными вложениями.
            modelBuilder.Entity<AttachmentTextIndex>()
                .HasRequired(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<SavedSearch>().ToTable("SavedSearches");
            modelBuilder.Entity<SavedSearch>()
                .Property(x => x.FilterJson)
                .HasColumnType("nvarchar(max)");
            modelBuilder.Entity<SavedSearch>()
                .HasRequired(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .WillCascadeOnDelete(false);

            // Phase 14 — каталог оборудования и связанные сущности ИТО.
            modelBuilder.Entity<NetworkSegment>().ToTable("NetworkSegments");

            modelBuilder.Entity<Equipment>().ToTable("Equipment");
            modelBuilder.Entity<Equipment>()
                .HasOptional(e => e.ResponsibleEmployee)
                .WithMany()
                .HasForeignKey(e => e.ResponsibleEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Equipment>()
                .HasOptional(e => e.NetworkSegment)
                .WithMany(s => s.Equipment)
                .HasForeignKey(e => e.NetworkSegmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ItTicket>()
                .HasOptional(t => t.AffectedEquipmentRef)
                .WithMany(e => e.Tickets)
                .HasForeignKey(t => t.AffectedEquipmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ItTicketDiagnosticEntry>().ToTable("ItTicketDiagnosticEntries");
            modelBuilder.Entity<ItTicketDiagnosticEntry>()
                .HasRequired(d => d.Ticket)
                .WithMany(t => t.DiagnosticEntries)
                .HasForeignKey(d => d.TicketId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<ItTicketDiagnosticEntry>()
                .HasRequired(d => d.Author)
                .WithMany()
                .HasForeignKey(d => d.AuthorId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<VideoConference>().ToTable("VideoConferences");
            modelBuilder.Entity<VideoConference>()
                .HasOptional(v => v.Ticket)
                .WithMany()
                .HasForeignKey(v => v.TicketId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<VideoConference>()
                .HasRequired(v => v.Organizer)
                .WithMany()
                .HasForeignKey(v => v.OrganizerId)
                .WillCascadeOnDelete(false);

            // ---- Phase 15 / Improvement #12 — журналы регистрации. ----

            modelBuilder.Entity<SafetyBriefing>().ToTable("SafetyBriefings");
            modelBuilder.Entity<SafetyBriefing>()
                .HasRequired(b => b.TraineeEmployee)
                .WithMany()
                .HasForeignKey(b => b.TraineeEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<SafetyBriefing>()
                .HasRequired(b => b.InstructorEmployee)
                .WithMany()
                .HasForeignKey(b => b.InstructorEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Inventarization>().ToTable("Inventarizations");
            modelBuilder.Entity<Inventarization>()
                .HasOptional(i => i.Chairman)
                .WithMany()
                .HasForeignKey(i => i.ChairmanId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Inventarization>()
                .HasOptional(i => i.ResultDocument)
                .WithMany()
                .HasForeignKey(i => i.ResultDocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventarizationDiscrepancy>().ToTable("InventarizationDiscrepancies");
            modelBuilder.Entity<InventarizationDiscrepancy>()
                .Ignore(d => d.Delta);
            modelBuilder.Entity<InventarizationDiscrepancy>()
                .Property(d => d.ExpectedQuantity)
                .HasPrecision(18, 3);
            modelBuilder.Entity<InventarizationDiscrepancy>()
                .Property(d => d.ActualQuantity)
                .HasPrecision(18, 3);
            modelBuilder.Entity<InventarizationDiscrepancy>()
                .HasRequired(d => d.Inventarization)
                .WithMany(i => i.Discrepancies)
                .HasForeignKey(d => d.InventarizationId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ArchiveTransfer>().ToTable("ArchiveTransfers");
            modelBuilder.Entity<ArchiveTransfer>()
                .HasRequired(t => t.NomenclatureCase)
                .WithMany()
                .HasForeignKey(t => t.NomenclatureCaseId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ArchiveTransfer>()
                .HasOptional(t => t.TransferredBy)
                .WithMany()
                .HasForeignKey(t => t.TransferredById)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ArchiveTransfer>()
                .HasOptional(t => t.AcceptedBy)
                .WithMany()
                .HasForeignKey(t => t.AcceptedById)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ArchiveTransfer>()
                .HasOptional(t => t.ActDocument)
                .WithMany()
                .HasForeignKey(t => t.ActDocumentId)
                .WillCascadeOnDelete(false);

            // Расширения автопарка для журнала ГСМ.
            modelBuilder.Entity<Vehicle>()
                .Property(v => v.FuelConsumptionPer100Km)
                .HasPrecision(7, 2);
            modelBuilder.Entity<VehicleTrip>()
                .Property(t => t.FuelIssuedLiters)
                .HasPrecision(9, 2);
            modelBuilder.Entity<VehicleTrip>()
                .Ignore(t => t.FuelUsedLiters);
            modelBuilder.Entity<VehicleTrip>()
                .Ignore(t => t.DistanceKm);

            // Phase 16 / Improvement #17 + Bug #8 — журнал входов, история паролей, настройки учреждения.
            modelBuilder.Entity<LoginAttempt>().ToTable("LoginAttempts");
            modelBuilder.Entity<LoginAttempt>()
                .HasOptional(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<EmployeePasswordHistory>().ToTable("EmployeePasswordHistories");
            modelBuilder.Entity<EmployeePasswordHistory>()
                .HasRequired(h => h.Employee)
                .WithMany(e => e.PasswordHistory)
                .HasForeignKey(h => h.EmployeeId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<OrganizationSettings>().ToTable("OrganizationSettings");
            modelBuilder.Entity<OrganizationSettings>()
                .Property(s => s.Id)
                .HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None);

            // ---- Phase 18 / Improvement #15 — здания, помещения, заявки, основные средства. ----
            modelBuilder.Entity<Building>().ToTable("Buildings");
            modelBuilder.Entity<Building>()
                .Property(b => b.TotalAreaSqm)
                .HasPrecision(10, 2);
            modelBuilder.Entity<Building>()
                .HasOptional(b => b.ResponsibleEmployee)
                .WithMany()
                .HasForeignKey(b => b.ResponsibleEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Room>().ToTable("Rooms");
            modelBuilder.Entity<Room>()
                .Property(r => r.AreaSqm)
                .HasPrecision(10, 2);
            modelBuilder.Entity<Room>()
                .HasRequired(r => r.Building)
                .WithMany(b => b.Rooms)
                .HasForeignKey(r => r.BuildingId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<Room>()
                .HasOptional(r => r.ResponsibleEmployee)
                .WithMany()
                .HasForeignKey(r => r.ResponsibleEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MaintenanceRequest>().ToTable("MaintenanceRequests");
            modelBuilder.Entity<MaintenanceRequest>()
                .HasRequired(r => r.Building)
                .WithMany()
                .HasForeignKey(r => r.BuildingId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<MaintenanceRequest>()
                .HasOptional(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<MaintenanceRequest>()
                .HasRequired(r => r.RequesterEmployee)
                .WithMany()
                .HasForeignKey(r => r.RequesterEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<MaintenanceRequest>()
                .HasOptional(r => r.AssigneeEmployee)
                .WithMany()
                .HasForeignKey(r => r.AssigneeEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<MaintenanceRequest>()
                .HasOptional(r => r.LinkedDocument)
                .WithMany()
                .HasForeignKey(r => r.LinkedDocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<FixedAsset>().ToTable("FixedAssets");
            modelBuilder.Entity<FixedAsset>()
                .Property(a => a.AcquisitionCost)
                .HasPrecision(18, 2);
            modelBuilder.Entity<FixedAsset>()
                .Property(a => a.BookValue)
                .HasPrecision(18, 2);
            modelBuilder.Entity<FixedAsset>()
                .HasOptional(a => a.Building)
                .WithMany()
                .HasForeignKey(a => a.BuildingId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<FixedAsset>()
                .HasOptional(a => a.Room)
                .WithMany()
                .HasForeignKey(a => a.RoomId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<FixedAsset>()
                .HasOptional(a => a.ResponsibleEmployee)
                .WithMany()
                .HasForeignKey(a => a.ResponsibleEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<FixedAsset>()
                .HasOptional(a => a.DecommissionDocument)
                .WithMany()
                .HasForeignKey(a => a.DecommissionDocumentId)
                .WillCascadeOnDelete(false);

            // ---- Phase 19 / Improvement #16 — архивные акты о выделении к уничтожению. ----
            modelBuilder.Entity<DestructionAct>().ToTable("DestructionActs");
            modelBuilder.Entity<DestructionAct>()
                .HasRequired(a => a.DraftedByEmployee)
                .WithMany()
                .HasForeignKey(a => a.DraftedByEmployeeId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<DestructionAct>()
                .HasOptional(a => a.ApprovedByEmployee)
                .WithMany()
                .HasForeignKey(a => a.ApprovedByEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<DestructionActItem>().ToTable("DestructionActItems");
            modelBuilder.Entity<DestructionActItem>()
                .HasRequired(i => i.DestructionAct)
                .WithMany(a => a.Items)
                .HasForeignKey(i => i.DestructionActId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<DestructionActItem>()
                .HasOptional(i => i.NomenclatureCase)
                .WithMany()
                .HasForeignKey(i => i.NomenclatureCaseId)
                .WillCascadeOnDelete(false);

            // ---- Phase 20 / Improvement #13 — закупки 44-ФЗ. ----
            modelBuilder.Entity<ProcurementPlan>().ToTable("ProcurementPlans");
            modelBuilder.Entity<ProcurementPlan>()
                .HasOptional(p => p.ApprovedByEmployee)
                .WithMany()
                .HasForeignKey(p => p.ApprovedByEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ProcurementPlanItem>().ToTable("ProcurementPlanItems");
            modelBuilder.Entity<ProcurementPlanItem>()
                .HasRequired(i => i.ProcurementPlan)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.ProcurementPlanId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<ProcurementPlanItem>()
                .Property(i => i.InitialMaxPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ProcurementProcedure>().ToTable("ProcurementProcedures");
            modelBuilder.Entity<ProcurementProcedure>()
                .HasRequired(p => p.ProcurementPlanItem)
                .WithMany()
                .HasForeignKey(p => p.ProcurementPlanItemId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ProcurementProcedure>()
                .Property(p => p.AwardedPrice)
                .HasPrecision(18, 2);

            // Contract уже отображается на dbo.Documents через TPH (см. выше).
            // Дополнительные FK / точности.
            modelBuilder.Entity<Contract>()
                .HasOptional(c => c.ProcurementProcedure)
                .WithMany()
                .HasForeignKey(c => c.ProcurementProcedureId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Contract>()
                .Property(c => c.ContractAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ContractMilestone>().ToTable("ContractMilestones");
            modelBuilder.Entity<ContractMilestone>()
                .HasRequired(m => m.Contract)
                .WithMany(c => c.Milestones)
                .HasForeignKey(m => m.ContractId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<ContractMilestone>()
                .Property(m => m.Amount)
                .HasPrecision(18, 2);

            base.OnModelCreating(modelBuilder);
        }
    }
}
