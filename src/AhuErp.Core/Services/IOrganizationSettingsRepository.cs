using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — доступ к singleton-записи
    /// <see cref="OrganizationSettings"/>. Реализации:
    /// <see cref="EfOrganizationSettingsRepository"/> и
    /// <see cref="InMemoryOrganizationSettingsRepository"/>.
    /// </summary>
    public interface IOrganizationSettingsRepository
    {
        /// <summary>
        /// Возвращает текущие настройки. Если запись отсутствует, создаёт её
        /// с дефолтными значениями (это упрощает миграцию из старой версии,
        /// где <see cref="OrganizationSettings"/> ещё не существовал).
        /// </summary>
        OrganizationSettings Get();

        /// <summary>Сохраняет (insert/update) singleton-запись.</summary>
        void Save(OrganizationSettings settings);
    }
}
