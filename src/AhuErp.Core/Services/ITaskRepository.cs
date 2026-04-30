using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Доступ к данным резолюций и поручений по документам.</summary>
    public interface ITaskRepository
    {
        DocumentResolution AddResolution(DocumentResolution resolution);
        DocumentResolution GetResolution(int id);
        IReadOnlyList<DocumentResolution> ListResolutionsByAuthor(int authorId);

        DocumentTask AddTask(DocumentTask task);
        DocumentTask GetTask(int id);
        void UpdateTask(DocumentTask task);

        IReadOnlyList<DocumentTask> ListByDocument(int documentId);
        IReadOnlyList<DocumentTask> ListByExecutor(int executorId);
        IReadOnlyList<DocumentTask> ListByController(int controllerId);
        IReadOnlyList<DocumentTask> ListByAuthor(int authorId);
        IReadOnlyList<DocumentTask> ListAll();
    }
}
