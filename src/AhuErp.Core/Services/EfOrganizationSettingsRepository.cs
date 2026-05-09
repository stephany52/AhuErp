using System;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6 реализация <see cref="IOrganizationSettingsRepository"/>.
    /// Singleton-запись хранится с фиксированным ID = 1; если её ещё нет
    /// в БД, создаётся при первом обращении к <see cref="Get"/> с
    /// дефолтными значениями. Принимает singleton-<see cref="AhuDbContext"/>
    /// напрямую (как и остальные EF-репозитории проекта).
    /// </summary>
    public sealed class EfOrganizationSettingsRepository : IOrganizationSettingsRepository
    {
        private readonly AhuDbContext _ctx;
        private readonly object _sync = new object();

        public EfOrganizationSettingsRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public OrganizationSettings Get()
        {
            lock (_sync)
            {
                var existing = _ctx.OrganizationSettings.AsNoTracking()
                    .FirstOrDefault(s => s.Id == OrganizationSettings.SingletonId);
                if (existing != null) return existing;

                var fresh = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
                _ctx.OrganizationSettings.Add(fresh);
                _ctx.SaveChanges();
                return fresh;
            }
        }

        public void Save(OrganizationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (_sync)
            {
                var existing = _ctx.OrganizationSettings
                    .FirstOrDefault(s => s.Id == OrganizationSettings.SingletonId);
                if (existing == null)
                {
                    settings.Id = OrganizationSettings.SingletonId;
                    _ctx.OrganizationSettings.Add(settings);
                }
                else
                {
                    existing.EncryptionKey = settings.EncryptionKey;
                    existing.EncryptionKeyGeneratedAt = settings.EncryptionKeyGeneratedAt;
                    existing.PasswordMinLength = settings.PasswordMinLength;
                    existing.PasswordExpiryDays = settings.PasswordExpiryDays;
                    existing.PasswordHistoryDepth = settings.PasswordHistoryDepth;
                    existing.LockoutFailureThreshold = settings.LockoutFailureThreshold;
                    existing.LockoutWindowMinutes = settings.LockoutWindowMinutes;
                    existing.LockoutDurationMinutes = settings.LockoutDurationMinutes;
                }
                _ctx.SaveChanges();
            }
        }
    }
}
