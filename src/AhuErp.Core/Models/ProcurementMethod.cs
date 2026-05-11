namespace AhuErp.Core.Models
{
    /// <summary>
    /// Способ определения поставщика согласно ст. 24 Федерального закона
    /// от 05.04.2013 № 44-ФЗ. Значения сохраняются как int — добавление новых
    /// членов в конец не требует миграции EF6.
    /// </summary>
    public enum ProcurementMethod
    {
        /// <summary>Электронный аукцион (ст. 49 44-ФЗ).</summary>
        ElectronicAuction = 0,

        /// <summary>Электронный конкурс (ст. 48 44-ФЗ).</summary>
        OpenCompetition = 1,

        /// <summary>Запрос котировок в электронной форме (ст. 50 44-ФЗ).</summary>
        QuoteRequest = 2,

        /// <summary>Закупка у единственного поставщика (ст. 93 44-ФЗ).</summary>
        SingleSupplier = 3,

        /// <summary>Закрытый аукцион (ст. 75 44-ФЗ — для гостайны).</summary>
        ClosedAuction = 4,
    }
}
