using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="ITaskService"/>. Все мутации логируются в журнале
    /// аудита; события завершения поручений (для интеграции с АХД-операциями)
    /// делегируются <see cref="IWorkflowService"/>, если он зарегистрирован.
    /// </summary>
    public sealed class TaskService : ITaskService
    {
        private readonly ITaskRepository _tasks;
        private readonly IDocumentRepository _documents;
        private readonly IAuditService _audit;
        private readonly IWorkflowService _workflow;
        private readonly ISubstitutionService _substitution;
        private readonly IDelegationRepository _delegations;
        private readonly INotificationService _notifications;
        private readonly IEmployeeRepository _employees;

        public TaskService(
            ITaskRepository tasks,
            IDocumentRepository documents,
            IAuditService audit,
            IWorkflowService workflow = null,
            ISubstitutionService substitution = null,
            IDelegationRepository delegations = null,
            INotificationService notifications = null,
            IEmployeeRepository employees = null)
        {
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _workflow = workflow;
            _substitution = substitution;
            _delegations = delegations;
            _notifications = notifications;
            _employees = employees;
        }

        // Bug #4. Распознаём упоминания исполнителя в тексте резолюции по
        // традиционному формату делопроизводства «@ФамилияИО» (например,
        // «@ИвановИИ» — Иванов И.И.). Берём заглавную кириллическую/латинскую
        // букву как начало упоминания после @ и далее буквы (без пробелов).
        // Совпадение ищется по сжатому виду ФИО сотрудника:
        //   "Иванов Иван Иванович" → "ИвановИИ".
        private static readonly Regex MentionRegex = new Regex(
            @"@([\p{L}]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public DocumentResolution AddResolution(int documentId, int authorId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Текст резолюции обязателен.", nameof(text));
            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");

            var resolution = new DocumentResolution
            {
                DocumentId = doc.Id,
                AuthorId = authorId,
                Text = text,
                IssuedAt = DateTime.Now
            };
            resolution = _tasks.AddResolution(resolution);
            _audit.Record(AuditActionType.ResolutionIssued, nameof(DocumentResolution), resolution.Id, authorId,
                newValues: $"DocumentId={doc.Id}; Length={text.Length}");

            // Уведомления упомянутым исполнителям (in-app + e-mail в зависимости
            // от индивидуальных предпочтений). Обработка best-effort: при
            // отсутствии EmployeeRepository или NotificationService резолюция
            // всё равно создаётся.
            NotifyMentionedExecutors(resolution, doc, authorId);
            return resolution;
        }

        private void NotifyMentionedExecutors(DocumentResolution resolution, Document doc, int authorId)
        {
            if (_notifications == null || _employees == null) return;

            var notified = new HashSet<int>();
            foreach (Match m in MentionRegex.Matches(resolution.Text ?? string.Empty))
            {
                var token = m.Groups[1].Value;
                var employee = ResolveEmployeeByMention(token);
                if (employee == null || employee.Id == authorId) continue;
                if (!notified.Add(employee.Id)) continue;

                _notifications.Create(
                    employee.Id,
                    NotificationKind.ResolutionAdded,
                    $"Резолюция по документу #{doc.Id}",
                    $"Документ {doc.RegistrationNumber ?? "#" + doc.Id} «{doc.Title}». {resolution.Text}",
                    docId: doc.Id);
            }
        }

        private Employee ResolveEmployeeByMention(string mention)
        {
            if (string.IsNullOrWhiteSpace(mention)) return null;
            // Кандидаты: все активные сотрудники. Сжатый вид ФИО сравниваем
            // case-insensitive: «ИвановИИ» ↔ "Иванов Иван Иванович".
            foreach (var emp in _employees.ListAll())
            {
                if (emp == null || string.IsNullOrWhiteSpace(emp.FullName)) continue;
                var compact = CompactFullName(emp.FullName);
                if (compact.Length == 0) continue;
                if (string.Equals(compact, mention, StringComparison.OrdinalIgnoreCase))
                    return emp;
            }
            return null;
        }

        private static string CompactFullName(string fullName)
        {
            // "Иванов Иван Иванович" → "ИвановИИ"
            // "Петрова Анна" → "ПетроваА"
            var parts = fullName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return string.Empty;
            var sb = new System.Text.StringBuilder(parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length > 0) sb.Append(parts[i][0]);
            }
            return sb.ToString();
        }

        public DocumentTask CreateTask(
            int documentId,
            int authorId,
            int executorId,
            string description,
            DateTime deadline,
            int? resolutionId = null,
            int? controllerId = null,
            int? parentTaskId = null,
            string coExecutors = null,
            bool isCritical = false)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Текст поручения обязателен.", nameof(description));
            if (deadline <= DateTime.Now.Date)
                throw new ArgumentException("Срок исполнения должен быть в будущем.", nameof(deadline));

            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");
            if (executorId <= 0) throw new ArgumentException("Исполнитель обязателен.");

            // Phase 11: при наличии активного замещения для исполнителя — задача
            // фактически достаётся заместителю. Признак замещения фиксируется
            // в журнале TaskDelegations (если зарегистрирован репозиторий) и
            // в аудите.
            int actualExecutorId = executorId;
            if (_substitution != null)
            {
                actualExecutorId = _substitution.ResolveActualExecutor(
                    executorId, DateTime.Now, SubstitutionScope.TasksOnly);
            }

            var task = new DocumentTask
            {
                DocumentId = doc.Id,
                ResolutionId = resolutionId,
                ParentTaskId = parentTaskId,
                AuthorId = authorId,
                ExecutorId = actualExecutorId,
                ControllerId = controllerId,
                CoExecutors = coExecutors,
                Description = description,
                CreatedAt = DateTime.Now,
                Deadline = deadline,
                Status = DocumentTaskStatus.New,
                IsCritical = isCritical
            };
            task = _tasks.AddTask(task);

            _audit.Record(AuditActionType.TaskAssigned, nameof(DocumentTask), task.Id, authorId,
                newValues: $"DocumentId={doc.Id}; ExecutorId={actualExecutorId}; Deadline={deadline:o}");

            if (actualExecutorId != executorId && _delegations != null)
            {
                var delegation = _delegations.Add(new TaskDelegation
                {
                    TaskId = task.Id,
                    FromEmployeeId = executorId,
                    ToEmployeeId = actualExecutorId,
                    DelegatedAt = DateTime.Now,
                    Comment = "По замещению"
                });
                _audit.Record(AuditActionType.TaskDelegated, nameof(DocumentTask), task.Id, authorId,
                    newValues: $"From={executorId}; To={actualExecutorId}; Reason=Substitution",
                    details: $"DelegationId={delegation.Id}");
            }

            // Phase 9: уведомление фактическому исполнителю.
            _notifications?.Create(actualExecutorId, NotificationKind.TaskAssigned,
                $"Назначено поручение #{task.Id}",
                $"Документ #{doc.Id}. Срок: {deadline:dd.MM.yyyy HH:mm}. {description}",
                docId: doc.Id, taskId: task.Id);

            return task;
        }

        public DocumentTask UpdateStatus(int taskId, DocumentTaskStatus newStatus, int actorId, string reportText = null)
        {
            var task = _tasks.GetTask(taskId)
                ?? throw new InvalidOperationException($"Поручение #{taskId} не найдено.");
            var oldStatus = task.Status;
            task.Status = newStatus;
            task.ReportText = reportText ?? task.ReportText;
            if (newStatus == DocumentTaskStatus.Completed)
            {
                task.CompletedAt = DateTime.Now;
            }
            _tasks.UpdateTask(task);

            _audit.Record(
                newStatus == DocumentTaskStatus.Completed
                    ? AuditActionType.TaskCompleted
                    : AuditActionType.Updated,
                nameof(DocumentTask), task.Id, actorId,
                oldValues: $"Status={oldStatus}",
                newValues: $"Status={newStatus}");

            // Завершение поручения может автоматически порождать связанную
            // хозяйственную операцию (списание ТМЦ, путевой лист и т.д.) —
            // делегируем интеграционному слою.
            if (newStatus == DocumentTaskStatus.Completed)
            {
                _workflow?.OnTaskCompleted(task, actorId);
            }
            return task;
        }

        public DocumentTask Reassign(int taskId, int newExecutorId, int actorId, string reason = null)
        {
            var task = _tasks.GetTask(taskId)
                ?? throw new InvalidOperationException($"Поручение #{taskId} не найдено.");
            var oldExecutorId = task.ExecutorId;
            task.ExecutorId = newExecutorId;
            _tasks.UpdateTask(task);
            _audit.Record(AuditActionType.TaskReassigned, nameof(DocumentTask), task.Id, actorId,
                oldValues: $"ExecutorId={oldExecutorId}",
                newValues: $"ExecutorId={newExecutorId}",
                details: reason);
            return task;
        }

        public IReadOnlyList<DocumentTask> ListByDocument(int documentId)
            => _tasks.ListByDocument(documentId);

        public IReadOnlyList<DocumentResolution> ListResolutionsByAuthor(int authorId)
            => _tasks.ListResolutionsByAuthor(authorId);

        public IReadOnlyList<DocumentResolution> ListResolutionsByDocument(int documentId)
            => _tasks.ListResolutionsByDocument(documentId);

        public IReadOnlyList<DocumentTask> ListMyTasks(int employeeId, MyTasksScope scope = MyTasksScope.AsExecutor)
        {
            switch (scope)
            {
                case MyTasksScope.AsExecutor: return _tasks.ListByExecutor(employeeId);
                case MyTasksScope.AsController: return _tasks.ListByController(employeeId);
                case MyTasksScope.AsAuthor: return _tasks.ListByAuthor(employeeId);
                default:
                    var all = new List<DocumentTask>();
                    all.AddRange(_tasks.ListByExecutor(employeeId));
                    all.AddRange(_tasks.ListByController(employeeId)
                        .Where(t => all.All(a => a.Id != t.Id)));
                    all.AddRange(_tasks.ListByAuthor(employeeId)
                        .Where(t => all.All(a => a.Id != t.Id)));
                    return all.AsReadOnly();
            }
        }

        public IReadOnlyList<DocumentTask> ListOverdue(DateTime now, int? departmentId = null)
        {
            IEnumerable<DocumentTask> overdue = _tasks.ListAll().Where(t => t.IsOverdue(now));
            if (departmentId.HasValue)
            {
                // Привязка отдела к поручению идёт через дело номенклатуры
                // (NomenclatureCase.DepartmentId) родительского документа —
                // другого источника отдела у поручения сейчас нет.
                var deptId = departmentId.Value;
                overdue = overdue.Where(t => t.Document != null
                                             && t.Document.NomenclatureCase != null
                                             && t.Document.NomenclatureCase.DepartmentId == deptId);
            }
            return overdue.OrderBy(t => t.Deadline).ToList().AsReadOnly();
        }

        public ExecutionDisciplineReport BuildDisciplineReport(DateTime from, DateTime to)
        {
            if (to < from) throw new ArgumentException("Дата окончания периода раньше начала.");
            // Просрочка определяется относительно текущего момента (а не конца
            // отчётного периода): иначе ещё не наступившие сроки в будущем
            // ошибочно учитывались бы как пропущенные.
            // Используем локальное время — Deadline хранится в local time из UI,
            // и сравнение с UtcNow давало сдвиг до часов часового пояса.
            var now = DateTime.Now;
            var inRange = _tasks.ListAll()
                .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
                .ToList();

            int onTime = inRange.Count(t => t.Status == DocumentTaskStatus.Completed
                                            && t.CompletedAt.HasValue
                                            && t.CompletedAt.Value <= t.Deadline);
            int late = inRange.Count(t => t.Status == DocumentTaskStatus.Completed
                                          && t.CompletedAt.HasValue
                                          && t.CompletedAt.Value > t.Deadline);
            int overdue = inRange.Count(t => t.Status != DocumentTaskStatus.Completed
                                             && t.Status != DocumentTaskStatus.Cancelled
                                             && t.Deadline < now);
            int inProgress = inRange.Count(t => t.Status == DocumentTaskStatus.InProgress
                                                || t.Status == DocumentTaskStatus.New
                                                || t.Status == DocumentTaskStatus.OnReview);

            var byExecutor = inRange
                .GroupBy(t => t.ExecutorId)
                .Select(g => new EmployeeDisciplineRow
                {
                    ExecutorId = g.Key,
                    ExecutorName = g.First().Executor?.FullName ?? $"#{g.Key}",
                    Total = g.Count(),
                    CompletedOnTime = g.Count(t => t.Status == DocumentTaskStatus.Completed
                                                   && t.CompletedAt.HasValue
                                                   && t.CompletedAt.Value <= t.Deadline),
                    CompletedLate = g.Count(t => t.Status == DocumentTaskStatus.Completed
                                                 && t.CompletedAt.HasValue
                                                 && t.CompletedAt.Value > t.Deadline),
                    Overdue = g.Count(t => t.Status != DocumentTaskStatus.Completed
                                           && t.Status != DocumentTaskStatus.Cancelled
                                           && t.Deadline < now)
                })
                .OrderBy(r => r.ExecutorName)
                .ToList();

            return new ExecutionDisciplineReport
            {
                From = from,
                To = to,
                TotalTasks = inRange.Count,
                CompletedOnTime = onTime,
                CompletedLate = late,
                Overdue = overdue,
                InProgress = inProgress,
                ByExecutor = byExecutor.AsReadOnly()
            };
        }
    }
}
