// =============================================================
//  OptiRoute  |  Core/Data/DriverRepository.cs
//
//  Schema reference:
//    Table_Users    : UserID, FirstName, LastName, Email, Phone,
//                     Username, Password, UserRole
//    Table_Drivers  : DriverID (FK→Users), Rating, LicenseNumber
//    Table_Vehicles : VehicleID, DriverID (FK→Drivers), PlateNumber,
//                     VehicleType, IsAvailable, NeedsMaintenance,
//                     CurrentFuel,
//                     TotalDistanceCovered,      ← NEW (schema Step 1)
//                     DistanceSinceLastService,  ← NEW
//                     MaintenanceIntervalKm      ← NEW
//    Table_Orders   : OrderID, CustomerID, VehicleID, ItemName,
//                     Weight, Priority, PickupPoint, DeliveryPoint,
//                     TotalFare, PaymentStatus, OrderStatus,
//                     OrderDate, Rating,
//                     RouteDistance,  ← NEW
//                     RoutePolyline   ← NEW
//    Table_OrderHistory : HistoryID, OrderID, CustomerID, ItemName,
//                         Weight, Priority, PickupPoint, DeliveryPoint,
//                         TotalFare, PaymentStatus, OrderDate,
//                         DeliveredDate, Rating
//    Table_Telemetry : TID, OrderID, Latitude, Longitude, FuelBurned
//
//  CHANGES FROM ORIGINAL:
//    LoadDriver()          — SQL now SELECTs the 3 new vehicle columns.
//    GetDriverStats()      — unchanged.
//    UpdateOrderStatus()   — on 'Delivered': increments
//                            TotalDistanceCovered and
//                            DistanceSinceLastService by RouteDistance;
//                            sets NeedsMaintenance=1 when
//                            DistanceSinceLastService >= MaintenanceIntervalKm.
//    UpdateDistanceAndFuel() — NEW: called by DriverDashboard timer every
//                              GPS tick to deduct fuel and log distance.
//    ResetMaintenanceCounter() — NEW: admin calls after vehicle is serviced.
//    MapDriver()           — reads the 3 new columns (safe ISNULL fallbacks).
// =============================================================
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Data
{
    public class DriverRepository
    {
        private SqlConnection GetConnection()
            => new SqlConnection(DbConfig.ConnectionString);

        // ═════════════════════════════════════════════════════════
        //  LOAD DRIVER  (login + dashboard initialise)
        //  Now SELECTs the 3 maintenance/distance columns.
        // ═════════════════════════════════════════════════════════
        public Driver? LoadDriver(string username)
        {
            // Uses vw_DriverDetails view — JOIN is defined once in SQL
            const string sql = @"
                SELECT *
                FROM  vw_DriverDetails
                WHERE Username = @Username";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Username", username);
            con.Open();

            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return null;
            return MapDriver(rdr);
        }

        // ═════════════════════════════════════════════════════════
        //  GET DRIVER STATS  (Dashboard stat cards)
        // ═════════════════════════════════════════════════════════
        public DriverStats GetDriverStats(int driverId)
        {
            // Uses vw_DriverStats view
            const string sql = @"
                SELECT Assigned, Delivered, Pending, Returned, TotalRatings
                FROM  vw_DriverStats
                WHERE DriverID = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();

            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return new DriverStats();

            return new DriverStats
            {
                Assigned = Val(rdr, "Assigned"),
                Delivered = Val(rdr, "Delivered"),
                Pending = Val(rdr, "Pending"),
                Returned = Val(rdr, "Returned"),
                TotalRatings = Val(rdr, "TotalRatings")
            };
        }
        public double GetLiveAverageRating(int driverId)
        {
            const string sql = @"
        SELECT AVG(CAST(o.Rating AS FLOAT))
        FROM   Table_Orders   o
        INNER  JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
        WHERE  v.DriverID  = @DriverID
          AND  o.Rating IS NOT NULL
          AND  o.Rating > 0";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            object res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return 5.0;
            return Math.Round(Convert.ToDouble(res), 1);
        }

        // ═════════════════════════════════════════════════════════
        //  GET RECENT ASSIGNMENTS  (Dashboard home table)
        // ═════════════════════════════════════════════════════════
        public List<Order> GetRecentAssignments(int driverId, int count = 7)
        {
            string sql = $@"
                SELECT TOP {count}
                    o.OrderID,  o.CustomerID,
                    v.DriverID, v.VehicleID,
                    o.ItemName, o.Weight,
                    o.PickupPoint, o.DeliveryPoint,
                    o.Priority, o.TotalFare,
                    o.PaymentStatus, o.OrderStatus,
                    o.OrderDate,
                    ISNULL(o.Rating, 0) AS Rating
                FROM  Table_Orders   o
                INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                WHERE v.DriverID = @DriverID
                ORDER BY o.OrderDate DESC";

            return Fetch(sql, driverId);
        }

        // ═════════════════════════════════════════════════════════
        //  GET ASSIGNED ORDERS  (My Assignments page)
        // ═════════════════════════════════════════════════════════
        public List<Order> GetAssignedOrders(int driverId)
        {
            const string sql = @"
                SELECT
                    o.OrderID,  o.CustomerID,
                    v.DriverID, v.VehicleID,
                    o.ItemName, o.Weight,
                    o.PickupPoint, o.DeliveryPoint,
                    o.Priority, o.TotalFare,
                    o.PaymentStatus, o.OrderStatus,
                    o.OrderDate,
                    ISNULL(o.Rating, 0) AS Rating
                FROM  Table_Orders   o
                INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                WHERE v.DriverID    = @DriverID
                  AND o.OrderStatus IN ('Assigned', 'Picked')
                ORDER BY o.OrderDate DESC";

            return Fetch(sql, driverId);
        }

        // ═════════════════════════════════════════════════════════
        //  GET ACTIVE ORDER  (Active Delivery page)
        // ═════════════════════════════════════════════════════════
        public Order? GetActiveOrder(int driverId)
        {
            const string sql = @"
                SELECT TOP 1
                    o.OrderID,  o.CustomerID,
                    v.DriverID, v.VehicleID,
                    o.ItemName, o.Weight,
                    o.PickupPoint, o.DeliveryPoint,
                    o.Priority, o.TotalFare,
                    o.PaymentStatus, o.OrderStatus,
                    o.OrderDate,
                    ISNULL(o.Rating, 0) AS Rating
                FROM  Table_Orders   o
                INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                WHERE v.DriverID    = @DriverID
                  AND o.OrderStatus = 'Picked'
                ORDER BY o.OrderDate DESC";

            var list = Fetch(sql, driverId);
            return list.Count > 0 ? list[0] : null;
        }

        // ═════════════════════════════════════════════════════════
        //  GET DELIVERY HISTORY  (History page)
        // ═════════════════════════════════════════════════════════
        public List<Order> GetDeliveryHistory(int driverId)
        {
            const string sql = @"
                SELECT
                    o.OrderID,  o.CustomerID,
                    v.DriverID, v.VehicleID,
                    o.ItemName, o.Weight,
                    o.PickupPoint, o.DeliveryPoint,
                    o.Priority, o.TotalFare,
                    o.PaymentStatus, o.OrderStatus,
                    o.OrderDate,
                    ISNULL(o.Rating, 0) AS Rating
                FROM  Table_Orders   o
                INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                WHERE v.DriverID    = @DriverID
                  AND o.OrderStatus IN ('Delivered', 'Returned')
                ORDER BY o.OrderDate DESC";

            return Fetch(sql, driverId);
        }

        // ═════════════════════════════════════════════════════════
        //  UPDATE ORDER STATUS
        //
        //  On 'Delivered':
        //    1. Archives the order to Table_OrderHistory.
        //    2. Frees the vehicle (IsAvailable = 1).
        //    3. Increments TotalDistanceCovered and DistanceSinceLastService
        //       by the order's RouteDistance (km).
        //    4. If DistanceSinceLastService >= MaintenanceIntervalKm,
        //       sets NeedsMaintenance = 1 automatically.
        // ═════════════════════════════════════════════════════════
        public bool UpdateOrderStatus(int orderId, int driverId, string newStatus) 
        {
            // The trigger trg_OrderStatus_AfterUpdate handles:
            //   - archiving to Table_OrderHistory on Delivered
            //   - setting IsAvailable on vehicle
            //   - incrementing distance columns
            //   - auto-flagging NeedsMaintenance
            // This method only needs to SET the status.
            const string sql = @"
                UPDATE o
                SET    o.OrderStatus = @Status
                FROM   Table_Orders   o
                INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                WHERE  o.OrderID  = @OrderID
                  AND  v.DriverID = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Status", newStatus);
            cmd.Parameters.AddWithValue("@OrderID", orderId);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ═════════════════════════════════════════════════════════
        //  UPDATE DISTANCE AND FUEL  (NEW)
        //  Called by DriverDashboard timer every GPS tick.
        //  Deducts fuelBurned from CurrentFuel and increments
        //  both distance columns by kmCovered.
        //  fuelBurned and kmCovered are computed by the form:
        //    fuelBurned = kmCovered × rate (0.05/0.10/0.18/0.25)
        // ═════════════════════════════════════════════════════════
        public bool UpdateDistanceAndFuel(int driverId, double kmCovered, double fuelBurned)
        {
            // NeedsMaintenance is now handled by trg_Vehicle_MaintenanceAutoFlag
            const string sql = @"
                UPDATE Table_Vehicles
                SET    CurrentFuel              = CASE
                                                    WHEN CurrentFuel - @Fuel < 0 THEN 0
                                                    ELSE CurrentFuel - @Fuel
                                                  END,
                       TotalDistanceCovered     = TotalDistanceCovered    + @Km,
                       DistanceSinceLastService = DistanceSinceLastService + @Km
                WHERE  DriverID = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Fuel", fuelBurned);
            cmd.Parameters.AddWithValue("@Km", kmCovered);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ═════════════════════════════════════════════════════════
        //  RESET MAINTENANCE COUNTER  (NEW)
        //  Admin calls this after the vehicle has been serviced.
        //  Resets DistanceSinceLastService to 0 and clears the flag.
        // ═════════════════════════════════════════════════════════
        public bool ResetMaintenanceCounter(int driverId)
        {
            const string sql = @"
                UPDATE Table_Vehicles
                SET    DistanceSinceLastService = 0,
                       NeedsMaintenance         = 0
                WHERE  DriverID = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ═════════════════════════════════════════════════════════
        //  UPDATE VEHICLE  (Profile page)
        // ═════════════════════════════════════════════════════════
        public bool UpdateVehicle(int driverId, string plateNumber, string vehicleType)
        {
            const string sql = @"
                UPDATE Table_Vehicles
                SET    PlateNumber = @Plate,
                       VehicleType = @Type
                WHERE  DriverID   = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Plate", plateNumber);
            cmd.Parameters.AddWithValue("@Type", vehicleType);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ═════════════════════════════════════════════════════════
        //  UPDATE FUEL LEVEL  (direct set — admin refuel action)
        // ═════════════════════════════════════════════════════════
        public bool UpdateFuelLevel(int driverId, double newFuelLevel)
        {
            const string sql = @"
                UPDATE Table_Vehicles
                SET    CurrentFuel = @Fuel
                WHERE  DriverID   = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Fuel", newFuelLevel);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ═════════════════════════════════════════════════════════
        //  UPDATE DRIVER AVAILABILITY
        // ═════════════════════════════════════════════════════════
        public bool UpdateAvailability(int driverId, bool isAvailable)
        {
            const string sql = @"
                UPDATE Table_Vehicles
                SET    IsAvailable = @IsAvailable
                WHERE  DriverID   = @DriverID";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@IsAvailable", isAvailable ? 1 : 0);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ═════════════════════════════════════════════════════════
        //  LOG TELEMETRY
        // ═════════════════════════════════════════════════════════
        public bool LogTelemetry(int orderId, double latitude,
                                  double longitude, double fuelBurned)
        {
            const string sql = @"
                INSERT INTO Table_Telemetry (OrderID, Latitude, Longitude, FuelBurned)
                VALUES (@OrderID, @Lat, @Lng, @Fuel)";

            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@OrderID", orderId);
            cmd.Parameters.AddWithValue("@Lat", latitude);
            cmd.Parameters.AddWithValue("@Lng", longitude);
            cmd.Parameters.AddWithValue("@Fuel", fuelBurned);
            con.Open();
            return cmd.ExecuteNonQuery() > 0;
        }


        // ═════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═════════════════════════════════════════════════════════
        private List<Order> Fetch(string sql, int driverId)
        {
            var list = new List<Order>();
            using var con = GetConnection();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@DriverID", driverId);
            con.Open();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) list.Add(MapOrder(rdr));
            return list;
        }

        /// <summary>
        /// Maps a SqlDataReader row to a Driver object.
        /// Safely reads the 3 new maintenance columns with ISNULL fallbacks
        /// so older DB rows (before schema migration) never throw KeyNotFoundException.
        /// </summary>
        private static Driver MapDriver(IDataRecord r) => new Driver
        {
            UserID = Convert.ToInt32(r["UserID"]),
            FirstName = r["FirstName"] == DBNull.Value ? string.Empty : r["FirstName"].ToString()!,
            LastName = r["LastName"] == DBNull.Value ? string.Empty : r["LastName"].ToString()!,
            Email = r["Email"] == DBNull.Value ? string.Empty : r["Email"].ToString()!,
            Phone = r["Phone"] == DBNull.Value ? string.Empty : r["Phone"].ToString()!,
            Username = r["Username"].ToString()!,
            UserRole = r["UserRole"].ToString()!,
            LicenseNumber = r["LicenseNumber"] == DBNull.Value ? string.Empty : r["LicenseNumber"].ToString()!,
            VehicleID = r["VehicleID"] == DBNull.Value ? 0 : Convert.ToInt32(r["VehicleID"]),
            PlateNumber = r["PlateNumber"] == DBNull.Value ? "N/A" : r["PlateNumber"].ToString()!,
            VehicleType = r["VehicleType"] == DBNull.Value ? "Bike" : r["VehicleType"].ToString()!,
            CurrentFuel = r["CurrentFuel"] == DBNull.Value ? 100.0 : Convert.ToDouble(r["CurrentFuel"]),
            IsAvailable = r["IsAvailable"] != DBNull.Value && Convert.ToBoolean(r["IsAvailable"]),
            NeedsMaintenance = r["NeedsMaintenance"] != DBNull.Value && Convert.ToBoolean(r["NeedsMaintenance"]),
            AverageRating = r["DriverRating"] == DBNull.Value ? 5.0 : Convert.ToDouble(r["DriverRating"]),
            // ── NEW: maintenance / distance columns ───────────────
            TotalDistanceCovered = r["TotalDistanceCovered"] == DBNull.Value ? 0.0 : Convert.ToDouble(r["TotalDistanceCovered"]),
            DistanceSinceLastService = r["DistanceSinceLastService"] == DBNull.Value ? 0.0 : Convert.ToDouble(r["DistanceSinceLastService"]),
            MaintenanceIntervalKm = r["MaintenanceIntervalKm"] == DBNull.Value ? 5000.0 : Convert.ToDouble(r["MaintenanceIntervalKm"])
        };

        private static Order MapOrder(IDataRecord r) => new Order
        {
            OrderID = Convert.ToInt32(r["OrderID"]),
            CustomerID = Convert.ToInt32(r["CustomerID"]),
            VehicleID = r["VehicleID"] == DBNull.Value ? 0 : Convert.ToInt32(r["VehicleID"]),
            ItemName = r["ItemName"].ToString()!,
            Weight = Convert.ToDouble(r["Weight"]),
            PickupPoint = r["PickupPoint"].ToString()!,
            DeliveryPoint = r["DeliveryPoint"].ToString()!,
            Priority = r["Priority"].ToString()!,
            TotalFare = r["TotalFare"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalFare"]),
            PaymentStatus = r["PaymentStatus"].ToString()!,
            OrderStatus = r["OrderStatus"].ToString()!,
            OrderDate = r["OrderDate"] == DBNull.Value
                                ? DateTime.Now : Convert.ToDateTime(r["OrderDate"]),
            Rating = r["Rating"] == DBNull.Value ? 0 : Convert.ToInt32(r["Rating"])
        };

        private static int Val(IDataRecord r, string col)
            => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);
    }

    // ─────────────────────────────────────────────────────────
    //  DriverStats — plain data container, no DB logic
    // ─────────────────────────────────────────────────────────
    public class DriverStats
    {
        public int Assigned { get; set; }
        public int Delivered { get; set; }
        public int Pending { get; set; }
        public int Returned { get; set; }
        public int TotalRatings { get; set; }
    }
}
