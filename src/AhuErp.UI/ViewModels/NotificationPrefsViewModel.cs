using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Phase 9 / A11 — пользовательские настройки уведомлений. Матрица
    /// <see cref="NotificationKind"/> × включить/канал/email-override.
    /// </summary>
    public partial class NotificationPrefsViewModel : ViewModelBase
    {
        private readonly INotificationService _notifications;
        private readonly IAuthService _auth;

        public ObservableCollection<NotificationPreferenceRow> Rows { get; } =
            new ObservableCollection<NotificationPreferenceRow>();

        public ObservableCollection<NotificationChannel> AvailableChannels { get; } =
            new ObservableCollection<NotificationChannel>(
                Enum.GetValues(typeof(NotificationChannel)).Cast<NotificationChannel>());

        [ObservableProperty]
        private string statusMessage;

        public NotificationPrefsViewModel(INotificationService notifications, IAuthService auth)
        {
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            Reload();
        }

        [RelayCommand]
        public void Reload()
        {
            Rows.Clear();
            StatusMessage = null;
            var me = _auth.CurrentEmployee;
            if (me == null) return;

            var existing = _notifications.ListPreferences(me.Id)
                .ToDictionary(p => p.Kind);

            foreach (var kind in Enum.GetValues(typeof(NotificationKind)).Cast<NotificationKind>())
            {
                if (existing.TryGetValue(kind, out var pref))
                {
                    Rows.Add(new NotificationPreferenceRow
                    {
                        Kind = kind,
                        IsEnabled = pref.IsEnabled,
                        Channel = pref.Channel,
                        EmailOverride = pref.EmailOverride,
                    });
                }
                else
                {
                    Rows.Add(new NotificationPreferenceRow
                    {
                        Kind = kind,
                        IsEnabled = true,
                        Channel = NotificationChannel.InApp,
                        EmailOverride = null,
                    });
                }
            }
        }

        [RelayCommand]
        public void Save()
        {
            var me = _auth.CurrentEmployee;
            if (me == null) return;
            try
            {
                foreach (var row in Rows)
                {
                    _notifications.SetPreference(me.Id, row.Kind, row.Channel, row.IsEnabled, row.EmailOverride);
                }
                StatusMessage = "Настройки сохранены.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка сохранения: " + ex.Message;
            }
        }
    }

    /// <summary>Строка матрицы настроек уведомлений (одна на NotificationKind).</summary>
    public partial class NotificationPreferenceRow : ObservableObject
    {
        [ObservableProperty]
        private NotificationKind kind;

        [ObservableProperty]
        private bool isEnabled;

        [ObservableProperty]
        private NotificationChannel channel;

        [ObservableProperty]
        private string emailOverride;
    }
}
