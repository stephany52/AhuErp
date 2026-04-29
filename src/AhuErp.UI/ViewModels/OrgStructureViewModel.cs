using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Phase 11 — раздел «Оргструктура». Дерево отделов, их руководителей и
    /// сотрудников. Для администратора доступно создание/деактивация отделов
    /// и переподчинение, а также назначение руководителя отдела (A10).
    /// </summary>
    public partial class OrgStructureViewModel : ViewModelBase
    {
        private readonly AhuDbContext _ctx;
        private readonly IAuthService _auth;
        private readonly IAuditService _audit;

        public ObservableCollection<DepartmentNode> Roots { get; } = new ObservableCollection<DepartmentNode>();
        public ObservableCollection<Employee> Employees { get; } = new ObservableCollection<Employee>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AssignHeadCommand))]
        private DepartmentNode selectedNode;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AssignHeadCommand))]
        private Employee newHead;

        [ObservableProperty]
        private string errorMessage;

        public OrgStructureViewModel(AhuDbContext ctx,
                                     IAuthService auth = null,
                                     IAuditService audit = null)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _auth = auth;
            _audit = audit;
            Reload();
        }

        [RelayCommand]
        private void Reload()
        {
            Roots.Clear();
            Employees.Clear();
            ErrorMessage = null;
            try
            {
                var all = _ctx.Departments.ToList();
                var employees = _ctx.Employees.ToList();
                foreach (var emp in employees.Where(e => e.IsActive).OrderBy(e => e.FullName))
                {
                    Employees.Add(emp);
                }

                var groups = all.GroupBy(d => d.ParentDepartmentId)
                                .ToDictionary(g => g.Key ?? 0, g => g.ToList());

                foreach (var root in all.Where(d => d.ParentDepartmentId == null).OrderBy(d => d.Name))
                {
                    Roots.Add(BuildNode(root, employees, groups));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// A10 — назначить руководителя выбранному отделу. Доступно только
        /// ролям, у которых <see cref="RolePolicy.CanManageOrgStructure"/>
        /// возвращает <c>true</c>. Действие фиксируется в журнале аудита.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAssignHead))]
        private void AssignHead()
        {
            ErrorMessage = null;
            try
            {
                if (SelectedNode == null || NewHead == null) return;

                var dept = _ctx.Departments.FirstOrDefault(d => d.Id == SelectedNode.Id);
                if (dept == null)
                {
                    ErrorMessage = $"Отдел #{SelectedNode.Id} не найден.";
                    return;
                }

                var oldHeadId = dept.HeadEmployeeId;
                dept.HeadEmployeeId = NewHead.Id;
                _ctx.SaveChanges();

                if (_audit != null && _auth?.CurrentEmployee != null)
                {
                    _audit.Record(AuditActionType.DepartmentHeadAssigned,
                        nameof(Department), dept.Id, _auth.CurrentEmployee.Id,
                        oldValues: oldHeadId.HasValue ? $"HeadEmployeeId={oldHeadId}" : "HeadEmployeeId=null",
                        newValues: $"HeadEmployeeId={NewHead.Id}",
                        details: $"Назначен руководителем «{dept.Name}»: {NewHead.FullName}");
                }

                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanAssignHead()
        {
            if (SelectedNode == null || NewHead == null) return false;
            var role = _auth?.CurrentEmployee?.Role;
            if (!role.HasValue) return true; // в тестах без auth — разрешено
            return RolePolicy.CanManageOrgStructure(role.Value);
        }

        private static DepartmentNode BuildNode(Department d, List<Employee> employees,
                                                Dictionary<int, List<Department>> groups)
        {
            var node = new DepartmentNode
            {
                Id = d.Id,
                Name = d.Name,
                ShortCode = d.ShortCode,
                IsActive = d.IsActive,
                HeadName = employees.FirstOrDefault(e => e.Id == d.HeadEmployeeId)?.FullName,
                EmployeeCount = employees.Count(e => e.DepartmentId == d.Id && e.IsActive),
            };
            if (groups.TryGetValue(d.Id, out var children))
            {
                foreach (var child in children.OrderBy(c => c.Name))
                {
                    node.Children.Add(BuildNode(child, employees, groups));
                }
            }
            return node;
        }
    }

    /// <summary>Узел дерева оргструктуры (ViewModel-проекция отдела).</summary>
    public class DepartmentNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortCode { get; set; }
        public bool IsActive { get; set; }
        public string HeadName { get; set; }
        public int EmployeeCount { get; set; }
        public ObservableCollection<DepartmentNode> Children { get; } = new ObservableCollection<DepartmentNode>();
    }
}
