using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// A6 / B3 — RBAC для отмены связанных операций и перестроения
    /// поискового индекса.
    /// </summary>
    public class RolePolicyAcceptanceTests
    {
        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, true)]
        [InlineData(EmployeeRole.WarehouseManager, true)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        public void CanCancelRelatedOperation_matrix(EmployeeRole role, bool expected)
        {
            Assert.Equal(expected, RolePolicy.CanCancelRelatedOperation(role));
        }

        [Theory]
        [InlineData(EmployeeRole.Admin, true)]
        [InlineData(EmployeeRole.Manager, false)]
        [InlineData(EmployeeRole.Archivist, false)]
        [InlineData(EmployeeRole.TechSupport, false)]
        [InlineData(EmployeeRole.WarehouseManager, false)]
        public void CanRebuildSearchIndex_matrix(EmployeeRole role, bool expected)
        {
            Assert.Equal(expected, RolePolicy.CanRebuildSearchIndex(role));
        }

        [Fact]
        public void NotificationPrefs_module_is_accessible_to_every_role()
        {
            // A11 — настройки уведомлений доступны всем ролям, как фундамент
            // персональной самообслуживаемости.
            foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
            {
                Assert.True(RolePolicy.IsAllowed(role, RolePolicy.NotificationPrefs),
                    $"Роль {role} должна видеть модуль настроек уведомлений.");
            }
        }
    }
}
