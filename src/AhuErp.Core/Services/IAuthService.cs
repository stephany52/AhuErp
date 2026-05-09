using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Причина неуспешной авторизации (для разделения сообщений в UI и диагностики).
    /// </summary>
    public enum LoginFailureReason
    {
        None,
        EmptyInput,
        UserNotFound,
        WrongPassword,

        // Phase 16 / Improvement #17.
        AccountLocked,
        AccountInactive,
        PasswordExpired
    }

    /// <summary>
    /// Сервис аутентификации и хранения текущего активного пользователя сессии.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Текущий вошедший сотрудник или <c>null</c>, если сессия не активна.
        /// </summary>
        Employee CurrentEmployee { get; }

        bool IsAuthenticated { get; }

        /// <summary>
        /// Причина последней неуспешной попытки входа (или <see cref="LoginFailureReason.None"/>
        /// после успешного входа / выхода).
        /// </summary>
        LoginFailureReason LastFailureReason { get; }

        /// <summary>
        /// Пытается войти под указанным ФИО / паролем. Возвращает <c>true</c>
        /// при успехе — при этом обновляется <see cref="CurrentEmployee"/>.
        /// При неверных данных пользователь остаётся не аутентифицирован, а
        /// <see cref="LastFailureReason"/> описывает причину.
        /// </summary>
        bool TryLogin(string fullName, string password);

        /// <summary>Закрывает текущую сессию.</summary>
        void Logout();
    }
}
