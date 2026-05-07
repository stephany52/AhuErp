using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory справочник номенклатуры дел / видов документов / отделов.
    /// Используется в тестах и демо-режиме.
    /// </summary>
    public sealed class InMemoryNomenclatureRepository : INomenclatureRepository
    {
        private readonly List<NomenclatureCase> _cases = new List<NomenclatureCase>();
        private readonly List<DocumentTypeRef> _types = new List<DocumentTypeRef>();
        private readonly List<Department> _departments = new List<Department>();
        private readonly Dictionary<(string typeCode, int year), int> _sequences = new Dictionary<(string, int), int>();
        private readonly object _sequenceSync = new object();
        private int _nextCaseId = 1;
        private int _nextTypeId = 1;
        private int _nextDeptId = 1;

        public IReadOnlyList<NomenclatureCase> ListCases(int? year, bool activeOnly)
        {
            IEnumerable<NomenclatureCase> q = _cases;
            if (year.HasValue) q = q.Where(c => c.Year == year.Value);
            if (activeOnly) q = q.Where(c => c.IsActive);
            return q.OrderBy(c => c.Index).ToList().AsReadOnly();
        }

        public NomenclatureCase GetCase(int id) => _cases.FirstOrDefault(c => c.Id == id);

        public NomenclatureCase AddCase(NomenclatureCase @case)
        {
            if (@case == null) throw new ArgumentNullException(nameof(@case));
            if (@case.Id == 0) @case.Id = _nextCaseId++;
            else _nextCaseId = Math.Max(_nextCaseId, @case.Id + 1);
            _cases.Add(@case);
            return @case;
        }

        public NomenclatureCase UpdateCase(NomenclatureCase @case)
        {
            var idx = _cases.FindIndex(c => c.Id == @case.Id);
            if (idx < 0) throw new InvalidOperationException($"Дело #{@case.Id} не найдено.");
            _cases[idx] = @case;
            return @case;
        }

        public IReadOnlyList<DocumentTypeRef> ListTypes(bool activeOnly)
        {
            IEnumerable<DocumentTypeRef> q = _types;
            if (activeOnly) q = q.Where(t => t.IsActive);
            return q.OrderBy(t => t.Name).ToList().AsReadOnly();
        }

        public DocumentTypeRef GetType(int id) => _types.FirstOrDefault(t => t.Id == id);

        public DocumentTypeRef AddType(DocumentTypeRef typeRef)
        {
            if (typeRef == null) throw new ArgumentNullException(nameof(typeRef));
            if (typeRef.Id == 0) typeRef.Id = _nextTypeId++;
            else _nextTypeId = Math.Max(_nextTypeId, typeRef.Id + 1);
            _types.Add(typeRef);
            return typeRef;
        }

        public DocumentTypeRef UpdateType(DocumentTypeRef typeRef)
        {
            var idx = _types.FindIndex(t => t.Id == typeRef.Id);
            if (idx < 0) throw new InvalidOperationException($"Вид документа #{typeRef.Id} не найден.");
            _types[idx] = typeRef;
            return typeRef;
        }

        public int GetNextSequence(string typeCode, int documentTypeRefId, int year)
        {
            if (string.IsNullOrWhiteSpace(typeCode))
                throw new ArgumentException("Код вида документа обязателен.", nameof(typeCode));

            var key = (typeCode.Trim(), year);
            lock (_sequenceSync)
            {
                var current = _sequences.TryGetValue(key, out var value) ? value : 0;
                var next = current + 1;
                _sequences[key] = next;
                return next;
            }
        }

        public IReadOnlyList<Department> ListDepartments() => _departments.AsReadOnly();

        public Department AddDepartment(Department department)
        {
            if (department == null) throw new ArgumentNullException(nameof(department));
            if (department.Id == 0) department.Id = _nextDeptId++;
            else _nextDeptId = Math.Max(_nextDeptId, department.Id + 1);
            _departments.Add(department);
            return department;
        }
    }
}
