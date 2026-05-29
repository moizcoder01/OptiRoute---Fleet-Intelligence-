// =============================================================
//  OptiRoute  |  Forms/DriverDashboardForm.cs
//
//  CHANGES IN THIS VERSION (zero theme / style / functionality changes):
//    1. LAYOUT EXPANSION: All cards, banners, tables now use dynamic
//       mainPanel.Width calculations (identical to AdminDashboardForm),
//       so every element fills the full page width on any screen size.
//    2. MAP INFO-BOX ICONS: The top-right info card on the Active
//       Delivery map now shows 📦 for Pickup and 🏁 for Delivery,
//       matching the actual map markers (was 🟢 / 🔴 before).
//    3. HOVER EFFECTS: Every card, block, and panel now has the same
//       full hover system as AdminDashboardForm — animated top colored
//       bar (5px → 8px), white shimmer overlay on the bar, blue border
//       glow on the card, and hover propagation from all child controls
//       so the hover never disappears when the mouse moves over labels.
//
//  ARCHITECTURE : Zero SQL in this file.
//  THEME        : 100 % preserved — every colour, font, paint event,
//                 sidebar, and all functionality unchanged.
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using OptiRoute.Core.Data;
using OptiRoute.Core.Models;

namespace OptiRoute.Forms
{
    public class DriverDashboardForm : Form
    {
        // ── Repositories ─────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();
        private readonly DriverRepository _driverRepo = new DriverRepository();
        private readonly OrderRepository _orderRepo = new OrderRepository();
        private readonly TelemetryRepository _telRepo = new TelemetryRepository();

        // ── Driver model ─────────────────────────────────────────
        private Driver _driver = null!;

        // ── Profile photo ─────────────────────────────────────────
        private string _profilePhotoPath = "";
        private Image? _profilePhoto = null;

        // ── Layout panels ─────────────────────────────────────────
        private Panel sidePanel = null!;
        private Panel mainPanel = null!;
        private Panel headerPanel = null!;

        // ── Sidebar controls ──────────────────────────────────────
        private Panel picAvatar = null!;
        private Label lblDriverName = null!;
        private Label lblDriverRole = null!;
        private Button btnDashboard = null!;
        private Button btnAssignments = null!;
        private Button btnActive = null!;
        private Button btnHistory = null!;
        private Button btnProfile = null!;
        private Button btnLogout = null!;
        private Button activeBtn = null!;

        // ── Content panels ────────────────────────────────────────
        private Panel pnlDashboard = null!;
        private Panel pnlAssignments = null!;
        private Panel pnlActive = null!;
        private Panel pnlHistory = null!;
        private Panel pnlProfile = null!;

        // ── Profile-page avatar ───────────────────────────────────
        private Panel pnlProfileAvatar = null!;

        // ── Active-delivery map state ─────────────────────────────
        private WebView2? _activeMapView = null;
        private bool _activeMapReady = false;
        private System.Windows.Forms.Timer? _trackTimer = null;
        private List<(double Lat, double Lng)> _waypoints = new();
        private int _waypointIndex = 0;
        private int _activeOrderId = 0;
        private double _fuelPerWaypoint = 0.0;
        private Label? _lblLiveFuel = null;
        private Panel? _liveFuelFill = null;

        // ── Colours ───────────────────────────────────────────────
        readonly Color Navy = Color.FromArgb(10, 35, 90);
        readonly Color RoyalBlue = Color.FromArgb(0, 82, 204);
        readonly Color SkyBlue = Color.FromArgb(0, 163, 255);
        readonly Color SidebarBg = Color.FromArgb(10, 25, 70);
        readonly Color PageBg = Color.FromArgb(240, 244, 252);
        readonly Color TextDark = Color.FromArgb(18, 32, 60);
        readonly Color TextGray = Color.FromArgb(120, 140, 170);
        readonly Color GreenOk = Color.FromArgb(34, 197, 94);
        readonly Color OrangeWarn = Color.FromArgb(251, 146, 60);
        readonly Color RedAlert = Color.FromArgb(239, 68, 68);
        readonly Color BorderBlue = Color.FromArgb(180, 210, 245);
        readonly Color CardBg = Color.FromArgb(245, 248, 255);

        // ── Fuel consumption rates (L per km) ─────────────────────
        private static double FuelRate(string vehicleType) =>
            vehicleType?.ToLower() switch
            {
                "bike" => 0.05,
                "car" => 0.10,
                "van" => 0.15,
                "truck" => 0.20,
                _ => 0.10
            };

        // ── Vehicle emoji per type ────────────────────────────────
        private static string GetVehicleEmoji(string vehicleType) =>
            vehicleType?.ToLower() switch
            {
                "bike" => "🏍️",
                "car" => "🚗",
                "van" => "🚐",
                "truck" => "🚚",
                _ => "🚗"
            };

        // ─────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────────────────
        public DriverDashboardForm(string userRole, string userName = "Driver")
        {
            try
            {
                _driver = _driverRepo.LoadDriver(userName)
                          ?? new Driver { Username = userName };
            }
            catch
            {
                _driver = new Driver { Username = userName };
            }

            Text = "OptiRoute  |  Driver Dashboard";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(1100, 700);
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 9f);

            FormClosing += (s, e) => StopTrackTimer();

            BuildLayout();
            Load += (s, e) => { RefreshDashboard(); ShowPanel(pnlDashboard, btnDashboard); };
            ShowPanel(pnlDashboard, btnDashboard);
        }

        // ═════════════════════════════════════════════════════════
        //  LAYOUT
        // ═════════════════════════════════════════════════════════
        private void BuildLayout()
        {
            sidePanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(232, ClientSize.Height),
                BackColor = SidebarBg
            };
            sidePanel.Paint += SidePanel_Paint;
            Controls.Add(sidePanel);
            BuildSidebar();

            headerPanel = new Panel
            {
                Location = new Point(232, 0),
                Size = new Size(ClientSize.Width - 232, 62),
                BackColor = Color.White
            };
            headerPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(220, 228, 245), 1);
                e.Graphics.DrawLine(p, 0, headerPanel.Height - 1,
                                       headerPanel.Width, headerPanel.Height - 1);
            };
            Controls.Add(headerPanel);
            BuildHeader();

            mainPanel = new Panel
            {
                Location = new Point(232, 62),
                Size = new Size(ClientSize.Width - 232, ClientSize.Height - 62),
                BackColor = PageBg,
                AutoScroll = false
            };
            Controls.Add(mainPanel);

            BuildDashboardPanel();
            BuildAssignmentsPanel();
            BuildActiveDeliveryPanel();
            BuildHistoryPanel();
            BuildProfilePanel();

            Resize += (s, e) =>
            {
                sidePanel.Size = new Size(232, ClientSize.Height);
                headerPanel.Size = new Size(ClientSize.Width - 232, 62);
                mainPanel.Size = new Size(ClientSize.Width - 232, ClientSize.Height - 62);
                foreach (Control c in mainPanel.Controls)
                    if (c is Panel p) p.Size = mainPanel.Size;
                sidePanel.Invalidate();
                headerPanel.Invalidate();
            };
        }

        // ═════════════════════════════════════════════════════════
        //  SIDEBAR
        // ═════════════════════════════════════════════════════════
        private void BuildSidebar()
        {
            var logo = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(232, 68),
                BackColor = Color.Transparent
            };
            logo.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(
                    logo.ClientRectangle, RoyalBlue, Navy, LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(br, logo.ClientRectangle);
            };
            logo.Controls.Add(new Label
            {
                Text = "⬡  OptiRoute",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(232, 68),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 0)
            });
            sidePanel.Controls.Add(logo);

            picAvatar = new Panel
            {
                Size = new Size(72, 72),
                Location = new Point(80, 85),
                BackColor = OrangeWarn
            };
            picAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                picAvatar.Region = new Region(RndPath(picAvatar.ClientRectangle, 36));
                if (_profilePhoto != null)
                    e.Graphics.DrawImage(_profilePhoto, picAvatar.ClientRectangle);
                else
                {
                    e.Graphics.FillEllipse(new SolidBrush(OrangeWarn), picAvatar.ClientRectangle);
                    string init = _driver.Username.Length > 0
                        ? _driver.Username[0].ToString().ToUpper() : "D";
                    e.Graphics.DrawString(init,
                        new Font("Segoe UI", 26, FontStyle.Bold), Brushes.White,
                        new RectangleF(0, 0, 72, 72), Centre());
                }
            };
            sidePanel.Controls.Add(picAvatar);

            lblDriverName = new Label
            {
                Text = _driver.FullName,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(232, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 165)
            };
            sidePanel.Controls.Add(lblDriverName);

            lblDriverRole = new Label
            {
                Text = "●  " + _driver.Role,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(255, 190, 100),
                BackColor = Color.Transparent,
                Size = new Size(232, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 188)
            };
            sidePanel.Controls.Add(lblDriverRole);

            sidePanel.Controls.Add(new Panel
            {
                Location = new Point(20, 216),
                Size = new Size(192, 1),
                BackColor = Color.FromArgb(50, 255, 255, 255)
            });

            btnDashboard = SideBtn("🏠   Dashboard", 234);
            btnAssignments = SideBtn("📋   My Assignments", 281);
            btnActive = SideBtn("🚚   Active Delivery", 328);
            btnHistory = SideBtn("📜   Delivery History", 375);
            btnProfile = SideBtn("👤   My Profile", 422);

            btnDashboard.Click += (s, e) => { RefreshDashboard(); ShowPanel(pnlDashboard, btnDashboard); };
            btnAssignments.Click += (s, e) => { RefreshAssignments(); ShowPanel(pnlAssignments, btnAssignments); };
            btnActive.Click += (s, e) => { RefreshActiveDelivery(); ShowPanel(pnlActive, btnActive); };
            btnHistory.Click += (s, e) => { RefreshHistory(); ShowPanel(pnlHistory, btnHistory); };
            btnProfile.Click += (s, e) => ShowPanel(pnlProfile, btnProfile);

            btnLogout = new Button
            {
                Text = "⏻   Logout",
                Location = new Point(16, 500),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(255, 100, 100),
                BackColor = Color.FromArgb(30, 255, 80, 80),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) =>
            {
                if (MessageBox.Show("Are you sure you want to logout?", "Logout",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { StopTrackTimer(); new LoginForm().Show(); Close(); }
            };
            sidePanel.Controls.Add(btnLogout);

            sidePanel.Controls.Add(new Label
            {
                Text = "OptiRoute v1.0",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(60, 255, 255, 255),
                BackColor = Color.Transparent,
                Size = new Size(232, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 560)
            });
        }

        private Button SideBtn(string text, int top)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(16, top),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(180, 220, 255),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (s, e) => { if (b != activeBtn) { b.BackColor = Color.FromArgb(0, 163, 255); b.ForeColor = Color.White; } };
            b.MouseLeave += (s, e) => { if (b != activeBtn) { b.BackColor = Color.Transparent; b.ForeColor = Color.FromArgb(180, 220, 255); } };
            sidePanel.Controls.Add(b);
            return b;
        }

        private void ShowPanel(Panel panel, Button btn)
        {
            foreach (Control c in mainPanel.Controls)
                if (c is Panel p) p.Visible = false;
            foreach (Control c in sidePanel.Controls)
                if (c is Button b && b != btnLogout)
                { b.BackColor = Color.Transparent; b.ForeColor = Color.FromArgb(180, 220, 255); }
            panel.Visible = true;
            btn.BackColor = RoyalBlue;
            btn.ForeColor = Color.White;
            activeBtn = btn;
        }

        // ═════════════════════════════════════════════════════════
        //  HEADER
        // ═════════════════════════════════════════════════════════
        private void BuildHeader()
        {
            headerPanel.Controls.Add(new Label
            {
                Text = "Driver Dashboard",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(22, 8),
                BackColor = Color.White
            });

            var lblDate = new Label
            {
                Text = "📅  " + DateTime.Now.ToString("dddd, dd MMMM yyyy"),
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextGray,
                AutoSize = true,
                BackColor = Color.White
            };
            lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 24, 20);
            headerPanel.Controls.Add(lblDate);
            headerPanel.Resize += (s, e) =>
                lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 24, 20);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 1 — DASHBOARD HOME
        //  Pattern mirrors AdminDashboardForm: empty builder + RefreshDashboard
        //  called on Load and on btnDashboard click so every element fills
        //  the full page width regardless of window size at construction time.
        // ═════════════════════════════════════════════════════════
        private void BuildDashboardPanel() { pnlDashboard = MakePage(autoScroll: true); }

        private void RefreshDashboard()
        {
            pnlDashboard.Controls.Clear();

            int W = mainPanel.Width > 0 ? mainPanel.Width : 1280;

            // ── Welcome banner — full width ───────────────────────
            var banner = new Panel
            {
                Location = new Point(30, 22),
                Size = new Size(W - 60, 92),
                BackColor = Color.Transparent
            };
            banner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(banner.ClientRectangle,
                    Color.FromArgb(200, 90, 0), OrangeWarn, LinearGradientMode.Horizontal);
                using var path = RndPath(banner.ClientRectangle, 16);
                e.Graphics.FillPath(br, path);
                banner.Region = new Region(path);
            };
            banner.Controls.Add(L("🚚  Ready to deliver, " + _driver.Username + "!",
                new Font("Segoe UI", 15, FontStyle.Bold), Color.White, new Point(24, 14)));
            banner.Controls.Add(L("Check your assignments and manage your deliveries from here.",
                new Font("Segoe UI", 10f), Color.FromArgb(255, 230, 180), new Point(24, 52)));
            pnlDashboard.Controls.Add(banner);

            // ── Stat cards row — 4 equal columns like Admin ───────
            var stats = _driverRepo.GetDriverStats(_driver.UserID);
            int gap = 16;
            int cW = (W - 60 - 3 * gap) / 4;
            int cH = 130;
            int cTop = 134;
            StatCard(pnlDashboard, 30, cTop, "📋", "Assigned", stats.Assigned.ToString(), RoyalBlue, cW, cH);
            StatCard(pnlDashboard, 30 + (cW + gap), cTop, "✅", "Delivered", stats.Delivered.ToString(), GreenOk, cW, cH);
            StatCard(pnlDashboard, 30 + (cW + gap) * 2, cTop, "⏳", "Pending Pickup", stats.Pending.ToString(), OrangeWarn, cW, cH);
            StatCard(pnlDashboard, 30 + (cW + gap) * 3, cTop, "↩️", "Returned", stats.Returned.ToString(), RedAlert, cW, cH);

            int row2Y = cTop + cH + gap;
            int halfW = (W - 60 - gap) / 2;

            // ── Vehicle & Fuel card — left half ───────────────────
            var vCard = Card(30, row2Y, halfW, 160);
            pnlDashboard.Controls.Add(vCard);
            AttachStripHover(vCard, halfW, OrangeWarn);
            vCard.Controls.Add(L("🚗  Vehicle Info", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            vCard.Controls.Add(L("Number Plate:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 46)));
            vCard.Controls.Add(L(_driver.PlateNumber, new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 64)));
            vCard.Controls.Add(L("Type:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(halfW / 2 + 10, 46)));
            vCard.Controls.Add(L(_driver.VehicleType, new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(halfW / 2 + 10, 64)));
            vCard.Controls.Add(L("⛽  Fuel Level:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 98)));
            double fuel = Math.Min(100, Math.Max(0, _driver.CurrentFuel));
            Color fuelColor = fuel > 60 ? GreenOk : fuel > 25 ? OrangeWarn : RedAlert;
            int trackW = halfW - 40;
            var fuelTrack = new Panel { Location = new Point(20, 118), Size = new Size(trackW, 14), BackColor = Color.FromArgb(220, 228, 245) };
            fuelTrack.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; fuelTrack.Region = new Region(RndPath(fuelTrack.ClientRectangle, 7)); };
            var fuelFill = new Panel { Location = new Point(0, 0), Size = new Size((int)(trackW * fuel / 100.0), 14), BackColor = fuelColor };
            fuelFill.Paint += (s, e) => { if (fuelFill.Width > 7) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; fuelFill.Region = new Region(RndPath(fuelFill.ClientRectangle, 7)); } };
            fuelTrack.Controls.Add(fuelFill);
            vCard.Controls.Add(fuelTrack);
            vCard.Controls.Add(L(fuel + "%", new Font("Segoe UI", 9f, FontStyle.Bold), fuelColor, new Point(trackW + 26, 115)));

            // ── Rating card — right half ──────────────────────────
            var rCard = Card(30 + halfW + gap, row2Y, halfW, 160);
            pnlDashboard.Controls.Add(rCard);
            bool hasDeliveries = stats.Delivered > 0;
            Color ratingAccent = hasDeliveries ? Color.Gold : Color.FromArgb(200, 210, 225);
            Color ratingValue = hasDeliveries ? OrangeWarn : TextGray;
            Color starColor = hasDeliveries ? Color.Gold : Color.FromArgb(180, 195, 215);
            double avg = hasDeliveries ? _driver.AverageRating : 5.0;
            int fullS = hasDeliveries ? (int)Math.Round(avg) : 5;
            AttachStripHover(rCard, halfW, ratingAccent);
            rCard.Controls.Add(L("⭐  My Rating & Performance", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            rCard.Controls.Add(L(avg.ToString("0.0") + " / 5.0", new Font("Segoe UI", 26, FontStyle.Bold), ratingValue, new Point(20, 42)));
            rCard.Controls.Add(L(new string('★', fullS) + new string('☆', 5 - fullS), new Font("Segoe UI", 17), starColor, new Point(20, 96)));
            rCard.Controls.Add(L(hasDeliveries ? "Based on " + stats.TotalRatings + " ratings" : "No deliveries yet",
                new Font("Segoe UI", 9.5f), TextGray, new Point(halfW / 2, 50)));
            rCard.Controls.Add(L("Total delivered: " + stats.Delivered, new Font("Segoe UI", 9.5f), TextGray, new Point(halfW / 2, 72)));

            // ── Recent Assignments table — full width ─────────────
            int tblTop = row2Y + 160 + gap;
            int tblH = Math.Max(280, mainPanel.Height - tblTop - 20);
            int tblW = W - 60;
            var tbl = Card(30, tblTop, tblW, tblH);
            pnlDashboard.Controls.Add(tbl);
            AttachStripHover(tbl, tblW, RoyalBlue);
            tbl.Controls.Add(L("📋  Recent Assignments", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 12)));

            // Column widths scale with table width
            int fixedW = 90 + 130 + 80 + 100 + 130 + 90;
            int routeCol = Math.Max(140, (tblW - 32 - fixedW) / 2);
            int[] wids = { 90, 130, 80, 100, routeCol, routeCol, 130, 90 };
            string[] hdrs = { "Order ID", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Status", "Fare" };
            int hx = 16;
            foreach (var (h, w) in Zip(hdrs, wids))
            {
                tbl.Controls.Add(new Label { Text = h, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextGray, BackColor = CardBg, Size = new Size(w, 32), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(hx, 46), Padding = new Padding(4, 0, 0, 0) });
                hx += w;
            }
            int rowY = 84; bool alt = false;
            foreach (Order o in _driverRepo.GetRecentAssignments(_driver.UserID, 6))
            {
                string[] row = { "#" + o.OrderID, o.ItemName, o.WeightDisplay, o.Priority,
                                  o.PickupPoint, o.DeliveryPoint, o.OrderStatus, o.FormattedFare };
                Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White; alt = !alt;
                int rx = 16;
                foreach (var (cell, w) in Zip(row, wids))
                {
                    Color fg = cell == "Picked" ? OrangeWarn : cell == "Delivered" ? GreenOk :
                               cell == "Urgent" ? RedAlert : cell == "Assigned" ? RoyalBlue : TextDark;
                    tbl.Controls.Add(new Label { Text = cell, Font = new Font("Segoe UI", 9.5f), ForeColor = fg, BackColor = bg, Size = new Size(w, 34), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(rx, rowY), Padding = new Padding(4, 0, 0, 0) });
                    rx += w;
                }
                rowY += 36;
            }
            pnlDashboard.AutoScrollMinSize = new Size(1, tblTop + tblH + 30);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 2 — MY ASSIGNMENTS
        // ═════════════════════════════════════════════════════════
        private void BuildAssignmentsPanel()
        {
            pnlAssignments = MakePage(autoScroll: true);
            pnlAssignments.Controls.Add(PageH("📋  My Assignments"));
            LoadAssignmentCards();
        }

        private void RefreshAssignments()
        {
            for (int i = pnlAssignments.Controls.Count - 1; i >= 0; i--)
                if (pnlAssignments.Controls[i] is Panel) pnlAssignments.Controls.RemoveAt(i);
            LoadAssignmentCards();
        }

        private void LoadAssignmentCards()
        {
            // Cards stretch full available width like Admin cards
            int panelW = pnlAssignments.Width > 100 ? pnlAssignments.Width : mainPanel.Width > 100 ? mainPanel.Width : 1280;
            int margin = 30;
            int cardW = panelW - margin * 2;
            int cardH = 220;
            int cardY = 70;
            int gap = 18;

            List<Order> orders = _driverRepo.GetAssignedOrders(_driver.UserID);

            if (orders.Count == 0)
            {
                pnlAssignments.Controls.Add(L("No active assignments.",
                    new Font("Segoe UI", 12f), TextGray, new Point(margin, 80)));
                pnlAssignments.Controls.Add(L("The Admin will assign orders to you shortly.",
                    new Font("Segoe UI", 10f), TextGray, new Point(margin, 110)));
                return;
            }

            foreach (Order o in orders)
            {
                Color sc = o.OrderStatus == "Delivered" ? GreenOk :
                           o.OrderStatus == "Picked" ? OrangeWarn :
                           o.OrderStatus == "Assigned" ? RoyalBlue : TextGray;

                var card = Card(margin, cardY, cardW, cardH);
                pnlAssignments.Controls.Add(card);

                // Colored left stripe with hover brightness
                bool stripeHov = false;
                var stripe = new Panel { Location = new Point(0, 0), Size = new Size(7, cardH), BackColor = sc };
                stripe.Paint += (s, e) =>
                {
                    if (!stripeHov) return;
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 255, 255, 255)), stripe.ClientRectangle);
                };
                card.Controls.Add(stripe);

                // Wire up hover to stripe brightening
                Action<bool>? cardTrigger = card.Tag as Action<bool>;
                Action<bool> cardHoverAll = (on) =>
                {
                    cardTrigger?.Invoke(on);
                    stripeHov = on;
                    stripe.Invalidate();
                };
                card.MouseEnter += (s, e) => cardHoverAll(true);
                card.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) cardHoverAll(false); };

                Color badgeBg = o.OrderStatus == "Delivered" ? Color.FromArgb(30, 34, 197, 94) :
                                o.OrderStatus == "Picked" ? Color.FromArgb(30, 251, 146, 60) :
                                o.OrderStatus == "Assigned" ? Color.FromArgb(30, 0, 82, 204) :
                                                               Color.FromArgb(30, 120, 140, 170);
                string statusIcon = o.OrderStatus == "Delivered" ? "✅ Delivered" :
                                    o.OrderStatus == "Picked" ? "🚚 In Transit" :
                                    o.OrderStatus == "Assigned" ? "🔵 Assigned" : "⏳ Pending";
                var badge = new Panel { Location = new Point(cardW - 170, 18), Size = new Size(148, 32), BackColor = badgeBg };
                badge.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; badge.Region = new Region(RndPath(badge.ClientRectangle, 8)); };
                badge.Controls.Add(new Label { Text = statusIcon, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = sc, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                card.Controls.Add(badge);

                card.Controls.Add(L("#" + o.OrderID, new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(24, 20)));
                card.Controls.Add(L("📅  " + o.FormattedDate, new Font("Segoe UI", 9.5f), TextGray, new Point(24, 50)));
                card.Controls.Add(L("📦  " + o.ItemName + "     ⚖  " + o.WeightDisplay, new Font("Segoe UI", 10.5f), TextDark, new Point(24, 78)));
                card.Controls.Add(L("💰  " + o.FormattedFare + "     💳  " + o.PaymentStatus, new Font("Segoe UI", 9.5f), TextGray, new Point(24, 106)));
                card.Controls.Add(L("📦  Pick-up:   " + o.PickupPoint, new Font("Segoe UI", 10.5f), TextDark, new Point(420, 46)));
                card.Controls.Add(L("🏁  Delivery:  " + o.DeliveryPoint, new Font("Segoe UI", 10.5f), TextDark, new Point(420, 76)));
                card.Controls.Add(L("📍  Route pre-calculated by Admin", new Font("Segoe UI", 9f), TextGray, new Point(420, 106)));

                var pBadge = new Panel { Location = new Point(24, 134), Size = new Size(110, 28), BackColor = o.IsUrgent ? Color.FromArgb(30, 239, 68, 68) : Color.FromArgb(30, 34, 197, 94) };
                pBadge.Controls.Add(new Label { Text = o.IsUrgent ? "⚡ URGENT" : "✓ Normal", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = o.IsUrgent ? RedAlert : GreenOk, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                card.Controls.Add(pBadge);

                if (o.PaymentStatus == "Cash on Delivery" || o.PaymentStatus == "Unpaid")
                {
                    var cod = new Panel { Location = new Point(148, 134), Size = new Size(120, 28), BackColor = Color.FromArgb(30, 251, 146, 60) };
                    cod.Controls.Add(new Label { Text = "💵 Collect COD", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = OrangeWarn, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                    card.Controls.Add(cod);
                }

                int capId = o.OrderID;
                string curStatus = o.OrderStatus;
                bool isCOD = o.PaymentStatus == "Cash on Delivery" || o.PaymentStatus == "Unpaid";
                string fareStr = o.FormattedFare;

                var btnPick = new Button
                {
                    Text = "📥  Pick Order",
                    Location = new Point(cardW - 360, 72),
                    Size = new Size(160, 40),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    BackColor = curStatus == "Assigned" ? Color.FromArgb(30, 0, 82, 204) : Color.FromArgb(220, 225, 235),
                    ForeColor = curStatus == "Assigned" ? RoyalBlue : TextGray,
                    Cursor = curStatus == "Assigned" ? Cursors.Hand : Cursors.Default,
                    Enabled = curStatus == "Assigned"
                };
                btnPick.FlatAppearance.BorderSize = 0;
                btnPick.MouseEnter += (s, e) => cardHoverAll(true);
                btnPick.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) cardHoverAll(false); };
                btnPick.Click += (s, e) =>
                {
                    if (MessageBox.Show("Confirm picking up Order #" + capId + "?",
                        "Confirm Pickup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Picked");
                        if (ok) { MessageBox.Show("✅  Order #" + capId + " marked Picked.", "Picked Up", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshAssignments(); }
                        else MessageBox.Show("❌  Could not update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                card.Controls.Add(btnPick);

                var btnDeliver = new Button
                {
                    Text = "✅  Deliver",
                    Location = new Point(cardW - 190, 72),
                    Size = new Size(160, 40),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    BackColor = curStatus == "Picked" ? Color.FromArgb(30, 34, 197, 94) : Color.FromArgb(220, 225, 235),
                    ForeColor = curStatus == "Picked" ? GreenOk : TextGray,
                    Cursor = curStatus == "Picked" ? Cursors.Hand : Cursors.Default,
                    Enabled = curStatus == "Picked"
                };
                btnDeliver.FlatAppearance.BorderSize = 0;
                btnDeliver.MouseEnter += (s, e) => cardHoverAll(true);
                btnDeliver.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) cardHoverAll(false); };
                btnDeliver.Click += (s, e) =>
                {
                    if (isCOD)
                    {
                        if (MessageBox.Show("💵  COD Order!\n\nHave you collected " + fareStr + " cash?",
                            "COD Collection", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    }
                    if (MessageBox.Show("Confirm delivery of Order #" + capId + "?",
                        "Confirm Delivery", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Delivered");
                        if (ok) { MessageBox.Show("🎉  Order #" + capId + " delivered!", "Delivered", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshAssignments(); }
                        else MessageBox.Show("❌  Could not update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                card.Controls.Add(btnDeliver);

                // Propagate hover from all remaining child controls
                foreach (Control ch in card.Controls)
                {
                    if (ch == btnPick || ch == btnDeliver) continue; // already wired
                    ch.MouseEnter += (s, e) => cardHoverAll(true);
                    ch.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) cardHoverAll(false); };
                }

                if (o.IsUrgent && curStatus == "Picked")
                {
                    var urg = new Panel { Location = new Point(24, 174), Size = new Size(cardW - 50, 32), BackColor = Color.FromArgb(30, 239, 68, 68) };
                    urg.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; urg.Region = new Region(RndPath(urg.ClientRectangle, 8)); };
                    urg.Controls.Add(new Label { Text = "⚡  URGENT — Priority delivery. Observe traffic rules.", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = RedAlert, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                    card.Controls.Add(urg);
                }
                cardY += cardH + gap;
            }
            pnlAssignments.AutoScrollMinSize = new Size(0, cardY + 40);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 3 — ACTIVE DELIVERY  (WebView2 Leaflet map)
        // ═════════════════════════════════════════════════════════
        private void BuildActiveDeliveryPanel()
        {
            pnlActive = MakePage(autoScroll: false);
            pnlActive.Controls.Add(PageH("🚚  Active Delivery"));
            LoadActiveDelivery();
        }

        private void RefreshActiveDelivery()
        {
            StopTrackTimer();
            _activeMapView = null;
            _activeMapReady = false;

            for (int i = pnlActive.Controls.Count - 1; i >= 0; i--)
                if (pnlActive.Controls[i] is Panel) pnlActive.Controls.RemoveAt(i);
            pnlActive.Controls.Add(PageH("🚚  Active Delivery"));
            LoadActiveDelivery();
        }

        private void LoadActiveDelivery()
        {
            Order? active = _driverRepo.GetActiveOrder(_driver.UserID);

            if (active == null)
            {
                var nc = Card(30, 70, mainPanel.Width - 60, 100);
                pnlActive.Controls.Add(nc);
                AttachStripHover(nc, mainPanel.Width - 60, GreenOk);
                nc.Controls.Add(L("🟢  No active delivery right now.", new Font("Segoe UI", 13, FontStyle.Bold), GreenOk, new Point(30, 18)));
                nc.Controls.Add(L("Pick an assigned order from 'My Assignments' to start.", new Font("Segoe UI", 10.5f), TextGray, new Point(30, 52)));
                return;
            }

            int topY = 70;

            // ── Urgent banner — full width ────────────────────────
            if (active.IsUrgent)
            {
                var urgPanel = new Panel { Location = new Point(30, 70), Size = new Size(pnlActive.Width - 60, 44), BackColor = Color.Transparent };
                urgPanel.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    urgPanel.Region = new Region(RndPath(urgPanel.ClientRectangle, 10));
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(30, 239, 68, 68)), RndPath(urgPanel.ClientRectangle, 10));
                };
                urgPanel.Controls.Add(new Label { Text = "⚡  URGENT DELIVERY — Handle with priority.", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = RedAlert, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                pnlActive.Controls.Add(urgPanel);
                topY = 128;
            }

            // ── Layout: left info column + right map ──────────────
            int leftW = 420;
            int rightX = leftW + 46;
            int mapW = pnlActive.Width - rightX - 30;
            if (mapW < 400) mapW = 400;
            int mapH = pnlActive.Height - topY - 20;
            if (mapH < 350) mapH = 350;

            // ── Order details card ────────────────────────────────
            var det = Card(30, topY, leftW, 220);
            pnlActive.Controls.Add(det);
            AttachStripHover(det, leftW, OrangeWarn);
            det.Controls.Add(L("📦  Order Details", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            LabelPair(det, "Order ID", "#" + active.OrderID, 24, 50);
            LabelPair(det, "Item", active.ItemName, 24, 90);
            LabelPair(det, "Weight", active.WeightDisplay, 24, 130);
            LabelPair(det, "Priority", active.Priority, 240, 50);
            LabelPair(det, "Fare", active.FormattedFare, 240, 90);
            LabelPair(det, "Payment", active.PaymentStatus, 240, 130);
            if (active.PaymentStatus == "Cash on Delivery" || active.PaymentStatus == "Unpaid")
                det.Controls.Add(L("💵 COLLECT: " + active.FormattedFare + " cash.", new Font("Segoe UI", 9.5f, FontStyle.Bold), OrangeWarn, new Point(20, 178)));

            // ── Live fuel bar ─────────────────────────────────────
            var fuelCard = Card(30, topY + 236, leftW, 80);
            pnlActive.Controls.Add(fuelCard);
            AttachStripHover(fuelCard, leftW, GreenOk);
            fuelCard.Controls.Add(L("⛽  Live Fuel", new Font("Segoe UI", 10, FontStyle.Bold), TextDark, new Point(20, 10)));
            double fuelNow = Math.Min(100, Math.Max(0, _driver.CurrentFuel));
            Color fuelColor = fuelNow > 60 ? GreenOk : fuelNow > 25 ? OrangeWarn : RedAlert;
            var fuelTrack = new Panel { Location = new Point(20, 34), Size = new Size(leftW - 40, 14), BackColor = Color.FromArgb(220, 228, 245) };
            fuelTrack.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; fuelTrack.Region = new Region(RndPath(fuelTrack.ClientRectangle, 7)); };
            _liveFuelFill = new Panel { Location = new Point(0, 0), Size = new Size((int)((leftW - 40) * fuelNow / 100.0), 14), BackColor = fuelColor };
            _liveFuelFill.Paint += (s, e) => { if (_liveFuelFill.Width > 7) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; _liveFuelFill.Region = new Region(RndPath(_liveFuelFill.ClientRectangle, 7)); } };
            fuelTrack.Controls.Add(_liveFuelFill);
            fuelCard.Controls.Add(fuelTrack);
            _lblLiveFuel = L(fuelNow.ToString("N1") + "%", new Font("Segoe UI", 9f, FontStyle.Bold), fuelColor, new Point(leftW - 68, 52));
            fuelCard.Controls.Add(_lblLiveFuel);

            // ── Action buttons ────────────────────────────────────
            int btnTopY = topY + 236 + 96;
            int capId = active.OrderID;
            bool isCOD = active.PaymentStatus == "Cash on Delivery" || active.PaymentStatus == "Unpaid";
            string fareStr = active.FormattedFare;

            var btnDeliver = new Button
            {
                Text = "✅  Mark as Delivered",
                Location = new Point(30, btnTopY),
                Size = new Size(200, 44),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 34, 197, 94),
                ForeColor = GreenOk,
                Cursor = Cursors.Hand
            };
            btnDeliver.FlatAppearance.BorderSize = 0;
            btnDeliver.Click += (s, e) =>
            {
                if (isCOD && MessageBox.Show("💵 COD!\n\nHave you collected " + fareStr + "?", "COD", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (MessageBox.Show("Confirm delivery of Order #" + capId + "?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Delivered");
                    if (ok) { StopTrackTimer(); MessageBox.Show("🎉  Delivered!", "Delivered", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshActiveDelivery(); }
                    else MessageBox.Show("❌  Could not update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            pnlActive.Controls.Add(btnDeliver);

            var btnFailed = new Button
            {
                Text = "↩️  Report Failed",
                Location = new Point(244, btnTopY),
                Size = new Size(170, 44),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 239, 68, 68),
                ForeColor = RedAlert,
                Cursor = Cursors.Hand
            };
            btnFailed.FlatAppearance.BorderSize = 0;
            btnFailed.Click += (s, e) =>
            {
                if (MessageBox.Show("Report Order #" + capId + " as failed?", "Report Failed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Returned");
                    if (ok) { StopTrackTimer(); MessageBox.Show("↩️  Order reported.", "Reported", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshActiveDelivery(); }
                    else MessageBox.Show("❌  Could not update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            pnlActive.Controls.Add(btnFailed);

            // ── Map panel ─────────────────────────────────────────
            var mapPanel = new Panel
            {
                Location = new Point(rightX, topY),
                Size = new Size(mapW, mapH),
                BackColor = Color.FromArgb(230, 235, 245)
            };
            pnlActive.Controls.Add(mapPanel);

            var lblMapLoading = new Label
            {
                Text = "🗺️  Loading map...",
                Font = new Font("Segoe UI", 13f),
                ForeColor = TextGray,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            lblMapLoading.Location = new Point((mapW - lblMapLoading.PreferredWidth) / 2, (mapH - 22) / 2);
            mapPanel.Controls.Add(lblMapLoading);

            pnlActive.SizeChanged += (s, e) =>
            {
                int newMapW = pnlActive.Width - rightX - 30;
                if (newMapW < 400) newMapW = 400;
                int newMapH = pnlActive.Height - topY - 20;
                if (newMapH < 350) newMapH = 350;
                mapPanel.Size = new Size(newMapW, newMapH);
                if (_activeMapView != null && !_activeMapView.IsDisposed)
                    _activeMapView.Size = mapPanel.Size;
            };

            // ── Load polyline + build waypoints ───────────────────
            string? polylineJson = _orderRepo.GetRoutePolyline(active.OrderID);
            double routeDistKm = _orderRepo.GetRouteDistance(active.OrderID);
            _waypoints = ParsePolyline(polylineJson);
            _waypointIndex = 0;
            _activeOrderId = active.OrderID;

            // ── Build full 3-phase route: randomStart → pickup → delivery ──
            var rng = new Random();
            double pickupLat = _waypoints.Count > 0 ? _waypoints[0].Lat : 31.5204;
            double pickupLng = _waypoints.Count > 0 ? _waypoints[0].Lng : 74.3587;

            double offsetLat = (rng.NextDouble() * 0.009 + 0.014) * (rng.Next(2) == 0 ? 1 : -1);
            double offsetLng = (rng.NextDouble() * 0.009 + 0.014) * (rng.Next(2) == 0 ? 1 : -1);
            double randomLat = pickupLat + offsetLat;
            double randomLng = pickupLng + offsetLng;

            var phase1 = new List<(double Lat, double Lng)>();
            const int phase1Steps = 15;
            for (int i = 0; i <= phase1Steps; i++)
            {
                double t = (double)i / phase1Steps;
                phase1.Add((
                    randomLat + (pickupLat - randomLat) * t,
                    randomLng + (pickupLng - randomLng) * t));
            }

            var phase2 = new List<(double Lat, double Lng)>();
            if (_waypoints.Count == 2)
            {
                var s2 = _waypoints[0];
                var e2 = _waypoints[1];
                const int phase2Steps = 30;
                for (int i = 0; i <= phase2Steps; i++)
                {
                    double t = (double)i / phase2Steps;
                    phase2.Add((
                        s2.Lat + (e2.Lat - s2.Lat) * t,
                        s2.Lng + (e2.Lng - s2.Lng) * t));
                }
            }
            else
            {
                phase2 = _waypoints;
            }

            var fullRoute = new List<(double Lat, double Lng)>();
            fullRoute.AddRange(phase1);
            if (phase2.Count > 0) fullRoute.AddRange(phase2);
            _waypoints = fullRoute;

            int waypointSteps = Math.Max(1, _waypoints.Count - 1);
            double totalFuel = routeDistKm * FuelRate(_driver.VehicleType);
            _fuelPerWaypoint = totalFuel / waypointSteps;

            // ── Initialise WebView2 ────────────────────────────────
            try
            {
                _activeMapView = new WebView2
                {
                    Location = new Point(0, 0),
                    Size = mapPanel.Size,
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };
                mapPanel.Controls.Add(_activeMapView);

                _activeMapView.CoreWebView2InitializationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess) return;
                    _activeMapReady = true;
                    mapPanel.Controls.Remove(lblMapLoading);

                    string html = BuildDriverMapHtml(
                        active, _waypoints, polylineJson ?? "[]",
                        routeDistKm, active.PickupPoint, active.DeliveryPoint,
                        _driver.VehicleType);
                    _activeMapView.CoreWebView2.NavigateToString(html);

                    StartTrackTimer();
                };

                pnlActive.VisibleChanged += async (s, e) =>
                {
                    if (pnlActive.Visible && _activeMapView != null && !_activeMapView.IsDisposed)
                        try { await _activeMapView.EnsureCoreWebView2Async(); } catch { }
                };

                if (pnlActive.Visible)
                    _ = _activeMapView.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                mapPanel.Controls.Clear();
                mapPanel.Controls.Add(new Label
                {
                    Text = "⚠️  WebView2 not available:\n" + ex.Message + "\n\nInstall Microsoft Edge WebView2 Runtime.",
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = OrangeWarn,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }
        }

        // ─────────────────────────────────────────────────────────
        //  TRACK TIMER
        // ─────────────────────────────────────────────────────────
        private void StartTrackTimer()
        {
            StopTrackTimer();
            if (_waypoints.Count < 2) return;

            _trackTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _trackTimer.Tick += TrackTimer_Tick;
            _trackTimer.Start();
        }

        private void StopTrackTimer()
        {
            if (_trackTimer == null) return;
            _trackTimer.Stop();
            _trackTimer.Dispose();
            _trackTimer = null;
        }

        private void TrackTimer_Tick(object? sender, EventArgs e)
        {
            if (_waypoints.Count < 2 || _waypointIndex >= _waypoints.Count - 1)
            {
                StopTrackTimer();
                return;
            }

            _waypointIndex++;
            var (lat, lng) = _waypoints[_waypointIndex];

            _driver.CurrentFuel = Math.Max(0, _driver.CurrentFuel - _fuelPerWaypoint);
            _driverRepo.UpdateFuelLevel(_driver.UserID, _driver.CurrentFuel);
            _telRepo.InsertPoint(_activeOrderId, lat, lng, _fuelPerWaypoint);

            double fuelPct = Math.Min(100, Math.Max(0, _driver.CurrentFuel));
            Color fc = fuelPct > 60 ? GreenOk : fuelPct > 25 ? OrangeWarn : RedAlert;
            if (_liveFuelFill != null)
            {
                int barW = _liveFuelFill.Parent?.Width ?? 380;
                _liveFuelFill.Size = new Size((int)(barW * fuelPct / 100.0), 14);
                _liveFuelFill.BackColor = fc;
                _liveFuelFill.Invalidate();
            }
            if (_lblLiveFuel != null)
            {
                _lblLiveFuel.Text = fuelPct.ToString("N1") + "%";
                _lblLiveFuel.ForeColor = fc;
            }

            if (_activeMapReady && _activeMapView != null && !_activeMapView.IsDisposed)
            {
                string latStr = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string lngStr = lng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                _ = _activeMapView.ExecuteScriptAsync(
                    $"moveTruck({latStr},{lngStr},{_waypointIndex},{_waypoints.Count});");
            }
        }

        // ─────────────────────────────────────────────────────────
        //  LEAFLET HTML — driver map
        //  CHANGE 2: info-box now shows 📦 Pickup and 🏁 Delivery
        //  to match the actual map marker icons (was 🟢/🔴 before).
        // ─────────────────────────────────────────────────────────
        private static string BuildDriverMapHtml(
            Order order,
            List<(double Lat, double Lng)> waypoints,
            string polylineJson,
            double distKm,
            string pickupLabel,
            string deliveryLabel,
            string vehicleType = "Bike")
        {
            string fmt(double v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string vehicleEmoji = GetVehicleEmoji(vehicleType);

            double startLat = waypoints.Count > 0 ? waypoints[0].Lat : 31.5204;
            double startLng = waypoints.Count > 0 ? waypoints[0].Lng : 74.3587;

            double pickLat = startLat, pickLng = startLng;
            double endLat = startLat, endLng = startLng;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(polylineJson) || polylineJson == "[]"
                        ? "[]" : polylineJson);
                var arr = doc.RootElement;
                if (arr.GetArrayLength() >= 1)
                {
                    pickLat = arr[0][0].GetDouble();
                    pickLng = arr[0][1].GetDouble();
                    var last = arr[arr.GetArrayLength() - 1];
                    endLat = last[0].GetDouble();
                    endLng = last[1].GetDouble();
                }
            }
            catch { }

            double centLat = (startLat + endLat) / 2.0;
            double centLng = (startLng + endLng) / 2.0;
            int total = Math.Max(1, waypoints.Count);

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1.0'/>
<title>Driver Map</title>
<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css'/>
<script src='https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js'></script>
<style>
* {{ margin:0;padding:0;box-sizing:border-box; }}
html,body,#map {{ height:100%;width:100%; }}
.info-box {{
  position:absolute;top:10px;right:10px;z-index:1000;
  background:rgba(255,255,255,.96);border-radius:10px;
  padding:12px 16px;font-family:'Segoe UI',sans-serif;
  font-size:13px;box-shadow:0 2px 12px rgba(0,0,0,.18);min-width:210px;
}}
.info-box h3 {{ font-size:14px;margin-bottom:8px;color:#0a235a; }}
.info-row {{ display:flex;justify-content:space-between;margin-bottom:4px; }}
.info-val {{ font-weight:700;color:#0052cc; }}
.prog-bar {{ background:#dde3f0;border-radius:6px;height:10px;margin-top:8px;overflow:hidden; }}
.prog-fill {{ background:#fb923c;border-radius:6px;height:10px;transition:width .5s; }}
</style>
</head>
<body>
<div id='map'></div>
<div class='info-box'>
  <h3>{vehicleEmoji} Live Delivery</h3>
  <div class='info-row'><span>📏 Route</span><span class='info-val'>{distKm:N1} km</span></div>
  <div class='info-row'><span>📦 Pickup</span><span style='font-size:11px'>{EscHtml(pickupLabel)}</span></div>
  <div class='info-row'><span>🏁 Delivery</span><span style='font-size:11px'>{EscHtml(deliveryLabel)}</span></div>
  <div class='info-row'><span>Progress</span><span class='info-val' id='pct'>0%</span></div>
  <div class='prog-bar'><div class='prog-fill' id='fill' style='width:0%'></div></div>
</div>
<script>
var map = L.map('map').setView([{fmt(centLat)},{fmt(centLng)}],13);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{
  maxZoom:19,attribution:'© OpenStreetMap'
}}).addTo(map);

// Route polyline (pickup → delivery, blue solid)
var pts = {polylineJson};
if(pts&&pts.length>1){{
  var rt = L.polyline(pts,{{color:'#0052cc',weight:5,opacity:.85}}).addTo(map);
  map.fitBounds(rt.getBounds(),{{padding:[60,60]}});
}}

// Approach polyline (random driver start → pickup, orange dashed)
var approachPts = [[{fmt(startLat)},{fmt(startLng)}],[{fmt(pickLat)},{fmt(pickLng)}]];
L.polyline(approachPts,{{
  color:'#fb923c',weight:3,opacity:.9,
  dashArray:'10,8',dashOffset:'0'
}}).addTo(map);

// Pickup marker — matches Admin map icon (📦 package)
var gIcon = L.divIcon({{className:'',html:'<div style=""font-size:26px;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45))"">📦</div>',iconSize:[30,30],iconAnchor:[15,28]}});
L.marker([{fmt(pickLat)},{fmt(pickLng)}],{{icon:gIcon}}).addTo(map).bindPopup('<b>📦 Pickup</b><br>{EscHtml(pickupLabel)}');

// Delivery marker — matches Admin map icon (🏁 flag)
var rIcon = L.divIcon({{className:'',html:'<div style=""font-size:26px;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45))"">🏁</div>',iconSize:[30,30],iconAnchor:[4,28]}});
L.marker([{fmt(endLat)},{fmt(endLng)}],{{icon:rIcon}}).addTo(map).bindPopup('<b>🏁 Delivery</b><br>{EscHtml(deliveryLabel)}');

// Vehicle starts at random point near pickup
var vehicleIcon = L.divIcon({{className:'',html:'<div style=""font-size:28px;line-height:1"">{vehicleEmoji}</div>',iconSize:[32,32],iconAnchor:[16,16]}});
var truck = L.marker([{fmt(startLat)},{fmt(startLng)}],{{icon:vehicleIcon,zIndexOffset:1000}}).addTo(map);

var totalSteps = {total};

function moveTruck(lat,lng,step,total){{
  truck.setLatLng([lat,lng]);
  map.panTo([lat,lng],{{animate:true,duration:0.8}});
  var pct = Math.round((step/(total-1))*100);
  document.getElementById('pct').textContent = pct+'%';
  document.getElementById('fill').style.width = pct+'%';
}}
</script>
</body>
</html>";
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 4 — DELIVERY HISTORY
        // ═════════════════════════════════════════════════════════
        private void BuildHistoryPanel()
        {
            pnlHistory = MakePage(autoScroll: false);
            pnlHistory.Controls.Add(PageH("📜  Delivery History"));
            LoadHistoryTable();
        }

        private void RefreshHistory()
        {
            for (int i = pnlHistory.Controls.Count - 1; i >= 0; i--)
                if (pnlHistory.Controls[i] is Panel) pnlHistory.Controls.RemoveAt(i);
            pnlHistory.Controls.Add(PageH("📜  Delivery History"));
            LoadHistoryTable();
        }

        private void LoadHistoryTable()
        {
            List<Order> history = _driverRepo.GetDeliveryHistory(_driver.UserID);
            if (history.Count == 0)
            {
                pnlHistory.Controls.Add(L("No delivery history yet.", new Font("Segoe UI", 12f), TextGray, new Point(30, 80)));
                return;
            }

            int tblW = mainPanel.Width - 60;
            int tblH = Math.Max(220, 70 + history.Count * 40);
            var tbl = Card(30, 62, tblW, tblH);
            pnlHistory.Controls.Add(tbl);
            AttachStripHover(tbl, tblW, RoyalBlue);

            // Scale columns to table width
            int fixedW = 80 + 120 + 70 + 90 + 110 + 90 + 80 + 118;
            int routeW = Math.Max(120, (tblW - 32 - fixedW) / 2);
            string[] hdrs = { "Order ID", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Status", "Fare", "Rating", "Date" };
            int[] wids = { 80, 120, 70, 90, routeW, routeW, 110, 90, 80, 118 };
            int hx = 16;
            foreach (var (h, w) in Zip(hdrs, wids))
            {
                tbl.Controls.Add(new Label { Text = h, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextGray, BackColor = CardBg, Size = new Size(w, 30), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(hx, 14), Padding = new Padding(4, 0, 0, 0) });
                hx += w;
            }
            tbl.Controls.Add(new Panel { Location = new Point(16, 46), Size = new Size(tblW - 32, 1), BackColor = BorderBlue });
            int rowY = 52; bool alt = false;
            foreach (Order o in history)
            {
                string ratingStr = o.Rating > 0 ? new string('★', o.Rating) + new string('☆', 5 - o.Rating) : "—";
                string[] row = { "#" + o.OrderID, o.ItemName, o.WeightDisplay, o.Priority,
                                  o.PickupPoint, o.DeliveryPoint, o.OrderStatus,
                                  o.FormattedFare, ratingStr, o.FormattedDate };
                Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White; alt = !alt;
                int rx = 16;
                foreach (var (cell, w) in Zip(row, wids))
                {
                    Color fg = cell == "Delivered" ? GreenOk : cell == "Returned" ? RedAlert :
                               cell == "Urgent" ? RedAlert : cell.StartsWith("★") ? Color.Gold : TextDark;
                    tbl.Controls.Add(new Label { Text = cell, Font = new Font("Segoe UI", 9.5f), ForeColor = fg, BackColor = bg, Size = new Size(w, 36), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(rx, rowY), Padding = new Padding(4, 0, 0, 0) });
                    rx += w;
                }
                rowY += 38;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 5 — MY PROFILE
        // ═════════════════════════════════════════════════════════
        private void BuildProfilePanel()
        {
            pnlProfile = MakePage(autoScroll: true);
            pnlProfile.AutoScrollMinSize = new Size(1224, 730);
            pnlProfile.Controls.Add(PageH("👤  My Profile"));

            int W = mainPanel.Width > 0 ? mainPanel.Width : 1280;
            int leftX = 30;
            int cardGap = 14;
            int row1Y = 62;
            int row1H = 195;
            int row2Y = row1Y + row1H + cardGap;
            int row2H = 215;
            int row3Y = row2Y + row2H + cardGap;
            int row3H = 185;

            // Responsive split: left ~37%, right ~60%
            int avatarW = (int)((W - 60 - cardGap) * 0.37);
            int infoW = W - 60 - cardGap - avatarW;
            int rightX = leftX + avatarW + cardGap;
            int vCardW = avatarW;
            int pwCardW = infoW;
            int fullW = W - 60;

            // ── ROW 1 LEFT — Avatar ───────────────────────────────
            var avatarCard = Card(leftX, row1Y, avatarW, row1H);
            pnlProfile.Controls.Add(avatarCard);
            AttachStripHover(avatarCard, avatarW, OrangeWarn);

            pnlProfileAvatar = new Panel { Size = new Size(100, 100), Location = new Point(24, 22), BackColor = OrangeWarn };
            pnlProfileAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pnlProfileAvatar.Region = new Region(RndPath(pnlProfileAvatar.ClientRectangle, 50));
                if (_profilePhoto != null) e.Graphics.DrawImage(_profilePhoto, pnlProfileAvatar.ClientRectangle);
                else
                {
                    e.Graphics.FillEllipse(new SolidBrush(OrangeWarn), pnlProfileAvatar.ClientRectangle);
                    string init = _driver.Username.Length > 0 ? _driver.Username[0].ToString().ToUpper() : "D";
                    e.Graphics.DrawString(init, new Font("Segoe UI", 30, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 100, 100), Centre());
                }
            };
            avatarCard.Controls.Add(pnlProfileAvatar);

            var lblPName = new Label { Text = _driver.FullName, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = TextDark, BackColor = Color.Transparent, AutoSize = true, Location = new Point(140, 28) };
            avatarCard.Controls.Add(lblPName);
            avatarCard.Controls.Add(L("@" + _driver.Username, new Font("Segoe UI", 10f), TextGray, new Point(140, 58)));
            avatarCard.Controls.Add(L("Role:  " + _driver.Role, new Font("Segoe UI", 10f), TextGray, new Point(140, 80)));

            var activeChip = new Panel { Location = new Point(140, 102), Size = new Size(130, 26), BackColor = Color.FromArgb(30, 34, 197, 94) };
            activeChip.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; activeChip.Region = new Region(RndPath(activeChip.ClientRectangle, 8)); };
            activeChip.Controls.Add(new Label { Text = "● Active Account", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = GreenOk, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
            avatarCard.Controls.Add(activeChip);

            var btnUpload = new Button { Text = "📷  Upload Photo", Location = new Point(24, 140), Size = new Size(avatarW - 48, 38), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnUpload.FlatAppearance.BorderSize = 0;
            btnUpload.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Title = "Select Profile Photo", Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif" };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    _profilePhotoPath = dlg.FileName;
                    _profilePhoto?.Dispose();
                    _profilePhoto = Image.FromFile(_profilePhotoPath);
                    pnlProfileAvatar.Invalidate();
                    picAvatar.Invalidate();
                }
                catch { MessageBox.Show("Could not load image.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            avatarCard.Controls.Add(btnUpload);

            // ── ROW 1 RIGHT — Account Info ────────────────────────
            var infoCard = Card(rightX, row1Y, infoW, row1H);
            pnlProfile.Controls.Add(infoCard);
            AttachStripHover(infoCard, infoW, RoyalBlue);
            infoCard.Controls.Add(L("Account Information", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(22, 14)));
            LabelPair(infoCard, "Username", _driver.Username, 24, 48);
            LabelPair(infoCard, "Driver ID", _driver.UserID.ToString(), 320, 48);
            LabelPair(infoCard, "Email", _driver.Email, 24, 98);
            LabelPair(infoCard, "Phone", string.IsNullOrEmpty(_driver.Phone) ? "—" : _driver.Phone, 320, 98);
            LabelPair(infoCard, "License No.", _driver.LicenseNumber, 24, 148);
            LabelPair(infoCard, "Member Since", "May 2026", 320, 148);

            // ── ROW 2 — Personal Info (editable) — full width ─────
            var card = Card(leftX, row2Y, fullW, row2H);
            pnlProfile.Controls.Add(card);
            AttachStripHover(card, fullW, RoyalBlue);
            card.Controls.Add(L("Personal Information", new Font("Segoe UI", 11, FontStyle.Bold), TextGray, new Point(24, 14)));

            card.Controls.Add(L("First Name", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(24, 42)));
            var txtFN = new TextBox { Text = _driver.FirstName, Location = new Point(24, 60), Size = new Size(230, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtFN);

            card.Controls.Add(L("Last Name", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(270, 42)));
            var txtLN = new TextBox { Text = _driver.LastName, Location = new Point(270, 60), Size = new Size(230, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtLN);

            card.Controls.Add(L("Email", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(516, 42)));
            var txtEM = new TextBox { Text = _driver.Email, Location = new Point(516, 60), Size = new Size(260, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtEM);

            card.Controls.Add(L("Phone", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(792, 42)));
            var txtPH = new TextBox { Text = _driver.Phone, Location = new Point(792, 60), Size = new Size(260, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtPH);

            card.Controls.Add(L("Username (read-only)", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(24, 106)));
            card.Controls.Add(new TextBox { Text = _driver.Username, Location = new Point(24, 124), Size = new Size(230, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(235, 240, 250), ReadOnly = true });

            var lblStatus = new Label { Text = "", Font = new Font("Segoe UI", 9.5f), ForeColor = GreenOk, BackColor = Color.Transparent, AutoSize = true, Location = new Point(270, 132) };
            card.Controls.Add(lblStatus);

            var btnSave = new Button { Text = "💾  Save Changes", Location = new Point(fullW - 220, 120), Size = new Size(196, 40), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                bool ok = _userRepo.UpdateProfile(_driver.UserID, txtFN.Text.Trim(), txtLN.Text.Trim(), txtEM.Text.Trim(), txtPH.Text.Trim());
                if (ok) { _driver.FirstName = txtFN.Text.Trim(); _driver.LastName = txtLN.Text.Trim(); _driver.Email = txtEM.Text.Trim(); _driver.Phone = txtPH.Text.Trim(); lblDriverName.Text = _driver.FullName; lblPName.Text = _driver.FullName; lblStatus.ForeColor = GreenOk; lblStatus.Text = "✅  Profile updated successfully."; }
                else { lblStatus.ForeColor = RedAlert; lblStatus.Text = "❌  Update failed."; }
            };
            card.Controls.Add(btnSave);

            // ── ROW 3 LEFT — Vehicle Details ──────────────────────
            var vCard = Card(leftX, row3Y, vCardW, row3H);
            pnlProfile.Controls.Add(vCard);
            AttachStripHover(vCard, vCardW, OrangeWarn);
            vCard.Controls.Add(L("🚗  Vehicle Details", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 14)));

            vCard.Controls.Add(L("Number Plate", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 46)));
            var txtPlate = new TextBox { Text = _driver.PlateNumber, Location = new Point(20, 64), Size = new Size(220, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            vCard.Controls.Add(txtPlate);

            vCard.Controls.Add(L("Vehicle Type", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(260, 46)));
            var cmbType = new ComboBox { Location = new Point(260, 64), Size = new Size(220, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11f), BackColor = CardBg, ForeColor = TextDark };
            cmbType.Items.AddRange(new object[] { "Bike", "Car", "Van", "Truck", "Rickshaw" });
            cmbType.SelectedItem = _driver.VehicleType;
            if (cmbType.SelectedIndex < 0) cmbType.SelectedIndex = 0;
            vCard.Controls.Add(cmbType);

            var lblVS = new Label { Text = "", Font = new Font("Segoe UI", 9.5f), ForeColor = GreenOk, BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, 112) };
            vCard.Controls.Add(lblVS);

            var btnSaveV = new Button { Text = "💾  Save Vehicle", Location = new Point(20, 130), Size = new Size(180, 38), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = OrangeWarn, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnSaveV.FlatAppearance.BorderSize = 0;
            btnSaveV.Click += (s, e) =>
            {
                bool ok = _driverRepo.UpdateVehicle(_driver.UserID, txtPlate.Text.Trim(), cmbType.SelectedItem?.ToString() ?? "Bike");
                if (ok) { _driver.PlateNumber = txtPlate.Text.Trim(); _driver.VehicleType = cmbType.SelectedItem?.ToString() ?? "Bike"; lblVS.ForeColor = GreenOk; lblVS.Text = "✅  Vehicle info updated."; }
                else { lblVS.ForeColor = RedAlert; lblVS.Text = "❌  Update failed."; }
            };
            vCard.Controls.Add(btnSaveV);

            // ── ROW 3 RIGHT — Change Password ─────────────────────
            var pwCard = Card(rightX, row3Y, pwCardW, row3H);
            pnlProfile.Controls.Add(pwCard);
            AttachStripHover(pwCard, pwCardW, RoyalBlue);
            pwCard.Controls.Add(L("🔒 Change Password", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(22, 14)));

            pwCard.Controls.Add(L("Current Password", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(22, 50)));
            var txtCurPw = new TextBox { Location = new Point(22, 72), Size = new Size(175, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, UseSystemPasswordChar = true };
            pwCard.Controls.Add(txtCurPw);

            pwCard.Controls.Add(L("New Password", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(212, 50)));
            var txtNewPw = new TextBox { Location = new Point(212, 72), Size = new Size(175, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, UseSystemPasswordChar = true };
            pwCard.Controls.Add(txtNewPw);

            pwCard.Controls.Add(L("Confirm Password", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(402, 50)));
            var txtConfPw = new TextBox { Location = new Point(402, 72), Size = new Size(175, 30), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, UseSystemPasswordChar = true };
            pwCard.Controls.Add(txtConfPw);

            var lblPwStatus = new Label { Text = "", Font = new Font("Segoe UI", 9f), ForeColor = GreenOk, BackColor = Color.Transparent, AutoSize = true, Location = new Point(22, 115) };
            pwCard.Controls.Add(lblPwStatus);

            var btnUpdate = new Button { Text = "Update", Location = new Point(pwCardW - 140, 110), Size = new Size(108, 38), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.Click += (s, e) =>
            {
                lblPwStatus.Text = "";
                if (string.IsNullOrWhiteSpace(txtCurPw.Text) || string.IsNullOrWhiteSpace(txtNewPw.Text) || string.IsNullOrWhiteSpace(txtConfPw.Text)) { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  All fields are required."; return; }
                if (txtNewPw.Text.Length < 6) { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  New password must be at least 6 characters."; return; }
                if (txtNewPw.Text != txtConfPw.Text) { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  Passwords do not match."; return; }
                string newHash = BCrypt.Net.BCrypt.HashPassword(txtNewPw.Text.Trim());
                var (success, error) = _userRepo.UpdatePassword(_driver.UserID, txtCurPw.Text.Trim(), newHash);
                if (success) { lblPwStatus.ForeColor = GreenOk; lblPwStatus.Text = "✅  Password updated."; txtCurPw.Clear(); txtNewPw.Clear(); txtConfPw.Clear(); }
                else { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  " + error; }
            };
            pwCard.Controls.Add(btnUpdate);
        }

        // ═════════════════════════════════════════════════════════
        //  SHARED UI HELPERS
        // ═════════════════════════════════════════════════════════
        private void SidePanel_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new LinearGradientBrush(sidePanel.ClientRectangle,
                Color.FromArgb(15, 40, 100), Color.FromArgb(5, 15, 50), LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(br, sidePanel.ClientRectangle);
        }

        private Panel MakePage(bool autoScroll = false)
        {
            var p = new Panel { Location = new Point(0, 0), Size = mainPanel.Size, BackColor = PageBg, Visible = false, AutoScroll = autoScroll };
            if (autoScroll) p.AutoScrollMinSize = new Size(0, 1400);
            mainPanel.Controls.Add(p);
            return p;
        }

        /// <summary>
        /// Creates a rounded white card with hover border glow.
        /// Stores an Action&lt;bool&gt; in Tag so callers can propagate hover
        /// from child controls.
        /// </summary>
        private Panel Card(int x, int y, int w, int h)
        {
            bool hovered = false;
            var c = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.White };
            c.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RndPath(c.ClientRectangle, 12);
                e.Graphics.FillPath(new SolidBrush(Color.White), path);
                if (hovered)
                {
                    using var borderPen = new Pen(Color.FromArgb(180, 210, 245), 2f);
                    e.Graphics.DrawPath(borderPen, path);
                }
                c.Region = new Region(path);
            };
            c.Tag = new Action<bool>(on => { hovered = on; c.Invalidate(); });
            c.MouseEnter += (s, e) => { hovered = true; c.Invalidate(); };
            c.MouseLeave += (s, e) => { hovered = false; c.Invalidate(); };
            return c;
        }

        /// <summary>
        /// Attaches a colored top accent strip to a card and wires full hover
        /// propagation — identical to Admin's StatCard hover system.
        /// Call this immediately after Card() for any non-stat card that needs
        /// the animated top bar (vehicle card, rating card, table card, etc.).
        /// </summary>
        private void AttachStripHover(Panel card, int cardW, Color accent)
        {
            bool stripHovered = false;
            var strip = new Panel { Location = new Point(0, 0), Size = new Size(cardW, 5), BackColor = accent };
            strip.Paint += (s, e) =>
            {
                if (!stripHovered) return;
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 255, 255, 255)), strip.ClientRectangle);
            };
            card.Controls.Add(strip);

            Action<bool>? trigger = card.Tag as Action<bool>;
            Action<bool> hoverAll = (on) =>
            {
                trigger?.Invoke(on);
                stripHovered = on;
                strip.Size = new Size(cardW, on ? 8 : 5);
                strip.Invalidate();
            };

            card.MouseEnter += (s, e) => hoverAll(true);
            card.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) hoverAll(false); };

            // We wire child controls after caller adds them — this is a deferred hook.
            // We use the Paint event as a safe post-construction point to wire children.
            bool childrenWired = false;
            card.Paint += (s, e) =>
            {
                if (childrenWired) return;
                childrenWired = true;
                foreach (Control ch in card.Controls)
                {
                    ch.MouseEnter += (cs, ce) => hoverAll(true);
                    ch.MouseLeave += (cs, ce) =>
                    {
                        var pos = card.PointToClient(Control.MousePosition);
                        if (!card.ClientRectangle.Contains(pos)) hoverAll(false);
                    };
                }
            };
        }

        /// <summary>
        /// Stat card — full hover system with animated accent strip (5→8 px),
        /// shimmer overlay, and child propagation. Identical to AdminDashboardForm.
        /// </summary>
        private void StatCard(Panel parent, int x, int y, string icon, string title, string value, Color accent, int w, int h)
        {
            var c = Card(x, y, w, h);

            bool stripHovered = false;
            var strip = new Panel { Location = new Point(0, 0), Size = new Size(w, 5), BackColor = accent };
            strip.Paint += (s, e) =>
            {
                if (!stripHovered) return;
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 255, 255, 255)), strip.ClientRectangle);
            };
            c.Controls.Add(strip);
            c.Controls.Add(L(icon, new Font("Segoe UI", 22), accent, new Point(16, 20)));
            c.Controls.Add(L(title, new Font("Segoe UI", 10f), TextGray, new Point(16, 62)));
            c.Controls.Add(L(value, new Font("Segoe UI", 20, FontStyle.Bold), accent, new Point(16, 82)));

            Action<bool>? trigger = c.Tag as Action<bool>;
            Action<bool> hoverAll = (on) =>
            {
                trigger?.Invoke(on);
                stripHovered = on;
                strip.Size = new Size(w, on ? 8 : 5);
                strip.Invalidate();
            };
            c.MouseEnter += (s, e) => hoverAll(true);
            c.MouseLeave += (s, e) => { var pos = c.PointToClient(Control.MousePosition); if (!c.ClientRectangle.Contains(pos)) hoverAll(false); };
            foreach (Control ch in c.Controls)
            {
                ch.MouseEnter += (s, e) => hoverAll(true);
                ch.MouseLeave += (s, e) =>
                {
                    var pos = c.PointToClient(Control.MousePosition);
                    if (!c.ClientRectangle.Contains(pos)) hoverAll(false);
                };
            }

            parent.Controls.Add(c);
        }

        private void LabelPair(Panel parent, string label, string value, int x, int y)
        {
            parent.Controls.Add(L(label, new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(x, y)));
            parent.Controls.Add(L(value, new Font("Segoe UI", 11f, FontStyle.Bold), TextDark, new Point(x, y + 20)));
        }

        private static Label L(string text, Font font, Color color, Point loc) =>
            new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, AutoSize = true, Location = loc };

        private static Label PageH(string text) =>
            new Label { Text = text, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(18, 32, 60), BackColor = Color.Transparent, AutoSize = true, Location = new Point(30, 22) };

        private static GraphicsPath RndPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static StringFormat Centre() =>
            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        private static IEnumerable<(T1, T2)> Zip<T1, T2>(T1[] a, T2[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++) yield return (a[i], b[i]);
        }

        // ─────────────────────────────────────────────────────────
        //  POLYLINE PARSER
        // ─────────────────────────────────────────────────────────
        private static List<(double Lat, double Lng)> ParsePolyline(string? json)
        {
            var result = new List<(double, double)>();
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return result;
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (JsonElement pt in doc.RootElement.EnumerateArray())
                {
                    if (pt.ValueKind != JsonValueKind.Array || pt.GetArrayLength() < 2) continue;
                    result.Add((pt[0].GetDouble(), pt[1].GetDouble()));
                }
            }
            catch { }
            return result;
        }

        private static string EscHtml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
             .Replace("'", "&#39;").Replace("\"", "&quot;");
    }
}
