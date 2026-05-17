// =============================================================
//  OptiRoute  |  Core/Models/Order.cs
//  Represents one consignment row in Table_Orders
// =============================================================
using System;

namespace OptiRoute.Core.Models
{
    public class Order
    {
        // ── Primary key ───────────────────────────────────────────
        public int OrderID { get; set; }

        // ── Foreign keys ──────────────────────────────────────────
        public int CustomerID { get; set; }
        public int? DriverID { get; set; }   // null until assigned
        public int VehicleID { get; set; }   // 0 until assigned

        // ── Consignment details ───────────────────────────────────
        public string ItemName { get; set; } = string.Empty;
        public double Weight { get; set; }   // kg
        public string PickupPoint { get; set; } = string.Empty;
        public string DeliveryPoint { get; set; } = string.Empty;

        // ── Priority — Normal | Urgent ────────────────────────────
        public string Priority { get; set; } = "Normal";

        // ── Status lifecycle ──────────────────────────────────────
        // Pending → Assigned → Picked → Delivered  (or Returned)
        public string OrderStatus { get; set; } = "Pending";

        // ── Financials ────────────────────────────────────────────
        public decimal BaseFee { get; set; } = 150m;
        public decimal WeightRate { get; set; } = 20m;   // per kg
        public decimal PrioritySurcharge { get; set; } = 0m;
        public decimal TotalFare { get; set; }

        /// <summary>Paid | Unpaid | Cash on Delivery</summary>
        public string PaymentStatus { get; set; } = "Unpaid";

        // ── Rating (1-5 stars, set by customer after delivery) ────
        public int Rating { get; set; } = 0;

        // ── Timestamps ────────────────────────────────────────────
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? DeliveredAt { get; set; }

        // ── Computed / display helpers ────────────────────────────
        public bool IsCancellable => OrderStatus == "Pending";
        public bool IsDelivered => OrderStatus == "Delivered";
        public bool IsUrgent => Priority == "Urgent";

        public string FormattedDate =>
            OrderDate.ToString("dd MMM yyyy");

        public string FormattedFare =>
            TotalFare == 0 ? "—" : "Rs. " + TotalFare.ToString("N0");

        public string WeightDisplay => $"{Weight} kg";

        // ── Fare engine ───────────────────────────────────────────
        /// <summary>
        /// Calculates and sets TotalFare.
        /// Total = BaseFee + (Weight × WeightRate) + PrioritySurcharge
        /// </summary>
        public void CalculateFare()
        {
            // All operands are decimal — no type mismatch
            PrioritySurcharge = IsUrgent ? 100m : 0m;
            TotalFare = BaseFee
                      + (decimal)Weight * WeightRate
                      + PrioritySurcharge;
        }

        /// <summary>
        /// Static helper: returns an estimated fare for the live preview
        /// in PlaceOrderPanel before the order object is built.
        /// </summary>
        public static decimal CalculateFare(double weight, bool urgent)
        {
            decimal surcharge = urgent ? 100m : 0m;
            return 150m + (decimal)weight * 20m + surcharge;
        }
    }
}
