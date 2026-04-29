namespace AhuErp.Core.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// Phase 11 — иерархия отделов, замещения и делегирование поручений.
    /// Добавляет:
    /// <list type="bullet">
    ///   <item><description><c>Departments.ParentDepartmentId</c> (self-referencing FK,
    ///     ON DELETE NO ACTION) и <c>Departments.HeadEmployeeId</c>.</description></item>
    ///   <item><description><c>Employees.DepartmentId</c>, <c>Employees.IsActive</c>,
    ///     <c>Employees.TerminatedAt</c>, <c>Employees.Email</c> (используется
    ///     SmtpEmailGateway в Phase 9).</description></item>
    ///   <item><description><c>Substitutions</c> — журнал замещений с областью
    ///     <c>SubstitutionScope</c> (TasksOnly / ApprovalsOnly / Full).</description></item>
    ///   <item><description><c>TaskDelegations</c> — история делегирования поручений
    ///     (ручного и автоматического по замещению).</description></item>
    /// </list>
    /// Все FK на <c>Employees</c> идут с <c>cascadeDelete: false</c>, чтобы
    /// удаление сотрудника не «вычищало» исторический журнал.
    /// </summary>
    public partial class AddOrgAndSubstitution : DbMigration
    {
        public override void Up()
        {
            // ---------- 1. Departments — иерархия и руководитель ---------------
            AddColumn("dbo.Departments", "ParentDepartmentId", c => c.Int());
            AddColumn("dbo.Departments", "HeadEmployeeId", c => c.Int());

            CreateIndex("dbo.Departments", "ParentDepartmentId");
            CreateIndex("dbo.Departments", "HeadEmployeeId");

            // Self-referencing FK без каскада, чтобы удалить родительский
            // отдел можно было только после удаления потомков (см. также
            // OrgStructureService с явным запретом на удаление с детьми).
            AddForeignKey("dbo.Departments", "ParentDepartmentId", "dbo.Departments", "Id", cascadeDelete: false);
            AddForeignKey("dbo.Departments", "HeadEmployeeId", "dbo.Employees", "Id", cascadeDelete: false);

            // ---------- 2. Employees — отдел, активность, e-mail --------------
            AddColumn("dbo.Employees", "Email", c => c.String(maxLength: 256));
            AddColumn("dbo.Employees", "DepartmentId", c => c.Int());
            AddColumn("dbo.Employees", "IsActive", c => c.Boolean(nullable: false, defaultValue: true));
            AddColumn("dbo.Employees", "TerminatedAt", c => c.DateTime());

            CreateIndex("dbo.Employees", "DepartmentId");
            AddForeignKey("dbo.Employees", "DepartmentId", "dbo.Departments", "Id", cascadeDelete: false);

            // ---------- 3. Substitutions ---------------------------------------
            CreateTable(
                "dbo.Substitutions",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    OriginalEmployeeId = c.Int(nullable: false),
                    SubstituteEmployeeId = c.Int(nullable: false),
                    From = c.DateTime(nullable: false),
                    To = c.DateTime(nullable: false),
                    Scope = c.Int(nullable: false),
                    Reason = c.String(maxLength: 512),
                    IsActive = c.Boolean(nullable: false),
                    CreatedById = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.OriginalEmployeeId)
                .ForeignKey("dbo.Employees", t => t.SubstituteEmployeeId)
                .Index(t => t.OriginalEmployeeId)
                .Index(t => t.SubstituteEmployeeId)
                .Index(t => new { t.IsActive, t.From, t.To });

            // ---------- 4. TaskDelegations -------------------------------------
            CreateTable(
                "dbo.TaskDelegations",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TaskId = c.Int(nullable: false),
                    FromEmployeeId = c.Int(nullable: false),
                    ToEmployeeId = c.Int(nullable: false),
                    DelegatedAt = c.DateTime(nullable: false),
                    Comment = c.String(maxLength: 512),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DocumentTasks", t => t.TaskId)
                .ForeignKey("dbo.Employees", t => t.FromEmployeeId)
                .ForeignKey("dbo.Employees", t => t.ToEmployeeId)
                .Index(t => t.TaskId)
                .Index(t => t.FromEmployeeId)
                .Index(t => t.ToEmployeeId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.TaskDelegations", "ToEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.TaskDelegations", "FromEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.TaskDelegations", "TaskId", "dbo.DocumentTasks");
            DropIndex("dbo.TaskDelegations", new[] { "ToEmployeeId" });
            DropIndex("dbo.TaskDelegations", new[] { "FromEmployeeId" });
            DropIndex("dbo.TaskDelegations", new[] { "TaskId" });
            DropTable("dbo.TaskDelegations");

            DropForeignKey("dbo.Substitutions", "SubstituteEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.Substitutions", "OriginalEmployeeId", "dbo.Employees");
            DropIndex("dbo.Substitutions", new[] { "IsActive", "From", "To" });
            DropIndex("dbo.Substitutions", new[] { "SubstituteEmployeeId" });
            DropIndex("dbo.Substitutions", new[] { "OriginalEmployeeId" });
            DropTable("dbo.Substitutions");

            DropForeignKey("dbo.Employees", "DepartmentId", "dbo.Departments");
            DropIndex("dbo.Employees", new[] { "DepartmentId" });
            DropColumn("dbo.Employees", "TerminatedAt");
            DropColumn("dbo.Employees", "IsActive");
            DropColumn("dbo.Employees", "DepartmentId");
            DropColumn("dbo.Employees", "Email");

            DropForeignKey("dbo.Departments", "HeadEmployeeId", "dbo.Employees");
            DropForeignKey("dbo.Departments", "ParentDepartmentId", "dbo.Departments");
            DropIndex("dbo.Departments", new[] { "HeadEmployeeId" });
            DropIndex("dbo.Departments", new[] { "ParentDepartmentId" });
            DropColumn("dbo.Departments", "HeadEmployeeId");
            DropColumn("dbo.Departments", "ParentDepartmentId");
        }
    }
}
