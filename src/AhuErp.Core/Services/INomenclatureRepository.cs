using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Доступ к данным справочников номенклатуры дел и видов документов.</summary>
    public interface INomenclatureRepository
    {
        IReadOnlyList<NomenclatureCase> ListCases(int? year, bool activeOnly);
        NomenclatureCase GetCase(int id);
        NomenclatureCase AddCase(NomenclatureCase @case);
        NomenclatureCase UpdateCase(NomenclatureCase @case);

        IReadOnlyList<DocumentTypeRef> ListTypes(bool activeOnly);
        DocumentTypeRef GetType(int id);
        DocumentTypeRef AddType(DocumentTypeRef typeRef);
        DocumentTypeRef UpdateType(DocumentTypeRef typeRef);

        /// <summary>Атомарно выдать следующую последовательность для пары код вида/год.</summary>
        int GetNextSequence(string typeCode, int documentTypeRefId, int year);

        IReadOnlyList<Department> ListDepartments();
        Department AddDepartment(Department department);
    }
}
