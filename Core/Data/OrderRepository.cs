// =============================================================
//  OptiRoute  |  Core/Data/OrderRepository.cs
//  All SQL for Table_Orders — no UI code ever goes here
//
//  NEW in this version (map integration):
//    SaveRouteData()         — saves ORS polyline + distance at assign time
//    UpdateDriverProgress()  — driver timer calls this every tick
//    GetRouteData()          — driver/customer dashboards read polyline from DB
// =============================================================
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Data
{
    public class OrderRepository
    {
        private readonly string _cs;

        public OrderRepository()
        {
            _cs = DbConfig.ConnectionString;
        }

        // ── PLACE ORDER ───────────────────────────────────────────
        /// <summary>Inserts a new order. Returns the generated OrderID or -1.</summary>
        public int PlaceOrder(Order o)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"INSERT INTO Table_Orders
                        (CustomerID, ItemName, Weight, PickupPoint,
                         DeliveryPoint, Priority, OrderStatus,
                         TotalFare, PaymentStatus, OrderDate)
                      VALUES
                        (@cid, @item, @wt, @pickup,
                         @delivery, @priority, 'Pending',
                         @fare, @pay, @date);
                      SELECT SCOPE_IDENTITY();", conn);

                cmd.Parameters.AddWithValue("@cid", o.CustomerID);
                cmd.Parameters.AddWithValue("@item", o.ItemName);
                cmd.Parameters.AddWithValue("@wt", o.Weight);
                cmd.Parameters.AddWithValue("@pickup", o.PickupPoint);
                cmd.Parameters.AddWithValue("@delivery", o.DeliveryPoint);
                cmd.Parameters.AddWithValue("@priority", o.Priority);
                cmd.Parameters.AddWithValue("@fare", o.TotalFare);
                cmd.Parameters.AddWithValue("@pay", o.PaymentStatus);
                cmd.Parameters.AddWithValue("@date", o.OrderDate);

                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : -1;
            }
            catch { return -1; }
        }

        // ── GET ALL ORDERS FOR CUSTOMER ───────────────────────────
        public List<Order> GetByCustomer(int customerID)
        {
            var list = new List<Order>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             v.DriverID,
                             ISNULL(o.VehicleID, 0)          AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)             AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders o
                      LEFT   JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                      WHERE  o.CustomerID = @cid
                      ORDER  BY o.OrderDate DESC", conn);
                cmd.Parameters.AddWithValue("@cid", customerID);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapOrder(r));
            }
            catch { }
            return list;
        }

        // ── GET TOP N RECENT ORDERS FOR DASHBOARD ─────────────────
        public List<Order> GetRecentByCustomer(int customerID, int top = 8)
        {
            var list = new List<Order>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    $@"SELECT TOP {top}
                              o.OrderID, o.CustomerID,
                              v.DriverID,
                              ISNULL(o.VehicleID, 0)           AS VehicleID,
                              o.ItemName, o.Weight, o.Priority,
                              o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                              o.TotalFare, o.PaymentStatus,
                              ISNULL(o.Rating, 0)              AS Rating,
                              o.OrderDate,
                              o.RoutePolyline,
                              ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                              ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                              o.DriverCurrentLat,
                              o.DriverCurrentLng
                       FROM   Table_Orders o
                       LEFT   JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                       WHERE  o.CustomerID = @cid
                       ORDER  BY o.OrderDate DESC", conn);
                cmd.Parameters.AddWithValue("@cid", customerID);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapOrder(r));
            }
            catch { }
            return list;
        }

        // ── ORDER SUMMARY (dashboard stat cards) ──────────────────
        public (int Total, int InTransit, int Delivered, int Urgent)
            GetSummary(int customerID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT COUNT(*) AS Total,
                             SUM(CASE WHEN OrderStatus IN ('Assigned','Picked') THEN 1 ELSE 0 END) AS InTransit,
                             SUM(CASE WHEN OrderStatus = 'Delivered'            THEN 1 ELSE 0 END) AS Delivered,
                             SUM(CASE WHEN Priority    = 'Urgent'               THEN 1 ELSE 0 END) AS Urgent
                      FROM   Table_Orders
                      WHERE  CustomerID = @cid", conn);
                cmd.Parameters.AddWithValue("@cid", customerID);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                    return (
                        r["Total"] != DBNull.Value ? Convert.ToInt32(r["Total"]) : 0,
                        r["InTransit"] != DBNull.Value ? Convert.ToInt32(r["InTransit"]) : 0,
                        r["Delivered"] != DBNull.Value ? Convert.ToInt32(r["Delivered"]) : 0,
                        r["Urgent"] != DBNull.Value ? Convert.ToInt32(r["Urgent"]) : 0
                    );
            }
            catch { }
            return (0, 0, 0, 0);
        }

        // ── TRACK BY ORDER ID ─────────────────────────────────────
        public Order? GetByID(int orderID, int customerID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             v.DriverID,
                             ISNULL(o.VehicleID, 0)           AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)              AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders o
                      LEFT   JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                      WHERE  o.OrderID = @id AND o.CustomerID = @cid", conn);
                cmd.Parameters.AddWithValue("@id", orderID);
                cmd.Parameters.AddWithValue("@cid", customerID);

                using var r = cmd.ExecuteReader();
                return r.Read() ? MapOrder(r) : null;
            }
            catch { return null; }
        }

        // ── GET ORDER BY ID ONLY (for driver tracking, no customerID guard) ──
        public Order? GetByID(int orderID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             v.DriverID,
                             ISNULL(o.VehicleID, 0)           AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)              AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders o
                      LEFT   JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                      WHERE  o.OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@id", orderID);

                using var r = cmd.ExecuteReader();
                return r.Read() ? MapOrder(r) : null;
            }
            catch { return null; }
        }

        // ── SAVE RATING ───────────────────────────────────────────
        public bool SaveRating(int orderID, int customerID, int rating)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET Rating = @r
                      WHERE OrderID = @id AND CustomerID = @cid", conn);
                cmd.Parameters.AddWithValue("@r", rating);
                cmd.Parameters.AddWithValue("@id", orderID);
                cmd.Parameters.AddWithValue("@cid", customerID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── CANCEL ORDER ──────────────────────────────────────────
        public bool CancelOrder(int orderID, int customerID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();

                var chk = new SqlCommand(
                    "SELECT OrderStatus FROM Table_Orders WHERE OrderID = @id AND CustomerID = @cid",
                    conn);
                chk.Parameters.AddWithValue("@id", orderID);
                chk.Parameters.AddWithValue("@cid", customerID);
                string? status = chk.ExecuteScalar()?.ToString();

                if (status != "Pending") return false;

                var del = new SqlCommand(
                    @"DELETE FROM Table_Orders
                      WHERE OrderID = @id AND CustomerID = @cid AND OrderStatus = 'Pending'",
                    conn);
                del.Parameters.AddWithValue("@id", orderID);
                del.Parameters.AddWithValue("@cid", customerID);
                return del.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ── UPDATE ORDER STATUS (admin / driver actions) ──────────
        public bool UpdateStatus(int orderID, string newStatus)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "UPDATE Table_Orders SET OrderStatus = @s WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@s", newStatus);
                cmd.Parameters.AddWithValue("@id", orderID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── ASSIGN VEHICLE TO ORDER ───────────────────────────────
        public bool AssignVehicle(int orderID, int vehicleID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET VehicleID   = @vid,
                          OrderStatus = 'Assigned'
                      WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@vid", vehicleID);
                cmd.Parameters.AddWithValue("@id", orderID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── ASSIGN DRIVER (legacy helper) ─────────────────────────
        public bool AssignDriver(int orderID, int driverID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var getVeh = new SqlCommand(
                    "SELECT TOP 1 VehicleID FROM Table_Vehicles WHERE DriverID = @did", conn);
                getVeh.Parameters.AddWithValue("@did", driverID);
                var vehResult = getVeh.ExecuteScalar();
                if (vehResult == null) return false;
                int vehicleID = Convert.ToInt32(vehResult);

                var cmd = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET VehicleID   = @vid,
                          OrderStatus = 'Assigned'
                      WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@vid", vehicleID);
                cmd.Parameters.AddWithValue("@id", orderID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── GET ORDERS ASSIGNED TO DRIVER ─────────────────────────
        public List<Order> GetByDriver(int driverID)
        {
            var list = new List<Order>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             v.DriverID,
                             o.VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)              AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders   o
                      INNER  JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                      WHERE  v.DriverID = @did
                        AND  o.OrderStatus NOT IN ('Delivered','Returned')
                      ORDER  BY o.OrderDate DESC", conn);
                cmd.Parameters.AddWithValue("@did", driverID);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapOrder(r));
            }
            catch { }
            return list;
        }

        // ── ADMIN: ALL PENDING ORDERS ─────────────────────────────
        public List<Order> GetAllPending()
        {
            var list = new List<Order>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             NULL                              AS DriverID,
                             ISNULL(o.VehicleID, 0)           AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)              AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders o
                      WHERE  o.OrderStatus = 'Pending'
                      ORDER  BY o.Priority DESC, o.OrderDate ASC", conn);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapOrder(r));
            }
            catch { }
            return list;
        }

        // ── ADMIN: ALL ORDERS ─────────────────────────────────────
        public List<Order> GetAllOrders()
        {
            var list = new List<Order>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             NULL                              AS DriverID,
                             ISNULL(o.VehicleID, 0)           AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)              AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders o
                      ORDER  BY o.OrderDate DESC", conn);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapOrder(r));
            }
            catch { }
            return list;
        }

        // ── ADMIN: ORDERS BY STATUS ───────────────────────────────
        public List<Order> GetByStatus(string status)
        {
            var list = new List<Order>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT o.OrderID, o.CustomerID,
                             NULL                              AS DriverID,
                             ISNULL(o.VehicleID, 0)           AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0)              AS Rating,
                             o.OrderDate,
                             o.RoutePolyline,
                             ISNULL(o.TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(o.DriverProgressKm, 0)     AS DriverProgressKm,
                             o.DriverCurrentLat,
                             o.DriverCurrentLng
                      FROM   Table_Orders o
                      WHERE  o.OrderStatus = @Status
                      ORDER  BY o.OrderDate DESC", conn);
                cmd.Parameters.AddWithValue("@Status", status);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapOrder(r));
            }
            catch { }
            return list;
        }

        // ── ADMIN: REVENUE SUMMARY ────────────────────────────────
        public (decimal TotalRevenue, int TotalOrders, int PaidOrders, int CodOrders)
            GetRevenueSummary()
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT ISNULL(SUM(TotalFare), 0)                                              AS Revenue,
                             COUNT(*)                                                                AS Total,
                             SUM(CASE WHEN PaymentStatus = 'Paid'             THEN 1 ELSE 0 END)   AS Paid,
                             SUM(CASE WHEN PaymentStatus = 'Cash on Delivery' THEN 1 ELSE 0 END)   AS COD
                      FROM   Table_Orders
                      WHERE  OrderStatus = 'Delivered'", conn);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                    return (
                        r["Revenue"] != DBNull.Value ? Convert.ToDecimal(r["Revenue"]) : 0m,
                        r["Total"] != DBNull.Value ? Convert.ToInt32(r["Total"]) : 0,
                        r["Paid"] != DBNull.Value ? Convert.ToInt32(r["Paid"]) : 0,
                        r["COD"] != DBNull.Value ? Convert.ToInt32(r["COD"]) : 0
                    );
            }
            catch { }
            return (0m, 0, 0, 0);
        }

        // ═════════════════════════════════════════════════════════
        //  NEW — SAVE ROUTE DATA  (called by Admin after ORS returns)
        //
        //  Called once at assignment time. Saves:
        //    • RoutePolyline          — JSON coordinate array from ORS
        //    • TotalRouteDistanceKm   — total of both legs (Driver→Pickup + Pickup→Dest)
        //  DriverProgressKm starts at 0 automatically (DB default).
        // ═════════════════════════════════════════════════════════
        public bool SaveRouteData(int orderID, string polylineJson, decimal totalDistanceKm)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET RoutePolyline        = @polyline,
                          TotalRouteDistanceKm = @dist,
                          DriverProgressKm     = 0
                      WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@polyline", polylineJson);
                cmd.Parameters.AddWithValue("@dist", totalDistanceKm);
                cmd.Parameters.AddWithValue("@id", orderID);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ═════════════════════════════════════════════════════════
        //  NEW — UPDATE DRIVER PROGRESS  (called by Driver timer every tick)
        //
        //  Updates:
        //    • DriverProgressKm   — how far the driver has covered so far
        //    • DriverCurrentLat   — driver's current latitude
        //    • DriverCurrentLng   — driver's current longitude
        //
        //  Also updates Table_Users.CurrentLat / CurrentLng so Admin
        //  can use the driver's live position as the route start point
        //  for any future assignment.
        // ═════════════════════════════════════════════════════════
        public bool UpdateDriverProgress(int orderID, int driverUserID,
            double progressKm, double currentLat, double currentLng)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();

                // Update order progress + live position
                var cmdOrder = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET DriverProgressKm = @prog,
                          DriverCurrentLat = @lat,
                          DriverCurrentLng = @lng
                      WHERE OrderID = @id", conn);
                cmdOrder.Parameters.AddWithValue("@prog", progressKm);
                cmdOrder.Parameters.AddWithValue("@lat", currentLat);
                cmdOrder.Parameters.AddWithValue("@lng", currentLng);
                cmdOrder.Parameters.AddWithValue("@id", orderID);
                cmdOrder.ExecuteNonQuery();

                // Keep Table_Users current position in sync
                var cmdUser = new SqlCommand(
                    @"UPDATE Table_Users
                      SET CurrentLat = @lat,
                          CurrentLng = @lng
                      WHERE UserID = @uid", conn);
                cmdUser.Parameters.AddWithValue("@lat", currentLat);
                cmdUser.Parameters.AddWithValue("@lng", currentLng);
                cmdUser.Parameters.AddWithValue("@uid", driverUserID);
                cmdUser.ExecuteNonQuery();

                return true;
            }
            catch { return false; }
        }

        // ═════════════════════════════════════════════════════════
        //  NEW — GET ROUTE DATA  (called by Customer tracking map)
        //
        //  Returns only the route-specific fields for a given order —
        //  lightweight, no joins needed.
        //  Returns null if the order has no route saved yet.
        // ═════════════════════════════════════════════════════════
        public RouteData? GetRouteData(int orderID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT OrderStatus,
                             RoutePolyline,
                             ISNULL(TotalRouteDistanceKm, 0) AS TotalRouteDistanceKm,
                             ISNULL(DriverProgressKm,     0) AS DriverProgressKm,
                             DriverCurrentLat,
                             DriverCurrentLng
                      FROM   Table_Orders
                      WHERE  OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@id", orderID);

                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                string? polyline = r["RoutePolyline"] == DBNull.Value
                    ? null : r["RoutePolyline"].ToString();

                if (string.IsNullOrEmpty(polyline)) return null;

                return new RouteData
                {
                    OrderStatus = r["OrderStatus"]?.ToString() ?? "Pending",
                    PolylineJson = polyline,
                    TotalRouteDistanceKm = Convert.ToDecimal(r["TotalRouteDistanceKm"]),
                    DriverProgressKm = Convert.ToDecimal(r["DriverProgressKm"]),
                    DriverCurrentLat = r["DriverCurrentLat"] == DBNull.Value
                                            ? 0 : Convert.ToDouble(r["DriverCurrentLat"]),
                    DriverCurrentLng = r["DriverCurrentLng"] == DBNull.Value
                                            ? 0 : Convert.ToDouble(r["DriverCurrentLng"])
                };
            }
            catch { return null; }
        }

        // ── PRIVATE MAPPER ────────────────────────────────────────
        private static Order MapOrder(SqlDataReader r)
        {
            int? driverIDVal = null;
            try
            {
                if (r["DriverID"] != DBNull.Value)
                    driverIDVal = Convert.ToInt32(r["DriverID"]);
            }
            catch { }

            return new Order
            {
                OrderID = Convert.ToInt32(r["OrderID"]),
                CustomerID = Convert.ToInt32(r["CustomerID"]),
                DriverID = driverIDVal,
                VehicleID = r["VehicleID"] != DBNull.Value ? Convert.ToInt32(r["VehicleID"]) : 0,
                ItemName = r["ItemName"]?.ToString() ?? "—",
                Weight = r["Weight"] != DBNull.Value ? Convert.ToDouble(r["Weight"]) : 0,
                Priority = r["Priority"]?.ToString() ?? "Normal",
                PickupPoint = r["PickupPoint"]?.ToString() ?? "—",
                DeliveryPoint = r["DeliveryPoint"]?.ToString() ?? "—",
                OrderStatus = r["OrderStatus"]?.ToString() ?? "Pending",
                TotalFare = r["TotalFare"] != DBNull.Value ? Convert.ToDecimal(r["TotalFare"]) : 0m,
                PaymentStatus = r["PaymentStatus"]?.ToString() ?? "Unpaid",
                Rating = r["Rating"] != DBNull.Value ? Convert.ToInt32(r["Rating"]) : 0,
                OrderDate = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]) : DateTime.Now,

                // ── Route fields (new) ──────────────────────────
                RoutePolyline = r["RoutePolyline"] == DBNull.Value
                                         ? null : r["RoutePolyline"]?.ToString(),
                TotalRouteDistanceKm = r["TotalRouteDistanceKm"] != DBNull.Value
                                         ? Convert.ToDecimal(r["TotalRouteDistanceKm"]) : 0m,
                DriverProgressKm = r["DriverProgressKm"] != DBNull.Value
                                         ? Convert.ToDecimal(r["DriverProgressKm"]) : 0m,
                DriverCurrentLat = r["DriverCurrentLat"] == DBNull.Value
                                         ? 0 : Convert.ToDouble(r["DriverCurrentLat"]),
                DriverCurrentLng = r["DriverCurrentLng"] == DBNull.Value
                                         ? 0 : Convert.ToDouble(r["DriverCurrentLng"])
            };
        }
    }

    // ─────────────────────────────────────────────────────────
    //  RouteData — lightweight container for tracking map reads
    //  Returned by GetRouteData() — no need to load full Order.
    // ─────────────────────────────────────────────────────────
    public class RouteData
    {
        public string OrderStatus { get; set; } = "Pending";
        public string PolylineJson { get; set; } = "";
        public decimal TotalRouteDistanceKm { get; set; }
        public decimal DriverProgressKm { get; set; }
        public double DriverCurrentLat { get; set; }
        public double DriverCurrentLng { get; set; }

        // Convenience: progress as 0.0–1.0 fraction
        public double ProgressFraction =>
            TotalRouteDistanceKm > 0
                ? Math.Min(1.0, (double)(DriverProgressKm / TotalRouteDistanceKm))
                : 0;
    }
}