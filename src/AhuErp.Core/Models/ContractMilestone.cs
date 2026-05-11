using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Этап исполнения контракта. Контракт может содержать один и более
    /// этапов с плановой и фактической датой исполнения и суммой.
    /// </summary>
    public class ContractMilestone
    {
        public int Id { get; set; }

        public int ContractId { get; set; }
        public virtual Contract Contract { get; set; }

        public int SequenceNumber { get; set; }

        [Required]
        [StringLength(512)]
        public string Title { get; set; }

        public DateTime PlannedDate { get; set; }

        public DateTime? ActualDate { get; set; }

        public decimal Amount { get; set; }

        public ContractMilestoneStatus Status { get; set; } = ContractMilestoneStatus.Planned;

        /// <summary>Дата отправки уведомления о приближении срока этапа
        /// (idempotency-маркер для <see cref="Services.ProcurementService"/>).</summary>
        public DateTime? DeadlineReminderSentAt { get; set; }

        [StringLength(2048)]
        public string Notes { get; set; }
    }
}
