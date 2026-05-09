using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Базовая реализация <see cref="IAuthService"/>. Держит текущего
    /// <see cref="Employee"/> в оперативной памяти. С Phase 16 / Improvement #17
    /// добавлены: проверка блокировки аккаунта, проверка срока действия пароля,
    /// запись попыток входа в <see cref="ILoginAttemptRepository"/>, аудит
    /// событий <see cref="AuditActionType.UserLogin"/> /
    /// <see cref="AuditActionType.LoginAttemptFailed"/> /
    /// <see cref="AuditActionType.AccountLocked"/>. Зависимости журнала
    /// (login attempts / audit / settings / password history) опциональны —
    /// конструктор без них оставлен для обратной совместимости с тестами.
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly IEmployeeRepository _employees;
        private readonly IPasswordHasher _hasher;
        private readonly ILoginAttemptRepository _loginAttempts;
        private readonly IEmployeePasswordHistoryRepository _passwordHistory;
        private readonly IPasswordPolicy _policy;
        private readonly IOrganizationSettingsRepository _settings;
        private readonly IAuditService _audit;
        private readonly Func<DateTime> _now;
        private readonly Func<string> _ipProvider;

        public Employee CurrentEmployee { get; private set; }

        public bool IsAuthenticated => CurrentEmployee != null;

        public LoginFailureReason LastFailureReason { get; private set; } = LoginFailureReason.None;

        /// <summary>Минималистичный конструктор для совместимости с старыми тестами.</summary>
        public AuthService(IEmployeeRepository employees, IPasswordHasher hasher)
            : this(employees, hasher,
                   loginAttempts: null,
                   passwordHistory: null,
                   policy: null,
                   settings: null,
                   audit: null,
                   now: null,
                   ipProvider: null)
        {
        }

        /// <summary>
        /// Полный конструктор с journal/audit/policy. Любая опциональная
        /// зависимость может быть <c>null</c> — соответствующая фича просто
        /// отключается, остальная логика продолжает работать.
        /// </summary>
        public AuthService(IEmployeeRepository employees,
                           IPasswordHasher hasher,
                           ILoginAttemptRepository loginAttempts,
                           IEmployeePasswordHistoryRepository passwordHistory,
                           IPasswordPolicy policy,
                           IOrganizationSettingsRepository settings,
                           IAuditService audit,
                           Func<DateTime> now,
                           Func<string> ipProvider)
        {
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _loginAttempts = loginAttempts;
            _passwordHistory = passwordHistory;
            _policy = policy;
            _settings = settings;
            _audit = audit;
            _now = now ?? (() => DateTime.UtcNow);
            _ipProvider = ipProvider ?? (() => "local");
        }

        public bool TryLogin(string fullName, string password)
        {
            CurrentEmployee = null;

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrEmpty(password))
            {
                LastFailureReason = LoginFailureReason.EmptyInput;
                RecordAttempt(null, fullName, false, LoginFailureReason.EmptyInput);
                return false;
            }

            // Trim + Unicode-нормализация (NFC) защищает от случайно вставленного NBSP /
            // декомпозированных символов из буфера обмена. Кириллица в Windows обычно
            // приходит в NFC, но из браузеров/MacOS бывает NFD, и сравнение тогда падает.
            var normalized = NormalizeFullName(fullName);

            var employee = _employees.FindByFullName(normalized);
            if (employee == null)
            {
                LastFailureReason = LoginFailureReason.UserNotFound;
                RecordAttempt(null, normalized, false, LoginFailureReason.UserNotFound);
                return false;
            }

            // Inactive — отдельный код возврата (не путать с lockout).
            if (!employee.IsActive)
            {
                LastFailureReason = LoginFailureReason.AccountInactive;
                RecordAttempt(employee.Id, normalized, false, LoginFailureReason.AccountInactive);
                return false;
            }

            // Lockout: если LockedUntil > now — отказываем сразу, не проверяя пароль.
            // Это снижает побочные эффекты атак на CPU (PBKDF2) от ботов.
            var now = _now();
            if (employee.LockedUntil.HasValue && employee.LockedUntil.Value > now)
            {
                LastFailureReason = LoginFailureReason.AccountLocked;
                RecordAttempt(employee.Id, normalized, false, LoginFailureReason.AccountLocked);
                return false;
            }

            if (string.IsNullOrEmpty(employee.PasswordHash))
            {
                LastFailureReason = LoginFailureReason.WrongPassword;
                RecordAttempt(employee.Id, normalized, false, LoginFailureReason.WrongPassword);
                ConsiderLockout(employee, now);
                return false;
            }

            if (!_hasher.Verify(password, employee.PasswordHash))
            {
                LastFailureReason = LoginFailureReason.WrongPassword;
                RecordAttempt(employee.Id, normalized, false, LoginFailureReason.WrongPassword);
                ConsiderLockout(employee, now);
                return false;
            }

            // Пароль верен. Проверяем срок действия (Improvement #17 — 90 дней).
            if (_policy != null && _policy.IsPasswordExpired(employee.LastPasswordChangeAt, now))
            {
                LastFailureReason = LoginFailureReason.PasswordExpired;
                RecordAttempt(employee.Id, normalized, false, LoginFailureReason.PasswordExpired);
                _audit?.Record(AuditActionType.PasswordExpired,
                    nameof(Employee), employee.Id, employee.Id);
                return false;
            }

            // На момент успешного входа сбрасываем висящий lockout, если он истёк.
            if (employee.LockedUntil.HasValue && employee.LockedUntil.Value <= now)
            {
                employee.LockedUntil = null;
                _employees.Save(employee);
            }

            LastFailureReason = LoginFailureReason.None;
            CurrentEmployee = employee;
            RecordAttempt(employee.Id, normalized, true, LoginFailureReason.None);
            _audit?.Record(AuditActionType.UserLogin, nameof(Employee), employee.Id, employee.Id);
            return true;
        }

        public void Logout()
        {
            var prev = CurrentEmployee;
            CurrentEmployee = null;
            LastFailureReason = LoginFailureReason.None;
            if (prev != null)
            {
                _audit?.Record(AuditActionType.UserLogout, nameof(Employee), prev.Id, prev.Id);
            }
        }

        private void RecordAttempt(int? employeeId, string fullName, bool success, LoginFailureReason reason)
        {
            if (_loginAttempts == null) return;
            _loginAttempts.Add(new LoginAttempt
            {
                EmployeeId = employeeId,
                AttemptedFullName = string.IsNullOrEmpty(fullName) ? null : fullName,
                Timestamp = _now(),
                IpAddress = _ipProvider(),
                Success = success,
                FailureReason = reason,
            });

            if (!success && employeeId.HasValue && _audit != null)
            {
                _audit.Record(AuditActionType.LoginAttemptFailed,
                    nameof(Employee), employeeId.Value, employeeId.Value,
                    details: reason.ToString());
            }
        }

        /// <summary>
        /// При неудачной попытке проверяет, не пора ли заблокировать аккаунт.
        /// Порог и окно берутся из <see cref="OrganizationSettings"/>.
        /// </summary>
        private void ConsiderLockout(Employee employee, DateTime nowUtc)
        {
            if (_loginAttempts == null) return;

            var s = _settings?.Get();
            int threshold = s?.LockoutFailureThreshold ?? 5;
            int windowMin = s?.LockoutWindowMinutes ?? 10;
            int durationMin = s?.LockoutDurationMinutes ?? 30;
            if (threshold <= 0 || windowMin <= 0 || durationMin <= 0) return;

            var failures = _loginAttempts.CountRecentFailures(employee.Id,
                nowUtc - TimeSpan.FromMinutes(windowMin));
            if (failures < threshold) return;

            employee.LockedUntil = nowUtc + TimeSpan.FromMinutes(durationMin);
            _employees.Save(employee);
            _audit?.Record(AuditActionType.AccountLocked,
                nameof(Employee), employee.Id, employee.Id,
                details: $"failures={failures}, durationMin={durationMin}");
        }

        private static string NormalizeFullName(string value)
        {
            var trimmed = value.Trim();
            try
            {
                return trimmed.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                // На неконвертируемых последовательностях возвращаем хоть что-то,
                // чтобы поиск отработал, а не валился исключением.
                return trimmed;
            }
        }
    }
}
