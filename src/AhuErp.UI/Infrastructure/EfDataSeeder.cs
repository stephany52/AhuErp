using System;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;
using AhuErp.Core.Services;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Минимальный сидинг для EF6-бэкенда:
    /// <list type="bullet">
    ///   <item><description>при пустой таблице <c>Employees</c> создаёт одного администратора
    ///     с паролем <c>password</c> (PBKDF2-хэш);</description></item>
    ///   <item><description>при пустых справочниках Phase 7 (отделы, виды документов,
    ///     номенклатура дел) — наполняет их типовыми позициями для МКУ АХУ.</description></item>
    /// </list>
    /// Реальная БД (если нужна «чистая») может быть инициализирована через
    /// <c>scripts/create-db.sql</c> без вызова сидинга.
    /// </summary>
    public static class EfDataSeeder
    {
        public const string DefaultPassword = "password";
        public const string AdminFullName = "Администратор МКУ АХУ БМР";

        public static void EnsureSeeded(AhuDbContext ctx, IPasswordHasher hasher)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (hasher == null) throw new ArgumentNullException(nameof(hasher));

            EnsureAdmin(ctx, hasher);
            EnsureNomenclatureReferenceData(ctx);
            EnsureOrganizationSettings(ctx);
        }

        private static void EnsureAdmin(AhuDbContext ctx, IPasswordHasher hasher)
        {
            var hasLoginable = ctx.Employees.Any(e => e.PasswordHash != null && e.PasswordHash != "");
            if (hasLoginable) return;

            var existing = ctx.Employees.FirstOrDefault(e => e.FullName == AdminFullName);
            if (existing != null)
            {
                existing.Role = EmployeeRole.Admin;
                existing.PasswordHash = hasher.Hash(DefaultPassword);
                // Phase 16: фиксируем дату смены пароля, чтобы первый вход
                // не валился по PasswordExpired (90-дневный отсчёт с now).
                existing.LastPasswordChangeAt = DateTime.UtcNow;
                ctx.SaveChanges();
                return;
            }

            ctx.Employees.Add(new Employee
            {
                FullName = AdminFullName,
                Position = "Администратор информационной системы",
                Role = EmployeeRole.Admin,
                PasswordHash = hasher.Hash(DefaultPassword),
                LastPasswordChangeAt = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        /// <summary>
        /// Phase 16: создаёт singleton-настройки учреждения с дефолтами политики
        /// безопасности (мин 8/1 цифра/1 заглавная, 90 дней, 5 паролей в истории,
        /// 5 неудач за 10 минут → 30 минут lockout). Шифр-ключ остаётся пустым —
        /// его задаёт администратор через админ-панель в нужный момент.
        /// </summary>
        private static void EnsureOrganizationSettings(AhuDbContext ctx)
        {
            if (ctx.OrganizationSettings.Any()) return;
            ctx.OrganizationSettings.Add(new OrganizationSettings
            {
                Id = 1,
                EncryptionKey = null,
                EncryptionKeyGeneratedAt = null,
                PasswordMinLength = 8,
                PasswordExpiryDays = 90,
                PasswordHistoryDepth = 5,
                LockoutFailureThreshold = 5,
                LockoutWindowMinutes = 10,
                LockoutDurationMinutes = 30,
            });
            ctx.SaveChanges();
        }

        /// <summary>
        /// Сидит справочники, без которых СЭД-функционал бесполезен:
        /// отделы → виды документов → номенклатура дел.
        /// </summary>
        private static void EnsureNomenclatureReferenceData(AhuDbContext ctx)
        {
            // 1. Отделы.
            if (!ctx.Departments.Any())
            {
                ctx.Departments.Add(new Department { Name = "Руководство", ShortCode = "РУК", IsActive = true });
                ctx.Departments.Add(new Department { Name = "Архивный отдел", ShortCode = "АРХ", IsActive = true });
                ctx.Departments.Add(new Department { Name = "Служба ИТ-обеспечения", ShortCode = "ИТО", IsActive = true });
                ctx.Departments.Add(new Department { Name = "Транспортный отдел", ShortCode = "ТРН", IsActive = true });
                ctx.Departments.Add(new Department { Name = "Хозяйственный отдел", ShortCode = "ХОЗ", IsActive = true });
                ctx.SaveChanges();
            }

            // 2. Виды документов.
            if (!ctx.DocumentTypeRefs.Any())
            {
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Приказ",
                    ShortCode = "ПР",
                    DefaultDirection = DocumentDirection.Directive,
                    DefaultRetentionYears = 75,
                    RegistrationNumberTemplate = "АХУ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Распоряжение",
                    ShortCode = "РСП",
                    DefaultDirection = DocumentDirection.Directive,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "РСП-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Служебная записка",
                    ShortCode = "СЗ",
                    DefaultDirection = DocumentDirection.Internal,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "СЗ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Входящее письмо",
                    ShortCode = "ВХ",
                    DefaultDirection = DocumentDirection.Incoming,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "ВХ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Исходящее письмо",
                    ShortCode = "ИСХ",
                    DefaultDirection = DocumentDirection.Outgoing,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "ИСХ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Акт списания ТМЦ",
                    ShortCode = "АКТ",
                    DefaultDirection = DocumentDirection.Internal,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "АКТ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Путевой лист",
                    ShortCode = "ПЛ",
                    DefaultDirection = DocumentDirection.Internal,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "ПЛ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "Заявка в архив",
                    ShortCode = "АРХ",
                    DefaultDirection = DocumentDirection.Internal,
                    DefaultRetentionYears = 5,
                    RegistrationNumberTemplate = "АРХ-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.DocumentTypeRefs.Add(new DocumentTypeRef
                {
                    Name = "IT-заявка",
                    ShortCode = "IT",
                    DefaultDirection = DocumentDirection.Internal,
                    DefaultRetentionYears = 3,
                    RegistrationNumberTemplate = "IT-{CaseIndex}/{Year}-{Sequence:00000}",
                    IsActive = true
                });
                ctx.SaveChanges();
            }

            // 3. Номенклатура дел.
            if (!ctx.NomenclatureCases.Any())
            {
                var year = DateTime.UtcNow.Year;
                var ruk = ctx.Departments.FirstOrDefault(d => d.ShortCode == "РУК");
                var arx = ctx.Departments.FirstOrDefault(d => d.ShortCode == "АРХ");
                var ito = ctx.Departments.FirstOrDefault(d => d.ShortCode == "ИТО");
                var trn = ctx.Departments.FirstOrDefault(d => d.ShortCode == "ТРН");
                var hoz = ctx.Departments.FirstOrDefault(d => d.ShortCode == "ХОЗ");

                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "01-01", Title = "Приказы по основной деятельности",
                    DepartmentId = ruk?.Id, RetentionPeriodYears = 75, Article = "19а", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "01-02", Title = "Распоряжения по административно-хозяйственным вопросам",
                    DepartmentId = ruk?.Id, RetentionPeriodYears = 5, Article = "19в", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "02-01", Title = "Служебные записки",
                    DepartmentId = ruk?.Id, RetentionPeriodYears = 5, Article = "84", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "03-01", Title = "Журнал входящей корреспонденции",
                    DepartmentId = ruk?.Id, RetentionPeriodYears = 5, Article = "258а", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "03-02", Title = "Журнал исходящей корреспонденции",
                    DepartmentId = ruk?.Id, RetentionPeriodYears = 5, Article = "258б", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "04-01", Title = "Акты списания материальных ценностей",
                    DepartmentId = hoz?.Id, RetentionPeriodYears = 5, Article = "362", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "05-01", Title = "Путевые листы и заявки на транспорт",
                    DepartmentId = trn?.Id, RetentionPeriodYears = 5, Article = "553", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "06-01", Title = "Запросы и справки архивного отдела",
                    DepartmentId = arx?.Id, RetentionPeriodYears = 10, Article = "256", Year = year, IsActive = true
                });
                ctx.NomenclatureCases.Add(new NomenclatureCase
                {
                    Index = "07-01", Title = "Заявки на ИТ-обслуживание и сопровождение",
                    DepartmentId = ito?.Id, RetentionPeriodYears = 3, Article = "656", Year = year, IsActive = true
                });
                ctx.SaveChanges();
            }
        }
    }
}
