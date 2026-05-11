using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Муниципальный контракт по 44-ФЗ. Наследует <see cref="Document"/>
    /// (TPH-дискриминатор <c>Contract</c>), что даёт стандартные плюсы:
    /// номенклатура, регистрационный номер по форме, согласование,
    /// КЭП-подпись и поиск. Дополнительные реквизиты — поставщик, сумма,
    /// срок, источник финансирования — хранятся в столбцах контракта.
    /// Контроль исполнения ведётся по этапам <see cref="ContractMilestone"/>.
    /// </summary>
    public class Contract : Document
    {
        public Contract()
        {
            Type = DocumentType.Office;
        }

        /// <summary>Привязка к процедуре закупки. Опциональна — для контрактов,
        /// заключённых вне 44-ФЗ (например, договоры на иные основания).</summary>
        public int? ProcurementProcedureId { get; set; }
        public virtual ProcurementProcedure ProcurementProcedure { get; set; }

        /// <summary>Наименование поставщика (контрагента).</summary>
        [StringLength(512)]
        public string SupplierName { get; set; }

        /// <summary>ИНН поставщика.</summary>
        [StringLength(32)]
        public string SupplierInn { get; set; }

        /// <summary>КПП поставщика.</summary>
        [StringLength(32)]
        public string SupplierKpp { get; set; }

        /// <summary>Цена контракта, ₽.</summary>
        public decimal ContractAmount { get; set; }

        /// <summary>Источник финансирования (КБК).</summary>
        [StringLength(128)]
        public string FundingSource { get; set; }

        public DateTime? ContractStartDate { get; set; }

        public DateTime? ContractEndDate { get; set; }

        public ContractStatus ContractStatus { get; set; } = ContractStatus.Draft;

        public DateTime? SignedAt { get; set; }

        public DateTime? ExecutedAt { get; set; }

        public virtual ICollection<ContractMilestone> Milestones { get; set; }
            = new HashSet<ContractMilestone>();
    }
}
