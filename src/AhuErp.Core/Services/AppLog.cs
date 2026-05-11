using System;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 20 / Improvement #18 — единая точка структурированного логирования
    /// для всех слоёв (Core, UI, тесты). Внутри живёт <see cref="Serilog.ILogger"/>;
    /// до явной инициализации (см. <see cref="Configure"/>) используется
    /// <see cref="Serilog.Core.Logger.None"/> — это идемпотентно безопасно
    /// для тестов и in-memory утилит.
    /// </summary>
    public static class AppLog
    {
        private static ILogger _logger = Logger.None;

        /// <summary>
        /// Подменить активный логгер. Вызывается из <c>App.xaml.cs</c> после
        /// настройки Serilog (RollingFile + EventLog).
        /// </summary>
        public static void Configure(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Снять привязку (для теста — возвращает no-op логгер).</summary>
        public static void Reset() => _logger = Logger.None;

        public static ILogger ForContext<T>() => _logger.ForContext<T>();
        public static ILogger ForContext(Type type) => _logger.ForContext(type);

        public static void Verbose(string template, params object[] args)
            => _logger.Verbose(template, args);
        public static void Debug(string template, params object[] args)
            => _logger.Debug(template, args);
        public static void Information(string template, params object[] args)
            => _logger.Information(template, args);
        public static void Warning(string template, params object[] args)
            => _logger.Warning(template, args);
        public static void Error(Exception ex, string template, params object[] args)
            => _logger.Error(ex, template, args);
        public static void Fatal(Exception ex, string template, params object[] args)
            => _logger.Fatal(ex, template, args);

        /// <summary>
        /// Логирует событие на указанном уровне. Помощник для случаев, когда
        /// уровень определяется динамически (см. <c>NotificationService</c>).
        /// </summary>
        public static void Write(LogEventLevel level, string template, params object[] args)
            => _logger.Write(level, template, args);
    }
}
