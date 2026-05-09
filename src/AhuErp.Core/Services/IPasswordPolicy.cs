using System.Collections.Generic;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — парольная политика учреждения.
    /// Конкретные значения (мин длина / срок действия / глубина истории)
    /// читаются из <see cref="Models.OrganizationSettings"/>; реализация
    /// проверяет их и принимает решение «допустимо ли это значение пароля».
    /// </summary>
    public interface IPasswordPolicy
    {
        /// <summary>
        /// Проверяет качество предлагаемого пароля по правилам политики:
        /// длина, наличие заглавной/строчной буквы и цифры. История паролей
        /// проверяется отдельным методом <see cref="ValidateAgainstHistory"/>,
        /// потому что для проверки нужны хэши, а не открытый пароль.
        /// </summary>
        /// <returns>
        /// Список нарушенных правил (в человекочитаемом виде, для UI).
        /// Пустой список = пароль удовлетворяет политике.
        /// </returns>
        IReadOnlyList<string> ValidateStrength(string password);

        /// <summary>
        /// Проверяет, что предлагаемый пароль не совпадает ни с одним из
        /// последних N (по умолчанию 5) хэшей. <paramref name="hasher"/>
        /// должен соответствовать тому, которым были посчитаны исторические
        /// хэши.
        /// </summary>
        bool ValidateAgainstHistory(string password,
                                    IEnumerable<Models.EmployeePasswordHistory> history,
                                    IPasswordHasher hasher);

        /// <summary>
        /// Истёк ли срок пароля у сотрудника. Сравнивает
        /// <see cref="Models.Employee.LastPasswordChangeAt"/> с
        /// текущим временем; <paramref name="lastChangedUtc"/> = <c>null</c>
        /// считается «срок не истёк» (для миграционной совместимости).
        /// </summary>
        bool IsPasswordExpired(System.DateTime? lastChangedUtc, System.DateTime nowUtc);
    }
}
