// =============================================================
//  OptiRoute  |  Core/Models/User.cs
//  Abstract base class — every system actor inherits from this
// =============================================================
using System;

namespace OptiRoute.Core.Models
{
    public abstract class User
    {
        // ── Identity ──────────────────────────────────────────────
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;   // stored hashed in DB

        // ── Personal Info ─────────────────────────────────────────
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // ── Role — set by each subclass constructor ───────────────
        public string Role { get; protected set; } = string.Empty;

        // ── UserRole alias (used by DriverRepository mapper) ──────
        public string UserRole
        {
            get => Role;
            set => Role = value;
        }

        // ── Computed ──────────────────────────────────────────────
        /// <summary>Returns "FirstName LastName" trimmed, falls back to Username.</summary>
        public string FullName =>
            string.IsNullOrWhiteSpace($"{FirstName} {LastName}".Trim())
                ? Username
                : $"{FirstName} {LastName}".Trim();

        // ── Abstract contract — every subclass must implement ─────
        public abstract string GetDisplayLabel();
        public abstract string DashboardTitle { get; }

        // ── Shared helper ─────────────────────────────────────────
        public override string ToString() => $"[{Role}] {FullName} (@{Username})";
    }
}
