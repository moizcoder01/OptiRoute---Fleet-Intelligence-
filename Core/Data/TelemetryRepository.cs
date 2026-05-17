// =============================================================
//  OptiRoute  |  Core/Data/TelemetryRepository.cs
//  All SQL for Table_Telemetry.
//
//  Schema: TID, OrderID, Latitude, Longitude, FuelBurned
//
//  Called by Driver laptop every 5-10 seconds.
//  Read by Customer laptop for live simulation.
// =============================================================
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace OptiRoute.Core.Data
{
    // Simple data carrier — no business logic
    public class TelemetryPoint
    {
        public int TID { get; set; }
        public int OrderID { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double FuelBurned { get; set; }
    }

    public class TelemetryRepository
    {
        private readonly string _cs;

        public TelemetryRepository()
        {
            _cs = DbConfig.ConnectionString;
        }

        // ── INSERT — called from Driver laptop every 5-10 seconds ──
        public bool InsertPoint(int orderID, double latitude,
                                 double longitude, double fuelBurned)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"INSERT INTO Table_Telemetry (OrderID, Latitude, Longitude, FuelBurned)
                      VALUES (@oid, @lat, @lng, @fuel)", conn);
                cmd.Parameters.AddWithValue("@oid", orderID);
                cmd.Parameters.AddWithValue("@lat", latitude);
                cmd.Parameters.AddWithValue("@lng", longitude);
                cmd.Parameters.AddWithValue("@fuel", fuelBurned);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── GET LATEST POINT — customer tracking page polls this ───
        public TelemetryPoint? GetLatest(int orderID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT TOP 1 TID, OrderID, Latitude, Longitude, FuelBurned
                      FROM   Table_Telemetry
                      WHERE  OrderID = @oid
                      ORDER BY TID DESC", conn);
                cmd.Parameters.AddWithValue("@oid", orderID);

                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                return new TelemetryPoint
                {
                    TID = Convert.ToInt32(r["TID"]),
                    OrderID = Convert.ToInt32(r["OrderID"]),
                    Latitude = Convert.ToDouble(r["Latitude"]),
                    Longitude = Convert.ToDouble(r["Longitude"]),
                    FuelBurned = Convert.ToDouble(r["FuelBurned"])
                };
            }
            catch { return null; }
        }

        // ── GET ALL POINTS FOR AN ORDER — for drawing the full route
        public List<TelemetryPoint> GetAllForOrder(int orderID)
        {
            var list = new List<TelemetryPoint>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT TID, OrderID, Latitude, Longitude, FuelBurned
                      FROM   Table_Telemetry
                      WHERE  OrderID = @oid
                      ORDER BY TID ASC", conn);
                cmd.Parameters.AddWithValue("@oid", orderID);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(new TelemetryPoint
                    {
                        TID = Convert.ToInt32(r["TID"]),
                        OrderID = Convert.ToInt32(r["OrderID"]),
                        Latitude = Convert.ToDouble(r["Latitude"]),
                        Longitude = Convert.ToDouble(r["Longitude"]),
                        FuelBurned = Convert.ToDouble(r["FuelBurned"])
                    });
            }
            catch { }
            return list;
        }

        // ── TOTAL FUEL BURNED FOR AN ORDER ────────────────────────
        public double GetTotalFuelBurned(int orderID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "SELECT ISNULL(SUM(FuelBurned), 0) FROM Table_Telemetry WHERE OrderID = @oid",
                    conn);
                cmd.Parameters.AddWithValue("@oid", orderID);
                return Convert.ToDouble(cmd.ExecuteScalar());
            }
            catch { return 0; }
        }
    }
}
