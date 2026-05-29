// =============================================================
//  OptiRoute  |  Forms/DashboardForm.cs   (Customer Dashboard)
//
//  STEP 6 ADDITIONS (zero theme / style changes):
//    • Track Order page now embeds a WebView2 Leaflet map.
//    • After the user tracks an order, a WinForms Timer polls
//      TelemetryRepository.GetLatest() every 5 seconds.
//    • Each poll calls ExecuteScriptAsync("movePin(lat,lng)")
//      to move the driver marker on the customer's map.
//    • Polling stops automatically when the order is Delivered
//      or the page is hidden / form is closed.
//
//  STEP 7 ADDITIONS (zero theme / style changes):
//    • Vehicle icon on the customer tracking map now changes
//      dynamically per the assigned driver's vehicle type:
//        Bike → 🏍️   Car → 🚗   Van → 🚐   Truck → 🚚
//    • GetVehicleEmoji() mirrors DriverDashboardForm logic.
//    • GetVehicleTypeForOrder() queries the DB once at track
//      time to resolve VehicleType from Table_Vehicles joined
//      via Table_Orders.VehicleID.
//    • BuildCustomerMapHtml gains a vehicleType parameter.
//
//  ARCHITECTURE : Zero SQL in this file except the one
//                 lightweight helper GetVehicleTypeForOrder().
//  THEME        : 100 % preserved.
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using BC = BCrypt.Net.BCrypt;
using Microsoft.Data.SqlClient;
using Microsoft.Web.WebView2.WinForms;
using OptiRoute.Core.Data;
using OptiRoute.Core.Models;

namespace OptiRoute.Forms
{
    public class DashboardForm : Form
    {
        // ── Repositories ──────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();
        private readonly OrderRepository _orderRepo = new OrderRepository();
        private readonly TelemetryRepository _telRepo = new TelemetryRepository();

        // ── Customer model ────────────────────────────────────────
        private Customer _customer = null!;

        // ── Profile photo ─────────────────────────────────────────
        private string _profilePhotoPath = "";

        // ── Layout panels ─────────────────────────────────────────
        private Panel sidePanel = null!;
        private Panel mainPanel = null!;
        private Panel headerPanel = null!;

        // ── Sidebar controls ──────────────────────────────────────
        private Panel picAvatar = null!;
        private Label lblCustomerName = null!;
        private Label lblCustomerRole = null!;
        private Button btnDashboard = null!;
        private Button btnMyOrders = null!;
        private Button btnTrackOrder = null!;
        private Button btnPlaceOrder = null!;
        private Button btnProfile = null!;
        private Button btnLogout = null!;
        private Button activeBtn = null!;

        // ── Content panels ────────────────────────────────────────
        private Panel pnlDashboard = null!;
        private Panel pnlMyOrders = null!;
        private Panel pnlTrackOrder = null!;
        private Panel pnlPlaceOrder = null!;
        private Panel pnlProfile = null!;

        // ── Track page live-tracking state ────────────────────────
        private WebView2? _trackMapView = null;
        private bool _trackMapReady = false;
        private System.Windows.Forms.Timer? _pollTimer = null;
        private int _trackedOrderId = 0;
        private int _lastTelTID = -1;   // avoid redundant JS calls
        private string _trackedVehicleType = "Truck"; // dynamic per order

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

        // ─────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────────────────
        public DashboardForm(string userRole, string userName = "Customer")
        {
            try
            {
                _customer = _userRepo.LoadCustomer(userName)
                            ?? new Customer { Username = userName };
            }
            catch
            {
                _customer = new Customer { Username = userName };
            }

            _profilePhotoPath = _customer.ProfilePhotoPath ?? "";

            Text = "OptiRoute  |  Customer Dashboard";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(1100, 650);
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 9f);

            FormClosing += (s, e) => StopPollTimer();

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
                Size = new Size(188, ClientSize.Height),
                BackColor = SidebarBg
            };
            sidePanel.Paint += SidePanel_Paint;
            Controls.Add(sidePanel);
            BuildSidebar();

            headerPanel = new Panel
            {
                Location = new Point(188, 0),
                Size = new Size(ClientSize.Width - 188, 56),
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
                Location = new Point(188, 56),
                Size = new Size(ClientSize.Width - 188, ClientSize.Height - 56),
                BackColor = PageBg,
                AutoScroll = false
            };
            Controls.Add(mainPanel);

            BuildDashboardPanel();
            BuildMyOrdersPanel();
            BuildTrackOrderPanel();
            BuildPlaceOrderPanel();
            BuildProfilePanel();

            Resize += (s, e) =>
            {
                sidePanel.Size = new Size(188, ClientSize.Height);
                headerPanel.Size = new Size(ClientSize.Width - 188, 56);
                mainPanel.Size = new Size(ClientSize.Width - 188, ClientSize.Height - 56);
                foreach (Control c in mainPanel.Controls)
                    if (c is Panel pg) pg.Size = mainPanel.Size;
                sidePanel.Invalidate();
                headerPanel.Invalidate();
            };
        }

        // ═════════════════════════════════════════════════════════
        //  SIDEBAR
        // ═════════════════════════════════════════════════════════
        private void BuildSidebar()
        {
            var logo = new Panel { Location = new Point(0, 0), Size = new Size(188, 56), BackColor = Color.Transparent };
            logo.Paint += (s, e) =>
            {
                using var br = new LinearGradientBrush(logo.ClientRectangle, RoyalBlue, Navy, LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(br, logo.ClientRectangle);
            };
            logo.Controls.Add(new Label { Text = "⬡  OptiRoute", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Size = new Size(188, 56), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 0) });
            sidePanel.Controls.Add(logo);

            picAvatar = new Panel { Size = new Size(60, 60), Location = new Point(64, 68), BackColor = RoyalBlue };
            picAvatar.Paint += AvatarPaint;
            sidePanel.Controls.Add(picAvatar);

            lblCustomerName = new Label { Text = _customer.FullName, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Size = new Size(188, 20), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 136) };
            sidePanel.Controls.Add(lblCustomerName);

            lblCustomerRole = new Label { Text = "●  " + _customer.Role, Font = new Font("Segoe UI", 8f), ForeColor = SkyBlue, BackColor = Color.Transparent, Size = new Size(188, 16), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 158) };
            sidePanel.Controls.Add(lblCustomerRole);

            sidePanel.Controls.Add(new Panel { Location = new Point(14, 182), Size = new Size(160, 1), BackColor = Color.FromArgb(50, 255, 255, 255) });

            btnDashboard = SideBtn("🏠  Dashboard", 196);
            btnMyOrders = SideBtn("📦  My Orders", 238);
            btnTrackOrder = SideBtn("📍  Track Order", 280);
            btnPlaceOrder = SideBtn("➕  Place Order", 322);
            btnProfile = SideBtn("👤  My Profile", 364);

            btnDashboard.Click += (s, e) => { RefreshDashboard(); ShowPanel(pnlDashboard, btnDashboard); };
            btnMyOrders.Click += (s, e) => { RefreshMyOrders(); ShowPanel(pnlMyOrders, btnMyOrders); };
            btnTrackOrder.Click += (s, e) => { StopPollTimer(); ShowPanel(pnlTrackOrder, btnTrackOrder); };
            btnPlaceOrder.Click += (s, e) => ShowPanel(pnlPlaceOrder, btnPlaceOrder);
            btnProfile.Click += (s, e) => ShowPanel(pnlProfile, btnProfile);

            btnLogout = new Button { Text = "⏻  Logout", Location = new Point(10, 420), Size = new Size(168, 38), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(255, 100, 100), BackColor = Color.FromArgb(30, 255, 80, 80), Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) =>
            {
                if (MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { StopPollTimer(); new LoginForm().Show(); Close(); }
            };
            sidePanel.Controls.Add(btnLogout);

            sidePanel.Controls.Add(new Label { Text = "OptiRoute v1.0", Font = new Font("Segoe UI", 7f), ForeColor = Color.FromArgb(60, 255, 255, 255), BackColor = Color.Transparent, Size = new Size(188, 16), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 470) });
        }

        private void AvatarPaint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            picAvatar.Region = new Region(RndPath(picAvatar.ClientRectangle, 30));
            if (!string.IsNullOrEmpty(_profilePhotoPath) && File.Exists(_profilePhotoPath))
            {
                try { using var img = Image.FromFile(_profilePhotoPath); e.Graphics.DrawImage(img, picAvatar.ClientRectangle); return; } catch { }
            }
            e.Graphics.FillEllipse(new SolidBrush(RoyalBlue), picAvatar.ClientRectangle);
            string initial = _customer.Username.Length > 0 ? _customer.Username[0].ToString().ToUpper() : "C";
            e.Graphics.DrawString(initial, new Font("Segoe UI", 22, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 60, 60), Centre());
        }

        private Button SideBtn(string text, int top)
        {
            var b = new Button { Text = text, Location = new Point(10, top), Size = new Size(168, 36), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(180, 220, 255), BackColor = Color.Transparent, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.Transparent;
            b.MouseEnter += (s, e) => { if (b != activeBtn) { b.BackColor = Color.FromArgb(0, 163, 255); b.ForeColor = Color.White; } };
            b.MouseLeave += (s, e) => { if (b != activeBtn) { b.BackColor = Color.Transparent; b.ForeColor = Color.FromArgb(180, 220, 255); } };
            sidePanel.Controls.Add(b);
            return b;
        }

        private void ShowPanel(Panel panel, Button btn)
        {
            // Stop polling when leaving Track page
            if (panel != pnlTrackOrder) StopPollTimer();

            foreach (Control c in mainPanel.Controls)
                if (c is Panel p) p.Visible = false;
            foreach (Control c in sidePanel.Controls)
                if (c is Button b && b != btnLogout)
                { b.BackColor = Color.Transparent; b.ForeColor = Color.FromArgb(180, 220, 255); b.FlatAppearance.MouseOverBackColor = Color.Transparent; }
            panel.Visible = true;
            btn.BackColor = RoyalBlue;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 220);
            activeBtn = btn;
        }

        // ═════════════════════════════════════════════════════════
        //  HEADER
        // ═════════════════════════════════════════════════════════
        private void BuildHeader()
        {
            headerPanel.Controls.Add(new Label { Text = "Customer Dashboard", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(20, 6), BackColor = Color.White });
            var lblDate = new Label { Text = "📅  " + DateTime.Now.ToString("dddd, dd MMMM yyyy"), Font = new Font("Segoe UI", 9f), ForeColor = TextGray, AutoSize = true, BackColor = Color.White };
            lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 20, 18);
            headerPanel.Controls.Add(lblDate);
            headerPanel.Resize += (s, e) => lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 20, 18);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 1 — DASHBOARD
        // ═════════════════════════════════════════════════════════
        private void BuildDashboardPanel() { pnlDashboard = MakePage(); }

        private void RefreshDashboard()
        {
            pnlDashboard.Size = mainPanel.Size;
            pnlDashboard.Controls.Clear();

            int W = mainPanel.Width;

            var banner = new Panel { Location = new Point(30, 22), Size = new Size(W - 60, 92), BackColor = Color.Transparent };
            banner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(banner.ClientRectangle, RoyalBlue, SkyBlue, LinearGradientMode.Horizontal);
                using var path = RndPath(banner.ClientRectangle, 14);
                e.Graphics.FillPath(br, path);
                banner.Region = new Region(path);
            };
            banner.Controls.Add(L("👋  Welcome back, " + _customer.FullName + "!", new Font("Segoe UI", 15, FontStyle.Bold), Color.White, new Point(28, 16)));
            banner.Controls.Add(L("Track your deliveries and manage orders from here.", new Font("Segoe UI", 10f), Color.FromArgb(200, 235, 255), new Point(28, 52)));
            pnlDashboard.Controls.Add(banner);

            var summary = _orderRepo.GetSummary(_customer.UserID);
            int gap = 16, cardH = 130, cardTop = 134;
            int cardW = (W - 60 - 3 * gap) / 4;
            BuildStatCard(pnlDashboard, 30, cardTop, "📦", "Total Orders", summary.Total.ToString(), RoyalBlue, cardW, cardH);
            BuildStatCard(pnlDashboard, 30 + (cardW + gap), cardTop, "🚚", "In Transit", summary.InTransit.ToString(), OrangeWarn, cardW, cardH);
            BuildStatCard(pnlDashboard, 30 + (cardW + gap) * 2, cardTop, "✅", "Delivered", summary.Delivered.ToString(), GreenOk, cardW, cardH);
            BuildStatCard(pnlDashboard, 30 + (cardW + gap) * 3, cardTop, "⚡", "Urgent", summary.Urgent.ToString(), RedAlert, cardW, cardH);

            int tblTop = cardTop + cardH + gap;
            int tblH = Math.Max(220, pnlDashboard.Height - tblTop - 20);
            int tblW = W - 60;
            var tbl = Card(30, tblTop, tblW, tblH);
            pnlDashboard.Controls.Add(tbl);
            tbl.Controls.Add(L("📋  Recent Orders", new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 16)));
            tbl.Controls.Add(L("📦  All Recent Orders", new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(20, 46)));

            string[] hdrs = { "Order ID", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Status", "Date" };
            int fixedCols = 80 + 130 + 75 + 90 + 140 + 150 + 110;
            int dateW = Math.Max(90, tblW - 32 - fixedCols);
            int[] wids = { 80, 130, 75, 90, 140, 150, 110, dateW };

            int hx = 16;
            foreach (var (h, w) in Zip(hdrs, wids))
            {
                tbl.Controls.Add(new Label { Text = h, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextGray, BackColor = CardBg, Size = new Size(w, 34), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(hx, 68), Padding = new Padding(4, 0, 0, 0) });
                hx += w;
            }

            int rowY = 106; bool alt = false;
            List<Order> recent = _orderRepo.GetRecentByCustomer(_customer.UserID, 8);
            if (recent.Count == 0) tbl.Controls.Add(L("No orders yet — place your first order!", new Font("Segoe UI", 10.5f), TextGray, new Point(20, 110)));

            foreach (Order o in recent)
            {
                string[] row = { "#" + o.OrderID, o.ItemName, o.WeightDisplay, o.Priority, o.PickupPoint, o.DeliveryPoint, o.OrderStatus, o.FormattedDate };
                Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White; alt = !alt;
                int rx = 16;
                foreach (var (cell, w) in Zip(row, wids))
                {
                    Color fg = cell == "Picked" ? OrangeWarn : cell == "Delivered" ? GreenOk : cell == "Assigned" ? RoyalBlue : cell == "Urgent" ? RedAlert : cell == "Pending" ? OrangeWarn : TextDark;
                    tbl.Controls.Add(new Label { Text = cell, Font = new Font("Segoe UI", 10f), ForeColor = fg, BackColor = bg, Size = new Size(w, 36), TextAlign = ContentAlignment.MiddleLeft, Location = new Point(rx, rowY), Padding = new Padding(4, 0, 0, 0) });
                    rx += w;
                }
                rowY += 38;
                if (rowY > tblH - 20) break;
            }
        }

        private void BuildStatCard(Panel parent, int x, int y, string icon, string title, string value, Color accent, int w, int h)
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

        // ═════════════════════════════════════════════════════════
        //  PAGE 2 — MY ORDERS
        // ═════════════════════════════════════════════════════════
        private void BuildMyOrdersPanel()
        {
            pnlMyOrders = MakePage();
            pnlMyOrders.AutoScroll = true;
            LoadOrderCards();
        }

        private void RefreshMyOrders()
        {
            pnlMyOrders.Controls.Clear();
            pnlMyOrders.AutoScroll = true;
            LoadOrderCards();
        }

        private void LoadOrderCards()
        {
            pnlMyOrders.Controls.Add(PageH("📦  My Orders"));
            List<Order> orders = _orderRepo.GetByCustomer(_customer.UserID);

            if (orders.Count == 0)
            {
                pnlMyOrders.Controls.Add(L("You have no orders yet.", new Font("Segoe UI", 11f), TextGray, new Point(30, 70)));
                pnlMyOrders.Controls.Add(L("Click '➕ Place Order' in the sidebar to get started.", new Font("Segoe UI", 9.5f), TextGray, new Point(30, 98)));
                return;
            }

            int cardH = 210, cardW = Math.Max(860, mainPanel.Width - 60), cardY = 58, cardGap = 14;

            foreach (Order o in orders)
            {
                Color sc = o.OrderStatus == "Delivered" ? GreenOk : o.OrderStatus == "Picked" ? OrangeWarn : o.OrderStatus == "Assigned" ? RoyalBlue : TextGray;
                var card = Card(30, cardY, cardW, cardH);

                // Accent strip with hover brighten
                bool stripHov = false;
                var strip = new Panel { Location = new Point(0, 0), Size = new Size(cardW, 5), BackColor = sc };
                strip.Paint += (s, e) =>
                {
                    if (!stripHov) return;
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 255, 255, 255)), strip.ClientRectangle);
                };
                card.Controls.Add(strip);

                Action<bool>? trigger = card.Tag as Action<bool>;
                Action<bool> hoverAll = (on) =>
                {
                    trigger?.Invoke(on);
                    stripHov = on;
                    strip.Size = new Size(cardW, on ? 8 : 5);
                    strip.Invalidate();
                };
                card.MouseEnter += (s, e) => hoverAll(true);
                card.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) hoverAll(false); };

                pnlMyOrders.Controls.Add(card);

                Color badgeBg = o.OrderStatus == "Delivered" ? Color.FromArgb(30, 34, 197, 94) : o.OrderStatus == "Picked" ? Color.FromArgb(30, 251, 146, 60) : o.OrderStatus == "Assigned" ? Color.FromArgb(30, 0, 82, 204) : Color.FromArgb(30, 120, 140, 170);
                string statusIcon = o.OrderStatus == "Delivered" ? "✅  Delivered" : o.OrderStatus == "Picked" ? "🚚  In Transit" : o.OrderStatus == "Assigned" ? "🔵  Assigned" : "⏳  Pending";
                var badge = new Panel { Location = new Point(cardW - 160, 14), Size = new Size(144, 30), BackColor = badgeBg };
                badge.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; badge.Region = new Region(RndPath(badge.ClientRectangle, 8)); };
                badge.Controls.Add(new Label { Text = statusIcon, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = sc, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                card.Controls.Add(badge);

                card.Controls.Add(L("#" + o.OrderID, new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
                card.Controls.Add(L("📅  " + o.FormattedDate, new Font("Segoe UI", 9.5f), TextGray, new Point(20, 44)));
                card.Controls.Add(L("📦  " + o.ItemName + "   ⚖  " + o.WeightDisplay, new Font("Segoe UI", 9.5f), TextDark, new Point(20, 68)));
                card.Controls.Add(L("💰  " + o.FormattedFare + "   💳  " + o.PaymentStatus, new Font("Segoe UI", 9.5f), TextGray, new Point(20, 94)));
                card.Controls.Add(L("🟢  " + o.PickupPoint, new Font("Segoe UI", 9.5f), TextDark, new Point(380, 44)));
                card.Controls.Add(L("🔴  " + o.DeliveryPoint, new Font("Segoe UI", 9.5f), TextDark, new Point(380, 68)));

                var pBadge = new Panel { Location = new Point(20, 124), Size = new Size(96, 26), BackColor = o.IsUrgent ? Color.FromArgb(30, 239, 68, 68) : Color.FromArgb(30, 34, 197, 94) };
                pBadge.Controls.Add(new Label { Text = o.IsUrgent ? "⚡ Urgent" : "✓ Normal", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = o.IsUrgent ? RedAlert : GreenOk, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                card.Controls.Add(pBadge);

                card.Controls.Add(L("Rate:", new Font("Segoe UI", 8.5f, FontStyle.Bold), TextGray, new Point(380, 108)));
                int savedRating = o.Rating;
                var stars = new Button[5];
                for (int si = 0; si < 5; si++)
                {
                    stars[si] = new Button { Text = savedRating >= si + 1 ? "★" : "☆", Location = new Point(424 + si * 28, 98), Size = new Size(28, 34), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 13), ForeColor = savedRating >= si + 1 ? Color.Gold : TextGray, BackColor = Color.Transparent, Cursor = Cursors.Hand };
                    stars[si].FlatAppearance.BorderSize = 0;
                    if (!o.IsDelivered) { stars[si].Enabled = false; stars[si].ForeColor = Color.FromArgb(200, 210, 230); }
                    else
                    {
                        int captured = si, capturedOid = o.OrderID;
                        stars[si].Click += (s, e) =>
                        {
                            int chosen = captured + 1;
                            _orderRepo.SaveRating(capturedOid, _customer.UserID, chosen);
                            for (int k = 0; k < 5; k++) { stars[k].Text = k < chosen ? "★" : "☆"; stars[k].ForeColor = k < chosen ? Color.Gold : TextGray; }
                            MessageBox.Show("⭐ Thank you! Rated " + chosen + "/5.", "Rating Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        };
                    }
                    card.Controls.Add(stars[si]);
                    stars[si].MouseEnter += (s, e) => hoverAll(true);
                    stars[si].MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) hoverAll(false); };
                }
                card.Controls.Add(L(!o.IsDelivered ? "(After delivery)" : savedRating > 0 ? savedRating + "/5" : "Tap to rate", new Font("Segoe UI", 7.5f), TextGray, new Point(380, 142)));

                var btnCancel = new Button { Text = "🗑  Cancel", Location = new Point(cardW - 160, 54), Size = new Size(144, 36), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), BackColor = o.IsCancellable ? Color.FromArgb(30, 239, 68, 68) : Color.FromArgb(220, 225, 235), ForeColor = o.IsCancellable ? RedAlert : TextGray, Cursor = o.IsCancellable ? Cursors.Hand : Cursors.Default };
                btnCancel.FlatAppearance.BorderSize = 0;
                int capturedId = o.OrderID; bool canCancel = o.IsCancellable; string currentStatus = o.OrderStatus;
                btnCancel.Click += (s, e) =>
                {
                    if (!canCancel) { MessageBox.Show("Cancellation only allowed for Pending orders.\nCurrent: " + currentStatus, "Cannot Cancel", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (MessageBox.Show("Cancel Order #" + capturedId + "?\nThis cannot be undone.", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = _orderRepo.CancelOrder(capturedId, _customer.UserID);
                        if (ok) RefreshMyOrders();
                        else MessageBox.Show("Could not cancel.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                btnCancel.MouseEnter += (s, e) => hoverAll(true);
                btnCancel.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) hoverAll(false); };
                card.Controls.Add(btnCancel);

                // Propagate hover to all remaining children
                foreach (Control ch in card.Controls)
                {
                    ch.MouseEnter += (s, e) => hoverAll(true);
                    ch.MouseLeave += (s, e) => { var pos = card.PointToClient(Control.MousePosition); if (!card.ClientRectangle.Contains(pos)) hoverAll(false); };
                }

                cardY += cardH + cardGap;
            }
            pnlMyOrders.AutoScrollMinSize = new Size(0, cardY + 20);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 3 — TRACK ORDER  (WebView2 Leaflet + live polling)
        // ═════════════════════════════════════════════════════════
        private void BuildTrackOrderPanel()
        {
            pnlTrackOrder = MakePage();
            pnlTrackOrder.Controls.Add(PageH("📍  Track Order"));

            // ── Search card ───────────────────────────────────────
            var sc = Card(20, 56, 780, 66);
            pnlTrackOrder.Controls.Add(sc);
            sc.Controls.Add(L("Enter Order ID:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextDark, new Point(14, 8)));

            var txtId = new TextBox { Location = new Point(14, 30), Size = new Size(560, 28), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11f), PlaceholderText = "e.g.  1001", BackColor = CardBg };
            sc.Controls.Add(txtId);

            var btnT = new Button { Text = "🔍  Track", Location = new Point(584, 28), Size = new Size(110, 30), BackColor = RoyalBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnT.FlatAppearance.BorderSize = 0;
            sc.Controls.Add(btnT);

            // ── Status panel (left, text-based progress) ──────────
            int splitX = 20;
            int statusW = 380;
            int mapLeft = splitX + statusW + 14;
            int mapW = pnlTrackOrder.Width - mapLeft - 20;
            if (mapW < 300) mapW = 300;
            int topY = 134;
            int contentH = pnlTrackOrder.Height - topY - 14;
            if (contentH < 300) contentH = 300;

            var statusCard = Card(splitX, topY, statusW, contentH);
            pnlTrackOrder.Controls.Add(statusCard);
            statusCard.Controls.Add(L("Enter an Order ID above and click Track.", new Font("Segoe UI", 10f), TextGray, new Point(16, 16)));

            // ── Map panel (right, WebView2) ────────────────────────
            var mapPanel = new Panel { Location = new Point(mapLeft, topY), Size = new Size(mapW, contentH), BackColor = Color.FromArgb(230, 235, 245) };
            pnlTrackOrder.Controls.Add(mapPanel);

            var lblMapHint = new Label { Text = "🗺️  Map will appear after tracking.", Font = new Font("Segoe UI", 11f), ForeColor = TextGray, BackColor = Color.Transparent, AutoSize = true };
            lblMapHint.Location = new Point((mapW - lblMapHint.PreferredWidth) / 2, (contentH - 22) / 2);
            mapPanel.Controls.Add(lblMapHint);

            // Keep panels filling available space on resize
            pnlTrackOrder.SizeChanged += (s, e) =>
            {
                int newMapW = pnlTrackOrder.Width - mapLeft - 20;
                if (newMapW < 300) newMapW = 300;
                int newH = pnlTrackOrder.Height - topY - 14;
                if (newH < 300) newH = 300;
                statusCard.Size = new Size(statusW, newH);
                mapPanel.Size = new Size(newMapW, newH);
                if (_trackMapView != null && !_trackMapView.IsDisposed) _trackMapView.Size = mapPanel.Size;
            };

            // ── Track button click ────────────────────────────────
            btnT.Click += async (s, e) =>
            {
                StopPollTimer();
                _trackMapView = null;
                _trackMapReady = false;
                mapPanel.Controls.Clear();
                statusCard.Controls.Clear();

                if (!int.TryParse(txtId.Text.Replace("#", "").Trim(), out int oid))
                {
                    statusCard.Controls.Add(L("⚠️  Please enter a valid numeric Order ID.", new Font("Segoe UI", 10f), RedAlert, new Point(16, 16)));
                    return;
                }

                Order? order = _orderRepo.GetByID(oid, _customer.UserID);
                if (order == null)
                {
                    statusCard.Controls.Add(L("❌  No order found with ID #" + oid, new Font("Segoe UI", 10f), RedAlert, new Point(16, 16)));
                    return;
                }

                _trackedOrderId = oid;
                _lastTelTID = -1;
                _trackedVehicleType = GetVehicleTypeForOrder(oid);

                // ── Draw text-based status in left panel ──────────
                statusCard.Controls.Add(L("#" + order.OrderID + "  " + order.ItemName, new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(16, 12)));
                statusCard.Controls.Add(L("📍 " + order.PickupPoint + " → " + order.DeliveryPoint, new Font("Segoe UI", 9.5f), TextGray, new Point(16, 38)));

                string[] titles = { "Order Placed", "Order Assigned", "Picked Up", "In Transit", "Delivered" };
                string[] subs =
                {
                    "Your order was placed successfully.",
                    "A driver has been assigned.",
                    "Package collected from "+order.PickupPoint+".",
                    "Package is on the way to "+order.DeliveryPoint+".",
                    "Package delivered successfully."
                };
                int done = order.OrderStatus == "Pending" ? 1 :
                           order.OrderStatus == "Assigned" ? 2 :
                           order.OrderStatus == "Picked" ? 3 :
                           order.OrderStatus == "Delivered" ? 5 : 1;

                int sy = 72;
                for (int i = 0; i < titles.Length; i++)
                {
                    bool d = i < done;
                    var circle = new Panel { Location = new Point(16, sy), Size = new Size(26, 26), BackColor = d ? GreenOk : Color.FromArgb(200, 210, 230) };
                    circle.Paint += (cs, ce) => { ce.Graphics.SmoothingMode = SmoothingMode.AntiAlias; circle.Region = new Region(RndPath(circle.ClientRectangle, 13)); };
                    statusCard.Controls.Add(circle);
                    if (i < titles.Length - 1) statusCard.Controls.Add(new Panel { Location = new Point(28, sy + 26), Size = new Size(2, 22), BackColor = Color.FromArgb(200, 210, 230) });
                    statusCard.Controls.Add(L(titles[i], new Font("Segoe UI", 9.5f, FontStyle.Bold), d ? TextDark : TextGray, new Point(54, sy)));
                    statusCard.Controls.Add(L(subs[i], new Font("Segoe UI", 8.5f), TextGray, new Point(54, sy + 16)));
                    sy += 50;
                }

                // Live tracking hint
                var liveLabel = new Label { Text = "", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = OrangeWarn, BackColor = Color.Transparent, AutoSize = true, Location = new Point(16, sy + 10) };
                statusCard.Controls.Add(liveLabel);

                if (order.OrderStatus == "Picked")
                    liveLabel.Text = "🔴  LIVE  — Driver location updating every 5 s";
                else if (order.OrderStatus == "Delivered")
                    liveLabel.Text = "✅  Delivery complete.";
                else
                    liveLabel.Text = "ℹ️  Live tracking starts once driver picks up.";

                // ── Build Leaflet map ─────────────────────────────
                string? polylineJson = _orderRepo.GetRoutePolyline(oid);
                double distKm = _orderRepo.GetRouteDistance(oid);

                // Get latest telemetry for initial truck position
                var latestTel = _telRepo.GetLatest(oid);
                double truckLat = latestTel?.Latitude ?? 0;
                double truckLng = latestTel?.Longitude ?? 0;
                if (latestTel != null) _lastTelTID = latestTel.TID;

                var lblMapLoading = new Label { Text = "🗺️  Loading map...", Font = new Font("Segoe UI", 11f), ForeColor = TextGray, BackColor = Color.Transparent, AutoSize = true };
                lblMapLoading.Location = new Point((mapPanel.Width - lblMapLoading.PreferredWidth) / 2, (mapPanel.Height - 22) / 2);
                mapPanel.Controls.Add(lblMapLoading);

                try
                {
                    _trackMapView = new WebView2 { Location = new Point(0, 0), Size = mapPanel.Size, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
                    mapPanel.Controls.Add(_trackMapView);

                    _trackMapView.CoreWebView2InitializationCompleted += (wvs, wve) =>
                    {
                        if (!wve.IsSuccess) return;
                        _trackMapReady = true;
                        mapPanel.Controls.Remove(lblMapLoading);

                        string html = BuildCustomerMapHtml(order, polylineJson ?? "[]", distKm, truckLat, truckLng, order.PickupPoint, order.DeliveryPoint, _trackedVehicleType);
                        _trackMapView.CoreWebView2.NavigateToString(html);

                        // Start polling only for in-transit orders
                        if (order.OrderStatus == "Picked")
                            StartPollTimer();
                    };

                    await _trackMapView.EnsureCoreWebView2Async();
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
            };
        }

        // ─────────────────────────────────────────────────────────
        //  POLL TIMER — moves driver pin on customer's map every 5 s
        // ─────────────────────────────────────────────────────────
        private void StartPollTimer()
        {
            StopPollTimer();
            _pollTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        private void StopPollTimer()
        {
            if (_pollTimer == null) return;
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _pollTimer = null;
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (_trackedOrderId <= 0) { StopPollTimer(); return; }

            // Check if order is still in transit
            Order? current = _orderRepo.GetByID(_trackedOrderId, _customer.UserID);
            if (current == null || current.OrderStatus == "Delivered" || current.OrderStatus == "Returned")
            {
                StopPollTimer();
                if (_trackMapReady && _trackMapView != null && !_trackMapView.IsDisposed)
                    _ = _trackMapView.ExecuteScriptAsync("showDelivered();");
                return;
            }

            // Get latest telemetry
            var tel = _telRepo.GetLatest(_trackedOrderId);
            if (tel == null || tel.TID == _lastTelTID) return; // nothing new
            _lastTelTID = tel.TID;

            // Move pin in Leaflet
            if (_trackMapReady && _trackMapView != null && !_trackMapView.IsDisposed)
            {
                string latStr = tel.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string lngStr = tel.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string fuel = tel.FuelBurned.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                _ = _trackMapView.ExecuteScriptAsync($"movePin({latStr},{lngStr},{fuel});");
            }
        }

        // ─────────────────────────────────────────────────────────
        //  VEHICLE EMOJI — mirrors DriverDashboardForm logic
        // ─────────────────────────────────────────────────────────
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
        //  VEHICLE TYPE FOR ORDER
        //  Reads VehicleType from Table_Vehicles joined via the
        //  VehicleID stored on Table_Orders.  Returns "Truck" on
        //  any error so the map always has a valid icon.
        // ─────────────────────────────────────────────────────────
        private static string GetVehicleTypeForOrder(int orderId)
        {
            try
            {
                const string sql = @"
                    SELECT TOP 1 v.VehicleType
                    FROM   Table_Orders   o
                    INNER JOIN Table_Vehicles v ON v.VehicleID = o.VehicleID
                    WHERE  o.OrderID = @OrderID";

                using var con = new SqlConnection(DbConfig.ConnectionString);
                con.Open();
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@OrderID", orderId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return result.ToString() ?? "Truck";
            }
            catch { }
            return "Truck";
        }

        // ─────────────────────────────────────────────────────────
        //  LEAFLET HTML — customer tracking map
        // ─────────────────────────────────────────────────────────
        private static string BuildCustomerMapHtml(
            Order order,
            string polylineJson,
            double distKm,
            double truckLat, double truckLng,
            string pickupLabel, string deliveryLabel,
            string vehicleType = "Truck")
        {
            string fmt(double v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string vehicleEmoji = GetVehicleEmoji(vehicleType);

            // Parse first and last waypoints for pickup / delivery markers
            double pickLat = truckLat, pickLng = truckLng;
            double delLat = truckLat, delLng = truckLng;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(polylineJson);
                var arr = doc.RootElement;
                if (arr.GetArrayLength() >= 2)
                {
                    pickLat = arr[0][0].GetDouble(); pickLng = arr[0][1].GetDouble();
                    var last = arr[arr.GetArrayLength() - 1];
                    delLat = last[0].GetDouble(); delLng = last[1].GetDouble();
                }
            }
            catch { }

            double centLat = (pickLat + delLat) / 2.0;
            double centLng = (pickLng + delLng) / 2.0;

            // If no telemetry yet, place truck at pickup
            if (truckLat == 0 && truckLng == 0) { truckLat = pickLat; truckLng = pickLng; }

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1.0'/>
<title>Track Order</title>
<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css'/>
<script src='https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js'></script>
<style>
* {{ margin:0;padding:0;box-sizing:border-box; }}
html,body,#map {{ height:100%;width:100%; }}
.info-box {{
  position:absolute;top:10px;right:10px;z-index:1000;
  background:rgba(255,255,255,.96);border-radius:10px;
  padding:12px 16px;font-family:'Segoe UI',sans-serif;
  font-size:13px;box-shadow:0 2px 12px rgba(0,0,0,.18);min-width:200px;
}}
.info-box h3 {{ font-size:14px;margin-bottom:8px;color:#0a235a; }}
.info-row {{ display:flex;justify-content:space-between;margin-bottom:4px; }}
.info-val {{ font-weight:700;color:#0052cc; }}
.live-dot {{ display:inline-block;width:10px;height:10px;border-radius:50%;background:#ef4444;animation:blink 1s infinite; }}
@keyframes blink {{ 0%,100%{{opacity:1}}50%{{opacity:.3}} }}
</style>
</head>
<body>
<div id='map'></div>
<div class='info-box'>
  <h3>📍 Order #{order.OrderID}</h3>
  <div class='info-row'><span>📏 Distance</span><span class='info-val'>{distKm:N1} km</span></div>
  <div class='info-row'><span>📦 Pickup</span><span style='font-size:11px;max-width:120px;overflow:hidden'>{EscHtml(pickupLabel)}</span></div>
  <div class='info-row'><span>🏁 Delivery</span><span style='font-size:11px;max-width:120px;overflow:hidden'>{EscHtml(deliveryLabel)}</span></div>
  <div class='info-row' id='liveRow'><span><span class='live-dot'></span> Live</span><span class='info-val' id='liveStatus'>Tracking...</span></div>
</div>
<script>
var map = L.map('map').setView([{fmt(centLat)},{fmt(centLng)}],13);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{
  maxZoom:19,attribution:'© OpenStreetMap'
}}).addTo(map);

// Route polyline
var pts = {polylineJson};
if(pts&&pts.length>1){{
  var rt = L.polyline(pts,{{color:'#0052cc',weight:5,opacity:.8}}).addTo(map);
  map.fitBounds(rt.getBounds(),{{padding:[40,40]}});
}}

// Pickup marker (package emoji icon)
var gIcon = L.divIcon({{className:'',html:'<div style=""font-size:26px;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45))"">📦</div>',iconSize:[30,30],iconAnchor:[15,28]}});
L.marker([{fmt(pickLat)},{fmt(pickLng)}],{{icon:gIcon}}).addTo(map).bindPopup('<b>📦 Pickup</b><br>{EscHtml(pickupLabel)}');

// Delivery marker (flag emoji icon)
var rIcon = L.divIcon({{className:'',html:'<div style=""font-size:26px;line-height:1;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45))"">🏁</div>',iconSize:[30,30],iconAnchor:[4,28]}});
L.marker([{fmt(delLat)},{fmt(delLng)}],{{icon:rIcon}}).addTo(map).bindPopup('<b>🏁 Delivery</b><br>{EscHtml(deliveryLabel)}');

// Truck marker — live position
var truckIcon = L.divIcon({{className:'',html:'<div style=""font-size:28px;line-height:1"">{vehicleEmoji}</div>',iconSize:[32,32],iconAnchor:[16,16]}});
var truck = L.marker([{fmt(truckLat)},{fmt(truckLng)}],{{icon:truckIcon,zIndexOffset:1000}}).addTo(map)
             .bindPopup('<b>{vehicleEmoji} Driver</b><br>Live location');

// Called from C# every 5 seconds via ExecuteScriptAsync
function movePin(lat,lng,fuel){{
  truck.setLatLng([lat,lng]);
  map.panTo([lat,lng],{{animate:true,duration:0.8}});
  var el = document.getElementById('liveStatus');
  if(el) el.textContent = 'Updated just now';
}}

// Called when order is delivered
function showDelivered(){{
  var el = document.getElementById('liveStatus');
  if(el) {{ el.textContent='Delivered ✅'; el.style.color='#22c55e'; }}
  var dot = document.querySelector('.live-dot');
  if(dot) {{ dot.style.background='#22c55e'; dot.style.animation='none'; }}
}}
</script>
</body>
</html>";
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 4 — PLACE ORDER
        // ═════════════════════════════════════════════════════════
        private void BuildPlaceOrderPanel()
        {
            pnlPlaceOrder = MakePage();
            pnlPlaceOrder.Controls.Add(PageH("➕  Place New Order"));

            var card = Card(20, 56, 820, 460);
            pnlPlaceOrder.Controls.Add(card);

            card.Controls.Add(L("📦  Item Name", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, 18)));
            var txtItem = TxtBox(card, 20, 42, 360);
            card.Controls.Add(L("🟢  Pick-up Point", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(410, 18)));
            var txtPickup = TxtBox(card, 410, 42, 360);
            card.Controls.Add(L("⚖  Weight (kg)", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, 102)));
            var txtWeight = TxtBox(card, 20, 126, 360);
            card.Controls.Add(L("🔴  Delivery Point", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(410, 102)));
            var txtDelivery = TxtBox(card, 410, 126, 360);
            card.Controls.Add(L("⚡  Priority", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, 186)));
            var cmbP = new ComboBox { Location = new Point(20, 210), Size = new Size(360, 36), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11f), BackColor = CardBg, ForeColor = TextDark };
            cmbP.Items.AddRange(new object[] { "Normal", "Urgent" });
            cmbP.SelectedIndex = 0;
            card.Controls.Add(cmbP);
            card.Controls.Add(L("💳  Payment Status", new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(410, 186)));
            var cmbPay = new ComboBox { Location = new Point(410, 210), Size = new Size(360, 36), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11f), BackColor = CardBg, ForeColor = TextDark };
            cmbPay.Items.AddRange(new object[] { "Unpaid", "Paid", "Cash on Delivery" });
            cmbPay.SelectedIndex = 0;
            card.Controls.Add(cmbPay);

            card.Controls.Add(L($"ℹ  Order placed under account: {_customer.Username}  (ID: {_customer.UserID})", new Font("Segoe UI", 8.5f), TextGray, new Point(20, 264)));

            var lblFare = new Label { Text = "", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = RoyalBlue, BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, 286) };
            card.Controls.Add(lblFare);

            EventHandler recalc = (s, e) =>
            {
                if (double.TryParse(txtWeight.Text.Trim(), out double wt) && wt > 0)
                {
                    bool urg = cmbP.SelectedItem?.ToString() == "Urgent";
                    decimal est = Order.CalculateFare(wt, urg);
                    lblFare.Text = $"💰  Estimated Fare:  Rs. {est:N0}";
                }
                else lblFare.Text = "";
            };
            txtWeight.TextChanged += recalc;
            cmbP.SelectedIndexChanged += recalc;

            var btnPlace = new Button { Text = "✅  Place Order", Location = new Point(20, 320), Size = new Size(180, 42), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnPlace.FlatAppearance.BorderSize = 0;
            card.Controls.Add(btnPlace);

            var btnClear = new Button { Text = "🗑  Clear", Location = new Point(214, 320), Size = new Size(120, 42), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), BackColor = CardBg, ForeColor = TextGray, Cursor = Cursors.Hand };
            btnClear.FlatAppearance.BorderSize = 1;
            btnClear.FlatAppearance.BorderColor = BorderBlue;
            btnClear.Click += (s, e) => { txtItem.Text = txtWeight.Text = txtPickup.Text = txtDelivery.Text = ""; cmbP.SelectedIndex = cmbPay.SelectedIndex = 0; lblFare.Text = ""; };
            card.Controls.Add(btnClear);

            btnPlace.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtItem.Text) || string.IsNullOrWhiteSpace(txtWeight.Text) || string.IsNullOrWhiteSpace(txtPickup.Text) || string.IsNullOrWhiteSpace(txtDelivery.Text))
                { MessageBox.Show("⚠️  Please fill in all fields.", "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!double.TryParse(txtWeight.Text.Trim(), out double weight) || weight <= 0)
                { MessageBox.Show("⚠️  Please enter a valid weight.", "Invalid Weight", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                var newOrder = new Order { CustomerID = _customer.UserID, ItemName = txtItem.Text.Trim(), Weight = weight, PickupPoint = txtPickup.Text.Trim(), DeliveryPoint = txtDelivery.Text.Trim(), Priority = cmbP.SelectedItem?.ToString() ?? "Normal", PaymentStatus = cmbPay.SelectedItem?.ToString() ?? "Unpaid", OrderDate = DateTime.Now };
                newOrder.CalculateFare();
                int newID = _orderRepo.PlaceOrder(newOrder);

                if (newID > 0) { MessageBox.Show($"✅  Order placed!\n\nOrder ID:  #{newID}\nTotal Fare:  {newOrder.FormattedFare}", "Order Placed", MessageBoxButtons.OK, MessageBoxIcon.Information); txtItem.Text = txtWeight.Text = txtPickup.Text = txtDelivery.Text = ""; cmbP.SelectedIndex = cmbPay.SelectedIndex = 0; lblFare.Text = ""; }
                else MessageBox.Show("❌  Failed to place order. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 5 — MY PROFILE
        // ═════════════════════════════════════════════════════════
        private void BuildProfilePanel()
        {
            pnlProfile = MakePage();
            pnlProfile.AutoScroll = true;
            pnlProfile.AutoScrollMinSize = new Size(1, 700);
            pnlProfile.Controls.Add(PageH("👤  My Profile"));

            // ── Top profile card ───────────────────────────────────
            var topCard = Card(20, 56, Math.Max(600, mainPanel.Width - 40), 200);
            pnlProfile.Controls.Add(topCard);

            var avatarBig = new Panel { Size = new Size(88, 88), Location = new Point(20, 20), BackColor = RoyalBlue };
            avatarBig.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                avatarBig.Region = new Region(RndPath(avatarBig.ClientRectangle, 44));
                if (!string.IsNullOrEmpty(_profilePhotoPath) && File.Exists(_profilePhotoPath))
                {
                    try { using var img = Image.FromFile(_profilePhotoPath); e.Graphics.DrawImage(img, avatarBig.ClientRectangle); return; } catch { }
                }
                e.Graphics.FillEllipse(new SolidBrush(RoyalBlue), avatarBig.ClientRectangle);
                string ini = _customer.Username.Length > 0 ? _customer.Username[0].ToString().ToUpper() : "C";
                e.Graphics.DrawString(ini, new Font("Segoe UI", 30, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 88, 88), Centre());
            };
            topCard.Controls.Add(avatarBig);

            topCard.Controls.Add(L(_customer.FullName, new Font("Segoe UI", 16, FontStyle.Bold), TextDark, new Point(124, 20)));
            topCard.Controls.Add(L("@" + _customer.Username, new Font("Segoe UI", 10f), TextGray, new Point(124, 50)));
            topCard.Controls.Add(L("Role:  " + _customer.Role, new Font("Segoe UI", 9.5f), TextGray, new Point(124, 74)));
            topCard.Controls.Add(L("● Active Account", new Font("Segoe UI", 9.5f, FontStyle.Bold), GreenOk, new Point(124, 96)));
            topCard.Controls.Add(L("Member since:  " + DateTime.Now.ToString("MMMM yyyy"), new Font("Segoe UI", 9f), TextGray, new Point(124, 118)));
            topCard.Controls.Add(L("Customer ID:  " + _customer.UserID, new Font("Segoe UI", 9f), TextGray, new Point(124, 158)));

            var picBox = new PictureBox { Location = new Point(680, 20), Size = new Size(88, 88), BackColor = CardBg, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            if (!string.IsNullOrEmpty(_profilePhotoPath) && File.Exists(_profilePhotoPath))
                try { picBox.Image = Image.FromFile(_profilePhotoPath); } catch { }
            topCard.Controls.Add(picBox);

            var btnUpload = new Button { Text = "📷  Upload", Location = new Point(680, 118), Size = new Size(88, 30), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnUpload.FlatAppearance.BorderSize = 0;
            btnUpload.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Title = "Select Profile Photo", Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif" };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var img = Image.FromFile(dlg.FileName);
                    picBox.Image = img;
                    _profilePhotoPath = dlg.FileName;
                    _userRepo.SaveProfilePhoto(_customer.Username, dlg.FileName);
                    _customer.ProfilePhotoPath = dlg.FileName;
                    picAvatar.Invalidate();
                    avatarBig.Invalidate();
                }
                catch { MessageBox.Show("Could not load image.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            topCard.Controls.Add(btnUpload);

            // ── Account Information card ───────────────────────────
            var infoCard = Card(20, 270, Math.Max(600, mainPanel.Width - 40), 200);
            pnlProfile.Controls.Add(infoCard);
            infoCard.Controls.Add(L("Account Information", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 14)));

            infoCard.Controls.Add(L("First Name", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(20, 44)));
            var txtFN = new TextBox { Text = _customer.FirstName, Location = new Point(20, 64), Size = new Size(260, 32), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            infoCard.Controls.Add(txtFN);

            infoCard.Controls.Add(L("Last Name", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(300, 44)));
            var txtLN = new TextBox { Text = _customer.LastName, Location = new Point(300, 64), Size = new Size(260, 32), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            infoCard.Controls.Add(txtLN);

            infoCard.Controls.Add(L("Email", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(20, 108)));
            var txtEM = new TextBox { Text = _customer.Email, Location = new Point(20, 128), Size = new Size(260, 32), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            infoCard.Controls.Add(txtEM);

            infoCard.Controls.Add(L("Phone", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(300, 108)));
            var txtPH = new TextBox { Text = _customer.Phone, Location = new Point(300, 128), Size = new Size(260, 32), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg };
            infoCard.Controls.Add(txtPH);

            infoCard.Controls.Add(L("Username", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(580, 44)));
            infoCard.Controls.Add(new TextBox { Text = _customer.Username, Location = new Point(580, 64), Size = new Size(250, 32), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(235, 240, 250), ReadOnly = true });

            infoCard.Controls.Add(L("Customer ID", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(580, 108)));
            infoCard.Controls.Add(new TextBox { Text = _customer.UserID.ToString(), Location = new Point(580, 128), Size = new Size(250, 32), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(235, 240, 250), ReadOnly = true });

            var lblProfileStatus = new Label { Text = "", Font = new Font("Segoe UI", 9f), ForeColor = GreenOk, BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, 170) };
            infoCard.Controls.Add(lblProfileStatus);

            var btnSaveProfile = new Button { Text = "💾  Save Changes", Location = new Point(580, 162), Size = new Size(200, 34), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnSaveProfile.FlatAppearance.BorderSize = 0;
            btnSaveProfile.Click += (s, e) =>
            {
                bool ok = _userRepo.UpdateProfile(_customer.UserID, txtFN.Text.Trim(), txtLN.Text.Trim(), txtEM.Text.Trim(), txtPH.Text.Trim());
                if (ok) { _customer.FirstName = txtFN.Text.Trim(); _customer.LastName = txtLN.Text.Trim(); _customer.Email = txtEM.Text.Trim(); _customer.Phone = txtPH.Text.Trim(); lblCustomerName.Text = _customer.FullName; lblProfileStatus.ForeColor = GreenOk; lblProfileStatus.Text = "✅  Profile updated successfully."; }
                else { lblProfileStatus.ForeColor = RedAlert; lblProfileStatus.Text = "❌  Update failed. Please try again."; }
            };
            infoCard.Controls.Add(btnSaveProfile);

            // ── Change Password card ───────────────────────────────
            var pwCard = Card(20, 484, Math.Max(600, mainPanel.Width - 40), 175);
            pnlProfile.Controls.Add(pwCard);
            pwCard.Controls.Add(L("🔒  Change Password", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 14)));

            var txtCurPw = new TextBox { PlaceholderText = "Current Password", Location = new Point(20, 48), Size = new Size(220, 32), Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, UseSystemPasswordChar = true };
            var txtNewPw = new TextBox { PlaceholderText = "New Password", Location = new Point(256, 48), Size = new Size(220, 32), Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, UseSystemPasswordChar = true };
            var txtConfPw = new TextBox { PlaceholderText = "Confirm Password", Location = new Point(492, 48), Size = new Size(220, 32), Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, UseSystemPasswordChar = true };
            pwCard.Controls.Add(txtCurPw);
            pwCard.Controls.Add(txtNewPw);
            pwCard.Controls.Add(txtConfPw);

            var lblPwStatus = new Label { Text = "", Font = new Font("Segoe UI", 9f), ForeColor = GreenOk, BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, 88) };
            pwCard.Controls.Add(lblPwStatus);

            var btnUpdatePw = new Button { Text = "Update", Location = new Point(728, 48), Size = new Size(110, 32), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = RoyalBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnUpdatePw.FlatAppearance.BorderSize = 0;
            btnUpdatePw.Click += (s, e) =>
            {
                lblPwStatus.Text = "";
                if (string.IsNullOrWhiteSpace(txtCurPw.Text) || string.IsNullOrWhiteSpace(txtNewPw.Text) || string.IsNullOrWhiteSpace(txtConfPw.Text)) { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  Please fill in all password fields."; return; }
                if (txtNewPw.Text.Length < 6) { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  New password must be at least 6 characters."; return; }
                if (txtNewPw.Text != txtConfPw.Text) { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  New passwords do not match."; return; }
                string newHash = BC.HashPassword(txtNewPw.Text.Trim());
                var (success, error) = _userRepo.UpdatePassword(_customer.UserID, txtCurPw.Text.Trim(), newHash);
                if (success) { lblPwStatus.ForeColor = GreenOk; lblPwStatus.Text = "✅  Password updated successfully."; txtCurPw.Text = txtNewPw.Text = txtConfPw.Text = ""; }
                else { lblPwStatus.ForeColor = RedAlert; lblPwStatus.Text = "❌  " + error; }
            };
            pwCard.Controls.Add(btnUpdatePw);
        }

        // ═════════════════════════════════════════════════════════
        //  SHARED UI HELPERS
        // ═════════════════════════════════════════════════════════
        private void SidePanel_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new LinearGradientBrush(sidePanel.ClientRectangle, Color.FromArgb(15, 40, 100), Color.FromArgb(5, 15, 50), LinearGradientMode.Vertical);
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

        private static TextBox TxtBox(Panel parent, int x, int y, int w)
        {
            var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 36), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(245, 248, 255) };
            parent.Controls.Add(tb);
            return tb;
        }

        private static Label L(string text, Font font, Color color, Point loc) =>
            new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, AutoSize = true, Location = loc };

        private static Label PageH(string text) =>
            new Label { Text = text, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.FromArgb(18, 32, 60), BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, 16) };

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

        private static string EscHtml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
             .Replace("'", "&#39;").Replace("\"", "&quot;");
    }
}
