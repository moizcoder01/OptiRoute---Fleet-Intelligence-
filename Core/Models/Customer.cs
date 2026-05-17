// =============================================================
namespace OptiRoute.Core.Models
{
    public class Customer : User
    {
        // ── Customer-specific properties ──────────────────────────
        public string ProfilePhotoPath { get; set; } = string.Empty;

        // ── Constructor sets role ─────────────────────────────────
        public Customer()
        {
            Role = "Customer";
        }

        // ── Polymorphism: override abstract members ───────────────
        public override string DashboardTitle => "Customer Dashboard";

        public override string GetDisplayLabel()
        {
            return $"●  Customer  |  {FullName}";
        }

        // ── Domain helper ─────────────────────────────────────────
        /// <summary>True if this customer has uploaded a profile photo.</summary>
        public bool HasProfilePhoto =>
            !string.IsNullOrWhiteSpace(ProfilePhotoPath);
    }
}
