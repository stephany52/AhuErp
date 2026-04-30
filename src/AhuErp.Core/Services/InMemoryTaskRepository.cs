using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory репозиторий резолюций и поручений (для тестов).</summary>
    public sealed class InMemoryTaskRepository : ITaskRepository
    {
        private readonly List<DocumentResolution> _resolutions = new List<DocumentResolution>();
        private readonly List<DocumentTask> _tasks = new List<DocumentTask>();
        private int _nextResolutionId = 1;
        private int _nextTaskId = 1;

        public DocumentResolution AddResolution(DocumentResolution resolution)
        {
            if (resolution.Id == 0) resolution.Id = _nextResolutionId++;
            else _nextResolutionId = System.Math.Max(_nextResolutionId, resolution.Id + 1);
            _resolutions.Add(resolution);
            return resolution;
        }

        public DocumentResolution GetResolution(int id) => _resolutions.FirstOrDefault(r => r.Id == id);

        public IReadOnlyList<DocumentResolution> ListResolutionsByAuthor(int authorId)
            => _resolutions.Where(r => r.AuthorId == authorId)
                           .OrderByDescending(r => r.IssuedAt)
                           .ToList()
                           .AsReadOnly();

        public DocumentTask AddTask(DocumentTask task)
        {
            if (task.Id == 0) task.Id = _nextTaskId++;
            else _nextTaskId = System.Math.Max(_nextTaskId, task.Id + 1);
            _tasks.Add(task);
            return task;
        }

        public DocumentTask GetTask(int id) => _tasks.FirstOrDefault(t => t.Id == id);

        public void UpdateTask(DocumentTask task)
        {
            var idx = _tasks.FindIndex(t => t.Id == task.Id);
            if (idx >= 0) _tasks[idx] = task;
        }

        public IReadOnlyList<DocumentTask> ListByDocument(int documentId)
            => _tasks.Where(t => t.DocumentId == documentId).ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListByExecutor(int executorId)
            => _tasks.Where(t => t.ExecutorId == executorId).ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListByController(int controllerId)
            => _tasks.Where(t => t.ControllerId == controllerId).ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListByAuthor(int authorId)
            => _tasks.Where(t => t.AuthorId == authorId).ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListAll() => _tasks.AsReadOnly();
    }
}
