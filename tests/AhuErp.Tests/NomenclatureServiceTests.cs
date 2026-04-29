using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Бизнес-правила <see cref="NomenclatureService"/>: автоматическое
    /// присвоение регистрационного номера по шаблону, защита от повторной
    /// регистрации, инкремент последовательности.
    /// </summary>
    public class NomenclatureServiceTests
    {
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryNomenclatureRepository _nomen = new InMemoryNomenclatureRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly NomenclatureService _service;
        private readonly DocumentTypeRef _orderType;
        private readonly NomenclatureCase _case;

        public NomenclatureServiceTests()
        {
            var audit = new AuditService(_auditRepo);
            _service = new NomenclatureService(_nomen, _docs, audit);

            _orderType = _nomen.AddType(new DocumentTypeRef
            {
                Name = "Приказ",
                ShortCode = "ПР",
                DefaultDirection = DocumentDirection.Directive,
                DefaultRetentionYears = 75,
                RegistrationNumberTemplate = "АХУ-{CaseIndex}/{Year}-{Sequence:00000}",
                IsActive = true
            });
            _case = _nomen.AddCase(new NomenclatureCase
            {
                Index = "01-02",
                Title = "Приказы по АХД",
                RetentionPeriodYears = 5,
                Year = DateTime.Now.Year,
                IsActive = true
            });
        }

        private Document AddDoc(int? typeRefId = null, int? caseId = null) =>
            AddDocCore(new Document
            {
                Title = "Тестовый приказ",
                Type = DocumentType.Internal,
                Direction = DocumentDirection.Directive,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                DocumentTypeRefId = typeRefId,
                NomenclatureCaseId = caseId
            });

        private Document AddDocCore(Document d)
        {
            _docs.Add(d);
            return d;
        }

        [Fact]
        public void Register_throws_when_typeRef_missing()
        {
            var doc = AddDoc(typeRefId: null);
            var ex = Assert.Throws<InvalidOperationException>(() => _service.Register(doc.Id));
            Assert.Contains("вид документа", ex.Message);
        }

        [Fact]
        public void Register_assigns_number_using_template_and_case()
        {
            var doc = AddDoc(_orderType.Id, _case.Id);

            var registered = _service.Register(doc.Id);

            Assert.True(registered.IsRegistered);
            var year = DateTime.Now.Year;
            Assert.Equal($"АХУ-01-02/{year}-00001", registered.RegistrationNumber);
            Assert.NotNull(registered.RegistrationDate);
        }

        [Fact]
        public void Register_increments_sequence_per_type_per_year()
        {
            var d1 = AddDoc(_orderType.Id, _case.Id);
            var d2 = AddDoc(_orderType.Id, _case.Id);

            _service.Register(d1.Id);
            _service.Register(d2.Id);

            var year = DateTime.Now.Year;
            Assert.Equal($"АХУ-01-02/{year}-00001", d1.RegistrationNumber);
            Assert.Equal($"АХУ-01-02/{year}-00002", d2.RegistrationNumber);
        }

        [Fact]
        public void Register_throws_when_already_registered()
        {
            var doc = AddDoc(_orderType.Id, _case.Id);
            _service.Register(doc.Id);
            var ex = Assert.Throws<InvalidOperationException>(() => _service.Register(doc.Id));
            Assert.Contains("уже зарегистрирован", ex.Message);
        }

        [Fact]
        public void BuildRegistrationNumber_falls_back_to_default_template_when_template_empty()
        {
            var noTpl = _nomen.AddType(new DocumentTypeRef
            {
                Name = "Распоряжение",
                ShortCode = "РСП",
                DefaultDirection = DocumentDirection.Directive,
                DefaultRetentionYears = 5,
                RegistrationNumberTemplate = null,
                IsActive = true
            });

            var num = _service.BuildRegistrationNumber(noTpl, _case, 2026, 7);
            Assert.Equal("РСП-01-02/2026-00007", num);
        }

        // A13: при регистрации без привязки к делу плейсхолдер CaseIndex
        // заменяется на «Б/Н» (без номера дела), чтобы регномер сразу читался
        // правильно вместо вводящего в заблуждение шифра «00».
        [Fact]
        public void BuildRegistrationNumber_uses_no_case_marker_when_no_case_provided()
        {
            var num = _service.BuildRegistrationNumber(_orderType, null, 2026, 1);
            Assert.Equal("АХУ-Б/Н/2026-00001", num);
        }
    }
}
