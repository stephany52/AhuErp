using System;
using AhuErp.Core.Services;
using Serilog;
using Serilog.Events;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 20 / Improvement #18 — фасад <see cref="AppLog"/>. Проверяет, что
    /// до явной инициализации он работает как no-op, что <see cref="AppLog.Configure"/>
    /// заменяет logger и что записи действительно доезжают до Serilog sink.
    /// </summary>
    public class AppLogTests : IDisposable
    {
        public void Dispose() => AppLog.Reset();

        [Fact]
        public void Default_logger_is_silent_noop()
        {
            AppLog.Reset();
            // Никакой sink не настроен — все вызовы должны быть безопасны.
            AppLog.Information("Тест {Value}", 42);
            AppLog.Warning("Предупреждение");
            AppLog.Error(new InvalidOperationException("x"), "Ошибка");
        }

        [Fact]
        public void Configure_swaps_logger_and_writes_to_sink()
        {
            var sink = new TestSink();
            var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(sink)
                .CreateLogger();
            AppLog.Configure(logger);

            AppLog.Information("Привет {Name}", "АХУ");
            AppLog.Write(LogEventLevel.Warning, "Уровень из переменной");

            Assert.Equal(2, sink.Events.Count);
            Assert.Equal(LogEventLevel.Information, sink.Events[0].Level);
            Assert.Equal(LogEventLevel.Warning, sink.Events[1].Level);
        }

        [Fact]
        public void Configure_throws_on_null()
        {
            Assert.Throws<ArgumentNullException>(() => AppLog.Configure(null));
        }

        [Fact]
        public void ForContext_returns_scoped_logger_or_noop()
        {
            // До Configure — no-op (контекст не падает).
            AppLog.Reset();
            Assert.NotNull(AppLog.ForContext<AppLogTests>());

            var sink = new TestSink();
            AppLog.Configure(new LoggerConfiguration()
                .WriteTo.Sink(sink).CreateLogger());
            var scoped = AppLog.ForContext<AppLogTests>();
            scoped.Information("В контексте");
            Assert.Single(sink.Events);
            Assert.True(sink.Events[0].Properties.ContainsKey("SourceContext"));
        }

        private sealed class TestSink : Serilog.Core.ILogEventSink
        {
            public System.Collections.Generic.List<LogEvent> Events { get; } = new System.Collections.Generic.List<LogEvent>();
            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }
    }
}
