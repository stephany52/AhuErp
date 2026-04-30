using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 9 / A11 — апсёрт пользовательских настроек уведомлений
    /// через <see cref="INotificationService"/>.
    /// </summary>
    public class NotificationServicePreferencesTests
    {
        private readonly InMemoryNotificationRepository _repo = new InMemoryNotificationRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryEmployeeRepository _employees;
        private readonly InMemoryTaskRepository _tasks = new InMemoryTaskRepository();
        private readonly NoOpEmailGateway _email = new NoOpEmailGateway();
        private readonly NotificationService _service;

        public NotificationServicePreferencesTests()
        {
            _employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Иванов И.И.", Email = "ivanov@bmr", Role = EmployeeRole.Admin },
            });
            _service = new NotificationService(_repo, _employees, _tasks,
                new AuditService(_auditRepo), _email);
        }

        [Fact]
        public void SetPreference_creates_new_record_when_missing()
        {
            _service.SetPreference(1, NotificationKind.TaskAssigned,
                NotificationChannel.Email, isEnabled: true,
                emailOverride: "  custom@bmr  ");

            var pref = _repo.GetPreference(1, NotificationKind.TaskAssigned);
            Assert.NotNull(pref);
            Assert.Equal(NotificationChannel.Email, pref.Channel);
            Assert.True(pref.IsEnabled);
            Assert.Equal("custom@bmr", pref.EmailOverride); // обрезка пробелов
        }

        [Fact]
        public void SetPreference_updates_existing_record_in_place()
        {
            _service.SetPreference(1, NotificationKind.TaskAssigned,
                NotificationChannel.InApp, isEnabled: true);

            _service.SetPreference(1, NotificationKind.TaskAssigned,
                NotificationChannel.Both, isEnabled: false, emailOverride: null);

            var prefs = _service.ListPreferences(1).ToList();
            Assert.Single(prefs);
            Assert.Equal(NotificationChannel.Both, prefs[0].Channel);
            Assert.False(prefs[0].IsEnabled);
            Assert.Null(prefs[0].EmailOverride);
        }

        [Fact]
        public void SetPreference_validates_employee_id()
        {
            Assert.Throws<ArgumentException>(
                () => _service.SetPreference(0, NotificationKind.TaskAssigned,
                    NotificationChannel.InApp, true));
        }

        [Fact]
        public void ListPreferences_returns_empty_for_employee_without_records()
        {
            Assert.Empty(_service.ListPreferences(1));
        }
    }
}
