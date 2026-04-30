using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="INomenclatureService"/>. Бизнес-логика:
    /// <list type="bullet">
    ///   <item><description>Регистрация документа атомарно увеличивает последовательность
    ///     по виду+году и формирует регистрационный номер по шаблону.</description></item>
    ///   <item><description>Все мутации сопровождаются записью в журнал аудита.</description></item>
    /// </list>
    /// </summary>
    public sealed class NomenclatureService : INomenclatureService
    {
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{(?<name>[A-Za-z]+)(:(?<format>[^\}]+))?\}", RegexOptions.Compiled);

        private readonly INomenclatureRepository _repository;
        private readonly IDocumentRepository _documents;
        private readonly IAuditService _audit;
        private readonly object _sequenceSync = new object();

        public NomenclatureService(
            INomenclatureRepository repository,
            IDocumentRepository documents,
            IAuditService audit)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public IReadOnlyList<NomenclatureCase> ListCases(int? year = null, bool activeOnly = true)
            => _repository.ListCases(year, activeOnly);

        public NomenclatureCase GetCase(int id) => _repository.GetCase(id);

        public NomenclatureCase AddCase(NomenclatureCase @case)
        {
            if (@case == null) throw new ArgumentNullException(nameof(@case));
            if (string.IsNullOrWhiteSpace(@case.Index))
                throw new ArgumentException("Индекс дела обязателен.", nameof(@case));
            if (string.IsNullOrWhiteSpace(@case.Title))
                throw new ArgumentException("Заголовок дела обязателен.", nameof(@case));
            var saved = _repository.AddCase(@case);
            _audit.Record(AuditActionType.Created, nameof(NomenclatureCase), saved.Id, null,
                newValues: $"Index={saved.Index}; Title={saved.Title}; Year={saved.Year}");
            return saved;
        }

        public NomenclatureCase UpdateCase(NomenclatureCase @case)
        {
            var saved = _repository.UpdateCase(@case);
            _audit.Record(AuditActionType.Updated, nameof(NomenclatureCase), saved.Id, null,
                newValues: $"Index={saved.Index}; Title={saved.Title}; IsActive={saved.IsActive}");
            return saved;
        }

        public void DeactivateCase(int id)
        {
            var c = _repository.GetCase(id) ?? throw new InvalidOperationException($"Дело #{id} не найдено.");
            c.IsActive = false;
            _repository.UpdateCase(c);
            _audit.Record(AuditActionType.Updated, nameof(NomenclatureCase), id, null, details: "Deactivated");
        }

        public IReadOnlyList<DocumentTypeRef> ListTypes(bool activeOnly = true)
            => _repository.ListTypes(activeOnly);

        public DocumentTypeRef GetType(int id) => _repository.GetType(id);

        public DocumentTypeRef AddType(DocumentTypeRef typeRef)
        {
            if (typeRef == null) throw new ArgumentNullException(nameof(typeRef));
            if (string.IsNullOrWhiteSpace(typeRef.Name))
                throw new ArgumentException("Наименование вида документа обязательно.", nameof(typeRef));
            var saved = _repository.AddType(typeRef);
            _audit.Record(AuditActionType.Created, nameof(DocumentTypeRef), saved.Id, null,
                newValues: $"Name={saved.Name}; Code={saved.ShortCode}");
            return saved;
        }

        public DocumentTypeRef UpdateType(DocumentTypeRef typeRef)
        {
            var saved = _repository.UpdateType(typeRef);
            _audit.Record(AuditActionType.Updated, nameof(DocumentTypeRef), saved.Id, null,
                newValues: $"Name={saved.Name}");
            return saved;
        }

        public Document Register(int documentId, int? caseId = null)
        {
            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");
            if (doc.IsRegistered)
                throw new InvalidOperationException("Документ уже зарегистрирован.");
            if (!doc.DocumentTypeRefId.HasValue)
                throw new InvalidOperationException(
                    "Перед регистрацией укажите вид документа (DocumentTypeRefId).");

            var typeRef = _repository.GetType(doc.DocumentTypeRefId.Value)
                ?? throw new InvalidOperationException(
                    $"Вид документа #{doc.DocumentTypeRefId.Value} не найден.");

            var year = DateTime.Now.Year;
            NomenclatureCase @case = null;
            if (caseId.HasValue)
            {
                @case = _repository.GetCase(caseId.Value)
                    ?? throw new InvalidOperationException($"Дело #{caseId.Value} не найдено.");
            }
            else if (doc.NomenclatureCaseId.HasValue)
            {
                @case = _repository.GetCase(doc.NomenclatureCaseId.Value);
            }

            int sequence;
            string registrationNumber;
            // Атомарно вычисляем следующую последовательность и формируем номер,
            // защищая от гонки одновременной регистрации двух документов.
            lock (_sequenceSync)
            {
                sequence = _repository.GetMaxSequence(typeRef.Id, year) + 1;
                registrationNumber = BuildRegistrationNumber(typeRef, @case, year, sequence);
                doc.RegistrationNumber = registrationNumber;
                doc.RegistrationDate = DateTime.Now;
                if (@case != null)
                {
                    doc.NomenclatureCaseId = @case.Id;
                }
                _documents.Update(doc);
                _repository.BumpSequence(typeRef.Id, year, sequence);
            }

            _audit.Record(AuditActionType.Registered, nameof(Document), doc.Id, doc.AuthorId,
                newValues: $"RegistrationNumber={registrationNumber}; CaseId={@case?.Id}");

            return doc;
        }

        public string BuildRegistrationNumber(DocumentTypeRef typeRef, NomenclatureCase @case, int year, int sequence)
        {
            if (typeRef == null) throw new ArgumentNullException(nameof(typeRef));
            var template = string.IsNullOrWhiteSpace(typeRef.RegistrationNumberTemplate)
                ? "{Code}-{CaseIndex}/{Year}-{Sequence:00000}"
                : typeRef.RegistrationNumberTemplate;

            return PlaceholderRegex.Replace(template, match =>
            {
                var name = match.Groups["name"].Value;
                var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

                switch (name)
                {
                    case "Code":
                        return typeRef.ShortCode ?? typeRef.Name;
                    case "CaseIndex":
                        // Если документ зарегистрирован без привязки к делу
                        // (caseId не указан) — заменяем плейсхолдер на «Б/Н»,
                        // чтобы регномер сразу читался как «без номера дела»,
                        // а не вводил пользователя в заблуждение шифром «00»
                        // (см. дефект A13).
                        return @case?.Index ?? "Б/Н";
                    case "Year":
                        return year.ToString(format ?? "0000", CultureInfo.InvariantCulture);
                    case "Sequence":
                        return sequence.ToString(format ?? "0", CultureInfo.InvariantCulture);
                    default:
                        return match.Value;
                }
            });
        }
    }
}
