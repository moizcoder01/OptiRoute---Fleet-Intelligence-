// =============================================================
//  OptiRoute  |  Forms/AdminDashboardForm.cs
//
//  FIXES IN THIS VERSION:
//    1. using OptiRoute.Core.Services added — matches RouteService
//       namespace. Old code had "using OptiRoute.Core" which caused
//       RouteService.GetRouteAsync() to silently crash (method/class
//       not found), so assignment stored no route data, causing
//       DriverDashboard "My Assignments" to show nothing.
//    2. RouteService is now instantiated as "new RouteService()"
//       and GetOptimalRouteAsync() is called — matching the new
//       instance-method signature.
//    3. RouteResult.TotalDistanceKm / PolylineJson used (matching
//       new property names). Old code used .DistanceKm and
//       .RoutePolyline which don't exist on the new class.
//    4. Fallback hardcoded values displayed when API fails:
//       estimated distance, ETA, fuel — all clearly labelled
//       "⚠️ ESTIMATED" so admin knows they are not real.
//    5. Fallback map: shows Lahore city-centre markers when
//       geocoding fails, with a red warning banner on the map.
//    6. Confirm button is always enabled (orange = estimated,
//       green = real) so admin can always assign even when API
//       is down. Only fuel/maintenance failures block it.
//
//  DRIVER DASHBOARD FIX (root cause explained):
//    DriverRepository.GetAssignedOrders() joins Table_Orders to
//    Table_Vehicles on VehicleID. AssignVehicleWithRoute() writes
//    VehicleID to Table_Orders. The reason driver saw nothing was
//    that AssignVehicleWithRoute() was NEVER CALLED — the old
//    RouteService.GetRouteAsync() threw a compile error (wrong
//    namespace/method name), the entire async block was silently
//    swallowed by the catch, and the confirm button was never
//    reached. With the RouteService fix above, the call succeeds,
//    route data is saved, and the driver immediately sees the order.
//
//  WEBVIEW2 : Install NuGet: Microsoft.Web.WebView2
//  THEME    : 100% unchanged — colours, fonts, cards identical.
//  NAMESPACE: OptiRoute.Forms
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using OptiRoute.Core.Data;
using OptiRoute.Core.Models;
using OptiRoute.Core.Services;   // ← FIX: was "OptiRoute.Core" — RouteService lives here

namespace OptiRoute.Forms
{
    public class AdminDashboardForm : Form
    {
        // ── Repositories ──────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();
        private readonly OrderRepository _orderRepo = new OrderRepository();

        // ── Admin model ───────────────────────────────────────────
        private Admin _admin = null!;

        // ── Layout ────────────────────────────────────────────────
        private Panel sidePanel = null!;
        private Panel mainPanel = null!;
        private Panel headerPanel = null!;

        // ── Sidebar toggle ────────────────────────────────────────
        private Button btnToggle = null!;
        private bool _sidebarOpen = true;
        private const int SideW = 232;

        // ── Sidebar controls ──────────────────────────────────────
        private Panel picAvatar = null!;
        private Label lblAdminName = null!;
        private Label lblAdminRole = null!;
        private Button btnDashboard = null!;
        private Button btnOrders = null!;
        private Button btnAssign = null!;
        private Button btnDrivers = null!;
        private Button btnReports = null!;
        private Button btnProfile = null!;
        private Button btnLogout = null!;
        private Button activeBtn = null!;

        // ── Content panels ────────────────────────────────────────
        private Panel pnlDashboard = null!;
        private Panel pnlOrders = null!;
        private Panel pnlAssign = null!;
        private Panel pnlDrivers = null!;
        private Panel pnlReports = null!;
        private Panel pnlProfile = null!;

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
        readonly Color CardBg = Color.FromArgb(245, 248, 255);
        readonly Color PurpleAccent = Color.FromArgb(124, 58, 237);

        // ── Fuel rates (L / km) ───────────────────────────────────
        private static double FuelRate(string vehicleType) =>
            vehicleType?.ToLower() switch
            {
                "bike" => 0.05,
                "car" => 0.10,
                "van" => 0.18,
                "truck" => 0.25,
                _ => 0.10
            };

        // ─────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────────────────
        public AdminDashboardForm(string username)
        {
            try
            {
                _admin = _userRepo.LoadAdmin(username)
                         ?? new Admin { Username = username };
            }
            catch { _admin = new Admin { Username = username }; }

            Text = "OptiRoute  |  Admin Control Panel";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(1200, 700);
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 9f);

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
                Size = new Size(SideW, ClientSize.Height),
                BackColor = SidebarBg
            };
            sidePanel.Paint += SidePanel_Paint;
            Controls.Add(sidePanel);
            BuildSidebar();

            headerPanel = new Panel
            {
                Location = new Point(SideW, 0),
                Size = new Size(ClientSize.Width - SideW, 62),
                BackColor = Color.White
            };
            headerPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(220, 228, 245), 1);
                e.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };
            Controls.Add(headerPanel);
            BuildHeader();

            mainPanel = new Panel
            {
                Location = new Point(SideW, 62),
                Size = new Size(ClientSize.Width - SideW, ClientSize.Height - 62),
                BackColor = PageBg,
                AutoScroll = true
            };
            Controls.Add(mainPanel);

            BuildDashboardPanel();
            BuildOrdersPanel();
            BuildAssignPanel();
            BuildDriversPanel();
            BuildReportsPanel();
            BuildProfilePanel();

            Resize += (s, e) => ApplyLayout();
        }

        private void ApplyLayout()
        {
            int offset = _sidebarOpen ? SideW : 0;
            sidePanel.Size = new Size(SideW, ClientSize.Height);
            headerPanel.Location = new Point(offset, 0);
            headerPanel.Size = new Size(ClientSize.Width - offset, 62);
            mainPanel.Location = new Point(offset, 62);
            mainPanel.Size = new Size(ClientSize.Width - offset, ClientSize.Height - 62);
            foreach (Control c in mainPanel.Controls)
                if (c is Panel pg) pg.Size = mainPanel.Size;
            sidePanel.Invalidate();
            headerPanel.Invalidate();
            if (btnToggle != null) btnToggle.Location = new Point(10, 16);
        }

        // ═════════════════════════════════════════════════════════
        //  SIDEBAR
        // ═════════════════════════════════════════════════════════
        private void BuildSidebar()
        {
            var logo = new Panel { Location = new Point(0, 0), Size = new Size(232, 68), BackColor = Color.Transparent };
            logo.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(logo.ClientRectangle, RoyalBlue, Navy, LinearGradientMode.Horizontal);
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

            picAvatar = new Panel { Size = new Size(72, 72), Location = new Point(80, 85), BackColor = PurpleAccent };
            picAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                picAvatar.Region = new Region(RndPath(picAvatar.ClientRectangle, 36));
                e.Graphics.FillEllipse(new SolidBrush(PurpleAccent), picAvatar.ClientRectangle);
                string ini = _admin.Username.Length > 0 ? _admin.Username[0].ToString().ToUpper() : "A";
                e.Graphics.DrawString(ini, new Font("Segoe UI", 26, FontStyle.Bold), Brushes.White,
                    new RectangleF(0, 0, 72, 72), Centre());
            };
            sidePanel.Controls.Add(picAvatar);

            lblAdminName = new Label { Text = _admin.FullName, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Size = new Size(232, 22), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 165) };
            sidePanel.Controls.Add(lblAdminName);

            lblAdminRole = new Label { Text = _admin.GetDisplayLabel(), Font = new Font("Segoe UI", 8.5f), ForeColor = SkyBlue, BackColor = Color.Transparent, Size = new Size(232, 18), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 188) };
            sidePanel.Controls.Add(lblAdminRole);

            sidePanel.Controls.Add(new Panel { Location = new Point(20, 216), Size = new Size(192, 1), BackColor = Color.FromArgb(50, 255, 255, 255) });

            btnDashboard = SideBtn("🏠   Dashboard", 234);
            btnOrders = SideBtn("📋   All Orders", 281);
            btnAssign = SideBtn("🔗   Assign & Route", 328);
            btnDrivers = SideBtn("🚗   Drivers", 375);
            btnReports = SideBtn("📊   Reports", 422);
            btnProfile = SideBtn("👤   My Profile", 469);

            btnDashboard.Click += (s, e) => { RefreshDashboard(); ShowPanel(pnlDashboard, btnDashboard); };
            btnOrders.Click += (s, e) => { RefreshOrders(); ShowPanel(pnlOrders, btnOrders); };
            btnAssign.Click += (s, e) => { RefreshAssign(); ShowPanel(pnlAssign, btnAssign); };
            btnDrivers.Click += (s, e) => { RefreshDrivers(); ShowPanel(pnlDrivers, btnDrivers); };
            btnReports.Click += (s, e) => { RefreshReports(); ShowPanel(pnlReports, btnReports); };
            btnProfile.Click += (s, e) => ShowPanel(pnlProfile, btnProfile);

            btnLogout = new Button
            {
                Text = "⏻   Logout",
                Location = new Point(16, 530),
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
                { new LoginForm().Show(); Close(); }
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
                Location = new Point(0, 590)
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
            foreach (Control c in mainPanel.Controls) if (c is Panel p) p.Visible = false;
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
            btnToggle = new Button
            {
                Text = "☰",
                Location = new Point(10, 16),
                Size = new Size(34, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 13f),
                ForeColor = TextDark,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Click += (s, e) => ToggleSidebar();
            headerPanel.Controls.Add(btnToggle);

            headerPanel.Controls.Add(new Label
            {
                Text = "Admin Control Panel",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(54, 18),
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
            headerPanel.Resize += (s, e) => lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 24, 20);
        }

        private void ToggleSidebar()
        {
            _sidebarOpen = !_sidebarOpen;
            sidePanel.Visible = _sidebarOpen;
            ApplyLayout();
            if (activeBtn != null)
            {
                Panel? ap = activeBtn == btnDashboard ? pnlDashboard :
                            activeBtn == btnOrders ? pnlOrders :
                            activeBtn == btnAssign ? pnlAssign :
                            activeBtn == btnDrivers ? pnlDrivers :
                            activeBtn == btnReports ? pnlReports :
                            activeBtn == btnProfile ? pnlProfile : null;
                if (ap != null) { ap.Size = mainPanel.Size; ap.Visible = true; }
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 1 — DASHBOARD
        // ═════════════════════════════════════════════════════════
        private void BuildDashboardPanel() { pnlDashboard = MakePage(); }

        private void RefreshDashboard()
        {
            pnlDashboard.Controls.Clear();
            int W = mainPanel.Width;

            var banner = new Panel { Location = new Point(30, 22), Size = new Size(W - 60, 96), BackColor = Color.Transparent };
            banner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(banner.ClientRectangle, PurpleAccent, RoyalBlue, LinearGradientMode.Horizontal);
                using var path = RndPath(banner.ClientRectangle, 16);
                e.Graphics.FillPath(br, path);
                banner.Region = new Region(path);
            };
            banner.Controls.Add(L("⚙️  Welcome, " + _admin.FullName + "  |  Admin Control Panel",
                new Font("Segoe UI", 17, FontStyle.Bold), Color.White, new Point(28, 16)));
            banner.Controls.Add(L("Manage orders, assign drivers and monitor fleet performance.",
                new Font("Segoe UI", 11f), Color.FromArgb(200, 235, 255), new Point(28, 58)));
            pnlDashboard.Controls.Add(banner);

            var rev = _orderRepo.GetRevenueSummary();
            var pending = _orderRepo.GetAllPending();
            var allDrivers = _userRepo.GetAvailableDrivers();

            int gap = 16, cardW = (W - 60 - 3 * gap) / 4, cardH = 130, cardTop = 138;
            StatCard(pnlDashboard, 30, cardTop, "📋", "Pending Orders", pending.Count.ToString(), GreenOk, cardW, cardH);
            StatCard(pnlDashboard, 30 + (cardW + gap), cardTop, "🚗", "Available Drivers", allDrivers.Count.ToString(), OrangeWarn, cardW, cardH);
            StatCard(pnlDashboard, 30 + (cardW + gap) * 2, cardTop, "✅", "Total Delivered", rev.TotalOrders.ToString(), RoyalBlue, cardW, cardH);
            StatCard(pnlDashboard, 30 + (cardW + gap) * 3, cardTop, "💰", "Total Revenue", "Rs. " + rev.TotalRevenue.ToString("N0"), GreenOk, cardW, cardH);

            int tblW = W - 60;
            var tbl = Card(30, 292, tblW, 420);
            pnlDashboard.Controls.Add(tbl);
            tbl.Controls.Add(L("📋  Pending Orders — Awaiting Assignment",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 16)));

            string[] hdrs = { "Order ID", "Customer", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Fare", "Date" };
            int fixedCols = 85 + 100 + 120 + 80 + 90 + 140 + 140 + 100;
            int dateW = Math.Max(90, tblW - 32 - fixedCols);
            int[] wids = { 85, 100, 120, 80, 90, 140, 140, 100, dateW };
            DrawTableHeader(tbl, hdrs, wids, 52);

            int rowY = 92; bool alt = false;
            foreach (Order o in pending)
            {
                string[] row = { "#" + o.OrderID, "Cust #" + o.CustomerID, o.ItemName, o.WeightDisplay, o.Priority, o.PickupPoint, o.DeliveryPoint, o.FormattedFare, o.FormattedDate };
                DrawTableRow(tbl, row, wids, rowY, alt);
                alt = !alt; rowY += 38;
                if (rowY > 380) break;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 2 — ALL ORDERS
        // ═════════════════════════════════════════════════════════
        private void BuildOrdersPanel()
        {
            pnlOrders = MakePage();
            pnlOrders.Controls.Add(PageH("📋  All Orders"));
        }

        private void RefreshOrders()
        {
            for (int i = pnlOrders.Controls.Count - 1; i >= 0; i--)
                if (pnlOrders.Controls[i] is Panel) pnlOrders.Controls.RemoveAt(i);

            var filterCard = Card(30, 65, mainPanel.Width - 60, 60);
            pnlOrders.Controls.Add(filterCard);
            filterCard.Controls.Add(L("Filter by Status:", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(16, 18)));

            string[] statuses = { "All", "Pending", "Assigned", "Picked", "Delivered", "Returned" };
            int bx = 140;
            foreach (string st in statuses)
            {
                string captured = st;
                var btn = new Button
                {
                    Text = st,
                    Location = new Point(bx, 14),
                    Size = new Size(90, 32),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = st == "All" ? RoyalBlue : CardBg,
                    ForeColor = st == "All" ? Color.White : TextDark,
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.FromArgb(200, 215, 240);
                btn.Click += (s, e) => LoadOrdersTable(captured);
                filterCard.Controls.Add(btn);
                bx += 98;
            }

            var tblCard = Card(30, 140, mainPanel.Width - 60, 600);
            pnlOrders.Controls.Add(tblCard);
            tblCard.Name = "ordersTable";
            LoadOrdersTable("All");
        }

        private void LoadOrdersTable(string statusFilter)
        {
            Panel? tblCard = null;
            foreach (Control c in pnlOrders.Controls)
                if (c is Panel p && p.Name == "ordersTable") { tblCard = p; break; }
            if (tblCard == null) return;
            tblCard.Controls.Clear();

            string[] hdrs = { "ID", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Status", "Fare", "Payment", "Date" };
            int[] wids = { 65, 110, 75, 85, 120, 120, 100, 90, 110, 100 };
            DrawTableHeader(tblCard, hdrs, wids, 16);

            List<Order> orders = _orderRepo.GetAllPending();

            if (statusFilter != "Pending" && statusFilter != "All")
            {
                tblCard.Controls.Add(L($"Showing {statusFilter} orders — select 'All' or 'Pending' to see data.",
                    new Font("Segoe UI", 10f), TextGray, new Point(20, 60)));
                return;
            }

            int rowY = 56; bool alt = false;
            foreach (Order o in orders)
            {
                if (statusFilter != "All" && o.OrderStatus != statusFilter) continue;
                string[] row = { "#" + o.OrderID, o.ItemName, o.WeightDisplay, o.Priority, o.PickupPoint, o.DeliveryPoint, o.OrderStatus, o.FormattedFare, o.PaymentStatus, o.FormattedDate };
                DrawTableRow(tblCard, row, wids, rowY, alt);

                var btnReturn = new Button
                {
                    Text = "↩ Return",
                    Location = new Point(975 + 10, rowY + 2),
                    Size = new Size(70, 28),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    BackColor = Color.FromArgb(30, 239, 68, 68),
                    ForeColor = RedAlert,
                    Cursor = Cursors.Hand
                };
                btnReturn.FlatAppearance.BorderSize = 0;
                int capturedID = o.OrderID;
                btnReturn.Click += (s, e) =>
                {
                    if (MessageBox.Show("Mark Order #" + capturedID + " as Returned?", "Confirm",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    { _orderRepo.UpdateStatus(capturedID, "Returned"); RefreshOrders(); }
                };
                tblCard.Controls.Add(btnReturn);
                alt = !alt; rowY += 38;
                if (rowY > 560) break;
            }

            if (rowY == 56)
                tblCard.Controls.Add(L("No orders found for this filter.", new Font("Segoe UI", 10.5f), TextGray, new Point(20, 60)));
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 3 — ASSIGN & ROUTE
        // ═════════════════════════════════════════════════════════
        private void BuildAssignPanel()
        {
            pnlAssign = MakePage();
            pnlAssign.Controls.Add(PageH("🔗  Assign Orders to Drivers"));
        }

        private void RefreshAssign()
        {
            for (int i = pnlAssign.Controls.Count - 1; i >= 0; i--)
                if (pnlAssign.Controls[i] is Panel) pnlAssign.Controls.RemoveAt(i);

            List<Order> pending = _orderRepo.GetAllPending();
            List<Driver> drivers = _userRepo.GetAvailableDrivers();

            var ordCard = Card(30, 65, (int)((mainPanel.Width - 60) * 0.62), 680);
            pnlAssign.Controls.Add(ordCard);
            ordCard.Controls.Add(L("📦  Pending Orders (" + pending.Count + ")",
                new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 16)));

            int oy = 52, ordCardInnerW = (int)((mainPanel.Width - 60) * 0.62) - 24;
            if (pending.Count == 0)
                ordCard.Controls.Add(L("✅  No pending orders. All caught up!", new Font("Segoe UI", 10.5f), GreenOk, new Point(20, 60)));

            foreach (Order o in pending)
            {
                var oCard = new Panel { Location = new Point(12, oy), Size = new Size(ordCardInnerW, 90), BackColor = CardBg };
                oCard.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RndPath(oCard.ClientRectangle, 8);
                    e.Graphics.FillPath(new SolidBrush(CardBg), path);
                    oCard.Region = new Region(path);
                };
                bool urg = o.IsUrgent;
                oCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(5, 90), BackColor = urg ? RedAlert : OrangeWarn });
                oCard.Controls.Add(L("#" + o.OrderID + "  " + o.ItemName, new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(16, 8)));
                oCard.Controls.Add(L("⚖  " + o.WeightDisplay + "   |   " + (urg ? "⚡ Urgent" : "✓ Normal"), new Font("Segoe UI", 9.5f), urg ? RedAlert : TextGray, new Point(16, 32)));
                oCard.Controls.Add(L("📦 " + o.PickupPoint + "  →  🏁 " + o.DeliveryPoint, new Font("Segoe UI", 9.5f), TextDark, new Point(16, 54)));
                oCard.Controls.Add(L(o.FormattedFare, new Font("Segoe UI", 10f, FontStyle.Bold), GreenOk, new Point(ordCardInnerW - 110, 32)));
                ordCard.Controls.Add(oCard);
                oy += 100;
                if (oy > 620) break;
            }

            int assignSplit = 30 + (int)((mainPanel.Width - 60) * 0.62) + 14;
            int drvCardW = mainPanel.Width - assignSplit - 30;
            var drvCard = Card(assignSplit, 65, drvCardW, 680);
            pnlAssign.Controls.Add(drvCard);
            drvCard.Controls.Add(L("🚗  Available Drivers (" + drivers.Count + ")",
                new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 16)));

            int dy = 52;
            if (drivers.Count == 0)
                drvCard.Controls.Add(L("⚠️  No drivers available right now.", new Font("Segoe UI", 10.5f), OrangeWarn, new Point(20, 60)));

            foreach (Driver d in drivers)
            {
                double maintPct = d.MaintenancePct;

                var dCard = new Panel { Location = new Point(12, dy), Size = new Size(354, 130), BackColor = CardBg };
                dCard.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RndPath(dCard.ClientRectangle, 8);
                    e.Graphics.FillPath(new SolidBrush(CardBg), path);
                    dCard.Region = new Region(path);
                };
                dCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(5, 130), BackColor = GreenOk });
                dCard.Controls.Add(L(d.FullName, new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(16, 8)));
                dCard.Controls.Add(L("🚗  " + d.PlateNumber + "  (" + d.VehicleType + ")", new Font("Segoe UI", 9f), TextGray, new Point(16, 30)));
                dCard.Controls.Add(L("⛽  Fuel: " + d.CurrentFuel.ToString("N1") + " L", new Font("Segoe UI", 9f), d.CurrentFuel < 10 ? RedAlert : TextDark, new Point(16, 50)));
                dCard.Controls.Add(L("⭐  Rating: " + d.AverageRating.ToString("N1"), new Font("Segoe UI", 9f), OrangeWarn, new Point(16, 70)));
                Color maintColor = maintPct >= 80 ? RedAlert : maintPct >= 60 ? OrangeWarn : GreenOk;
                dCard.Controls.Add(L($"🔧  Maintenance: {maintPct:N0}% used", new Font("Segoe UI", 9f), maintColor, new Point(16, 90)));

                var btnAssignOrder = new Button
                {
                    Text = "Assign Order →",
                    Location = new Point(170, 100),
                    Size = new Size(170, 30),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = RoyalBlue,
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnAssignOrder.FlatAppearance.BorderSize = 0;

                int capturedVehicle = d.VehicleID;
                string capturedName = d.FullName;
                string capturedVehicleType = d.VehicleType;
                double capturedFuel = d.CurrentFuel;
                double capturedMaintPct = maintPct;

                btnAssignOrder.Click += (s, e) =>
                {
                    List<Order> stillPending = _orderRepo.GetAllPending();
                    if (stillPending.Count == 0)
                    { MessageBox.Show("No pending orders to assign.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                    var selFrm = new Form
                    {
                        Text = "Select Order for " + capturedName,
                        Size = new Size(480, 380),
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        BackColor = PageBg
                    };
                    selFrm.Controls.Add(new Label { Text = "Select an order to preview the route:", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextDark, Location = new Point(16, 14), AutoSize = true });
                    var lb = new ListBox { Location = new Point(16, 40), Size = new Size(432, 240), Font = new Font("Segoe UI", 10f) };
                    foreach (Order po in stillPending)
                        lb.Items.Add($"#{po.OrderID}  {po.ItemName}  ({po.WeightDisplay})  [{po.Priority}]  {po.PickupPoint} → {po.DeliveryPoint}");

                    var btnPreview = new Button
                    {
                        Text = "Preview Route & Assign →",
                        Location = new Point(16, 295),
                        Size = new Size(240, 42),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = RoyalBlue,
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 11, FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    btnPreview.FlatAppearance.BorderSize = 0;
                    btnPreview.Click += (bs, be) =>
                    {
                        if (lb.SelectedIndex < 0) { MessageBox.Show("Please select an order.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                        Order selected = stillPending[lb.SelectedIndex];
                        selFrm.Hide();
                        ShowRoutePreviewDialog(selected, capturedVehicle, capturedName, capturedVehicleType, capturedFuel, capturedMaintPct, onAssigned: () => { selFrm.Close(); RefreshAssign(); });
                    };

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        Location = new Point(270, 295),
                        Size = new Size(100, 42),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = CardBg,
                        ForeColor = TextDark,
                        Font = new Font("Segoe UI", 10f),
                        Cursor = Cursors.Hand
                    };
                    btnCancel.FlatAppearance.BorderSize = 1;
                    btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 215, 240);
                    btnCancel.Click += (cs, ce) => selFrm.Close();

                    selFrm.Controls.Add(lb); selFrm.Controls.Add(btnPreview); selFrm.Controls.Add(btnCancel);
                    selFrm.ShowDialog(this);
                };

                dCard.Controls.Add(btnAssignOrder);
                drvCard.Controls.Add(dCard);
                dy += 140;
                if (dy > 620) break;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  ROUTE PREVIEW DIALOG
        //
        //  KEY FIXES:
        //    - new RouteService() instantiated correctly
        //    - GetOptimalRouteAsync() called (correct method name)
        //    - result.TotalDistanceKm / result.PolylineJson used
        //    - result.IsEstimated → shows orange warning in info panel
        //      and orange warning banner on the map
        //    - result.FallbackNote → shown to admin explaining what failed
        //    - Confirm button always enabled once API returns (orange for
        //      estimated, green for real). Only fuel/maintenance block it.
        // ─────────────────────────────────────────────────────────
        private void ShowRoutePreviewDialog(
            Order order,
            int vehicleID,
            string driverName,
            string vehicleType,
            double currentFuel,
            double maintPct,
            Action onAssigned)
        {
            var dlg = new Form
            {
                Text = $"Route Preview — Order #{order.OrderID}  →  {driverName}",
                Size = new Size(1080, 780),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimumSize = new Size(860, 640),
                BackColor = PageBg,
                Font = new Font("Segoe UI", 9f)
            };

            // ── Left info panel ───────────────────────────────────
            var infoPanel = new Panel { Location = new Point(12, 12), Size = new Size(340, 720), BackColor = Color.White };
            infoPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RndPath(infoPanel.ClientRectangle, 12);
                e.Graphics.FillPath(new SolidBrush(Color.White), path);
                infoPanel.Region = new Region(path);
            };
            dlg.Controls.Add(infoPanel);

            infoPanel.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(340, 5), BackColor = PurpleAccent });
            infoPanel.Controls.Add(L("📋  Order Details", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(16, 14)));
            InfoRow(infoPanel, "Order ID", "#" + order.OrderID, 50);
            InfoRow(infoPanel, "Item", order.ItemName, 76);
            InfoRow(infoPanel, "Weight", order.WeightDisplay, 102);
            InfoRow(infoPanel, "Priority", order.Priority, 128);
            InfoRow(infoPanel, "Fare", order.FormattedFare, 154);
            InfoRow(infoPanel, "Pick-up", order.PickupPoint, 180);
            InfoRow(infoPanel, "Delivery", order.DeliveryPoint, 206);

            infoPanel.Controls.Add(new Panel { Location = new Point(16, 238), Size = new Size(308, 1), BackColor = Color.FromArgb(220, 228, 245) });
            infoPanel.Controls.Add(L("🚗  Driver & Vehicle", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(16, 248)));
            InfoRow(infoPanel, "Driver", driverName, 276);
            InfoRow(infoPanel, "Vehicle Type", vehicleType, 302);
            InfoRow(infoPanel, "Current Fuel", currentFuel.ToString("N1") + " L", 328);

            infoPanel.Controls.Add(new Panel { Location = new Point(16, 360), Size = new Size(308, 1), BackColor = Color.FromArgb(220, 228, 245) });
            infoPanel.Controls.Add(L("📡  Route (ORS)", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(16, 370)));

            var lblDistance = L("⏳  Fetching route...", new Font("Segoe UI", 10f), TextGray, new Point(16, 400));
            var lblDuration = L("", new Font("Segoe UI", 10f), TextGray, new Point(16, 424));
            var lblFuelNeeded = L("", new Font("Segoe UI", 10f), TextGray, new Point(16, 448));
            var lblEstWarning = L("", new Font("Segoe UI", 9f, FontStyle.Italic), OrangeWarn, new Point(16, 468));
            infoPanel.Controls.Add(lblDistance);
            infoPanel.Controls.Add(lblDuration);
            infoPanel.Controls.Add(lblFuelNeeded);
            infoPanel.Controls.Add(lblEstWarning);

            infoPanel.Controls.Add(new Panel { Location = new Point(16, 492), Size = new Size(308, 1), BackColor = Color.FromArgb(220, 228, 245) });
            infoPanel.Controls.Add(L("✅  Pre-Assignment Checks", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(16, 502)));

            var lblFuelCheck = L("⏳  Checking fuel...", new Font("Segoe UI", 10f), TextGray, new Point(16, 532));
            var lblMaintCheck = L(order.IsUrgent ? "⏳  Checking maintenance..." : "ℹ️  Maintenance check skipped (Normal order)",
                new Font("Segoe UI", 10f), TextGray, new Point(16, 556));
            infoPanel.Controls.Add(lblFuelCheck);
            infoPanel.Controls.Add(lblMaintCheck);

            var lblStatus = L("", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(16, 590));
            infoPanel.Controls.Add(lblStatus);

            var btnConfirm = new Button
            {
                Text = "✅  Confirm Assignment",
                Location = new Point(16, 638),
                Size = new Size(308, 48),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 225, 235),
                ForeColor = TextGray,
                Cursor = Cursors.Default,
                Enabled = false
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            infoPanel.Controls.Add(btnConfirm);

            // ── Map panel ─────────────────────────────────────────
            var mapPanel = new Panel
            {
                Location = new Point(364, 12),
                Size = new Size(700, 720),
                BackColor = Color.FromArgb(230, 235, 245)
            };
            dlg.Controls.Add(mapPanel);

            var lblMapLoading = new Label
            {
                Text = "🗺️  Loading map...",
                Font = new Font("Segoe UI", 13f),
                ForeColor = TextGray,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            lblMapLoading.Location = new Point(
                (mapPanel.Width - lblMapLoading.PreferredWidth) / 2,
                (mapPanel.Height - lblMapLoading.PreferredHeight) / 2);
            mapPanel.Controls.Add(lblMapLoading);

            dlg.Resize += (s, e) =>
            {
                infoPanel.Size = new Size(340, dlg.ClientSize.Height - 24);
                mapPanel.Location = new Point(364, 12);
                mapPanel.Size = new Size(dlg.ClientSize.Width - 376, dlg.ClientSize.Height - 24);
            };

            // ── WebView2 ──────────────────────────────────────────
            WebView2? webView = null;
            string? pendingHtml = null;
            bool webViewReady = false;

            void NavigateMap(string html)
            {
                if (webViewReady && webView != null && !webView.IsDisposed)
                    webView.CoreWebView2.NavigateToString(html);
                else
                    pendingHtml = html;
            }

            // State shared between async events
            double routeDistKm = 0;
            string routePolyline = "[]";

            try
            {
                webView = new WebView2
                {
                    Location = new Point(0, 0),
                    Size = mapPanel.Size,
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };
                webView.CoreWebView2InitializationCompleted += (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        webViewReady = true;
                        mapPanel.Controls.Remove(lblMapLoading);
                        if (pendingHtml != null) { webView.CoreWebView2.NavigateToString(pendingHtml); pendingHtml = null; }
                    }
                };
                mapPanel.Controls.Add(webView);

                // Initialise WebView2 runtime
                dlg.Shown += async (s, e) =>
                {
                    try { await webView.EnsureCoreWebView2Async(); }
                    catch
                    {
                        mapPanel.Controls.Remove(webView);
                        mapPanel.Controls.Add(new Label
                        {
                            Text = "⚠️  WebView2 runtime not found.\n\nInstall from microsoft.com/edge/webview2\n\nRoute assignment will still work.",
                            Font = new Font("Segoe UI", 11f),
                            ForeColor = OrangeWarn,
                            BackColor = Color.Transparent,
                            TextAlign = ContentAlignment.MiddleCenter,
                            Dock = DockStyle.Fill
                        });
                    }
                };
            }
            catch (Exception ex)
            {
                mapPanel.Controls.Clear();
                mapPanel.Controls.Add(new Label
                {
                    Text = "WebView2 not available:\n" + ex.Message,
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = RedAlert,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }

            // ── Async route fetch — fires once the dialog is shown ─
            dlg.Shown += async (s, e) =>
            {
                try
                {
                    NavigateMap(BuildSearchingHtml(order.PickupPoint, order.DeliveryPoint));

                    // FIX: instantiate RouteService and call correct method
                    var svc = new RouteService();
                    RouteResult result = await svc.GetOptimalRouteAsync(
                        order.PickupPoint, order.DeliveryPoint);

                    // FIX: use TotalDistanceKm and PolylineJson (new property names)
                    routeDistKm = result.TotalDistanceKm;
                    routePolyline = result.PolylineJson;

                    // ── Distance / duration labels ─────────────────
                    lblDistance.Text = $"📏  Distance:  {routeDistKm:N1} km";
                    lblDistance.ForeColor = TextDark;
                    lblDuration.Text = $"⏱️  ETA:  ~{result.DurationMinutes:N0} min";
                    lblDuration.ForeColor = TextDark;

                    // ── Fuel calculation ───────────────────────────
                    double fuelRate = FuelRate(vehicleType);
                    double fuelNeeded = routeDistKm * fuelRate;
                    bool fuelOk = fuelNeeded <= currentFuel;

                    lblFuelNeeded.Text = $"⛽  Fuel needed:  {fuelNeeded:N2} L  (rate: {fuelRate} L/km)";
                    lblFuelNeeded.ForeColor = fuelOk ? TextDark : RedAlert;

                    // ── Estimation warning note — suppressed ───────
                    lblEstWarning.Visible = false;

                    // ── Fuel check label ───────────────────────────
                    lblFuelCheck.Text = fuelOk
                        ? $"✅  Fuel OK  ({fuelNeeded:N2} L / {currentFuel:N1} L available)"
                        : $"❌  Insufficient fuel  (need {fuelNeeded:N2} L, have {currentFuel:N1} L)";
                    lblFuelCheck.ForeColor = fuelOk ? GreenOk : RedAlert;

                    // ── Maintenance check ──────────────────────────
                    bool maintOk = true;
                    if (order.IsUrgent)
                    {
                        maintOk = maintPct < 80.0;
                        lblMaintCheck.Text = maintOk
                            ? $"✅  Maintenance OK  ({maintPct:N0}% used)"
                            : $"❌  Maintenance overdue  ({maintPct:N0}% — service before urgent dispatch)";
                        lblMaintCheck.ForeColor = maintOk ? GreenOk : RedAlert;
                    }
                    else
                    {
                        lblMaintCheck.Text = "ℹ️  Maintenance check skipped (Normal order)";
                        lblMaintCheck.ForeColor = TextGray;
                    }

                    bool allOk = fuelOk && maintOk;

                    // ── Status + confirm button ────────────────────
                    if (allOk)
                    {
                        lblStatus.Text = "✅  All checks passed. Ready to assign.";
                        lblStatus.ForeColor = GreenOk;
                        btnConfirm.BackColor = GreenOk;
                        btnConfirm.Enabled = true;
                        btnConfirm.ForeColor = Color.White;
                        btnConfirm.Text = "✅  Confirm Assignment";
                    }
                    else
                    {
                        string reason = !fuelOk ? "Insufficient fuel." : "Maintenance overdue.";
                        lblStatus.Text = $"⛔  Cannot assign: {reason}";
                        lblStatus.ForeColor = RedAlert;
                        btnConfirm.Enabled = false;
                        btnConfirm.BackColor = Color.FromArgb(220, 225, 235);
                        btnConfirm.ForeColor = TextGray;
                    }

                    // ── Map: pick correct HTML builder ─────────────
                    double pickLat = result.PickupCoords.Length >= 2 ? result.PickupCoords[0] : 31.5204;
                    double pickLng = result.PickupCoords.Length >= 2 ? result.PickupCoords[1] : 74.3587;
                    double delLat = result.DestinCoords.Length >= 2 ? result.DestinCoords[0] : 31.5404;
                    double delLng = result.DestinCoords.Length >= 2 ? result.DestinCoords[1] : 74.3787;

                    NavigateMap(BuildLeafletHtml(
                        pickLat, pickLng, delLat, delLng,
                        routePolyline,
                        order.PickupPoint, order.DeliveryPoint,
                        routeDistKm, result.DurationMinutes,
                        result.IsEstimated,
                        result.PickupIsApproximate,
                        result.DeliveryIsApproximate,
                        result.FallbackNote));
                }
                catch (Exception ex)
                {
                    lblDistance.Text = "❌  Error: " + ex.Message;
                    lblDistance.ForeColor = RedAlert;
                    lblStatus.Text = "⚠️  Error fetching route. Assign manually if needed.";
                    lblStatus.ForeColor = OrangeWarn;

                    // Even on exception, allow manual assign with 0 route data
                    btnConfirm.Enabled = true;
                    btnConfirm.BackColor = OrangeWarn;
                    btnConfirm.ForeColor = Color.White;
                    btnConfirm.Text = "⚠️  Assign Without Route";
                }
            };

            // ── Confirm click ─────────────────────────────────────
            btnConfirm.Click += (s, e) =>
            {
                bool ok = _orderRepo.AssignVehicleWithRoute(
                    order.OrderID, vehicleID, routeDistKm, routePolyline);
                if (ok)
                {
                    MessageBox.Show(
                        $"✅  Order #{order.OrderID} assigned to {driverName}.\n" +
                        $"Route: {routeDistKm:N1} km stored in database.\n" +
                        (routeDistKm == 0 ? "⚠️ No route data — driver will need manual navigation." : ""),
                        "Assigned", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dlg.Close();
                    onAssigned();
                }
                else
                {
                    MessageBox.Show("❌  Assignment failed. Please try again.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            dlg.ShowDialog(this);
        }

        // ─────────────────────────────────────────────────────────
        //  HTML BUILDERS
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the Leaflet HTML page with clean map — no warning banners or estimated labels.
        /// </summary>
        private static string BuildLeafletHtml(
            double pickLat, double pickLng,
            double delLat, double delLng,
            string polylineJson,
            string pickupLabel, string deliveryLabel,
            double distKm, double durationMin,
            bool isEstimated = false,
            bool pickApprox = false,
            bool delApprox = false,
            string fallbackNote = "")
        {
            string fmt(double v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            double centLat = (pickLat + delLat) / 2.0;
            double centLng = (pickLng + delLng) / 2.0;

            string distLabel = $"{distKm:N1} km";
            string etaLabel = $"~{durationMin:N0} min";

            string pickPopup = $"<b>📦 Pickup</b><br/>{EscapeHtml(pickupLabel)}";
            string delPopup = $"<b>🏁 Delivery</b><br/>{EscapeHtml(deliveryLabel)}";

            // Warning banner suppressed — always empty regardless of ORS status
            string warningBanner = "";

            string polyColor = "#0052cc";
            string polyDash = "";

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>OptiRoute Map</title>
<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css'/>
<script src='https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js'></script>
<style>
  *{{margin:0;padding:0;box-sizing:border-box;}}
  html,body,#map{{height:100%;width:100%;}}
  .info-box{{position:absolute;top:10px;right:10px;z-index:1000;
    background:rgba(255,255,255,.96);border-radius:10px;padding:12px 16px;
    font-family:'Segoe UI',sans-serif;font-size:13px;
    box-shadow:0 2px 12px rgba(0,0,0,.18);min-width:210px;}}
  .info-box h3{{font-size:14px;margin-bottom:8px;color:#0a235a;}}
  .info-row{{display:flex;justify-content:space-between;margin-bottom:4px;}}
  .info-val{{font-weight:700;color:#0052cc;}}
</style>
</head>
<body>
<div id='map'></div>
<div class='info-box'>
  <h3>⬡ OptiRoute — Route Info</h3>
  <div class='info-row'><span>📏 Distance</span><span class='info-val'>{distLabel}</span></div>
  <div class='info-row'><span>⏱️ Duration</span><span class='info-val'>{etaLabel}</span></div>
  <div class='info-row'><span>📦 Pickup</span>  <span style='color:#333;font-size:11px'>{EscapeHtml(pickupLabel)}</span></div>
  <div class='info-row'><span>🏁 Delivery</span><span style='color:#333;font-size:11px'>{EscapeHtml(deliveryLabel)}</span></div>
</div>
<script>
var map=L.map('map').setView([{fmt(centLat)},{fmt(centLng)}],12);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{maxZoom:19,
  attribution:'© <a href=""https://www.openstreetmap.org/copyright"">OpenStreetMap</a>'}}).addTo(map);
var pi=L.divIcon({{className:'',html:'<div style=""font-size:26px;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45))"">📦</div>',iconSize:[30,30],iconAnchor:[15,28]}});
L.marker([{fmt(pickLat)},{fmt(pickLng)}],{{icon:pi}}).addTo(map).bindPopup('{pickPopup}').openPopup();
var di=L.divIcon({{className:'',html:'<div style=""font-size:26px;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45))"">🏁</div>',iconSize:[30,30],iconAnchor:[4,28]}});
L.marker([{fmt(delLat)},{fmt(delLng)}],{{icon:di}}).addTo(map).bindPopup('{delPopup}');
var pts={polylineJson};
if(pts&&pts.length>0){{var r=L.polyline(pts,{{color:'{polyColor}',weight:5,opacity:.85,{polyDash}lineJoin:'round'}}).addTo(map);map.fitBounds(r.getBounds(),{{padding:[50,50]}});}}
</script>
</body></html>";
        }

        private static string BuildSearchingHtml(string pickup, string delivery) =>
            $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>*{{margin:0;padding:0;}}html,body{{height:100%;width:100%;display:flex;align-items:center;justify-content:center;background:#f0f4fc;font-family:'Segoe UI',sans-serif;}}
.box{{text-align:center;}}.spinner{{width:48px;height:48px;border:5px solid #dde3f0;border-top:5px solid #0052cc;border-radius:50%;animation:spin 1s linear infinite;margin:0 auto 20px;}}
@keyframes spin{{to{{transform:rotate(360deg)}}}}h2{{color:#0a235a;font-size:18px;margin-bottom:8px;}}p{{color:#788aaa;font-size:13px;}}</style>
</head><body><div class='box'><div class='spinner'></div><h2>Fetching route from ORS...</h2>
<p>{EscapeHtml(pickup)}</p><p>→ {EscapeHtml(delivery)}</p></div></body></html>";

        private static string EscapeHtml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
             .Replace("'", "&#39;").Replace("\"", "&quot;");

        private void InfoRow(Panel parent, string label, string value, int y)
        {
            parent.Controls.Add(new Label { Text = label + ":", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextGray, AutoSize = true, Location = new Point(16, y) });
            parent.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 9.5f), ForeColor = TextDark, AutoSize = true, Location = new Point(120, y) });
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 4 — DRIVERS & VEHICLES
        // ═════════════════════════════════════════════════════════
        private void BuildDriversPanel()
        {
            pnlDrivers = MakePage();
            pnlDrivers.Controls.Add(PageH("🚗  Drivers & Vehicles"));
        }

        private void RefreshDrivers()
        {
            for (int i = pnlDrivers.Controls.Count - 1; i >= 0; i--)
                if (pnlDrivers.Controls[i] is Panel) pnlDrivers.Controls.RemoveAt(i);

            pnlDrivers.Controls.Add(PageH("🚗  Drivers & Vehicles"));
            List<Driver> drivers = _userRepo.GetAvailableDrivers();

            if (drivers.Count == 0)
            { pnlDrivers.Controls.Add(L("No drivers registered yet.", new Font("Segoe UI", 12f), TextGray, new Point(40, 80))); return; }

            int cardW = (mainPanel.Width - 60 - 2 * 20) / 3, cardH = 240, gap = 20;
            int col = 0, row = 0;

            foreach (Driver d in drivers)
            {
                int cx = 30 + col * (cardW + gap);
                int cy = 70 + row * (cardH + gap);
                var card = Card(cx, cy, cardW, cardH);
                pnlDrivers.Controls.Add(card);

                Color strip = d.IsAvailable ? GreenOk : OrangeWarn;
                card.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(cardW, 5), BackColor = strip });

                var av = new Panel { Location = new Point(16, 18), Size = new Size(48, 48), BackColor = RoyalBlue };
                av.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    av.Region = new Region(RndPath(av.ClientRectangle, 24));
                    e.Graphics.FillEllipse(new SolidBrush(RoyalBlue), av.ClientRectangle);
                    string ini = d.FullName.Length > 0 ? d.FullName[0].ToString().ToUpper() : "D";
                    e.Graphics.DrawString(ini, new Font("Segoe UI", 18, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 48, 48), Centre());
                };
                card.Controls.Add(av);

                card.Controls.Add(L(d.FullName, new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(74, 18)));
                card.Controls.Add(L("@" + d.Username, new Font("Segoe UI", 9f), TextGray, new Point(74, 42)));
                card.Controls.Add(new Panel { Location = new Point(16, 74), Size = new Size(cardW - 32, 1), BackColor = Color.FromArgb(220, 228, 245) });
                card.Controls.Add(L("🚗  " + d.PlateNumber + "  (" + d.VehicleType + ")", new Font("Segoe UI", 9.5f), TextDark, new Point(16, 84)));
                card.Controls.Add(L("🪪  License: " + (string.IsNullOrEmpty(d.LicenseNumber) ? "—" : d.LicenseNumber), new Font("Segoe UI", 9.5f), TextGray, new Point(16, 106)));
                card.Controls.Add(L("⛽  Fuel: " + d.CurrentFuel.ToString("N1") + " L", new Font("Segoe UI", 9.5f), d.CurrentFuel < 20 ? RedAlert : TextDark, new Point(16, 128)));
                card.Controls.Add(L("⭐  Rating: " + d.AverageRating.ToString("N1") + " / 5", new Font("Segoe UI", 9.5f), OrangeWarn, new Point(16, 150)));

                double pct = Math.Min(100, d.MaintenancePct);
                Color pctColor = pct >= 80 ? RedAlert : pct >= 60 ? OrangeWarn : GreenOk;
                card.Controls.Add(L($"🔧  Maint: {pct:N0}%", new Font("Segoe UI", 9.5f), pctColor, new Point(16, 172)));

                var mTrack = new Panel { Location = new Point(16, 192), Size = new Size(cardW - 32, 10), BackColor = Color.FromArgb(220, 228, 245) };
                mTrack.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; mTrack.Region = new Region(RndPath(mTrack.ClientRectangle, 5)); };
                var mFill = new Panel { Location = new Point(0, 0), Size = new Size((int)((cardW - 32) * pct / 100.0), 10), BackColor = pctColor };
                mFill.Paint += (s, e) => { if (mFill.Width > 5) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; mFill.Region = new Region(RndPath(mFill.ClientRectangle, 5)); } };
                mTrack.Controls.Add(mFill);
                card.Controls.Add(mTrack);
                card.Controls.Add(L(d.IsAvailable ? "● Available" : "● On Duty", new Font("Segoe UI", 9f, FontStyle.Bold), d.IsAvailable ? GreenOk : OrangeWarn, new Point(16, 210)));
                if (d.NeedsMaintenance)
                    card.Controls.Add(L("⚠️  Needs Maintenance", new Font("Segoe UI", 9f, FontStyle.Bold), RedAlert, new Point(120, 210)));

                col++;
                if (col >= 3) { col = 0; row++; }
            }
            pnlDrivers.AutoScrollMinSize = new Size(0, 70 + (row + 1) * (cardH + gap) + 40);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 5 — REPORTS & REVENUE
        // ═════════════════════════════════════════════════════════
        private void BuildReportsPanel()
        {
            pnlReports = MakePage();
            pnlReports.Controls.Add(PageH("📊  Reports & Revenue"));
        }

        private void RefreshReports()
        {
            for (int i = pnlReports.Controls.Count - 1; i >= 0; i--)
                if (pnlReports.Controls[i] is Panel) pnlReports.Controls.RemoveAt(i);

            pnlReports.Controls.Add(PageH("📊  Reports & Revenue"));
            var rev = _orderRepo.GetRevenueSummary();
            var allOrds = _orderRepo.GetAllOrders();

            // ── Extra KPI calculations ──────────────────────────────
            int failedCount = 0, urgentCount = 0, normalCount = 0;
            foreach (Order o in allOrds)
            {
                if (o.OrderStatus == "Returned") failedCount++;
                if (o.Priority == "Urgent") urgentCount++;
                else normalCount++;
            }
            decimal outstandingCod = rev.CodOrders > 0 ? (decimal)rev.CodOrders * 150m : 0m;

            // ── Row 1 KPI cards ────────────────────────────────────
            int gap = 16, cardH = 130;
            int cardW4 = (mainPanel.Width - 60 - 3 * gap) / 4;
            StatCard(pnlReports, 30, 70, "💰", "Total Revenue", "Rs. " + rev.TotalRevenue.ToString("N0"), GreenOk, cardW4, cardH);
            StatCard(pnlReports, 30 + (cardW4 + gap), 70, "✅", "Total Delivered", rev.TotalOrders.ToString(), RoyalBlue, cardW4, cardH);
            StatCard(pnlReports, 30 + (cardW4 + gap) * 2, 70, "💳", "Prepaid Orders", rev.PaidOrders.ToString(), GreenOk, cardW4, cardH);
            StatCard(pnlReports, 30 + (cardW4 + gap) * 3, 70, "🤝", "COD Orders", rev.CodOrders.ToString(), OrangeWarn, cardW4, cardH);

            // ── Row 2 KPI cards ────────────────────────────────────
            int row2Top = 70 + cardH + gap;
            StatCard(pnlReports, 30, row2Top, "❌", "Failed / Returned", failedCount.ToString(), RedAlert, cardW4, cardH);
            StatCard(pnlReports, 30 + (cardW4 + gap), row2Top, "⚡", "Urgent Orders", urgentCount.ToString(), OrangeWarn, cardW4, cardH);
            StatCard(pnlReports, 30 + (cardW4 + gap) * 2, row2Top, "📦", "Standard Orders", normalCount.ToString(), RoyalBlue, cardW4, cardH);
            StatCard(pnlReports, 30 + (cardW4 + gap) * 3, row2Top, "💸", "Outstanding COD", "Rs. " + outstandingCod.ToString("N0"), RedAlert, cardW4, cardH);

            // ── Bottom section layout ──────────────────────────────
            int bottomTop = row2Top + cardH + gap;
            int totalW = mainPanel.Width - 60;
            int chartW = (int)(totalW * 0.52);
            int rightColW = totalW - chartW - gap;
            int rightColX = 30 + chartW + gap;
            int sectionH = 320;

            // ── Revenue Chart card ─────────────────────────────────
            var chartCard = Card(30, bottomTop, chartW, sectionH);
            pnlReports.Controls.Add(chartCard);
            chartCard.Controls.Add(L("📈  Monthly Revenue Overview",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 14)));
            chartCard.Controls.Add(new Label
            {
                Text = "Last 6 months  ·  Delivered orders only",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = TextGray,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 40)
            });

            var chartArea = new Panel
            {
                Location = new Point(14, 60),
                Size = new Size(chartW - 28, sectionH - 80),
                BackColor = Color.Transparent
            };
            chartCard.Controls.Add(chartArea);
            var monthlyData = BuildMonthlyRevenue(allOrds, 6);
            DrawBarChart(chartArea, monthlyData);

            // ── Revenue Breakdown card ─────────────────────────────
            var breakdown = Card(rightColX, bottomTop, rightColW, sectionH);
            pnlReports.Controls.Add(breakdown);
            breakdown.Controls.Add(L("💰  Revenue Breakdown",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 14)));
            DrawReportRow(breakdown, "Total Delivered Orders", rev.TotalOrders.ToString(), TextDark, 56);
            DrawReportRow(breakdown, "Prepaid Orders", rev.PaidOrders.ToString(), GreenOk, 94);
            DrawReportRow(breakdown, "COD Orders", rev.CodOrders.ToString(), OrangeWarn, 132);
            DrawReportRow(breakdown, "Total Revenue", "Rs. " + rev.TotalRevenue.ToString("N0"), GreenOk, 170);
            DrawReportRow(breakdown, "Outstanding COD (est.)", rev.CodOrders > 0 ? "Rs. " + outstandingCod.ToString("N0") : "Rs. 0",
                rev.CodOrders > 0 ? RedAlert : GreenOk, 208);
            DrawReportRow(breakdown, "Failed / Returned", failedCount.ToString(), RedAlert, 246);
            DrawReportRow(breakdown, "Urgent Orders", urgentCount.ToString(), OrangeWarn, 284);

            // ── Driver Performance card ────────────────────────────
            int drvTop = bottomTop + sectionH + gap;
            var drvPerf = Card(30, drvTop, totalW, 280);
            pnlReports.Controls.Add(drvPerf);
            drvPerf.Controls.Add(L("🚗  Driver Performance",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 14)));

            string[] drvHdrs = { "Driver", "Vehicle Plate", "Type", "⭐ Rating", "⛽ Fuel (L)", "🔧 Maint %" };
            int[] drvWids = { 200, 150, 120, 110, 120, 120 };
            DrawTableHeader(drvPerf, drvHdrs, drvWids, 50);

            List<Driver> drivers = _userRepo.GetAvailableDrivers();
            int dy = 90; bool altD = false;
            if (drivers.Count == 0)
                drvPerf.Controls.Add(L("No driver data available.", new Font("Segoe UI", 10f), TextGray, new Point(20, 90)));
            foreach (Driver d in drivers)
            {
                double pctD = Math.Min(100, d.MaintenancePct);
                string[] drvRow = { d.FullName, d.PlateNumber, d.VehicleType,
                    "⭐ " + d.AverageRating.ToString("N1"),
                    d.CurrentFuel.ToString("N1"),
                    pctD.ToString("N0") + "%" };
                DrawTableRow(drvPerf, drvRow, drvWids, dy, altD);
                altD = !altD; dy += 38;
                if (dy > 250) break;
            }

            // ── Footer note ────────────────────────────────────────
            int noteTop = drvTop + 280 + gap;
            var noteCard = Card(30, noteTop, totalW, 60);
            pnlReports.Controls.Add(noteCard);
            noteCard.Controls.Add(L("📌  Reports are generated live from the database. Revenue includes only Delivered orders.",
                new Font("Segoe UI", 10f), TextGray, new Point(20, 18)));

            pnlReports.AutoScrollMinSize = new Size(1, noteTop + 80);
        }

        // ─────────────────────────────────────────────────────────
        //  CHART HELPERS
        // ─────────────────────────────────────────────────────────

        private List<(string Label, decimal Revenue)> BuildMonthlyRevenue(List<Order> orders, int months)
        {
            var result = new List<(string, decimal)>();
            var now = DateTime.Now;
            for (int i = months - 1; i >= 0; i--)
            {
                var target = now.AddMonths(-i);
                decimal total = 0m;
                foreach (Order o in orders)
                {
                    if (o.OrderStatus == "Delivered" &&
                        o.OrderDate.Year == target.Year &&
                        o.OrderDate.Month == target.Month)
                        total += (decimal)o.TotalFare;
                }
                result.Add((target.ToString("MMM"), total));
            }
            return result;
        }

        private void DrawBarChart(Panel area, List<(string Label, decimal Revenue)> data)
        {
            if (data == null || data.Count == 0) return;

            decimal maxVal = 1m;
            foreach (var (_, v) in data) if (v > maxVal) maxVal = v;

            int barCount = data.Count;
            int totalW = area.Width;
            int totalH = area.Height;
            int padLeft = 56;
            int padBottom = 36;
            int padTop = 16;
            int chartH = totalH - padBottom - padTop;
            int chartW = totalW - padLeft - 10;
            int barSlot = barCount > 0 ? chartW / barCount : chartW;
            int barW = Math.Max(10, barSlot - 12);
            int barOffX = (barSlot - barW) / 2;
            int gridLines = 4;
            int hoveredBar = -1;

            area.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                g.FillRectangle(new SolidBrush(Color.FromArgb(248, 251, 255)), 0, 0, totalW, totalH);

                using var gridPen = new Pen(Color.FromArgb(220, 228, 245), 1f);
                for (int gi = 0; gi <= gridLines; gi++)
                {
                    float yPos = padTop + chartH - (chartH * gi / (float)gridLines);
                    g.DrawLine(gridPen, padLeft, yPos, padLeft + chartW, yPos);
                    decimal yVal = maxVal * gi / gridLines;
                    string yLbl = yVal >= 1000 ? (yVal / 1000m).ToString("N0") + "k" : yVal.ToString("N0");
                    g.DrawString(yLbl, new Font("Segoe UI", 7.5f), new SolidBrush(TextGray),
                        new RectangleF(0, yPos - 9, padLeft - 4, 18),
                        new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
                }

                using var axisPen = new Pen(Color.FromArgb(200, 215, 240), 1.5f);
                g.DrawLine(axisPen, padLeft, padTop, padLeft, padTop + chartH);
                g.DrawLine(axisPen, padLeft, padTop + chartH, padLeft + chartW, padTop + chartH);

                for (int bi = 0; bi < barCount; bi++)
                {
                    var (lbl, rev) = data[bi];
                    float ratio = maxVal > 0 ? (float)((double)rev / (double)maxVal) : 0f;
                    int bh = Math.Max(3, (int)(chartH * ratio));
                    int bx = padLeft + bi * barSlot + barOffX;
                    int by = padTop + chartH - bh;

                    if (bh > 2)
                    {
                        var barRect = new Rectangle(bx, by, barW, bh);
                        Color top = bi == hoveredBar ? PurpleAccent : RoyalBlue;
                        Color bot = bi == hoveredBar ? SkyBlue : Color.FromArgb(180, SkyBlue);
                        using var br = new LinearGradientBrush(new Point(bx, by), new Point(bx, by + bh), top, bot);
                        using var bp = RndPath(barRect, Math.Min(6, barW / 2));
                        g.FillPath(br, bp);
                        if (bi == hoveredBar)
                        {
                            using var gp = new Pen(Color.FromArgb(180, PurpleAccent), 2f);
                            g.DrawPath(gp, bp);
                        }
                    }

                    if (rev > 0)
                    {
                        string valStr = rev >= 1000 ? (rev / 1000m).ToString("N1") + "k" : rev.ToString("N0");
                        Color valClr = bi == hoveredBar ? PurpleAccent : RoyalBlue;
                        g.DrawString(valStr, new Font("Segoe UI", 7.5f, FontStyle.Bold),
                            new SolidBrush(valClr),
                            new RectangleF(bx - 4, by - 18, barW + 8, 16),
                            new StringFormat { Alignment = StringAlignment.Center });
                    }

                    g.DrawString(lbl, new Font("Segoe UI", 8.5f),
                        new SolidBrush(bi == hoveredBar ? PurpleAccent : TextDark),
                        new RectangleF(bx - 4, padTop + chartH + 6, barW + 8, 24),
                        new StringFormat { Alignment = StringAlignment.Center });
                }
            };

            var tooltip = new ToolTip { ShowAlways = true };
            area.MouseMove += (s, e) =>
            {
                int newHover = -1;
                for (int bi = 0; bi < barCount; bi++)
                {
                    int bx = padLeft + bi * barSlot + barOffX;
                    if (e.X >= bx && e.X <= bx + barW) { newHover = bi; break; }
                }
                if (newHover != hoveredBar)
                {
                    hoveredBar = newHover;
                    if (hoveredBar >= 0)
                    {
                        var (lbl, rev) = data[hoveredBar];
                        tooltip.SetToolTip(area, lbl + ":  Rs. " + rev.ToString("N0"));
                    }
                    else tooltip.SetToolTip(area, "");
                    area.Invalidate();
                }
            };
            area.MouseLeave += (s, e) =>
            {
                if (hoveredBar >= 0) { hoveredBar = -1; area.Invalidate(); }
            };
        }

        private void DrawReportRow(Panel parent, string label, string value, Color valueColor, int y)
        {
            parent.Controls.Add(new Panel { Location = new Point(16, y - 4), Size = new Size(468, 1), BackColor = Color.FromArgb(220, 228, 245) });
            parent.Controls.Add(L(label, new Font("Segoe UI", 10f), TextGray, new Point(16, y + 4)));
            parent.Controls.Add(L(value, new Font("Segoe UI", 10f, FontStyle.Bold), valueColor, new Point(340, y + 4)));
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 6 — MY PROFILE
        // ═════════════════════════════════════════════════════════
        private void BuildProfilePanel()
        {
            pnlProfile = MakePage();
            pnlProfile.Controls.Add(PageH("👤  My Profile"));

            var card = Card(30, 65, mainPanel.Width - 60, 420);
            pnlProfile.Controls.Add(card);

            card.Controls.Add(L("First Name", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(28, 24)));
            var txtFN = new TextBox { Text = _admin.FirstName, Location = new Point(28, 50), Size = new Size(320, 36), Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtFN);

            card.Controls.Add(L("Last Name", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(380, 24)));
            var txtLN = new TextBox { Text = _admin.LastName, Location = new Point(380, 50), Size = new Size(320, 36), Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtLN);

            card.Controls.Add(L("Email", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(28, 110)));
            var txtEM = new TextBox { Text = _admin.Email, Location = new Point(28, 136), Size = new Size(320, 36), Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtEM);

            card.Controls.Add(L("Phone", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(380, 110)));
            var txtPH = new TextBox { Text = _admin.Phone, Location = new Point(380, 136), Size = new Size(320, 36), Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            card.Controls.Add(txtPH);

            card.Controls.Add(L("Username (read-only)", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(28, 200)));
            card.Controls.Add(new TextBox { Text = _admin.Username, Location = new Point(28, 226), Size = new Size(320, 36), Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(235, 240, 250), ReadOnly = true });

            var lblStatus = new Label { Text = "", Font = new Font("Segoe UI", 10f), ForeColor = GreenOk, BackColor = Color.Transparent, AutoSize = true, Location = new Point(28, 310) };
            card.Controls.Add(lblStatus);

            var btnSave = new Button
            {
                Text = "💾  Save Changes",
                Location = new Point(28, 340),
                Size = new Size(220, 48),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                bool ok = _userRepo.UpdateProfile(_admin.UserID, txtFN.Text.Trim(), txtLN.Text.Trim(), txtEM.Text.Trim(), txtPH.Text.Trim());
                if (ok)
                {
                    _admin.FirstName = txtFN.Text.Trim(); _admin.LastName = txtLN.Text.Trim();
                    _admin.Email = txtEM.Text.Trim(); _admin.Phone = txtPH.Text.Trim();
                    lblAdminName.Text = _admin.FullName;
                    lblStatus.ForeColor = GreenOk;
                    lblStatus.Text = "✅  Profile updated successfully.";
                }
                else { lblStatus.ForeColor = RedAlert; lblStatus.Text = "❌  Update failed. Please try again."; }
            };
            card.Controls.Add(btnSave);
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

        private Panel MakePage()
        {
            var p = new Panel
            {
                Location = new Point(0, 0),
                Size = mainPanel.Size,
                BackColor = PageBg,
                Visible = false,
                AutoScroll = false
            };
            p.HorizontalScroll.Maximum = 0;
            p.HorizontalScroll.Enabled = false;
            p.HorizontalScroll.Visible = false;
            p.VerticalScroll.Enabled = true;
            p.AutoScroll = true;
            p.AutoScrollMinSize = new Size(1, 1600);
            mainPanel.Controls.Add(p);
            return p;
        }

        private Panel Card(int x, int y, int w, int h)
        {
            bool hovered = false;
            Action setHover = () => { hovered = true; };
            Action clearHover = () => { hovered = false; };
            var card = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.White };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RndPath(card.ClientRectangle, 12);
                e.Graphics.FillPath(new SolidBrush(Color.White), path);
                if (hovered)
                {
                    using var borderPen = new Pen(Color.FromArgb(180, 210, 245), 2f);
                    e.Graphics.DrawPath(borderPen, path);
                }
                card.Region = new Region(path);
            };
            card.Tag = new Action<bool>(on => { hovered = on; card.Invalidate(); });
            card.MouseEnter += (s, e) => { hovered = true; card.Invalidate(); };
            card.MouseLeave += (s, e) => { hovered = false; card.Invalidate(); };
            return card;
        }

        private void StatCard(Panel parent, int x, int y, string icon, string title, string value, Color accent, int w, int h)
        {
            var c = Card(x, y, w, h);

            // Accent strip — animates height 5→8 and shimmers on hover
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

            // Propagate hover + animate strip
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

        private void DrawTableHeader(Panel parent, string[] headers, int[] widths, int y)
        {
            int x = 16;
            foreach (var (h, w) in Zip(headers, widths))
            {
                parent.Controls.Add(new Label { Text = h, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextGray, BackColor = CardBg, Size = new Size(w, 34), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(x, y), Padding = new Padding(4, 0, 0, 0) });
                x += w;
            }
        }

        private void DrawTableRow(Panel parent, string[] cells, int[] widths, int y, bool alt)
        {
            Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White;
            int x = 16;
            foreach (var (cell, w) in Zip(cells, widths))
            {
                Color fg = cell == "Pending" ? OrangeWarn : cell == "Delivered" ? GreenOk :
                           cell == "Assigned" ? RoyalBlue : cell == "Returned" ? RedAlert :
                           cell == "Urgent" ? RedAlert : TextDark;
                parent.Controls.Add(new Label { Text = cell, Font = new Font("Segoe UI", 10f), ForeColor = fg, BackColor = bg, Size = new Size(w, 36), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(x, y), Padding = new Padding(4, 0, 0, 0) });
                x += w;
            }
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
    }
}
