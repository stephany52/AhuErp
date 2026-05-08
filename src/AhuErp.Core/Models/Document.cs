using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Базовая сущность документа (РКК — регистрационно-контрольная карточка).
    /// Наследники (например, <see cref="ArchiveRequest"/>, <see cref="ItTicket"/>)
    /// различаются EF6 TPH-дискриминатором <c>DocumentDiscriminator</c>.
    ///
    /// В Phase 7 модель расширена обязательными для муниципального
    /// делопроизводства реквизитами: регистрационный номер, индекс дела,
    /// гриф доступа, направление, корреспондент, краткое содержание.
    /// </summary>
    public class Document
    {
        public int Id { get; set; }

        public DocumentType Type { get; set; }

        public DocumentDirection Direction { get; set; } = DocumentDirection.Internal;

        public DocumentAccessLevel AccessLevel { get; set; } = DocumentAccessLevel.Public;

        /// <summary>
        /// Регистрационный номер по форме шаблона вида документа,
        /// например «АХУ-01/2026-00037». Пуст до момента регистрации
        /// (см. <see cref="Services.INomenclatureService"/>).
        /// </summary>
        [StringLength(64)]
        public string RegistrationNumber { get; set; }

        public DateTime? RegistrationDate { get; set; }

        public static void ValidateRegistrationNumber(string registrationNumber)
        {
            if (!string.IsNullOrEmpty(registrationNumber)
                && (registrationNumber.IndexOf('{') >= 0 || registrationNumber.IndexOf('}') >= 0))
            {
                throw new InvalidOperationException(
                    "Регистрационный номер не может содержать шаблонные плейсхолдеры { или }. ");
            }
        }

        /// <summary>Справочный вид документа (приказ, служебная записка и т.д.).</summary>
        public int? DocumentTypeRefId { get; set; }
        public virtual DocumentTypeRef DocumentTypeRef { get; set; }

        /// <summary>Основное дело номенклатуры.</summary>
        public int? NomenclatureCaseId { get; set; }
        public virtual NomenclatureCase NomenclatureCase { get; set; }

        /// <summary>Автор/регистратор документа.</summary>
        public int? AuthorId { get; set; }
        public virtual Employee Author { get; set; }

        [Required]
        [StringLength(512)]
        public string Title { get; set; }

        /// <summary>Краткое содержание.</summary>
        [StringLength(4000)]
        public string Summary { get; set; }

        /// <summary>Корреспондент (для входящих/исходящих).</summary>
        [StringLength(512)]
        public string Correspondent { get; set; }

        /// <summary>Номер документа корреспондента (для входящих).</summary>
        [StringLength(64)]
        public string IncomingNumber { get; set; }

        public DateTime? IncomingDate { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime Deadline { get; set; }

        public DocumentStatus Status { get; set; }

        public int? AssignedEmployeeId { get; set; }
        public virtual Employee AssignedEmployee { get; set; }

        /// <summary>
        /// Опциональный документ-основание (для дочерних документов:
        /// акт списания, выпущенный по служебной записке, и т.п.).
        /// </summary>
        public int? BasisDocumentId { get; set; }
        public virtual Document BasisDocument { get; set; }

        public ApprovalRouteStatus ApprovalStatus { get; set; } = ApprovalRouteStatus.Draft;

        /// <summary>
        /// Phase 8 — документ заблокирован к редактированию основных полей
        /// после первой Qualified-подписи или при <see cref="DocumentStatus.Completed"/>.
        /// Изменять можно только статус, исполнителя и гриф доступа.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Phase 8 — актуальная подписанная версия PDF/DOCX-вложения,
        /// фиксируется в момент подписания КЭП.
        /// </summary>
        public int? CurrentVersionAttachmentId { get; set; }
        public virtual DocumentAttachment CurrentVersionAttachment { get; set; }

        public virtual ICollection<VehicleTrip> VehicleTrips { get; set; } = new HashSet<VehicleTrip>();

        public virtual ICollection<DocumentAttachment> Attachments { get; set; } = new HashSet<DocumentAttachment>();

        public virtual ICollection<DocumentResolution> Resolutions { get; set; } = new HashSet<DocumentResolution>();

        public virtual ICollection<DocumentTask> Tasks { get; set; } = new HashSet<DocumentTask>();

        public virtual ICollection<DocumentApproval> Approvals { get; set; } = new HashSet<DocumentApproval>();

        public virtual ICollection<DocumentCaseLink> CaseLinks { get; set; } = new HashSet<DocumentCaseLink>();

        /// <summary>
        /// Документ просрочен, если срок истёк, а работа не завершена/не отменена.
        /// </summary>
        public bool IsOverdue(DateTime now)
        {
            return Deadline < now
                   && Status != DocumentStatus.Completed
                   && Status != DocumentStatus.Cancelled;
        }

        /// <summary>Зарегистрирован ли документ (имеет регистрационный номер).</summary>
        public bool IsRegistered => !string.IsNullOrWhiteSpace(RegistrationNumber);
    }
}
