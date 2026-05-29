// =============================================================
//  OptiRoute  |  Core/Data/OrderRepository.cs
//  All SQL for Table_Orders — no UI code ever goes here
//
//  CHANGES FROM ORIGINAL:
//    + AssignVehicleWithRoute() — saves VehicleID, RouteDistance,
//      RoutePolyline, sets status 'Assigned' in one UPDATE.
//      Called by AdminDashboardForm after OSRM route is fetched.
//    + GetRoutePolyline()      — reads RoutePolyline for a given
//      OrderID so DriverDashboard / CustomerDashboard can re-render
//      the Leaflet map without re-calling OSRM.
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
                             ISNULL(o.VehicleID, 0) AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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
                              ISNULL(o.VehicleID, 0) AS VehicleID,
                              o.ItemName, o.Weight, o.Priority,
                              o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                              o.TotalFare, o.PaymentStatus,
                              ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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
                             ISNULL(o.VehicleID, 0) AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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

        // ── UPDATE ORDER STATUS ───────────────────────────────────
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

        // ── ASSIGN VEHICLE (original — no route data) ─────────────
        /// <summary>
        /// Links a VehicleID to an order and sets status to 'Assigned'.
        /// Used when route calculation is skipped or already stored.
        /// </summary>
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

        // ── ASSIGN VEHICLE WITH ROUTE DATA (NEW) ──────────────────
        /// <summary>
        /// Assigns a vehicle AND saves OSRM route data in one UPDATE.
        /// Called by AdminDashboardForm after RouteService.GetRouteAsync().
        ///
        /// Parameters:
        ///   orderID       — the order being assigned
        ///   vehicleID     — the driver's vehicle
        ///   routeDistKm   — real road distance from OSRM (km)
        ///   routePolyline — JSON [[lat,lng],...] from OSRM for Leaflet rendering
        /// </summary>
        public bool AssignVehicleWithRoute(int orderID, int vehicleID,
                                           double routeDistKm, string routePolyline)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET VehicleID      = @vid,
                          OrderStatus    = 'Assigned',
                          RouteDistance  = @dist,
                          RoutePolyline  = @poly
                      WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@vid", vehicleID);
                cmd.Parameters.AddWithValue("@dist", routeDistKm);
                cmd.Parameters.AddWithValue("@poly", routePolyline);
                cmd.Parameters.AddWithValue("@id", orderID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }
        

        // ── SAVE ROUTE DATA (called after AssignVehicle, saves polyline + distance) ──
        public bool SaveRouteData(int orderID, string polylineJson, decimal totalDistanceKm)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Orders
                      SET RoutePolyline = @polyline,
                          RouteDistance = @dist
                      WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@polyline", polylineJson);
                cmd.Parameters.AddWithValue("@dist", (double)totalDistanceKm);
                cmd.Parameters.AddWithValue("@id", orderID);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ── GET ROUTE POLYLINE FOR AN ORDER ───────────────────────

        // ── GET ROUTE POLYLINE FOR AN ORDER (NEW) ─────────────────
        /// <summary>
        /// Returns the stored RoutePolyline JSON string for an order.
        /// Used by DriverDashboard and CustomerDashboard to render the
        /// Leaflet map without re-calling OSRM.
        /// Returns null if not yet set (order not assigned or OSRM failed).
        /// </summary>
        public string? GetRoutePolyline(int orderID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "SELECT RoutePolyline FROM Table_Orders WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@id", orderID);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return null;
                return result.ToString();
            }
            catch { return null; }
        }

        // ── GET ROUTE DISTANCE FOR AN ORDER (NEW) ─────────────────
        /// <summary>
        /// Returns the stored RouteDistance (km) for an order.
        /// Used for fuel burn calculation on DriverDashboard.
        /// Returns 0 if not yet set.
        /// </summary>
        public double GetRouteDistance(int orderID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "SELECT ISNULL(RouteDistance, 0) FROM Table_Orders WHERE OrderID = @id", conn);
                cmd.Parameters.AddWithValue("@id", orderID);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToDouble(result) : 0.0;
            }
            catch { return 0.0; }
        }

        // ── ASSIGN DRIVER (legacy — kept for compatibility) ────────
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
                             ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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
                             NULL AS DriverID,
                             ISNULL(o.VehicleID, 0) AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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
                             NULL AS DriverID,
                             ISNULL(o.VehicleID, 0) AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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
                             NULL AS DriverID,
                             ISNULL(o.VehicleID, 0) AS VehicleID,
                             o.ItemName, o.Weight, o.Priority,
                             o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                             o.TotalFare, o.PaymentStatus,
                             ISNULL(o.Rating, 0) AS Rating, o.OrderDate
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
                OrderDate = r["OrderDate"] != DBNull.Value
                                    ? Convert.ToDateTime(r["OrderDate"]) : DateTime.Now
            };
        }
    }
}
