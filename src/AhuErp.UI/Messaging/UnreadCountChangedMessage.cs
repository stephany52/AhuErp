using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AhuErp.UI.Messaging
{
    /// <summary>
    /// Bug #2 — сообщение об изменении числа непрочитанных уведомлений.
    /// Отправляется <see cref="ViewModels.MyDesktopViewModel"/> при
    /// MarkRead/MarkAllRead, обрабатывается <see cref="ViewModels.MainViewModel"/>
    /// для обновления бейджа в шапке без жёсткой связи между VM-ами.
    /// </summary>
    public sealed class UnreadCountChangedMessage : ValueChangedMessage<int>
    {
        public UnreadCountChangedMessage(int unreadCount) : base(unreadCount) { }
    }
}
