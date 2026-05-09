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
    /// дефолтными значениями.
    /// </summary>
    public sealed class EfOrganizationSettingsRepository : IOrganizationSettingsRepository
    {
        private readonly Func<AhuDbContext> _contextFactory;
        private readonly object _sync = new object();

        public EfOrganizationSettingsRepository(Func<AhuDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public OrganizationSettings Get()
        {
            lock (_sync)
            {
                using (var ctx = _contextFactory())
                {
                    var existing = ctx.OrganizationSettings.AsNoTracking()
                        .FirstOrDefault(s => s.Id == OrganizationSettings.SingletonId);
                    if (existing != null) return existing;

                    var fresh = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
                    ctx.OrganizationSettings.Add(fresh);
                    ctx.SaveChanges();
                    return fresh;
                }
            }
        }

        public void Save(OrganizationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (_sync)
            {
                using (var ctx = _contextFactory())
                {
                    var existing = ctx.OrganizationSettings
                        .FirstOrDefault(s => s.Id == OrganizationSettings.SingletonId);
                    if (existing == null)
                    {
                        settings.Id = OrganizationSettings.SingletonId;
                        ctx.OrganizationSettings.Add(settings);
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
                    ctx.SaveChanges();
                }
            }
        }
    }
}
