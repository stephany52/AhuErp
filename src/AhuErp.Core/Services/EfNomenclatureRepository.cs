using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="INomenclatureRepository"/>.</summary>
    public sealed class EfNomenclatureRepository : INomenclatureRepository
    {
        private readonly AhuDbContext _ctx;

        public EfNomenclatureRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<NomenclatureCase> ListCases(int? year, bool activeOnly)
        {
            IQueryable<NomenclatureCase> q = _ctx.NomenclatureCases;
            if (year.HasValue) q = q.Where(c => c.Year == year.Value);
            if (activeOnly) q = q.Where(c => c.IsActive);
            return q.OrderBy(c => c.Index).ToList().AsReadOnly();
        }

        public NomenclatureCase GetCase(int id) => _ctx.NomenclatureCases.Find(id);

        public NomenclatureCase AddCase(NomenclatureCase @case)
        {
            _ctx.NomenclatureCases.Add(@case);
            _ctx.SaveChanges();
            return @case;
        }

        public NomenclatureCase UpdateCase(NomenclatureCase @case)
        {
            if (_ctx.Entry(@case).State == EntityState.Detached)
            {
                _ctx.NomenclatureCases.Attach(@case);
                _ctx.Entry(@case).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
            return @case;
        }

        public IReadOnlyList<DocumentTypeRef> ListTypes(bool activeOnly)
        {
            IQueryable<DocumentTypeRef> q = _ctx.DocumentTypeRefs;
            if (activeOnly) q = q.Where(t => t.IsActive);
            return q.OrderBy(t => t.Name).ToList().AsReadOnly();
        }

        public DocumentTypeRef GetType(int id) => _ctx.DocumentTypeRefs.Find(id);

        public DocumentTypeRef AddType(DocumentTypeRef typeRef)
        {
            _ctx.DocumentTypeRefs.Add(typeRef);
            _ctx.SaveChanges();
            return typeRef;
        }

        public DocumentTypeRef UpdateType(DocumentTypeRef typeRef)
        {
            if (_ctx.Entry(typeRef).State == EntityState.Detached)
            {
                _ctx.DocumentTypeRefs.Attach(typeRef);
                _ctx.Entry(typeRef).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
            return typeRef;
        }

        public int GetNextSequence(string typeCode, int documentTypeRefId, int year)
        {
            if (string.IsNullOrWhiteSpace(typeCode))
                throw new ArgumentException("Код вида документа обязателен.", nameof(typeCode));

            var normalizedCode = typeCode.Trim();
            using (var tx = _ctx.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var counter = _ctx.NomenclatureCounters.SingleOrDefault(c => c.TypeCode == normalizedCode && c.Year == year);
                if (counter == null)
                {
                    counter = new NomenclatureCounter
                    {
                        TypeCode = normalizedCode,
                        Year = year,
                        LastNumber = GetMaxSequenceFromDocuments(normalizedCode, year) + 1
                    };
                    _ctx.NomenclatureCounters.Add(counter);
                }
                else
                {
                    counter.LastNumber++;
                }

                _ctx.SaveChanges();
                tx.Commit();
                return counter.LastNumber;
            }
        }

        private int GetMaxSequenceFromDocuments(string typeCode, int year)
        {
            var documents = _ctx.Documents
                .Include(d => d.DocumentTypeRef)
                .Where(d => d.RegistrationDate.HasValue
                            && d.RegistrationDate.Value.Year == year
                            && d.RegistrationNumber != null)
                .ToList();

            int max = 0;
            foreach (var doc in documents)
            {
                if (!string.Equals(ResolveTypeCode(doc.DocumentTypeRef), typeCode, StringComparison.Ordinal))
                    continue;

                var seq = ParseTrailingSequence(doc.RegistrationNumber);
                if (seq > max) max = seq;
            }
            return max;
        }

        private static string ResolveTypeCode(DocumentTypeRef typeRef)
        {
            if (typeRef == null) return null;
            var code = string.IsNullOrWhiteSpace(typeRef.ShortCode) ? typeRef.Name : typeRef.ShortCode;
            return code?.Trim();
        }

        private static int ParseTrailingSequence(string registrationNumber)
        {
            if (string.IsNullOrEmpty(registrationNumber)) return 0;
            int end = registrationNumber.Length - 1;
            while (end >= 0 && !char.IsDigit(registrationNumber[end])) end--;
            if (end < 0) return 0;
            int start = end;
            while (start - 1 >= 0 && char.IsDigit(registrationNumber[start - 1])) start--;
            var slice = registrationNumber.Substring(start, end - start + 1);
            return int.TryParse(slice, out var value) ? value : 0;
        }

        public IReadOnlyList<Department> ListDepartments()
            => _ctx.Departments.OrderBy(d => d.Name).ToList().AsReadOnly();

        public Department AddDepartment(Department department)
        {
            _ctx.Departments.Add(department);
            _ctx.SaveChanges();
            return department;
        }
    }
}
