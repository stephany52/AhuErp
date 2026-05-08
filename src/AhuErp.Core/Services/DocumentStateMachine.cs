using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Конечный автомат жизненного цикла документа (Phase 13). Описывает
    /// разрешённые переходы между значениями <see cref="DocumentStatus"/>:
    /// произвольные изменения запрещены, чтобы исключить сценарии типа
    /// «из Cancelled сразу в Completed» или «из Draft сразу в Archived».
    /// </summary>
    /// <remarks>
    /// Класс статический, без состояния — таблица переходов фиксирована во
    /// время компиляции. Использование:
    /// <code>
    /// if (!DocumentStateMachine.CanTransition(doc.Status, newStatus, actorRole))
    ///     throw new InvalidOperationException(...);
    /// DocumentStateMachine.Transition(doc, newStatus, actorRole, actorId, audit, reason);
    /// </code>
    /// Метод <see cref="Transition"/> сам пишет запись в <see cref="IAuditService"/>
    /// (тип <see cref="AuditActionType.StatusChanged"/>) — вызывающий код не должен
    /// дублировать логирование.
    /// </remarks>
    public static class DocumentStateMachine
    {
        /// <summary>
        /// Полная таблица допустимых переходов: ключ — исходный статус,
        /// значение — множество допустимых следующих статусов.
        /// </summary>
        private static readonly IReadOnlyDictionary<DocumentStatus, IReadOnlyCollection<DocumentStatus>>
            Transitions = BuildTransitions();

        /// <summary>
        /// Список ролей, которым разрешён конкретный переход. Если для перехода
        /// нет записи — он открыт всем (типичная ситуация для системных переходов
        /// внутри сервисов согласования/подписи).
        /// </summary>
        private static readonly IReadOnlyDictionary<(DocumentStatus From, DocumentStatus To), IReadOnlyCollection<EmployeeRole>>
            RoleConstraints = BuildRoleConstraints();

        /// <summary>Все статусы, в которые можно перейти из <paramref name="from"/>.</summary>
        public static IReadOnlyCollection<DocumentStatus> NextStates(DocumentStatus from)
        {
            return Transitions.TryGetValue(from, out var set)
                ? set
                : EmptySet;
        }

        /// <summary>True, если переход <paramref name="from"/> → <paramref name="to"/> допустим логически.</summary>
        public static bool CanTransition(DocumentStatus from, DocumentStatus to)
        {
            if (from == to) return false;
            return Transitions.TryGetValue(from, out var set) && set.Contains(to);
        }

        /// <summary>
        /// True, если переход допустим логически И роль <paramref name="role"/>
        /// уполномочена его выполнить.
        /// </summary>
        public static bool CanTransition(DocumentStatus from, DocumentStatus to, EmployeeRole role)
        {
            if (!CanTransition(from, to)) return false;
            // Admin всегда может — это удобно для админ-расследований/исправлений
            // ошибок делопроизводства; действие всё равно пишется в AuditLog.
            if (role == EmployeeRole.Admin) return true;
            if (RoleConstraints.TryGetValue((from, to), out var allowed))
            {
                return allowed.Contains(role);
            }
            // Если ограничение не задано — считаем, что переход системный
            // (выполняется сервисом, например ApprovalService) и роль не проверяем.
            return true;
        }

        /// <summary>True, если статус терминальный — выйти из него нельзя.</summary>
        public static bool IsTerminal(DocumentStatus status)
        {
            return !Transitions.TryGetValue(status, out var set) || set.Count == 0;
        }

        /// <summary>
        /// Перевести документ в новый статус с проверкой валидности и записью
        /// в журнал аудита. Бросает <see cref="InvalidOperationException"/>,
        /// если переход недопустим.
        /// </summary>
        /// <param name="doc">Документ. Поле <see cref="Document.Status"/> мутируется.</param>
        /// <param name="to">Целевой статус.</param>
        /// <param name="actorRole">
        /// Роль исполнителя — используется для проверки разрешения. Если
        /// <c>null</c>, проверяется только логическая валидность перехода
        /// (пригодно для системных переходов из сервисов).
        /// </param>
        /// <param name="actorId">Идентификатор сотрудника-инициатора (для аудита).</param>
        /// <param name="audit">Сервис аудита; обязателен.</param>
        /// <param name="reason">Опциональная причина/комментарий для журнала.</param>
        public static void Transition(
            Document doc,
            DocumentStatus to,
            EmployeeRole? actorRole,
            int? actorId,
            IAuditService audit,
            string reason = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (audit == null) throw new ArgumentNullException(nameof(audit));

            var from = doc.Status;
            if (from == to)
            {
                // Идемпотентность: повторная установка того же статуса не ломается,
                // но и не пишет аудит — это шум и нарушение «реальных» событий.
                return;
            }

            bool allowed = actorRole.HasValue
                ? CanTransition(from, to, actorRole.Value)
                : CanTransition(from, to);

            if (!allowed)
            {
                var roleSuffix = actorRole.HasValue ? $" (роль {actorRole.Value})" : string.Empty;
                throw new InvalidOperationException(
                    $"Недопустимый переход статуса документа: {from} → {to}{roleSuffix}.");
            }

            doc.Status = to;
            audit.Record(
                AuditActionType.StatusChanged,
                nameof(Document),
                doc.Id,
                actorId,
                oldValues: $"Status={from}",
                newValues: $"Status={to}",
                details: reason);
        }

        /// <summary>
        /// «Мягкий» вариант <see cref="Transition"/>: при недопустимом переходе
        /// возвращает <c>false</c> вместо исключения. Подходит для оппортунистического
        /// продвижения статуса из сервисного слоя — там не всегда известно
        /// исходное состояние, и хочется тихо пропустить, если документ
        /// уже не в подходящем статусе (например, ещё в Draft до StartApproval).
        /// </summary>
        public static bool TryTransition(
            Document doc,
            DocumentStatus to,
            EmployeeRole? actorRole,
            int? actorId,
            IAuditService audit,
            string reason = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (audit == null) throw new ArgumentNullException(nameof(audit));

            if (doc.Status == to) return true;

            bool allowed = actorRole.HasValue
                ? CanTransition(doc.Status, to, actorRole.Value)
                : CanTransition(doc.Status, to);

            if (!allowed) return false;

            Transition(doc, to, actorRole, actorId, audit, reason);
            return true;
        }

        // ----------------------------------------------------------------
        // Построение таблицы переходов.
        // ----------------------------------------------------------------

        private static readonly IReadOnlyCollection<DocumentStatus> EmptySet =
            new HashSet<DocumentStatus>();

        private static IReadOnlyDictionary<DocumentStatus, IReadOnlyCollection<DocumentStatus>>
            BuildTransitions()
        {
            // Источник правды по жизненному циклу документа в МКУ «АХУ».
            // Любое изменение здесь требует пересмотра тестов
            // DocumentStateMachineTests + миграции данных, если в БД остались
            // документы в исходном статусе.
            var map = new Dictionary<DocumentStatus, HashSet<DocumentStatus>>
            {
                // Черновик (в т.ч. legacy-значение New=0).
                [DocumentStatus.New] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.Registered,
                    DocumentStatus.OnApproval,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.Registered] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.OnApproval,
                    DocumentStatus.OnSigning,
                    DocumentStatus.OnExecution,
                    DocumentStatus.Completed,
                    DocumentStatus.Cancelled,
                    DocumentStatus.Archived,
                },

                [DocumentStatus.OnApproval] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.Approved,
                    DocumentStatus.Rejected,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.Approved] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.OnSigning,
                    DocumentStatus.OnExecution,
                    DocumentStatus.Completed,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.Rejected] = new HashSet<DocumentStatus>
                {
                    // Отклонённый документ возвращается автору на доработку
                    // → снова Черновик; либо отменяется.
                    DocumentStatus.New,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.OnSigning] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.Signed,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.Signed] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.OnExecution,
                    DocumentStatus.Completed,
                    DocumentStatus.Archived,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.OnExecution] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.OnHold,
                    DocumentStatus.Completed,
                    DocumentStatus.Cancelled,
                },

                // Legacy-статус. Поддерживаем переходы Phase 1-12, но также
                // считаем эквивалентом OnExecution для целей машины состояний.
                [DocumentStatus.InProgress] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.OnHold,
                    DocumentStatus.Completed,
                    DocumentStatus.Cancelled,
                    DocumentStatus.OnExecution,
                },

                [DocumentStatus.OnHold] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.OnExecution,
                    DocumentStatus.InProgress,
                    DocumentStatus.Completed,
                    DocumentStatus.Cancelled,
                },

                [DocumentStatus.Completed] = new HashSet<DocumentStatus>
                {
                    DocumentStatus.Archived,
                },

                // Терминальные.
                [DocumentStatus.Cancelled] = new HashSet<DocumentStatus>(),
                [DocumentStatus.Archived] = new HashSet<DocumentStatus>(),
            };

            // Уплотняем в IReadOnlyDictionary с IReadOnlyCollection-значениями.
            return map.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyCollection<DocumentStatus>)new HashSet<DocumentStatus>(kvp.Value));
        }

        private static IReadOnlyDictionary<(DocumentStatus, DocumentStatus), IReadOnlyCollection<EmployeeRole>>
            BuildRoleConstraints()
        {
            // Итог: «офисные» переходы делает делопроизводство и руководство;
            // переходы согласования/подписи — системные (роль не проверяется,
            // защиту делает соответствующий сервис); архивные — архивист.
            //
            // Если переход не упомянут в этой таблице — роль не проверяется.

            var officeFlow = new HashSet<EmployeeRole>
            {
                EmployeeRole.Manager,
                EmployeeRole.DeputyHead,
                EmployeeRole.Clerk,
            };
            var officeFlowExtended = new HashSet<EmployeeRole>(officeFlow)
            {
                EmployeeRole.TechSupport,
                EmployeeRole.WarehouseManager,
                EmployeeRole.FleetManager,
                EmployeeRole.Archivist,
            };
            var executors = new HashSet<EmployeeRole>(officeFlowExtended);
            var archiveOnly = new HashSet<EmployeeRole>
            {
                EmployeeRole.Manager,
                EmployeeRole.DeputyHead,
                EmployeeRole.Archivist,
            };

            var map = new Dictionary<(DocumentStatus, DocumentStatus), IReadOnlyCollection<EmployeeRole>>
            {
                // Регистрация — делопроизводитель/руководство, но также
                // и доменные ответственные (создал ИТ-заявку → сам же её
                // регистрирует через NomenclatureService).
                [(DocumentStatus.New, DocumentStatus.Registered)] = officeFlowExtended,
                [(DocumentStatus.New, DocumentStatus.Cancelled)] = officeFlowExtended,
                [(DocumentStatus.New, DocumentStatus.OnApproval)] = officeFlowExtended,

                [(DocumentStatus.Registered, DocumentStatus.OnApproval)] = officeFlowExtended,
                [(DocumentStatus.Registered, DocumentStatus.OnSigning)] = officeFlow,
                [(DocumentStatus.Registered, DocumentStatus.OnExecution)] = officeFlow,
                [(DocumentStatus.Registered, DocumentStatus.Completed)] = officeFlow,
                [(DocumentStatus.Registered, DocumentStatus.Cancelled)] = officeFlow,
                [(DocumentStatus.Registered, DocumentStatus.Archived)] = archiveOnly,

                // Approval-переходы — системные (через ApprovalService), но
                // ручную отмену маршрута выполняет руководство/делопроизводство.
                [(DocumentStatus.OnApproval, DocumentStatus.Cancelled)] = officeFlow,

                [(DocumentStatus.Approved, DocumentStatus.OnSigning)] = officeFlow,
                [(DocumentStatus.Approved, DocumentStatus.OnExecution)] = officeFlow,
                [(DocumentStatus.Approved, DocumentStatus.Completed)] = officeFlow,
                [(DocumentStatus.Approved, DocumentStatus.Cancelled)] = officeFlow,

                [(DocumentStatus.Rejected, DocumentStatus.New)] = officeFlowExtended,
                [(DocumentStatus.Rejected, DocumentStatus.Cancelled)] = officeFlow,

                [(DocumentStatus.OnSigning, DocumentStatus.Cancelled)] = officeFlow,

                [(DocumentStatus.Signed, DocumentStatus.OnExecution)] = officeFlow,
                [(DocumentStatus.Signed, DocumentStatus.Completed)] = officeFlow,
                [(DocumentStatus.Signed, DocumentStatus.Archived)] = archiveOnly,
                [(DocumentStatus.Signed, DocumentStatus.Cancelled)] = officeFlow,

                // Исполнители имеют право двигать своё в On Hold / Completed.
                [(DocumentStatus.OnExecution, DocumentStatus.OnHold)] = executors,
                [(DocumentStatus.OnExecution, DocumentStatus.Completed)] = executors,
                [(DocumentStatus.OnExecution, DocumentStatus.Cancelled)] = officeFlow,

                [(DocumentStatus.InProgress, DocumentStatus.OnHold)] = executors,
                [(DocumentStatus.InProgress, DocumentStatus.OnExecution)] = executors,
                [(DocumentStatus.InProgress, DocumentStatus.Completed)] = executors,
                [(DocumentStatus.InProgress, DocumentStatus.Cancelled)] = officeFlow,

                [(DocumentStatus.OnHold, DocumentStatus.OnExecution)] = executors,
                [(DocumentStatus.OnHold, DocumentStatus.InProgress)] = executors,
                [(DocumentStatus.OnHold, DocumentStatus.Completed)] = executors,
                [(DocumentStatus.OnHold, DocumentStatus.Cancelled)] = officeFlow,

                // Передача в архив — архивист или руководство.
                [(DocumentStatus.Completed, DocumentStatus.Archived)] = archiveOnly,
            };
            return map;
        }
    }
}
