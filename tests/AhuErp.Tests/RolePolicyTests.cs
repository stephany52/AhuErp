using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Покрывает приёмочную матрицу Bug #6 / Improvement #9 (Role × Module).
    /// 9 ролей × 17 модулей = 153 кейса по <see cref="RolePolicy.IsAllowed"/>
    /// плюс отдельные кейсы по <see cref="RolePolicy.CanAccessDocument"/>
    /// и поведенческим предикатам.
    /// </summary>
    public class RolePolicyTests
    {
        // ============================================================
        // ADMIN — полный доступ ко всем 17 модулям, включая админ-панель.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, true)]
        [InlineData(RolePolicy.Office, true)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, true)]
        [InlineData(RolePolicy.Warehouse, true)]
        [InlineData(RolePolicy.ItService, true)]
        [InlineData(RolePolicy.Fleet, true)]
        [InlineData(RolePolicy.Nomenclature, true)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, true)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, true)]
        [InlineData(RolePolicy.AdminPanel, true)]
        public void Admin_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.Admin, module));

        // ============================================================
        // MANAGER — всё кроме админ-панели.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, true)]
        [InlineData(RolePolicy.Office, true)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, true)]
        [InlineData(RolePolicy.Warehouse, true)]
        [InlineData(RolePolicy.ItService, true)]
        [InlineData(RolePolicy.Fleet, true)]
        [InlineData(RolePolicy.Nomenclature, true)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, true)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, true)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void Manager_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.Manager, module));

        // ============================================================
        // DEPUTY HEAD — матрица идентична Manager.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, true)]
        [InlineData(RolePolicy.Office, true)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, true)]
        [InlineData(RolePolicy.Warehouse, true)]
        [InlineData(RolePolicy.ItService, true)]
        [InlineData(RolePolicy.Fleet, true)]
        [InlineData(RolePolicy.Nomenclature, true)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, true)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, true)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void DeputyHead_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.DeputyHead, module));

        // ============================================================
        // ARCHIVIST — РКК, архив, номенклатура, журналы, поиск, отчёты,
        // оргструктура (read-only) и настройки уведомлений.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, false)]
        [InlineData(RolePolicy.Office, true)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, true)]
        [InlineData(RolePolicy.Warehouse, false)]
        [InlineData(RolePolicy.ItService, false)]
        [InlineData(RolePolicy.Fleet, false)]
        [InlineData(RolePolicy.Nomenclature, true)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, false)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, false)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void Archivist_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.Archivist, module));

        // ============================================================
        // TECHSUPPORT (ИТО) — Bug #6 ключ: должен видеть РКК и журналы.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, false)]
        [InlineData(RolePolicy.Office, true)]            // ⬅ Bug #6: РКК открыта.
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, false)]
        [InlineData(RolePolicy.Warehouse, true)]         // ⬅ узкий доступ (только списания по ИТ-заявкам).
        [InlineData(RolePolicy.ItService, true)]
        [InlineData(RolePolicy.Fleet, false)]
        [InlineData(RolePolicy.Nomenclature, false)]
        [InlineData(RolePolicy.Journals, true)]          // ⬅ Bug #6: журналы регистрации видит.
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, false)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, false)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void TechSupport_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.TechSupport, module));

        // ============================================================
        // WAREHOUSEMANAGER — ТМЦ + транспорт + РКК (для регистрации
        // хоз-заявок) + общие модули.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, false)]
        [InlineData(RolePolicy.Office, true)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, false)]
        [InlineData(RolePolicy.Warehouse, true)]
        [InlineData(RolePolicy.ItService, false)]
        [InlineData(RolePolicy.Fleet, true)]
        [InlineData(RolePolicy.Nomenclature, false)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, false)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, false)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void WarehouseManager_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.WarehouseManager, module));

        // ============================================================
        // CLERK (делопроизводитель, новая роль) — общий поток
        // документов без ТМЦ/транспорта/ИТО.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, true)]
        [InlineData(RolePolicy.Office, true)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, true)]
        [InlineData(RolePolicy.Warehouse, false)]
        [InlineData(RolePolicy.ItService, false)]
        [InlineData(RolePolicy.Fleet, false)]
        [InlineData(RolePolicy.Nomenclature, true)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, false)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, false)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void Clerk_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.Clerk, module));

        // ============================================================
        // HR ADMIN (новая роль) — оргструктура / замещения / личный
        // кабинет; документов в матрице нет, доступ к своим документам
        // через CanAccessDocument.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, false)]
        [InlineData(RolePolicy.Office, false)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, false)]
        [InlineData(RolePolicy.Warehouse, false)]
        [InlineData(RolePolicy.ItService, false)]
        [InlineData(RolePolicy.Fleet, false)]
        [InlineData(RolePolicy.Nomenclature, false)]
        [InlineData(RolePolicy.Journals, false)]
        [InlineData(RolePolicy.Search, false)]
        [InlineData(RolePolicy.Reports, false)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, true)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, false)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void HRAdmin_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.HRAdmin, module));

        // ============================================================
        // FLEET MANAGER (опциональная роль) — транспорт + базовые
        // справочные модули. Без склада.
        // ============================================================

        [Theory]
        [InlineData(RolePolicy.MyDesktop, true)]
        [InlineData(RolePolicy.Dashboard, false)]
        [InlineData(RolePolicy.Office, false)]
        [InlineData(RolePolicy.MyTasks, true)]
        [InlineData(RolePolicy.Archive, false)]
        [InlineData(RolePolicy.Warehouse, false)]
        [InlineData(RolePolicy.ItService, false)]
        [InlineData(RolePolicy.Fleet, true)]
        [InlineData(RolePolicy.Nomenclature, false)]
        [InlineData(RolePolicy.Journals, true)]
        [InlineData(RolePolicy.Search, true)]
        [InlineData(RolePolicy.Reports, true)]
        [InlineData(RolePolicy.OrgStructure, true)]
        [InlineData(RolePolicy.Substitutions, false)]
        [InlineData(RolePolicy.NotificationPrefs, true)]
        [InlineData(RolePolicy.AuditJournal, false)]
        [InlineData(RolePolicy.AdminPanel, false)]
        public void FleetManager_matrix(string module, bool expected)
            => Assert.Equal(expected, RolePolicy.IsAllowed(EmployeeRole.FleetManager, module));

        // ============================================================
        // Граничные кейсы матрицы.
        // ============================================================

        [Fact]
        public void IsAllowed_returns_false_for_unknown_module()
        {
            Assert.False(RolePolicy.IsAllowed(EmployeeRole.Admin, "NotAModule"));
            Assert.False(RolePolicy.IsAllowed(EmployeeRole.Manager, ""));
            Assert.False(RolePolicy.IsAllowed(EmployeeRole.Manager, null));
        }

        [Fact]
        public void AdminPanel_is_only_for_admin()
        {
            foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
            {
                bool expected = role == EmployeeRole.Admin;
                Assert.Equal(expected, RolePolicy.IsAllowed(role, RolePolicy.AdminPanel));
            }
        }

        [Fact]
        public void MyDesktop_is_for_every_role()
        {
            foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
            {
                Assert.True(RolePolicy.IsAllowed(role, RolePolicy.MyDesktop),
                    $"Роль {role} должна видеть «Мой стол».");
            }
        }

        [Fact]
        public void NotificationPrefs_is_for_every_role()
        {
            foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
            {
                Assert.True(RolePolicy.IsAllowed(role, RolePolicy.NotificationPrefs),
                    $"Роль {role} должна видеть настройки уведомлений.");
            }
        }

        [Fact]
        public void OrgStructure_is_visible_for_every_role()
        {
            // Read-only для всех; edit ограничен CanManageOrgStructure.
            foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
            {
                Assert.True(RolePolicy.IsAllowed(role, RolePolicy.OrgStructure),
                    $"Роль {role} должна как минимум читать оргструктуру.");
            }
        }

        [Fact]
        public void RolesAllowed_returns_admin_and_only_admin_for_AdminPanel()
        {
            var roles = RolePolicy.RolesAllowed(RolePolicy.AdminPanel);
            Assert.Single(roles);
            Assert.Equal(EmployeeRole.Admin, roles.Single());
        }

        [Fact]
        public void RolesAllowed_returns_all_roles_for_MyDesktop()
        {
            var roles = RolePolicy.RolesAllowed(RolePolicy.MyDesktop);
            Assert.Equal(System.Enum.GetValues(typeof(EmployeeRole)).Length, roles.Count);
        }

        [Fact]
        public void TechSupport_is_in_roles_allowed_for_office()
        {
            // Регрессионный тест Bug #6: ИТО обязан быть среди ролей,
            // которым открыт модуль РКК.
            var roles = RolePolicy.RolesAllowed(RolePolicy.Office);
            Assert.Contains(EmployeeRole.TechSupport, roles);
        }

        // ============================================================
        // Поведенческие предикаты Phase 8–12.
        // ============================================================

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanIssueResolution_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanIssueResolution(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanSign_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanSign(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanSignQualified_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanSignQualified(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, false)]
        [InlineData(EmployeeRole.DeputyHead, false)]
        [InlineData(EmployeeRole.HRAdmin, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanManageOrgStructure_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanManageOrgStructure(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.HRAdmin, true)]
        [InlineData(EmployeeRole.Archivist, true)]
        [InlineData(EmployeeRole.TechSupport, true)]
        [InlineData(EmployeeRole.WarehouseManager, true)]
        [InlineData(EmployeeRole.Clerk, true)]
        [InlineData(EmployeeRole.FleetManager, true)]
        public void CanReadOrgStructure_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanReadOrgStructure(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.HRAdmin, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanCreateSubstitution_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanCreateSubstitution(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.WarehouseManager, true)]
        [InlineData(EmployeeRole.FleetManager, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        public void CanCancelRelatedOperation_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanCancelRelatedOperation(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, false)]
        [InlineData(EmployeeRole.DeputyHead, false)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanRebuildSearchIndex_only_admin(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanRebuildSearchIndex(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, false)]
        [InlineData(EmployeeRole.DeputyHead, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        public void CanAccessAdminPanel_only_admin(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanAccessAdminPanel(role));

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.DeputyHead, true)]
        [InlineData(EmployeeRole.WarehouseManager, true)]
        [InlineData(EmployeeRole.TechSupport, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.Clerk, false)]
        [InlineData(EmployeeRole.HRAdmin, false)]
        [InlineData(EmployeeRole.FleetManager, false)]
        public void CanWriteOffInventory_matrix(EmployeeRole role, bool expected)
            => Assert.Equal(expected, RolePolicy.CanWriteOffInventory(role));

        [Theory]
        // Bug #6: TechSupport списывает только под ИТ-заявку.
        [InlineData(EmployeeRole.TechSupport, DocumentType.It, true)]
        [InlineData(EmployeeRole.TechSupport, DocumentType.General, false)]
        [InlineData(EmployeeRole.TechSupport, DocumentType.Office, false)]
        [InlineData(EmployeeRole.TechSupport, DocumentType.Internal, false)]
        // Manager и WarehouseManager — без ограничения по типу основания.
        [InlineData(EmployeeRole.Manager, DocumentType.General, true)]
        [InlineData(EmployeeRole.WarehouseManager, DocumentType.It, true)]
        [InlineData(EmployeeRole.Admin, DocumentType.Internal, true)]
        // У ролей без права списания — всё false.
        [InlineData(EmployeeRole.Clerk, DocumentType.General, false)]
        [InlineData(EmployeeRole.HRAdmin, DocumentType.It, false)]
        public void CanWriteOffInventoryWithBasis_matrix(EmployeeRole role, DocumentType basis, bool expected)
            => Assert.Equal(expected, RolePolicy.CanWriteOffInventoryWithBasis(role, basis));

        // ============================================================
        // CanAccessDocument — документ-уровневый доступ.
        // ============================================================

        [Fact]
        public void CanAccessDocument_admin_sees_everything_including_confidential()
        {
            var doc = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Confidential,
                AuthorId = 999,
            };
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.Admin, doc, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_manager_sees_internal_doc_but_not_confidential_unrelated()
        {
            var publicDoc = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            var confDoc = new Document
            {
                Id = 2,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Confidential,
                AuthorId = 999,
            };

            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.Manager, publicDoc, employeeId: 5));
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.Manager, confDoc, employeeId: 5));
        }

        [Fact]
        public void CanAccessDocument_manager_sees_confidential_when_author()
        {
            var doc = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Confidential,
                AuthorId = 5,
            };
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.Manager, doc, employeeId: 5));
        }

        [Fact]
        public void CanAccessDocument_techsupport_sees_all_it_tickets()
        {
            var ticket = new Document
            {
                Id = 1,
                Type = DocumentType.It,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, ticket, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_does_not_see_unrelated_general_doc()
        {
            // Bug #6 spec: TechSupport видит ИТ-заявки + только то, в чём он
            // сам участник, и не видит общие/конфиденциальные документы,
            // к которым его не приписали.
            var doc = new Document
            {
                Id = 1,
                Type = DocumentType.General,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, doc, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_sees_internal_memo_when_author()
        {
            var memo = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 1,
            };
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, memo, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_sees_internal_memo_when_executor()
        {
            var memo = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AssignedEmployeeId = 1,
            };
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, memo, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_sees_internal_memo_when_approver()
        {
            var memo = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
            };
            memo.Approvals.Add(new DocumentApproval { ApproverId = 1 });
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, memo, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_does_not_see_confidential_unrelated()
        {
            var memo = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Confidential,
                AuthorId = 999,
            };
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, memo, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_warehouse_sees_fleet_and_general_but_not_random_internal()
        {
            var fleet = new Document { Id = 1, Type = DocumentType.Fleet };
            var general = new Document { Id = 2, Type = DocumentType.General };
            var unrelated = new Document
            {
                Id = 3,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };

            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.WarehouseManager, fleet, employeeId: 5));
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.WarehouseManager, general, employeeId: 5));
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.WarehouseManager, unrelated, employeeId: 5));
        }

        [Fact]
        public void CanAccessDocument_fleetmanager_sees_only_fleet_or_own()
        {
            var fleet = new Document { Id = 1, Type = DocumentType.Fleet };
            var general = new Document { Id = 2, Type = DocumentType.General, AuthorId = 999 };
            var ownInternal = new Document
            {
                Id = 3,
                Type = DocumentType.Internal,
                AssignedEmployeeId = 5,
            };

            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.FleetManager, fleet, employeeId: 5));
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.FleetManager, general, employeeId: 5));
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.FleetManager, ownInternal, employeeId: 5));
        }

        [Fact]
        public void CanAccessDocument_hradmin_sees_only_own_documents()
        {
            var unrelated = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            var own = new Document
            {
                Id = 2,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 5,
            };
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.HRAdmin, unrelated, employeeId: 5));
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.HRAdmin, own, employeeId: 5));
        }

        [Fact]
        public void CanAccessDocument_clerk_sees_general_internal_flow_but_not_unrelated_confidential()
        {
            var publicDoc = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            var conf = new Document
            {
                Id = 2,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Confidential,
                AuthorId = 999,
            };
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.Clerk, publicDoc, employeeId: 5));
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.Clerk, conf, employeeId: 5));
        }

        [Fact]
        public void CanAccessDocument_returns_false_for_null_doc()
        {
            Assert.False(RolePolicy.CanAccessDocument(EmployeeRole.Admin, null, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_sees_internal_memo_when_task_executor()
        {
            var memo = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            memo.Tasks.Add(new DocumentTask { ExecutorId = 1 });
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, memo, employeeId: 1));
        }

        [Fact]
        public void CanAccessDocument_techsupport_sees_internal_memo_when_task_controller()
        {
            var memo = new Document
            {
                Id = 1,
                Type = DocumentType.Internal,
                AccessLevel = DocumentAccessLevel.Internal,
                AuthorId = 999,
            };
            memo.Tasks.Add(new DocumentTask { ControllerId = 1 });
            Assert.True(RolePolicy.CanAccessDocument(EmployeeRole.TechSupport, memo, employeeId: 1));
        }
    }
}
