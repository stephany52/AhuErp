namespace AhuErp.Core.Models
{
    /// <summary>
    /// Жизненный цикл муниципального контракта 44-ФЗ. Хранится в
    /// <see cref="Contract"/> поверх стандартного <see cref="DocumentStatus"/>,
    /// чтобы отделить «состояние документа» от «состояния контракта»: документ
    /// может быть подписан и заархивирован, а контракт ещё не исполнен.
    /// </summary>
    public enum ContractStatus
    {
        Draft = 0,
        Signed = 1,
        InExecution = 2,
        Executed = 3,
        Terminated = 4,
        Cancelled = 5,
    }
}
