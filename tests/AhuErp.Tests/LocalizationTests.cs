using System.Globalization;
using System.Threading;
using AhuErp.Core.Resources;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 20 / Improvement #18 — каркас локализации. Проверяет, что
    /// ключи разрешаются в нейтральную русскую и английскую (satellite)
    /// сборки и что отсутствующий ключ возвращает сам ключ (graceful fallback).
    /// </summary>
    public class LocalizationTests
    {
        [Fact]
        public void Neutral_culture_returns_russian()
        {
            using var _ = new CultureScope("ru-RU");
            Assert.Equal("Закупки 44-ФЗ", Strings.Procurement_Module);
            Assert.Equal("Обновить", Strings.App_Refresh);
        }

        [Fact]
        public void English_culture_returns_english()
        {
            using var _ = new CultureScope("en-US");
            Assert.Equal("Procurement (44-FZ)", Strings.Procurement_Module);
            Assert.Equal("Refresh", Strings.App_Refresh);
        }

        [Fact]
        public void Missing_key_returns_key_itself()
        {
            Assert.Equal("Nonexistent_Key", Strings.Get("Nonexistent_Key"));
        }

        private sealed class CultureScope : System.IDisposable
        {
            private readonly CultureInfo _previousUi;
            public CultureScope(string name)
            {
                _previousUi = Thread.CurrentThread.CurrentUICulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            }
            public void Dispose() => Thread.CurrentThread.CurrentUICulture = _previousUi;
        }
    }
}
