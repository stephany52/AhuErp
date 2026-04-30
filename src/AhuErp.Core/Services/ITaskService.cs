using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис управления резолюциями и поручениями по документам.
    /// Реализует контроль исполнительской дисциплины: автор → исполнитель →
    /// контролёр, дедлайны, отчёты, делегирование.
    /// </summary>
    public interface ITaskService
    {
        DocumentResolution AddResolution(int documentId, int authorId, string text);

        DocumentTask CreateTask(
            int documentId,
            int authorId,
            int executorId,
            string description,
            DateTime deadline,
            int? resolutionId = null,
            int? controllerId = null,
            int? parentTaskId = null,
            string coExecutors = null,
            bool isCritical = false);

        DocumentTask UpdateStatus(int taskId, DocumentTaskStatus newStatus, int actorId, string reportText = null);

        DocumentTask Reassign(int taskId, int newExecutorId, int actorId, string reason = null);

        IReadOnlyList<DocumentTask> ListByDocument(int documentId);

        IReadOnlyList<DocumentResolution> ListResolutionsByAuthor(int authorId);

        /// <summary>Поручения, по которым сотрудник является исполнителем или контролёром.</summary>
        IReadOnlyList<DocumentTask> ListMyTasks(int employeeId, MyTasksScope scope = MyTasksScope.AsExecutor);

        /// <summary>Просроченные поручения по фильтру.</summary>
        IReadOnlyList<DocumentTask> ListOverdue(DateTime now, int? departmentId = null);

        /// <summary>Сводный отчёт по исполнительской дисциплине за период.</summary>
        ExecutionDisciplineReport BuildDisciplineReport(DateTime from, DateTime to);
    }

    public enum MyTasksScope
    {
        AsExecutor,
        AsController,
        AsAuthor,
        Any
    }

    /// <summary>
    /// Сводный отчёт по исполнительской дисциплине: общее количество поручений,
    /// количество выполненных в срок и просроченных, доля своевременного исполнения.
    /// </summary>
    public sealed class ExecutionDisciplineReport
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedOnTime { get; set; }
        public int CompletedLate { get; set; }
        public int Overdue { get; set; }
        public int InProgress { get; set; }
        public IReadOnlyList<EmployeeDisciplineRow> ByExecutor { get; set; }

        public double TimelyExecutionRate
            => TotalTasks == 0 ? 1.0 : (double)CompletedOnTime / TotalTasks;
    }

    public sealed class EmployeeDisciplineRow
    {
        public int ExecutorId { get; set; }
        public string ExecutorName { get; set; }
        public int Total { get; set; }
        public int CompletedOnTime { get; set; }
        public int CompletedLate { get; set; }
        public int Overdue { get; set; }
        public double TimelyExecutionRate
            => Total == 0 ? 1.0 : (double)CompletedOnTime / Total;
    }
}
