using System;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IOrganizationSettingsRepository"/>
    /// для тестов и для UI до подключения <see cref="Data.AhuDbContext"/>.
    /// </summary>
    public sealed class InMemoryOrganizationSettingsRepository : IOrganizationSettingsRepository
    {
        private readonly object _sync = new object();
        private OrganizationSettings _settings;

        public OrganizationSettings Get()
        {
            lock (_sync)
            {
                if (_settings == null)
                {
                    _settings = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
                }
                return Clone(_settings);
            }
        }

        public void Save(OrganizationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            lock (_sync)
            {
                _settings = Clone(settings);
                _settings.Id = OrganizationSettings.SingletonId;
            }
        }

        private static OrganizationSettings Clone(OrganizationSettings s) => new OrganizationSettings
        {
            Id = s.Id,
            EncryptionKey = s.EncryptionKey,
            EncryptionKeyGeneratedAt = s.EncryptionKeyGeneratedAt,
            PasswordMinLength = s.PasswordMinLength,
            PasswordExpiryDays = s.PasswordExpiryDays,
            PasswordHistoryDepth = s.PasswordHistoryDepth,
            LockoutFailureThreshold = s.LockoutFailureThreshold,
            LockoutWindowMinutes = s.LockoutWindowMinutes,
            LockoutDurationMinutes = s.LockoutDurationMinutes,
        };
    }
}
