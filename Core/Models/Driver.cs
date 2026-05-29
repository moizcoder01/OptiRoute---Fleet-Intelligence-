// =============================================================
//  OptiRoute  |  Core/Models/Driver.cs
//  Inherits User.
//  Driver data spans three tables:
//    Table_Users    → identity / contact
//    Table_Drivers  → DriverID (same as UserID), Rating, LicenseNumber
//    Table_Vehicles → vehicle assigned to this driver
//
//  CHANGES FROM ORIGINAL:
//    + TotalDistanceCovered property added (was in DB schema but missing
//      from the model — needed by DriverRepository mapper and AdminDashboard).
// =============================================================
namespace OptiRoute.Core.Models
{
    public class Driver : User
    {
        // ── From Table_Drivers ────────────────────────────────────
        public double AverageRating { get; set; } = 5.0;
        public string LicenseNumber { get; set; } = string.Empty;

        // ── From Table_Vehicles ───────────────────────────────────
        public int VehicleID { get; set; } = 0;
        public string PlateNumber { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public bool NeedsMaintenance { get; set; } = false;
        public double CurrentFuel { get; set; } = 100.0;

        // ── Maintenance / distance tracking ───────────────────────
        /// <summary>
        /// Lifetime odometer in km.
        /// Stored in Table_Vehicles.TotalDistanceCovered.
        /// Incremented every time the driver completes a delivery.
        /// </summary>
        public double TotalDistanceCovered { get; set; } = 0.0;

        /// <summary>
        /// Km driven since the last service.
        /// Stored in Table_Vehicles.DistanceSinceLastService.
        /// Resets to 0 when admin marks vehicle as serviced.
        /// </summary>
        public double DistanceSinceLastService { get; set; } = 0.0;

        /// <summary>
        /// Km interval between services.
        /// Stored in Table_Vehicles.MaintenanceIntervalKm.
        /// Default 5000 km (matches DB DEFAULT constraint).
        /// </summary>
        public double MaintenanceIntervalKm { get; set; } = 5000.0;

        // ── Constructor ───────────────────────────────────────────
        public Driver()
        {
            Role = "Driver";
        }

        // ── Polymorphism ──────────────────────────────────────────
        public override string DashboardTitle => "Driver App";

        public override string GetDisplayLabel()
            => $"●  Driver  |  {FullName}  [{PlateNumber}]";

        // ── Computed helpers ──────────────────────────────────────

        /// <summary>
        /// Percentage of the maintenance interval consumed (0–100).
        /// Used by AdminDashboard to block urgent-order assignment at ≥ 80 %.
        /// </summary>
        public double MaintenancePct =>
            MaintenanceIntervalKm > 0
                ? (DistanceSinceLastService / MaintenanceIntervalKm) * 100.0
                : 0.0;

        /// <summary>
        /// Returns true when the driver can take a new order:
        ///   - vehicle is available
        ///   - enough fuel for the required distance
        ///   - no outstanding maintenance flag
        /// </summary>
        public bool IsEligibleForAssignment(double requiredFuelDistance)
            => IsAvailable && CurrentFuel >= requiredFuelDistance && !NeedsMaintenance;
    }
}
