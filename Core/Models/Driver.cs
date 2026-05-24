// =============================================================
//  OptiRoute  |  Core/Models/Driver.cs
//  Inherits User.
//  Driver data spans three tables:
//    Table_Users    → identity / contact
//    Table_Drivers  → DriverID (same as UserID), Rating, LicenseNumber
//    Table_Vehicles → vehicle assigned to this driver
// =============================================================
namespace OptiRoute.Core.Models
{
    public class Driver : User
    {
        // ── From Table_Drivers ────────────────────────────────────
        public double AverageRating { get; set; } = 5.0;
        public string LicenseNumber { get; set; } = string.Empty;

        // ── From Table_Vehicles (the vehicle assigned to this driver)
        public int VehicleID { get; set; } = 0;
        public string PlateNumber { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public bool NeedsMaintenance { get; set; } = false;
        public double CurrentFuel { get; set; } = 100.0;
        public double CurrentLat { get; set; }
        public double CurrentLng { get; set; }
        public double TotalDistanceCoveredKm { get; set; }
        public double DistanceSinceLastServiceKm { get; set; }
        public double MaintenanceIntervalKm { get; set; } = 500;
        public double MaintenancePct { get; set; } = 100;

        // ── Constructor ───────────────────────────────────────────
        public Driver()
        {
            Role = "Driver";
        }

        // ── Polymorphism: override abstract members ───────────────
        public override string DashboardTitle => "Driver App";

        public override string GetDisplayLabel()
        {
            return $"●  Driver  |  {FullName}  [{PlateNumber}]";
        }

        // ── Domain helper ─────────────────────────────────────────
        public bool IsEligibleForAssignment(double requiredFuelDistance)
        {
            return IsAvailable
                && CurrentFuel >= requiredFuelDistance
                && !NeedsMaintenance;
        }
    }
}
