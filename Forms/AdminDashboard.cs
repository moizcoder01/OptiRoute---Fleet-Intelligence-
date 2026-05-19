// =============================================================
//  OptiRoute  |  Forms/AdminDashboardForm.cs
//
//  PAGES:
//    Page 1 — Dashboard Overview   (stat cards + recent orders)
//    Page 2 — Orders Management    (all orders, status filter)
//    Page 3 — Assign & Route       (pending orders + assign driver)
//    Page 4 — Drivers & Vehicles   (all drivers, fuel, rating)
//    Page 5 — Reports & Revenue    (financial summary)
//    Page 6 — My Profile           (admin account info)
//
//  ARCHITECTURE : Zero SQL in this file.
//                 All data via UserRepository + OrderRepository.
//  THEME        : Identical to CustomerDashboard — same sidebar,
//                 cards, colours, fonts. Admin accent = Purple.
//  NAMESPACE    : OptiRoute.Forms
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OptiRoute.Core.Data;
using OptiRoute.Core.Models;

namespace OptiRoute.Forms
{
    public class AdminDashboardForm : Form
    {
        // ── Repositories ──────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();
        private readonly OrderRepository _orderRepo = new OrderRepository();

        // ── Loaded admin model ────────────────────────────────────
        private Admin _admin = null!;

        // ── Panels ────────────────────────────────────────────────
        private Panel sidePanel = null!;
        private Panel mainPanel = null!;
        private Panel headerPanel = null!;

        // ── Sidebar buttons ───────────────────────────────────────
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
            catch
            {
                _admin = new Admin { Username = username };
            }

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
                AutoScroll = true
            };
            Controls.Add(mainPanel);

            BuildDashboardPanel();
            BuildOrdersPanel();
            BuildAssignPanel();
            BuildDriversPanel();
            BuildReportsPanel();
            BuildProfilePanel();

            Resize += (s, e) =>
            {
                sidePanel.Size = new Size(232, ClientSize.Height);
                headerPanel.Size = new Size(ClientSize.Width - 232, 62);
                mainPanel.Size = new Size(ClientSize.Width - 232, ClientSize.Height - 62);
                // Resize every page panel to match mainPanel so content fills the screen
                foreach (Control c in mainPanel.Controls)
                    if (c is Panel pg) pg.Size = mainPanel.Size;
                RefreshDashboard();
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
                    logo.ClientRectangle, RoyalBlue, Navy,
                    LinearGradientMode.Horizontal);
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
                BackColor = PurpleAccent
            };
            picAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                picAvatar.Region = new Region(RndPath(picAvatar.ClientRectangle, 36));
                e.Graphics.FillEllipse(new SolidBrush(PurpleAccent), picAvatar.ClientRectangle);
                string initial = _admin.Username.Length > 0
                    ? _admin.Username[0].ToString().ToUpper() : "A";
                e.Graphics.DrawString(initial,
                    new Font("Segoe UI", 26, FontStyle.Bold),
                    Brushes.White,
                    new RectangleF(0, 0, 72, 72), Centre());
            };
            sidePanel.Controls.Add(picAvatar);

            lblAdminName = new Label
            {
                Text = _admin.FullName,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(232, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 165)
            };
            sidePanel.Controls.Add(lblAdminName);

            lblAdminRole = new Label
            {
                Text = _admin.GetDisplayLabel(),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = SkyBlue,
                BackColor = Color.Transparent,
                Size = new Size(232, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 188)
            };
            sidePanel.Controls.Add(lblAdminRole);

            sidePanel.Controls.Add(new Panel
            {
                Location = new Point(20, 216),
                Size = new Size(192, 1),
                BackColor = Color.FromArgb(50, 255, 255, 255)
            });

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
            b.MouseEnter += (s, e) => { if (b != activeBtn) b.BackColor = Color.FromArgb(20, 255, 255, 255); };
            b.MouseLeave += (s, e) => { if (b != activeBtn) b.BackColor = Color.Transparent; };
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
                Text = "Admin Control Panel",
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
        //  PAGE 1 — DASHBOARD OVERVIEW
        // ═════════════════════════════════════════════════════════
        private void BuildDashboardPanel()
        {
            pnlDashboard = MakePage();
        }

        private void RefreshDashboard()
        {
            // Clear all except the page panel itself — remove old content
            pnlDashboard.Controls.Clear();

            int W = mainPanel.Width; // real width after maximized

            // Welcome banner
            var banner = new Panel
            {
                Location = new Point(30, 22),
                Size = new Size(W - 60, 96),
                BackColor = Color.Transparent
            };
            banner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(banner.ClientRectangle,
                    PurpleAccent, RoyalBlue, LinearGradientMode.Horizontal);
                using var path = RndPath(banner.ClientRectangle, 16);
                e.Graphics.FillPath(br, path);
                banner.Region = new Region(path);
            };
            banner.Controls.Add(L("⚙️  Welcome, " + _admin.FullName + "  |  Admin Control Panel",
                new Font("Segoe UI", 17, FontStyle.Bold), Color.White, new Point(28, 16)));
            banner.Controls.Add(L("Manage orders, assign drivers and monitor fleet performance.",
                new Font("Segoe UI", 11f), Color.FromArgb(200, 235, 255), new Point(28, 58)));
            pnlDashboard.Controls.Add(banner);

            // Stat cards
            var rev = _orderRepo.GetRevenueSummary();
            var pending = _orderRepo.GetAllPending();
            var allDrivers = _userRepo.GetAvailableDrivers();

            int gap = 16;
            int cardW = (W - 60 - 3 * gap) / 4, cardH = 130, cardTop = 138;
            StatCard(pnlDashboard, 30, cardTop, "📋", "Pending Orders", pending.Count.ToString(), OrangeWarn, cardW, cardH);
            StatCard(pnlDashboard, 30 + (cardW + gap), cardTop, "🚗", "Available Drivers", allDrivers.Count.ToString(), GreenOk, cardW, cardH);
            StatCard(pnlDashboard, 30 + (cardW + gap) * 2, cardTop, "✅", "Total Delivered", rev.TotalOrders.ToString(), RoyalBlue, cardW, cardH);
            StatCard(pnlDashboard, 30 + (cardW + gap) * 3, cardTop, "💰", "Total Revenue", "Rs. " + rev.TotalRevenue.ToString("N0"), GreenOk, cardW, cardH);

            // Recent pending orders table — full width, Date column fills remaining space
            int tblW = W - 60;
            var tbl = Card(30, 292, tblW, 420);
            pnlDashboard.Controls.Add(tbl);
            tbl.Controls.Add(L("📋  Pending Orders — Awaiting Assignment",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 16)));

            string[] hdrs = { "Order ID", "Customer", "Item", "Weight", "Priority",
                               "Pick-up", "Delivery", "Fare", "Date" };
            int fixedCols = 85 + 100 + 120 + 80 + 90 + 140 + 140 + 100; // sum of first 8 cols
            int dateW = tblW - 32 - fixedCols; // tblW minus left(16)+right(16) padding minus fixed cols
            if (dateW < 90) dateW = 90;
            int[] wids = { 85, 100, 120, 80, 90, 140, 140, 100, dateW };
            DrawTableHeader(tbl, hdrs, wids, 52);

            int rowY = 92; bool alt = false;
            foreach (Order o in pending)
            {
                string[] row =
                {
                    "#" + o.OrderID,
                    "Cust #" + o.CustomerID,
                    o.ItemName,
                    o.WeightDisplay,
                    o.Priority,
                    o.PickupPoint,
                    o.DeliveryPoint,
                    o.FormattedFare,
                    o.FormattedDate
                };
                DrawTableRow(tbl, row, wids, rowY, alt);
                alt = !alt; rowY += 38;
                if (rowY > 380) break;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 2 — ALL ORDERS MANAGEMENT
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

            // Filter bar
            var filterCard = Card(30, 65, mainPanel.Width - 60, 60);
            pnlOrders.Controls.Add(filterCard);

            filterCard.Controls.Add(L("Filter by Status:",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(16, 18)));

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

            string[] hdrs = { "ID", "Item", "Weight", "Priority", "Pick-up",
                               "Delivery", "Status", "Fare", "Payment", "Date" };
            int[] wids = { 65, 110, 75, 85, 120, 120, 100, 90, 110, 100 };
            DrawTableHeader(tblCard, hdrs, wids, 16);

            // Fetch correct set based on filter
            List<Order> orders = statusFilter == "All"
                ? _orderRepo.GetAllOrders()
                : _orderRepo.GetByStatus(statusFilter);

            int rowY = 56; bool alt = false;
            foreach (Order o in orders)
            {
                string[] row =
                {
                    "#" + o.OrderID, o.ItemName, o.WeightDisplay, o.Priority,
                    o.PickupPoint,   o.DeliveryPoint, o.OrderStatus,
                    o.FormattedFare, o.PaymentStatus, o.FormattedDate
                };
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
                    if (MessageBox.Show("Mark Order #" + capturedID + " as Returned?",
                        "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _orderRepo.UpdateStatus(capturedID, "Returned");
                        RefreshOrders();
                    }
                };
                tblCard.Controls.Add(btnReturn);

                alt = !alt; rowY += 38;
                if (rowY > 560) break;
            }

            if (rowY == 56)
                tblCard.Controls.Add(L("No orders found for this filter.",
                    new Font("Segoe UI", 10.5f), TextGray, new Point(20, 60)));
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

            // ── Pending Orders card ───────────────────────────────
            var ordCard = Card(30, 65, (int)((mainPanel.Width - 60) * 0.62), 680);
            pnlAssign.Controls.Add(ordCard);
            ordCard.Controls.Add(L("📦  Pending Orders (" + pending.Count + ")",
                new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 16)));

            int oy = 52;
            if (pending.Count == 0)
                ordCard.Controls.Add(L("✅  No pending orders. All caught up!",
                    new Font("Segoe UI", 10.5f), GreenOk, new Point(20, 60)));

            int ordCardInnerW = (int)((mainPanel.Width - 60) * 0.62) - 24;
            foreach (Order o in pending)
            {
                var oCard = new Panel
                {
                    Location = new Point(12, oy),
                    Size = new Size(ordCardInnerW, 90),
                    BackColor = CardBg
                };
                oCard.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RndPath(oCard.ClientRectangle, 8);
                    e.Graphics.FillPath(new SolidBrush(CardBg), path);
                    oCard.Region = new Region(path);
                };
                bool urg = o.IsUrgent;
                oCard.Controls.Add(new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(5, 90),
                    BackColor = urg ? RedAlert : OrangeWarn
                });
                oCard.Controls.Add(L("#" + o.OrderID + "  " + o.ItemName,
                    new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(16, 8)));
                oCard.Controls.Add(L("⚖  " + o.WeightDisplay + "   |   " + (urg ? "⚡ Urgent" : "✓ Normal"),
                    new Font("Segoe UI", 9.5f), urg ? RedAlert : TextGray, new Point(16, 32)));
                oCard.Controls.Add(L("🟢 " + o.PickupPoint + "  →  🔴 " + o.DeliveryPoint,
                    new Font("Segoe UI", 9.5f), TextDark, new Point(16, 54)));
                oCard.Controls.Add(L(o.FormattedFare,
                    new Font("Segoe UI", 10f, FontStyle.Bold), GreenOk, new Point(ordCardInnerW - 110, 32)));
                ordCard.Controls.Add(oCard);
                oy += 100;
                if (oy > 620) break;
            }

            // ── Available Drivers card ────────────────────────────
            int assignSplit = 30 + (int)((mainPanel.Width - 60) * 0.62) + 14;
            int drvCardW = mainPanel.Width - assignSplit - 30;
            var drvCard = Card(assignSplit, 65, drvCardW, 680);
            pnlAssign.Controls.Add(drvCard);
            drvCard.Controls.Add(L("🚗  Available Drivers (" + drivers.Count + ")",
                new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 16)));

            int dy = 52;
            if (drivers.Count == 0)
                drvCard.Controls.Add(L("⚠️  No drivers available right now.",
                    new Font("Segoe UI", 10.5f), OrangeWarn, new Point(20, 60)));

            foreach (Driver d in drivers)
            {
                var dCard = new Panel
                {
                    Location = new Point(12, dy),
                    Size = new Size(354, 110),
                    BackColor = CardBg
                };
                dCard.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = RndPath(dCard.ClientRectangle, 8);
                    e.Graphics.FillPath(new SolidBrush(CardBg), path);
                    dCard.Region = new Region(path);
                };
                dCard.Controls.Add(new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(5, 110),
                    BackColor = GreenOk
                });
                dCard.Controls.Add(L(d.FullName,
                    new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(16, 8)));
                dCard.Controls.Add(L("🚗  " + d.PlateNumber + "  (" + d.VehicleType + ")",
                    new Font("Segoe UI", 9f), TextGray, new Point(16, 30)));
                dCard.Controls.Add(L("⛽  Fuel: " + d.CurrentFuel.ToString("N0") + " L",
                    new Font("Segoe UI", 9f), TextDark, new Point(16, 50)));
                dCard.Controls.Add(L("⭐  Rating: " + d.AverageRating.ToString("N1"),
                    new Font("Segoe UI", 9f), OrangeWarn, new Point(16, 70)));

                // Assign button
                var btnAssignOrder = new Button
                {
                    Text = "Assign Order →",
                    Location = new Point(180, 72),
                    Size = new Size(160, 30),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = RoyalBlue,
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnAssignOrder.FlatAppearance.BorderSize = 0;

                // Capture VehicleID (not DriverID) for AssignVehicle()
                int capturedVehicle = d.VehicleID;
                string capturedName = d.FullName;

                btnAssignOrder.Click += (s, e) =>
                {
                    List<Order> stillPending = _orderRepo.GetAllPending();
                    if (stillPending.Count == 0)
                    {
                        MessageBox.Show("No pending orders to assign.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var frm = new Form
                    {
                        Text = "Select Order to Assign to " + capturedName,
                        Size = new Size(420, 360),
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        BackColor = PageBg
                    };
                    var lb = new ListBox
                    {
                        Location = new Point(16, 16),
                        Size = new Size(370, 240),
                        Font = new Font("Segoe UI", 10f)
                    };
                    foreach (Order po in stillPending)
                        lb.Items.Add($"#{po.OrderID}  {po.ItemName}  ({po.WeightDisplay})  " +
                                     $"{po.PickupPoint} → {po.DeliveryPoint}");

                    var btnOk = new Button
                    {
                        Text = "Assign",
                        Location = new Point(16, 272),
                        Size = new Size(180, 40),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = RoyalBlue,
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 11, FontStyle.Bold)
                    };
                    btnOk.FlatAppearance.BorderSize = 0;
                    btnOk.Click += (bs, be) =>
                    {
                        if (lb.SelectedIndex < 0)
                        {
                            MessageBox.Show("Please select an order.", "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        Order selected = stillPending[lb.SelectedIndex];
                        // Uses AssignVehicle (VehicleID) — aligned with DB schema
                        bool ok = _orderRepo.AssignVehicle(selected.OrderID, capturedVehicle);
                        if (ok)
                        {
                            MessageBox.Show(
                                $"✅  Order #{selected.OrderID} assigned to {capturedName}.",
                                "Assigned", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            frm.Close();
                            RefreshAssign();
                        }
                        else
                        {
                            MessageBox.Show("❌  Assignment failed. Please try again.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    };

                    frm.Controls.Add(lb);
                    frm.Controls.Add(btnOk);
                    frm.ShowDialog(this);
                };

                dCard.Controls.Add(btnAssignOrder);
                drvCard.Controls.Add(dCard);
                dy += 120;
                if (dy > 620) break;
            }
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
            {
                pnlDrivers.Controls.Add(L("No drivers registered yet.",
                    new Font("Segoe UI", 12f), TextGray, new Point(40, 80)));
                return;
            }

            int cardW = (mainPanel.Width - 60 - 2 * 20) / 3, cardH = 220, gap = 20;
            int col = 0, row = 0;

            foreach (Driver d in drivers)
            {
                int cx = 30 + col * (cardW + gap);
                int cy = 70 + row * (cardH + gap);

                var card = Card(cx, cy, cardW, cardH);
                pnlDrivers.Controls.Add(card);

                Color stripColor = d.IsAvailable ? GreenOk : OrangeWarn;
                card.Controls.Add(new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(cardW, 5),
                    BackColor = stripColor
                });

                var av = new Panel
                {
                    Location = new Point(16, 18),
                    Size = new Size(48, 48),
                    BackColor = RoyalBlue
                };
                av.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    av.Region = new Region(RndPath(av.ClientRectangle, 24));
                    e.Graphics.FillEllipse(new SolidBrush(RoyalBlue), av.ClientRectangle);
                    string ini = d.FullName.Length > 0 ? d.FullName[0].ToString().ToUpper() : "D";
                    e.Graphics.DrawString(ini, new Font("Segoe UI", 18, FontStyle.Bold),
                        Brushes.White, new RectangleF(0, 0, 48, 48), Centre());
                };
                card.Controls.Add(av);

                card.Controls.Add(L(d.FullName,
                    new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(74, 18)));
                card.Controls.Add(L("@" + d.Username,
                    new Font("Segoe UI", 9f), TextGray, new Point(74, 42)));

                card.Controls.Add(new Panel
                {
                    Location = new Point(16, 74),
                    Size = new Size(cardW - 32, 1),
                    BackColor = Color.FromArgb(220, 228, 245)
                });

                card.Controls.Add(L("🚗  " + d.PlateNumber + "  (" + d.VehicleType + ")",
                    new Font("Segoe UI", 9.5f), TextDark, new Point(16, 84)));
                card.Controls.Add(L("🪪  License: " + (string.IsNullOrEmpty(d.LicenseNumber) ? "—" : d.LicenseNumber),
                    new Font("Segoe UI", 9.5f), TextGray, new Point(16, 106)));
                card.Controls.Add(L("⛽  Fuel: " + d.CurrentFuel.ToString("N0") + " L",
                    new Font("Segoe UI", 9.5f),
                    d.CurrentFuel < 20 ? RedAlert : TextDark, new Point(16, 128)));
                card.Controls.Add(L("⭐  Rating: " + d.AverageRating.ToString("N1") + " / 5",
                    new Font("Segoe UI", 9.5f), OrangeWarn, new Point(16, 150)));

                string statusText = d.IsAvailable ? "● Available" : "● On Duty";
                card.Controls.Add(L(statusText,
                    new Font("Segoe UI", 9f, FontStyle.Bold),
                    d.IsAvailable ? GreenOk : OrangeWarn,
                    new Point(16, 178)));

                if (d.NeedsMaintenance)
                    card.Controls.Add(L("⚠️  Needs Maintenance",
                        new Font("Segoe UI", 9f, FontStyle.Bold), RedAlert, new Point(120, 178)));

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

            // Revenue summary — CodOrders matches the fixed tuple field name
            var rev = _orderRepo.GetRevenueSummary();

            int cardW = (mainPanel.Width - 60 - 3 * 16) / 4, cardH = 130, gap = 16;
            StatCard(pnlReports, 30, 70, "💰", "Total Revenue",
                "Rs. " + rev.TotalRevenue.ToString("N0"), GreenOk, cardW, cardH);
            StatCard(pnlReports, 30 + cardW + gap, 70, "✅", "Total Delivered",
                rev.TotalOrders.ToString(), RoyalBlue, cardW, cardH);
            StatCard(pnlReports, 30 + (cardW + gap) * 2, 70, "💳", "Prepaid Orders",
                rev.PaidOrders.ToString(), GreenOk, cardW, cardH);
            StatCard(pnlReports, 30 + (cardW + gap) * 3, 70, "🤝", "COD Orders",
                rev.CodOrders.ToString(), OrangeWarn, cardW, cardH);

            // Revenue breakdown card
            int repHalfW = (mainPanel.Width - 60) / 2 - 7;
            var breakdown = Card(30, 224, repHalfW, 300);
            pnlReports.Controls.Add(breakdown);
            breakdown.Controls.Add(L("💰  Revenue Breakdown",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 16)));

            DrawReportRow(breakdown, "Total Delivered Orders",
                rev.TotalOrders.ToString(), TextDark, 58);
            DrawReportRow(breakdown, "Prepaid Orders",
                rev.PaidOrders.ToString(), GreenOk, 96);
            DrawReportRow(breakdown, "COD Orders",
                rev.CodOrders.ToString(), OrangeWarn, 134);
            DrawReportRow(breakdown, "Total Revenue",
                "Rs. " + rev.TotalRevenue.ToString("N0"), GreenOk, 172);
            // COD outstanding estimate — all arithmetic is decimal * int = decimal, no type error
            decimal codEst = rev.CodOrders > 0 ? (decimal)rev.CodOrders * 150m : 0m;
            DrawReportRow(breakdown, "Outstanding COD",
                rev.CodOrders > 0 ? "Rs. " + codEst.ToString("N0") + " (est.)" : "Rs. 0",
                rev.CodOrders > 0 ? RedAlert : GreenOk, 210);

            // Driver performance card
            int repRightX = 30 + repHalfW + 14;
            int repRightW = mainPanel.Width - repRightX - 30;
            var drvPerf = Card(repRightX, 224, repRightW, 300);
            pnlReports.Controls.Add(drvPerf);
            drvPerf.Controls.Add(L("🚗  Driver Performance",
                new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 16)));

            List<Driver> drivers = _userRepo.GetAvailableDrivers();
            int dy = 58;
            if (drivers.Count == 0)
                drvPerf.Controls.Add(L("No driver data available.",
                    new Font("Segoe UI", 10f), TextGray, new Point(20, 60)));

            foreach (Driver d in drivers)
            {
                drvPerf.Controls.Add(L(d.FullName,
                    new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, dy)));
                drvPerf.Controls.Add(L(d.PlateNumber,
                    new Font("Segoe UI", 9f), TextGray, new Point(200, dy)));
                drvPerf.Controls.Add(L("⭐ " + d.AverageRating.ToString("N1"),
                    new Font("Segoe UI", 10f, FontStyle.Bold), OrangeWarn, new Point(310, dy)));
                drvPerf.Controls.Add(L("⛽ " + d.CurrentFuel.ToString("N0") + "L",
                    new Font("Segoe UI", 9f),
                    d.CurrentFuel < 20 ? RedAlert : GreenOk, new Point(390, dy)));
                dy += 38;
                if (dy > 270) break;
            }

            // Summary note
            var noteCard = Card(30, 548, mainPanel.Width - 60, 80);
            pnlReports.Controls.Add(noteCard);
            noteCard.Controls.Add(L(
                "📌  Reports are generated live from the database. Revenue includes only Delivered orders.",
                new Font("Segoe UI", 10f), TextGray, new Point(20, 28)));
        }

        private void DrawReportRow(Panel parent, string label, string value, Color valueColor, int y)
        {
            parent.Controls.Add(new Panel
            {
                Location = new Point(16, y - 4),
                Size = new Size(468, 1),
                BackColor = Color.FromArgb(220, 228, 245)
            });
            parent.Controls.Add(L(label, new Font("Segoe UI", 10f), TextGray, new Point(16, y + 4)));
            parent.Controls.Add(L(value, new Font("Segoe UI", 10f, FontStyle.Bold),
                valueColor, new Point(340, y + 4)));
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

            card.Controls.Add(L("First Name",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(28, 24)));
            var txtFN = new TextBox
            {
                Text = _admin.FirstName,
                Location = new Point(28, 50),
                Size = new Size(320, 36),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            card.Controls.Add(txtFN);

            card.Controls.Add(L("Last Name",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(380, 24)));
            var txtLN = new TextBox
            {
                Text = _admin.LastName,
                Location = new Point(380, 50),
                Size = new Size(320, 36),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            card.Controls.Add(txtLN);

            card.Controls.Add(L("Email",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(28, 110)));
            var txtEM = new TextBox
            {
                Text = _admin.Email,
                Location = new Point(28, 136),
                Size = new Size(320, 36),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            card.Controls.Add(txtEM);

            card.Controls.Add(L("Phone",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(380, 110)));
            var txtPH = new TextBox
            {
                Text = _admin.Phone,
                Location = new Point(380, 136),
                Size = new Size(320, 36),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            card.Controls.Add(txtPH);

            card.Controls.Add(L("Username (read-only)",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(28, 200)));
            card.Controls.Add(new TextBox
            {
                Text = _admin.Username,
                Location = new Point(28, 226),
                Size = new Size(320, 36),
                Font = new Font("Segoe UI", 12f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(235, 240, 250),
                ReadOnly = true
            });

            var lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10f),
                ForeColor = GreenOk,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(28, 310)
            };
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
                bool ok = _userRepo.UpdateProfile(_admin.UserID,
                    txtFN.Text.Trim(), txtLN.Text.Trim(),
                    txtEM.Text.Trim(), txtPH.Text.Trim());
                if (ok)
                {
                    _admin.FirstName = txtFN.Text.Trim();
                    _admin.LastName = txtLN.Text.Trim();
                    _admin.Email = txtEM.Text.Trim();
                    _admin.Phone = txtPH.Text.Trim();
                    lblAdminName.Text = _admin.FullName;
                    lblStatus.ForeColor = GreenOk;
                    lblStatus.Text = "✅  Profile updated successfully.";
                }
                else
                {
                    lblStatus.ForeColor = RedAlert;
                    lblStatus.Text = "❌  Update failed. Please try again.";
                }
            };
            card.Controls.Add(btnSave);
        }

        // ═════════════════════════════════════════════════════════
        //  SHARED UI HELPERS
        // ═════════════════════════════════════════════════════════
        private void SidePanel_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new LinearGradientBrush(
                sidePanel.ClientRectangle,
                Color.FromArgb(15, 40, 100),
                Color.FromArgb(5, 15, 50),
                LinearGradientMode.Vertical);
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
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.White
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RndPath(card.ClientRectangle, 12);
                e.Graphics.FillPath(new SolidBrush(Color.White), path);
                card.Region = new Region(path);
            };
            return card;
        }

        private void StatCard(Panel parent, int x, int y,
            string icon, string title, string value,
            Color accent, int w, int h)
        {
            var c = Card(x, y, w, h);
            c.Controls.Add(new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(w, 5),
                BackColor = accent
            });
            c.Controls.Add(L(icon, new Font("Segoe UI", 22), accent, new Point(16, 20)));
            c.Controls.Add(L(title, new Font("Segoe UI", 10f), TextGray, new Point(16, 62)));
            c.Controls.Add(L(value, new Font("Segoe UI", 20, FontStyle.Bold), accent, new Point(16, 82)));
            parent.Controls.Add(c);
        }

        private void DrawTableHeader(Panel parent, string[] headers, int[] widths, int y)
        {
            int x = 16;
            foreach (var (h, w) in Zip(headers, widths))
            {
                parent.Controls.Add(new Label
                {
                    Text = h,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = TextGray,
                    BackColor = CardBg,
                    Size = new Size(w, 34),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(x, y),
                    Padding = new Padding(4, 0, 0, 0)
                });
                x += w;
            }
        }

        private void DrawTableRow(Panel parent, string[] cells, int[] widths, int y, bool alt)
        {
            Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White;
            int x = 16;
            foreach (var (cell, w) in Zip(cells, widths))
            {
                Color fg =
                    cell == "Pending" ? OrangeWarn :
                    cell == "Delivered" ? GreenOk :
                    cell == "Assigned" ? RoyalBlue :
                    cell == "Returned" ? RedAlert :
                    cell == "Urgent" ? RedAlert : TextDark;

                parent.Controls.Add(new Label
                {
                    Text = cell,
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = fg,
                    BackColor = bg,
                    Size = new Size(w, 36),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(x, y),
                    Padding = new Padding(4, 0, 0, 0)
                });
                x += w;
            }
        }

        private static Label L(string text, Font font, Color color, Point loc) =>
            new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = loc
            };

        private static Label PageH(string text) =>
            new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(18, 32, 60),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(30, 22)
            };

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
            new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

        private static IEnumerable<(T1, T2)> Zip<T1, T2>(T1[] a, T2[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++) yield return (a[i], b[i]);
        }
    }
}