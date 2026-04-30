using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-репозиторий резолюций и поручений.</summary>
    public sealed class EfTaskRepository : ITaskRepository
    {
        private readonly AhuDbContext _ctx;

        public EfTaskRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public DocumentResolution AddResolution(DocumentResolution resolution)
        {
            _ctx.DocumentResolutions.Add(resolution);
            _ctx.SaveChanges();
            return resolution;
        }

        public DocumentResolution GetResolution(int id) => _ctx.DocumentResolutions.Find(id);

        public IReadOnlyList<DocumentResolution> ListResolutionsByAuthor(int authorId)
            => _ctx.DocumentResolutions.Include(r => r.Document)
                .Where(r => r.AuthorId == authorId)
                .OrderByDescending(r => r.IssuedAt)
                .ToList()
                .AsReadOnly();

        public DocumentTask AddTask(DocumentTask task)
        {
            _ctx.DocumentTasks.Add(task);
            _ctx.SaveChanges();
            return task;
        }

        public DocumentTask GetTask(int id) => _ctx.DocumentTasks.Find(id);

        public void UpdateTask(DocumentTask task)
        {
            if (_ctx.Entry(task).State == EntityState.Detached)
            {
                _ctx.DocumentTasks.Attach(task);
                _ctx.Entry(task).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
        }

        public IReadOnlyList<DocumentTask> ListByDocument(int documentId)
            => _ctx.DocumentTasks.Include(t => t.Executor)
                .Where(t => t.DocumentId == documentId)
                .ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListByExecutor(int executorId)
            => _ctx.DocumentTasks.Include(t => t.Document)
                .Where(t => t.ExecutorId == executorId)
                .ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListByController(int controllerId)
            => _ctx.DocumentTasks.Include(t => t.Document)
                .Where(t => t.ControllerId == controllerId)
                .ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListByAuthor(int authorId)
            => _ctx.DocumentTasks.Include(t => t.Document)
                .Where(t => t.AuthorId == authorId)
                .ToList().AsReadOnly();

        public IReadOnlyList<DocumentTask> ListAll()
            => _ctx.DocumentTasks.Include(t => t.Executor).ToList().AsReadOnly();
    }
}
