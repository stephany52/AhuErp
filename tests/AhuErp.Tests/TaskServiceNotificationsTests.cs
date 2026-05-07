using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 9 — интеграция <see cref="TaskService"/> + <see cref="NotificationService"/>:
    /// при создании поручения исполнителю должно прилететь in-app уведомление.
    /// </summary>
    public class TaskServiceNotificationsTests
    {
        [Fact]
        public void CreateTask_sends_TaskAssigned_to_executor()
        {
            var docs = new InMemoryDocumentRepository();
            var tasks = new InMemoryTaskRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var notifRepo = new InMemoryNotificationRepository();
            var employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Author", Role = EmployeeRole.Manager },
                new Employee { Id = 2, FullName = "Exec",   Role = EmployeeRole.TechSupport, Email = "exec@bmr" },
            });
            var audit = new AuditService(auditRepo);
            var notifications = new NotificationService(notifRepo, employees, tasks, audit);
            var service = new TaskService(tasks, docs, audit,
                workflow: null, substitution: null, delegations: null,
                notifications: notifications);

            var doc = new Document
            {
                Title = "Сл. зап.",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            docs.Add(doc);

            service.CreateTask(doc.Id, authorId: 1, executorId: 2,
                description: "Подготовить", deadline: DateTime.Now.AddDays(3));

            var inbox = notifications.ListForUser(2, unreadOnly: true);
            Assert.Single(inbox);
            Assert.Equal(NotificationKind.TaskAssigned, inbox[0].Kind);
            Assert.Equal(doc.Id, inbox[0].RelatedDocumentId);
        }

        // Bug #4 — резолюция руководителя с упоминанием исполнителя
        // «@ФамилияИО» должна порождать in-app уведомление и запись AuditLog.
        [Fact]
        public void IssueResolution_creates_resolution_and_notifies_mentioned_executor()
        {
            var docs = new InMemoryDocumentRepository();
            var tasks = new InMemoryTaskRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var notifRepo = new InMemoryNotificationRepository();
            var employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Сидоров Сидор Сидорович", Role = EmployeeRole.Manager },
                new Employee { Id = 2, FullName = "Иванов Иван Иванович",    Role = EmployeeRole.TechSupport, Email = "ivanov@bmr" },
            });
            var audit = new AuditService(auditRepo);
            var notifications = new NotificationService(notifRepo, employees, tasks, audit);
            var service = new TaskService(tasks, docs, audit,
                workflow: null, substitution: null, delegations: null,
                notifications: notifications, employees: employees);

            var doc = new Document
            {
                Title = "Служебная записка",
                Type = DocumentType.Internal,
                RegistrationNumber = "СЗ-2026-00001",
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            docs.Add(doc);

            var resolution = service.AddResolution(doc.Id, authorId: 1,
                text: "@ИвановИИ — рассмотреть и подготовить ответ к 10.06.");

            // Резолюция сохранена и связана с документом.
            Assert.NotEqual(0, resolution.Id);
            Assert.Equal(doc.Id, resolution.DocumentId);

            // Аудит-запись о наложении резолюции.
            var auditLogs = audit.Query(new AuditQueryFilter { ActionType = AuditActionType.ResolutionIssued });
            Assert.Single(auditLogs);
            Assert.Equal(resolution.Id, auditLogs[0].EntityId);

            // Упомянутый исполнитель получил уведомление.
            var inbox = notifications.ListForUser(2, unreadOnly: true);
            Assert.Single(inbox);
            Assert.Equal(NotificationKind.TaskAssigned, inbox[0].Kind);
            Assert.Equal(doc.Id, inbox[0].RelatedDocumentId);
            Assert.Contains("Резолюция", inbox[0].Title);
        }

        // Если в тексте резолюции нет упоминаний — никому уведомлений не шлём.
        [Fact]
        public void IssueResolution_without_mentions_does_not_notify()
        {
            var docs = new InMemoryDocumentRepository();
            var tasks = new InMemoryTaskRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var notifRepo = new InMemoryNotificationRepository();
            var employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Сидоров Сидор Сидорович", Role = EmployeeRole.Manager },
                new Employee { Id = 2, FullName = "Иванов Иван Иванович",    Role = EmployeeRole.TechSupport, Email = "ivanov@bmr" },
            });
            var audit = new AuditService(auditRepo);
            var notifications = new NotificationService(notifRepo, employees, tasks, audit);
            var service = new TaskService(tasks, docs, audit,
                workflow: null, substitution: null, delegations: null,
                notifications: notifications, employees: employees);

            var doc = new Document
            {
                Title = "Служебная записка",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            docs.Add(doc);

            service.AddResolution(doc.Id, authorId: 1, text: "В работу.");

            Assert.Empty(notifications.ListForUser(2, unreadOnly: true));
        }
    }
}
