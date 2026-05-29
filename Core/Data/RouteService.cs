// =============================================================
//  OptiRoute  |  Core/Services/RouteService.cs
//
//  GEOCODING  : Nominatim (OpenStreetMap) — free, no key needed.
//  ROUTING    : OpenRouteService (ORS) driving-car profile.
//
//  FALLBACK BEHAVIOUR (new):
//    If geocoding fails  → use hardcoded centre-of-city coordinates
//                          for Lahore (31.5204, 74.3587). The result
//                          is flagged as IsEstimated = true so the UI
//                          can warn the admin.
//    If ORS routing fails → use straight-line distance (Haversine)
//                          and estimate ETA at 40 km/h average speed.
//                          Fuel is estimated from that distance.
//                          All values flagged as IsEstimated = true.
//    Assignment still works in both fallback cases — the admin just
//    sees a clear warning banner on the map and in the info panel.
//
//  NAMESPACE  : OptiRoute.Core.Services
//               AdminDashboardForm must use:
//                 using OptiRoute.Core.Services;
//               and call:
//                 var svc = new RouteService();
//                 RouteResult? r = await svc.GetOptimalRouteAsync(pickup, delivery);
// =============================================================
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OptiRoute.Core.Services
{
    // ─────────────────────────────────────────────────────────
    //  RESULT MODEL
    // ─────────────────────────────────────────────────────────
    public class RouteResult
    {
        /// <summary>Real road distance in km (or Haversine estimate if ORS failed).</summary>
        public double TotalDistanceKm { get; set; }

        /// <summary>Estimated drive time in minutes.</summary>
        public double DurationMinutes { get; set; }

        /// <summary>Polyline points as [lat, lng] pairs — for Leaflet.</summary>
        public List<double[]> Polyline { get; set; } = new();

        /// <summary>JSON string of Polyline — store in Table_Orders.RoutePolyline.</summary>
        public string PolylineJson { get; set; } = "[]";

        /// <summary>[lat, lng] of pickup — resolved by Nominatim or fallback.</summary>
        public double[] PickupCoords { get; set; } = Array.Empty<double>();

        /// <summary>[lat, lng] of delivery — resolved by Nominatim or fallback.</summary>
        public double[] DestinCoords { get; set; } = Array.Empty<double>();

        // ── Fallback flags ────────────────────────────────────
        /// <summary>
        /// True when any value in this result is estimated rather than real.
        /// AdminDashboard shows a warning banner when this is true.
        /// </summary>
        public bool IsEstimated { get; set; } = false;

        /// <summary>
        /// True if pickup coordinates came from the hardcoded city-centre fallback.
        /// </summary>
        public bool PickupIsApproximate { get; set; } = false;

        /// <summary>
        /// True if delivery coordinates came from the hardcoded city-centre fallback.
        /// </summary>
        public bool DeliveryIsApproximate { get; set; } = false;

        /// <summary>
        /// Human-readable explanation of what failed and what was estimated.
        /// Shown in the warning banner on the map.
        /// </summary>
        public string FallbackNote { get; set; } = "";
    }

    // ─────────────────────────────────────────────────────────
    //  SERVICE
    // ─────────────────────────────────────────────────────────
    public class RouteService
    {
        // ── ORS credentials ───────────────────────────────────
        private const string OrsApiKey =
            "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6IjVkZjk2OTBiMzEwMzRiMDliNmYxMDAxOTVjNDJkN2U5IiwiaCI6Im11cm11cjY0In0=";
        private const string OrsUrl =
            "https://api.openrouteservice.org/v2/directions/driving-car";

        // ── Nominatim ─────────────────────────────────────────
        private const string NominatimBase =
            "https://nominatim.openstreetmap.org/search";

        // ── Fallback coordinates (centre of Lahore, Pakistan) ─
        // Used when Nominatim cannot resolve an address.
        // Chosen because OptiRoute is a Lahore-centric project.
        private const double FallbackLat = 31.5204;
        private const double FallbackLng = 74.3587;

        // ── Fallback speed for ETA when ORS fails (km/h) ──────
        private const double FallbackSpeedKmh = 40.0;

        // ── Rate limiting ─────────────────────────────────────
        private static readonly SemaphoreSlim _throttle = new(1, 1);
        private static DateTime _lastNominatimCall = DateTime.MinValue;

        // ─────────────────────────────────────────────────────────
        //  PUBLIC ENTRY POINT
        //  Always returns a non-null RouteResult.
        //  IsEstimated = true means the admin should see a warning.
        // ─────────────────────────────────────────────────────────
        public async Task<RouteResult> GetOptimalRouteAsync(
            string pickupAddress,
            string deliveryAddress)
        {
            var result = new RouteResult();
            var notes = new System.Text.StringBuilder();
            bool anyFail = false;

            // ── Step 1: Geocode pickup ────────────────────────────
            double[]? pickupLngLat = null;
            try { pickupLngLat = await GeocodeAsync(pickupAddress); }
            catch { /* network error — handled below */ }

            if (pickupLngLat == null)
            {
                pickupLngLat = new[] { FallbackLng, FallbackLat };
                result.PickupIsApproximate = true;
                result.IsEstimated = true;
                anyFail = true;
                notes.AppendLine($"⚠️ Pickup address not found: \"{pickupAddress}\"");
                notes.AppendLine($"   Using city-centre coordinates ({FallbackLat:F4}, {FallbackLng:F4}).");
            }

            // ── Step 2: Geocode delivery ──────────────────────────
            double[]? deliveryLngLat = null;
            try { deliveryLngLat = await GeocodeAsync(deliveryAddress); }
            catch { /* network error — handled below */ }

            if (deliveryLngLat == null)
            {
                // Offset the fallback slightly so pickup/delivery markers are distinct on map
                deliveryLngLat = new[] { FallbackLng + 0.02, FallbackLat + 0.02 };
                result.DeliveryIsApproximate = true;
                result.IsEstimated = true;
                anyFail = true;
                notes.AppendLine($"⚠️ Delivery address not found: \"{deliveryAddress}\"");
                notes.AppendLine($"   Using approximate city-centre coordinates.");
            }

            // ── Step 3: Set coordinate metadata (lat,lng for Leaflet)
            result.PickupCoords = new[] { pickupLngLat[1], pickupLngLat[0] };
            result.DestinCoords = new[] { deliveryLngLat[1], deliveryLngLat[0] };

            // ── Step 4: Try ORS routing ───────────────────────────
            bool orsSuccess = false;
            try
            {
                var waypoints = new List<double[]> { pickupLngLat, deliveryLngLat };
                RouteResult? orsResult = await CallOrsAsync(waypoints);

                if (orsResult != null)
                {
                    result.TotalDistanceKm = orsResult.TotalDistanceKm;
                    result.DurationMinutes = orsResult.DurationMinutes;
                    result.Polyline = orsResult.Polyline;
                    orsSuccess = true;
                }
            }
            catch { /* ORS failure handled below */ }

            // ── Step 5: ORS failed → Haversine straight-line fallback
            if (!orsSuccess)
            {
                result.IsEstimated = true;
                anyFail = true;

                double distKm = HaversineKm(
                    pickupLngLat[1], pickupLngLat[0],
                    deliveryLngLat[1], deliveryLngLat[0]);

                // Add 30% to straight-line for road detours
                double roadEst = Math.Round(distKm * 1.3, 1);
                double etaMin = Math.Round((roadEst / FallbackSpeedKmh) * 60.0, 0);

                result.TotalDistanceKm = roadEst;
                result.DurationMinutes = etaMin;

                // Build a straight-line polyline so the map shows something
                result.Polyline = new List<double[]>
                {
                    new[] { pickupLngLat[1],   pickupLngLat[0]   },
                    new[] { deliveryLngLat[1], deliveryLngLat[0] }
                };

                notes.AppendLine("⚠️ ORS routing API unavailable.");
                notes.AppendLine($"   Distance estimated from straight-line ({distKm:N1} km × 1.3 road factor = {roadEst:N1} km).");
                notes.AppendLine($"   ETA estimated at {FallbackSpeedKmh} km/h avg speed.");
                notes.AppendLine("   These are NOT real route values.");
            }

            // ── Step 6: Serialise polyline ────────────────────────
            result.PolylineJson = JsonSerializer.Serialize(result.Polyline);

            // ── Step 7: Build fallback note ───────────────────────
            if (anyFail)
                result.FallbackNote = notes.ToString().Trim();

            return result;
        }

        // ─────────────────────────────────────────────────────────
        //  NOMINATIM GEOCODER
        //  Returns [lng, lat] (ORS order) or null on failure.
        // ─────────────────────────────────────────────────────────
        private static async Task<double[]?> GeocodeAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            await _throttle.WaitAsync();
            try
            {
                double elapsed = (DateTime.UtcNow - _lastNominatimCall).TotalMilliseconds;
                if (elapsed < 1100) await Task.Delay((int)(1100 - elapsed));

                string encoded = Uri.EscapeDataString(address.Trim());
                string url = $"{NominatimBase}?q={encoded}&format=json&limit=1&addressdetails=0&countrycodes=pk";

                using var client = MakeClient();
                var resp = await client.GetAsync(url);
                _lastNominatimCall = DateTime.UtcNow;
                if (!resp.IsSuccessStatusCode) return null;

                string body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                    return null;

                var first = root[0];
                if (!first.TryGetProperty("lat", out var latEl) ||
                    !first.TryGetProperty("lon", out var lonEl)) return null;

                if (!double.TryParse(latEl.GetString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double lat)) return null;
                if (!double.TryParse(lonEl.GetString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double lng)) return null;

                return new[] { lng, lat }; // ORS wants [lng, lat]
            }
            catch { return null; }
            finally { _throttle.Release(); }
        }

        // ─────────────────────────────────────────────────────────
        //  ORS ROUTING CALL
        //  Returns null on any failure — caller uses Haversine.
        // ─────────────────────────────────────────────────────────
        private static async Task<RouteResult?> CallOrsAsync(List<double[]> waypoints)
        {
            try
            {
                var body = new
                {
                    coordinates = waypoints,
                    geometry = true,
                    instructions = false,
                    units = "km"
                };
                string json = JsonSerializer.Serialize(body);

                using var client = MakeClient();
                client.DefaultRequestHeaders.Add("Authorization", OrsApiKey);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await client.PostAsync(OrsUrl, content);

                if (!resp.IsSuccessStatusCode) return null;

                string respBody = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respBody);
                var root = doc.RootElement;

                if (!root.TryGetProperty("routes", out var routes) ||
                    routes.GetArrayLength() == 0) return null;

                var route = routes[0];
                double dist = 0, dur = 0;

                if (route.TryGetProperty("summary", out var summary))
                {
                    if (summary.TryGetProperty("distance", out var d)) dist = d.GetDouble();
                    if (summary.TryGetProperty("duration", out var du)) dur = du.GetDouble();
                }

                // Parse geometry — ORS returns [lng, lat], convert to [lat, lng] for Leaflet
                var polyline = new List<double[]>();
                if (route.TryGetProperty("geometry", out var geom) &&
                    geom.TryGetProperty("coordinates", out var coords))
                {
                    foreach (var pt in coords.EnumerateArray())
                        polyline.Add(new[] { pt[1].GetDouble(), pt[0].GetDouble() });
                }

                return new RouteResult
                {
                    TotalDistanceKm = Math.Round(dist, 1),
                    DurationMinutes = Math.Round(dur / 60.0, 1),
                    Polyline = polyline
                };
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────────────────
        //  HAVERSINE STRAIGHT-LINE DISTANCE (km)
        //  Used when ORS fails.
        // ─────────────────────────────────────────────────────────
        private static double HaversineKm(
            double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371.0; // Earth radius km
            double dLat = ToRad(lat2 - lat1);
            double dLng = ToRad(lng2 - lng1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                     * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;

        // ─────────────────────────────────────────────────────────
        //  HTTP CLIENT FACTORY
        // ─────────────────────────────────────────────────────────
        private static HttpClient MakeClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(
                "OptiRoute/1.0 (student-project; contact@optiroute.local)");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return c;
        }
    }
}
