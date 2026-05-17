// =============================================================
//  OptiRoute  |  Core/Models/Admin.cs
//  Inherits User — represents the Admin sitting on Laptop B
// =============================================================
namespace OptiRoute.Core.Models
{
    public class Admin : User
    {
        // ── Admin-specific properties ─────────────────────────────
        public bool IsSuperAdmin { get; set; } = false;

        // ── Constructor ───────────────────────────────────────────
        public Admin()
        {
            Role = "Admin";
        }

        // ── Polymorphism: override abstract members ───────────────
        public override string DashboardTitle => "Admin Control Panel";

        public override string GetDisplayLabel()
        {
            string tag = IsSuperAdmin ? "Super Admin" : "Admin";
            return $"●  {tag}  |  {FullName}";
        }
    }
}
