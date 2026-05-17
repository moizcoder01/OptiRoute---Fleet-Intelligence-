// =============================================================
//  OptiRoute  |  Core/Data/UserRepository.cs
//  All SQL for Table_Users — no UI code ever goes here
// =============================================================
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Data
{
    public class UserRepository
    {
        private readonly string _cs;

        public UserRepository()
        {
            _cs = DbConfig.ConnectionString;
        }

        // ── GET HASH AND ROLE (used by LoginForm BCrypt flow) ─────
        public (string Hash, string Role) GetHashAndRole(string username)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "SELECT Password, UserRole FROM Table_Users WHERE Username = @u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    return (r["Password"]?.ToString() ?? "", r["UserRole"]?.ToString() ?? "");
                return ("", "");
            }
            catch { return ("", ""); }
        }

        // ── CHECK USERNAME EXISTS ─────────────────────────────────
        public bool UsernameExists(string username)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM Table_Users WHERE Username = @u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch { return false; }
        }

        // ── REGISTER CUSTOMER ─────────────────────────────────────
        public int RegisterCustomer(string firstName, string lastName,
                                     string username, string passwordHash,
                                     string email, string phone)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"INSERT INTO Table_Users
                        (FirstName, LastName, Username, Password,
                         Email, Phone, UserRole)
                      VALUES
                        (@fn, @ln, @u, @p, @e, @ph, 'Customer');
                      SELECT SCOPE_IDENTITY();", conn);

                cmd.Parameters.AddWithValue("@fn", firstName);
                cmd.Parameters.AddWithValue("@ln", lastName);
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", passwordHash);
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@ph", phone);

                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : -1;
            }
            catch { return -1; }
        }

        // ── REGISTER DRIVER ───────────────────────────────────────
        public int RegisterDriver(string firstName, string lastName,
                                   string username, string passwordHash,
                                   string email, string phone,
                                   string licenseNumber, string plateNumber,
                                   string vehicleType)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                using var txn = conn.BeginTransaction();
                try
                {
                    var userCmd = new SqlCommand(
                        @"INSERT INTO Table_Users
                            (FirstName, LastName, Username, Password,
                             Email, Phone, UserRole)
                          VALUES
                            (@fn, @ln, @u, @p, @e, @ph, 'Driver');
                          SELECT SCOPE_IDENTITY();", conn, txn);

                    userCmd.Parameters.AddWithValue("@fn", firstName);
                    userCmd.Parameters.AddWithValue("@ln", lastName);
                    userCmd.Parameters.AddWithValue("@u", username);
                    userCmd.Parameters.AddWithValue("@p", passwordHash);
                    userCmd.Parameters.AddWithValue("@e", email);
                    userCmd.Parameters.AddWithValue("@ph", phone);

                    var result = userCmd.ExecuteScalar();
                    if (result == null) { txn.Rollback(); return -1; }
                    int newUserID = Convert.ToInt32(result);

                    var drvCmd = new SqlCommand(
                        @"INSERT INTO Table_Drivers (DriverID, Rating, LicenseNumber)
                          VALUES (@id, 5.0, @lic);", conn, txn);
                    drvCmd.Parameters.AddWithValue("@id", newUserID);
                    drvCmd.Parameters.AddWithValue("@lic", licenseNumber);
                    drvCmd.ExecuteNonQuery();

                    var vehCmd = new SqlCommand(
                        @"INSERT INTO Table_Vehicles
                            (DriverID, PlateNumber, VehicleType,
                             IsAvailable, NeedsMaintenance, CurrentFuel)
                          VALUES
                            (@did, @plate, @vtype, 1, 0, 100.0);", conn, txn);
                    vehCmd.Parameters.AddWithValue("@did", newUserID);
                    vehCmd.Parameters.AddWithValue("@plate", plateNumber);
                    vehCmd.Parameters.AddWithValue("@vtype", vehicleType);
                    vehCmd.ExecuteNonQuery();

                    txn.Commit();
                    return newUserID;
                }
                catch { txn.Rollback(); return -1; }
            }
            catch { return -1; }
        }

        // ── LOAD CUSTOMER ─────────────────────────────────────────
        public Customer? LoadCustomer(string username)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();

                try
                {
                    var cmd = new SqlCommand(
                        @"SELECT UserID, FirstName, LastName, Email, Phone, ProfilePhoto
                          FROM   Table_Users
                          WHERE  Username = @u AND UserRole = 'Customer'", conn);
                    cmd.Parameters.AddWithValue("@u", username);
                    using var r = cmd.ExecuteReader();
                    if (r.Read()) return MapCustomer(r, username, hasPhoto: true);
                }
                catch { }

                var cmd2 = new SqlCommand(
                    @"SELECT UserID, FirstName, LastName, Email, Phone
                      FROM   Table_Users
                      WHERE  Username = @u AND UserRole = 'Customer'", conn);
                cmd2.Parameters.AddWithValue("@u", username);
                using var r2 = cmd2.ExecuteReader();
                if (r2.Read()) return MapCustomer(r2, username, hasPhoto: false);

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception("CustomerLoad: " + ex.Message, ex);
            }
        }

        // ── LOAD ADMIN ────────────────────────────────────────────
        public Admin? LoadAdmin(string username)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT UserID, FirstName, LastName, Email, Phone
                      FROM   Table_Users
                      WHERE  Username = @u AND UserRole = 'Admin'", conn);
                cmd.Parameters.AddWithValue("@u", username);

                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                return new Admin
                {
                    UserID = r["UserID"] != DBNull.Value ? Convert.ToInt32(r["UserID"]) : 0,
                    Username = username,
                    FirstName = r["FirstName"]?.ToString() ?? "",
                    LastName = r["LastName"]?.ToString() ?? "",
                    Email = r["Email"]?.ToString() ?? "",
                    Phone = r["Phone"]?.ToString() ?? ""
                };
            }
            catch (Exception ex)
            {
                throw new Exception("AdminLoad: " + ex.Message, ex);
            }
        }

        // ── GET ALL DRIVERS (Admin pages) ─────────────────────────
        public List<Driver> GetAvailableDrivers()
        {
            var list = new List<Driver>();
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"SELECT
                        u.UserID,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.Phone,
                        u.Username,
                        u.UserRole,
                        d.Rating          AS DriverRating,
                        d.LicenseNumber,
                        v.VehicleID,
                        ISNULL(v.PlateNumber,      'N/A')  AS PlateNumber,
                        ISNULL(v.VehicleType,      'Bike') AS VehicleType,
                        ISNULL(v.CurrentFuel,      100.0)  AS CurrentFuel,
                        ISNULL(v.IsAvailable,      1)      AS IsAvailable,
                        ISNULL(v.NeedsMaintenance, 0)      AS NeedsMaintenance
                      FROM  Table_Users    u
                      INNER JOIN Table_Drivers  d ON d.DriverID = u.UserID
                      LEFT  JOIN Table_Vehicles v ON v.DriverID = d.DriverID
                      WHERE u.UserRole = 'Driver'
                      ORDER BY u.FirstName", conn);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapDriver(r));
            }
            catch { }
            return list;
        }

        // ── UPDATE PROFILE (name / email / phone) ─────────────────
        public bool UpdateProfile(int userID, string firstName, string lastName,
                                   string email, string phone)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Users
                      SET FirstName = @fn, LastName = @ln,
                          Email = @e, Phone = @ph
                      WHERE UserID = @id", conn);
                cmd.Parameters.AddWithValue("@fn", firstName);
                cmd.Parameters.AddWithValue("@ln", lastName);
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@ph", phone);
                cmd.Parameters.AddWithValue("@id", userID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── UPDATE PASSWORD ───────────────────────────────────────
        /// <summary>
        /// Verifies the current plain-text password against the stored
        /// BCrypt hash, then writes the new BCrypt hash to the DB.
        /// Returns (true, "") on success or (false, errorMessage) on failure.
        /// All BCrypt work is done here — no BCrypt calls in the Form.
        /// </summary>
        public (bool Success, string Error) UpdatePassword(int userID,
                                                            string currentPasswordPlain,
                                                            string newPasswordHash)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();

                // 1. Fetch stored hash
                var fetch = new SqlCommand(
                    "SELECT Password FROM Table_Users WHERE UserID = @id", conn);
                fetch.Parameters.AddWithValue("@id", userID);
                var scalar = fetch.ExecuteScalar();

                if (scalar == null || scalar == DBNull.Value)
                    return (false, "User account not found.");

                string storedHash = scalar.ToString()!;

                // 2. BCrypt verify — current password must match
                if (!BCrypt.Net.BCrypt.Verify(currentPasswordPlain, storedHash))
                    return (false, "Current password is incorrect.");

                // 3. Write new hash
                var upd = new SqlCommand(
                    "UPDATE Table_Users SET Password = @pw WHERE UserID = @id", conn);
                upd.Parameters.AddWithValue("@pw", newPasswordHash);
                upd.Parameters.AddWithValue("@id", userID);
                upd.ExecuteNonQuery();

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, "Database error: " + ex.Message);
            }
        }

        // ── SAVE PROFILE PHOTO PATH ───────────────────────────────
        /// <summary>
        /// Stores the local file path in ProfilePhoto column.
        /// The image file itself is NOT stored in the DB —
        /// only the path so the form can reload it on next login.
        /// </summary>
        public bool SaveProfilePhoto(string username, string photoPath)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();

                // Safe migration — add column if it doesn't exist yet
                new SqlCommand(
                    @"IF NOT EXISTS (
                          SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                          WHERE  TABLE_NAME  = 'Table_Users'
                          AND    COLUMN_NAME = 'ProfilePhoto')
                      ALTER TABLE Table_Users ADD ProfilePhoto NVARCHAR(500) NULL;", conn)
                    .ExecuteNonQuery();

                var upd = new SqlCommand(
                    "UPDATE Table_Users SET ProfilePhoto = @pp WHERE Username = @u", conn);
                upd.Parameters.AddWithValue("@pp",
                    string.IsNullOrEmpty(photoPath) ? DBNull.Value : (object)photoPath);
                upd.Parameters.AddWithValue("@u", username);
                upd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── DRIVER FUEL UPDATE ────────────────────────────────────
        public bool UpdateDriverFuel(int driverID, double fuelLevel)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    "UPDATE Table_Vehicles SET CurrentFuel = @fl WHERE DriverID = @id", conn);
                cmd.Parameters.AddWithValue("@fl", fuelLevel);
                cmd.Parameters.AddWithValue("@id", driverID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── DRIVER RATING UPDATE ──────────────────────────────────
        public bool UpdateDriverAverageRating(int driverID)
        {
            try
            {
                using var conn = new SqlConnection(_cs);
                conn.Open();
                var cmd = new SqlCommand(
                    @"UPDATE Table_Drivers
                      SET Rating = (
                          SELECT AVG(CAST(o.Rating AS FLOAT))
                          FROM   Table_Orders   o
                          INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                          WHERE  v.DriverID = @id AND o.Rating > 0)
                      WHERE DriverID = @id", conn);
                cmd.Parameters.AddWithValue("@id", driverID);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────
        private static Customer MapCustomer(SqlDataReader r, string username, bool hasPhoto)
        {
            return new Customer
            {
                UserID = r["UserID"] != DBNull.Value ? Convert.ToInt32(r["UserID"]) : 0,
                Username = username,
                FirstName = r["FirstName"]?.ToString() ?? "",
                LastName = r["LastName"]?.ToString() ?? "",
                Email = r["Email"]?.ToString() ?? "",
                Phone = r["Phone"]?.ToString() ?? "",
                ProfilePhotoPath = hasPhoto ? (r["ProfilePhoto"]?.ToString() ?? "") : ""
            };
        }

        private static Driver MapDriver(SqlDataReader r)
        {
            return new Driver
            {
                UserID = Convert.ToInt32(r["UserID"]),
                Username = r["Username"]?.ToString() ?? "",
                FirstName = r["FirstName"]?.ToString() ?? "",
                LastName = r["LastName"]?.ToString() ?? "",
                Email = r["Email"]?.ToString() ?? "",
                Phone = r["Phone"]?.ToString() ?? "",
                LicenseNumber = r["LicenseNumber"] != DBNull.Value ? r["LicenseNumber"].ToString()! : "",
                VehicleID = r["VehicleID"] != DBNull.Value ? Convert.ToInt32(r["VehicleID"]) : 0,
                PlateNumber = r["PlateNumber"]?.ToString() ?? "N/A",
                VehicleType = r["VehicleType"]?.ToString() ?? "Bike",
                CurrentFuel = r["CurrentFuel"] != DBNull.Value ? Convert.ToDouble(r["CurrentFuel"]) : 100.0,
                IsAvailable = r["IsAvailable"] != DBNull.Value && Convert.ToBoolean(r["IsAvailable"]),
                NeedsMaintenance = r["NeedsMaintenance"] != DBNull.Value && Convert.ToBoolean(r["NeedsMaintenance"]),
                AverageRating = r["DriverRating"] != DBNull.Value ? Convert.ToDouble(r["DriverRating"]) : 5.0
            };
        }
    }
}