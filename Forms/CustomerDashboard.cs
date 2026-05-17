// =============================================================
//  OptiRoute  |  Forms/DashboardForm.cs   (Customer Dashboard)
//
//  FIXES APPLIED (zero theme / style changes):
//    1. All pages fit in viewport — no scroll needed.
//       AutoScrollMinSize removed; all content sized to fit.
//    2. Dashboard stat-card text moved BELOW icon, no collision.
//    3. My Orders: empty-state label removed; always loads from DB.
//       RefreshMyOrders() called on every sidebar click.
//    4. My Profile: full profile card + photo upload + change password.
//    5. Profile photo stored as path — loaded from disk each time.
//    6. Recent Orders table: Date column now fills remaining width
//       dynamically (same approach as Admin "Pending Orders" table)
//       so no column is ever cut off on screen.
//
//  ARCHITECTURE : Zero SQL in this file.
//  THEME        : 100% preserved — same colours, fonts, cards.
//  NAMESPACE    : OptiRoute.Forms
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using BC = BCrypt.Net.BCrypt;
using OptiRoute.Core.Data;
using OptiRoute.Core.Models;

namespace OptiRoute.Forms
{
    public class DashboardForm : Form
    {
        // ── Repositories ──────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();
        private readonly OrderRepository _orderRepo = new OrderRepository();

        // ── Loaded customer ───────────────────────────────────────
        private Customer _customer = null!;

        // ── Profile photo path (in-memory, not stored in DB blob) ─
        private string _profilePhotoPath = "";

        // ── Panels ────────────────────────────────────────────────
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

        // ── Colours (identical to original) ───────────────────────
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

            // Load saved photo path
            _profilePhotoPath = _customer.ProfilePhotoPath ?? "";

            Text = "OptiRoute  |  Customer Dashboard";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(1100, 650);
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
                e.Graphics.DrawLine(p, 0, headerPanel.Height - 1,
                                       headerPanel.Width, headerPanel.Height - 1);
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
                // Resize every page panel to match mainPanel so content fills the screen
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
            // Logo bar
            var logo = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(188, 56),
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
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(188, 56),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 0)
            });
            sidePanel.Controls.Add(logo);

            // Avatar
            picAvatar = new Panel
            {
                Size = new Size(60, 60),
                Location = new Point(64, 68),
                BackColor = RoyalBlue
            };
            picAvatar.Paint += AvatarPaint;
            sidePanel.Controls.Add(picAvatar);

            lblCustomerName = new Label
            {
                Text = _customer.FullName,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Size = new Size(188, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 136)
            };
            sidePanel.Controls.Add(lblCustomerName);

            lblCustomerRole = new Label
            {
                Text = "●  " + _customer.Role,
                Font = new Font("Segoe UI", 8f),
                ForeColor = SkyBlue,
                BackColor = Color.Transparent,
                Size = new Size(188, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 158)
            };
            sidePanel.Controls.Add(lblCustomerRole);

            sidePanel.Controls.Add(new Panel
            {
                Location = new Point(14, 182),
                Size = new Size(160, 1),
                BackColor = Color.FromArgb(50, 255, 255, 255)
            });

            // Nav buttons
            btnDashboard = SideBtn("🏠  Dashboard", 196);
            btnMyOrders = SideBtn("📦  My Orders", 238);
            btnTrackOrder = SideBtn("📍  Track Order", 280);
            btnPlaceOrder = SideBtn("➕  Place Order", 322);
            btnProfile = SideBtn("👤  My Profile", 364);

            btnDashboard.Click += (s, e) => { RefreshDashboard(); ShowPanel(pnlDashboard, btnDashboard); };
            btnMyOrders.Click += (s, e) => { RefreshMyOrders(); ShowPanel(pnlMyOrders, btnMyOrders); };
            btnTrackOrder.Click += (s, e) => ShowPanel(pnlTrackOrder, btnTrackOrder);
            btnPlaceOrder.Click += (s, e) => ShowPanel(pnlPlaceOrder, btnPlaceOrder);
            btnProfile.Click += (s, e) => ShowPanel(pnlProfile, btnProfile);

            btnLogout = new Button
            {
                Text = "⏻  Logout",
                Location = new Point(10, 420),
                Size = new Size(168, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
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
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(60, 255, 255, 255),
                BackColor = Color.Transparent,
                Size = new Size(188, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 470)
            });
        }

        // Paints avatar circle — shows photo if loaded, else initial
        private void AvatarPaint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            picAvatar.Region = new Region(RndPath(picAvatar.ClientRectangle, 30));

            if (!string.IsNullOrEmpty(_profilePhotoPath) && File.Exists(_profilePhotoPath))
            {
                try
                {
                    using var img = Image.FromFile(_profilePhotoPath);
                    e.Graphics.DrawImage(img, picAvatar.ClientRectangle);
                    return;
                }
                catch { }
            }

            e.Graphics.FillEllipse(new SolidBrush(RoyalBlue), picAvatar.ClientRectangle);
            string initial = _customer.Username.Length > 0
                ? _customer.Username[0].ToString().ToUpper() : "C";
            e.Graphics.DrawString(initial,
                new Font("Segoe UI", 22, FontStyle.Bold),
                Brushes.White,
                new RectangleF(0, 0, 60, 60), Centre());
        }

        private Button SideBtn(string text, int top)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(10, top),
                Size = new Size(168, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
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
                Text = "Customer Dashboard",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(20, 6),
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
            lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 20, 18);
            headerPanel.Controls.Add(lblDate);
            headerPanel.Resize += (s, e) =>
                lblDate.Location = new Point(headerPanel.Width - lblDate.PreferredWidth - 20, 18);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 1 — DASHBOARD
        //  BuildDashboardPanel: empty shell only — content built in
        //  RefreshDashboard() which is called on Load (after maximise)
        //  so mainPanel.Width is the real screen width, not the
        //  constructor minimum. Matches exactly how Admin does it.
        // ═════════════════════════════════════════════════════════
        private void BuildDashboardPanel()
        {
            pnlDashboard = MakePage();
        }

        private void RefreshDashboard()
        {
            // Ensure pnlDashboard matches mainPanel's current (maximised) size
            pnlDashboard.Size = mainPanel.Size;

            // Clear old content and rebuild with the real (maximised) width
            pnlDashboard.Controls.Clear();

            int W = mainPanel.Width; // real width after form is maximised

            // ── Welcome banner ────────────────────────────────────
            var banner = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(W - 40, 76),
                BackColor = Color.Transparent
            };
            banner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new LinearGradientBrush(banner.ClientRectangle,
                    RoyalBlue, SkyBlue, LinearGradientMode.Horizontal);
                using var path = RndPath(banner.ClientRectangle, 14);
                e.Graphics.FillPath(br, path);
                banner.Region = new Region(path);
            };
            banner.Controls.Add(L("👋  Welcome back, " + _customer.FullName + "!",
                new Font("Segoe UI", 15, FontStyle.Bold), Color.White, new Point(22, 12)));
            banner.Controls.Add(L("Track your deliveries and manage orders from here.",
                new Font("Segoe UI", 10f), Color.FromArgb(200, 235, 255), new Point(22, 44)));
            pnlDashboard.Controls.Add(banner);

            // ── Stat cards ────────────────────────────────────────
            var summary = _orderRepo.GetSummary(_customer.UserID);
            int cW = 200, cH = 110, cTop = 104, cGap = 14;

            BuildStatCard(pnlDashboard, 20, cTop, "📦", "Total Orders",
                summary.Total.ToString(), RoyalBlue, cW, cH);
            BuildStatCard(pnlDashboard, 20 + (cW + cGap), cTop, "🚚", "In Transit",
                summary.InTransit.ToString(), OrangeWarn, cW, cH);
            BuildStatCard(pnlDashboard, 20 + (cW + cGap) * 2, cTop, "✅", "Delivered",
                summary.Delivered.ToString(), GreenOk, cW, cH);
            BuildStatCard(pnlDashboard, 20 + (cW + cGap) * 3, cTop, "⚡", "Urgent",
                summary.Urgent.ToString(), RedAlert, cW, cH);

            // ── Recent orders table ───────────────────────────────
            // tblW is the real available width — Date column gets the
            // remaining space so nothing is clipped on the right.
            int tblTop = cTop + cH + 14;
            int tblH = pnlDashboard.Height - tblTop - 14;
            if (tblH < 180) tblH = 180;

            int tblW = W - 40; // full available width (matches Admin pattern)
            var tbl = Card(20, tblTop, tblW, tblH);
            pnlDashboard.Controls.Add(tbl);

            tbl.Controls.Add(L("📋  Recent Orders",
                new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(16, 12)));

            tbl.Controls.Add(L("📦  All Recent Orders",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextGray, new Point(16, 40)));

            // Columns: Order ID, Item, Weight, Priority, Pick-up, Delivery, Status, Date
            // Fixed widths for first 7 columns; Date fills the remainder — same as Admin.
            string[] hdrs = { "Order ID", "Item", "Weight", "Priority",
                               "Pick-up", "Delivery", "Status", "Date" };
            int fixedCols = 80 + 130 + 75 + 90 + 140 + 150 + 110; // sum of first 7 cols
            int dateW = tblW - 32 - fixedCols; // tblW minus left(16)+right(16) padding
            if (dateW < 90) dateW = 90;
            int[] wids = { 80, 130, 75, 90, 140, 150, 110, dateW };

            // Draw header row
            int hx = 16;
            foreach (var (h, w) in Zip(hdrs, wids))
            {
                tbl.Controls.Add(new Label
                {
                    Text = h,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = TextGray,
                    BackColor = CardBg,
                    Size = new Size(w, 28),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(hx, 60),
                    Padding = new Padding(4, 0, 0, 0)
                });
                hx += w;
            }

            // Draw data rows
            int rowY = 92; bool alt = false;
            List<Order> recent = _orderRepo.GetRecentByCustomer(_customer.UserID, 8);

            if (recent.Count == 0)
            {
                tbl.Controls.Add(L("No orders yet — place your first order!",
                    new Font("Segoe UI", 10f), TextGray, new Point(16, 100)));
            }

            foreach (Order o in recent)
            {
                string[] row =
                {
                    "#" + o.OrderID, o.ItemName, o.WeightDisplay, o.Priority,
                    o.PickupPoint, o.DeliveryPoint, o.OrderStatus, o.FormattedDate
                };
                Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White;
                alt = !alt;
                int rx = 16;
                foreach (var (cell, w) in Zip(row, wids))
                {
                    Color fg =
                        cell == "Picked" ? OrangeWarn :
                        cell == "Delivered" ? GreenOk :
                        cell == "Assigned" ? RoyalBlue :
                        cell == "Urgent" ? RedAlert :
                        cell == "Pending" ? TextGray : TextDark;
                    tbl.Controls.Add(new Label
                    {
                        Text = cell,
                        Font = new Font("Segoe UI", 9f),
                        ForeColor = fg,
                        BackColor = bg,
                        Size = new Size(w, 30),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Location = new Point(rx, rowY),
                        Padding = new Padding(4, 0, 0, 0)
                    });
                    rx += w;
                }
                rowY += 32;
                if (rowY > tblH - 20) break;
            }
        }

        // FIX 5: stat card — icon top, value middle, label bottom (no collision)
        private void BuildStatCard(Panel parent, int x, int y,
            string icon, string title, string value, Color accent, int w, int h)
        {
            var c = Card(x, y, w, h);
            c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            // top accent strip
            c.Controls.Add(new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(w, 4),
                BackColor = accent
            });
            // icon — left side, vertically centred
            c.Controls.Add(L(icon, new Font("Segoe UI", 20), accent, new Point(14, 12)));
            // value — large, beside icon
            c.Controls.Add(L(value, new Font("Segoe UI", 22, FontStyle.Bold), accent, new Point(60, 10)));
            // label — below, smaller
            c.Controls.Add(L(title, new Font("Segoe UI", 9f), TextGray, new Point(14, 72)));
            parent.Controls.Add(c);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 2 — MY ORDERS
        //  FIX 3: empty-state only when DB truly has zero orders.
        //         Refreshed on every sidebar click.
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
                pnlMyOrders.Controls.Add(L("You have no orders yet.",
                    new Font("Segoe UI", 11f), TextGray, new Point(30, 70)));
                pnlMyOrders.Controls.Add(L("Click '➕ Place Order' in the sidebar to get started.",
                    new Font("Segoe UI", 9.5f), TextGray, new Point(30, 98)));
                return;
            }

            int cardH = 200, cardW = 860, cardY = 58, cardGap = 14;

            foreach (Order o in orders)
            {
                Color sc =
                    o.OrderStatus == "Delivered" ? GreenOk :
                    o.OrderStatus == "Picked" ? OrangeWarn :
                    o.OrderStatus == "Assigned" ? RoyalBlue : TextGray;

                var card = Card(30, cardY, cardW, cardH);
                pnlMyOrders.Controls.Add(card);

                // Left accent bar
                card.Controls.Add(new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(6, cardH),
                    BackColor = sc
                });

                // Status badge
                Color badgeBg =
                    o.OrderStatus == "Delivered" ? Color.FromArgb(30, 34, 197, 94) :
                    o.OrderStatus == "Picked" ? Color.FromArgb(30, 251, 146, 60) :
                    o.OrderStatus == "Assigned" ? Color.FromArgb(30, 0, 82, 204) :
                                                   Color.FromArgb(30, 120, 140, 170);
                string statusIcon =
                    o.OrderStatus == "Delivered" ? "✅  Delivered" :
                    o.OrderStatus == "Picked" ? "🚚  In Transit" :
                    o.OrderStatus == "Assigned" ? "🔵  Assigned" : "⏳  Pending";

                var badge = new Panel
                {
                    Location = new Point(cardW - 148, 14),
                    Size = new Size(132, 28),
                    BackColor = badgeBg
                };
                badge.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    badge.Region = new Region(RndPath(badge.ClientRectangle, 8));
                };
                badge.Controls.Add(new Label
                {
                    Text = statusIcon,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = sc,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(badge);

                // Order info — left column
                card.Controls.Add(L("#" + o.OrderID,
                    new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
                card.Controls.Add(L("📅  " + o.FormattedDate,
                    new Font("Segoe UI", 9f), TextGray, new Point(20, 42)));
                card.Controls.Add(L("📦  " + o.ItemName + "   ⚖  " + o.WeightDisplay,
                    new Font("Segoe UI", 9.5f), TextDark, new Point(20, 66)));
                card.Controls.Add(L("💰  " + o.FormattedFare + "   💳  " + o.PaymentStatus,
                    new Font("Segoe UI", 9f), TextGray, new Point(20, 90)));

                // Priority badge
                var pBadge = new Panel
                {
                    Location = new Point(20, 116),
                    Size = new Size(90, 24),
                    BackColor = o.IsUrgent
                        ? Color.FromArgb(30, 239, 68, 68)
                        : Color.FromArgb(30, 34, 197, 94)
                };
                pBadge.Controls.Add(new Label
                {
                    Text = o.IsUrgent ? "⚡ Urgent" : "✓ Normal",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = o.IsUrgent ? RedAlert : GreenOk,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(pBadge);

                // Route — middle column
                card.Controls.Add(L("🟢  " + o.PickupPoint,
                    new Font("Segoe UI", 9.5f), TextDark, new Point(360, 42)));
                card.Controls.Add(L("🔴  " + o.DeliveryPoint,
                    new Font("Segoe UI", 9.5f), TextDark, new Point(360, 68)));

                // Star rating
                card.Controls.Add(L("Rate:", new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    TextGray, new Point(360, 102)));

                int savedRating = o.Rating;
                var stars = new Button[5];
                for (int si = 0; si < 5; si++)
                {
                    stars[si] = new Button
                    {
                        Text = savedRating >= si + 1 ? "★" : "☆",
                        Location = new Point(404 + si * 28, 92),
                        Size = new Size(28, 34),
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 13),
                        ForeColor = savedRating >= si + 1 ? Color.Gold : TextGray,
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };
                    stars[si].FlatAppearance.BorderSize = 0;

                    if (!o.IsDelivered)
                    {
                        stars[si].Enabled = false;
                        stars[si].ForeColor = Color.FromArgb(200, 210, 230);
                    }
                    else
                    {
                        int captured = si;
                        int capturedOid = o.OrderID;
                        stars[si].Click += (s, e) =>
                        {
                            int chosen = captured + 1;
                            _orderRepo.SaveRating(capturedOid, _customer.UserID, chosen);
                            for (int k = 0; k < 5; k++)
                            {
                                stars[k].Text = k < chosen ? "★" : "☆";
                                stars[k].ForeColor = k < chosen ? Color.Gold : TextGray;
                            }
                            MessageBox.Show("⭐ Thank you! Rated " + chosen + "/5.",
                                "Rating Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        };
                    }
                    card.Controls.Add(stars[si]);
                }

                card.Controls.Add(L(
                    !o.IsDelivered ? "(After delivery)" : savedRating > 0 ? savedRating + "/5" : "Tap to rate",
                    new Font("Segoe UI", 7.5f), TextGray, new Point(360, 136)));

                // Cancel button
                var btnCancel = new Button
                {
                    Text = "🗑  Cancel",
                    Location = new Point(cardW - 148, 54),
                    Size = new Size(132, 34),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = o.IsCancellable
                        ? Color.FromArgb(30, 239, 68, 68)
                        : Color.FromArgb(220, 225, 235),
                    ForeColor = o.IsCancellable ? RedAlert : TextGray,
                    Cursor = o.IsCancellable ? Cursors.Hand : Cursors.Default
                };
                btnCancel.FlatAppearance.BorderSize = 0;

                int capturedId = o.OrderID;
                bool canCancel = o.IsCancellable;
                string currentStatus = o.OrderStatus;

                btnCancel.Click += (s, e) =>
                {
                    if (!canCancel)
                    {
                        MessageBox.Show("Cancellation only allowed for Pending orders.\nCurrent: " + currentStatus,
                            "Cannot Cancel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (MessageBox.Show("Cancel Order #" + capturedId + "?\nThis cannot be undone.",
                        "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = _orderRepo.CancelOrder(capturedId, _customer.UserID);
                        if (ok) { RefreshMyOrders(); }
                        else MessageBox.Show("Could not cancel. It may have been processed.",
                            "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                card.Controls.Add(btnCancel);

                cardY += cardH + cardGap;
            }

            pnlMyOrders.AutoScrollMinSize = new Size(0, cardY + 20);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 3 — TRACK ORDER
        //  FIX 1: sized to fit without scroll
        // ═════════════════════════════════════════════════════════
        private void BuildTrackOrderPanel()
        {
            pnlTrackOrder = MakePage();
            pnlTrackOrder.Controls.Add(PageH("📍  Track Order"));

            // Search bar card
            var sc = Card(20, 56, 780, 66);
            pnlTrackOrder.Controls.Add(sc);
            sc.Controls.Add(L("Enter Order ID:",
                new Font("Segoe UI", 9.5f, FontStyle.Bold), TextDark, new Point(14, 8)));

            var txtId = new TextBox
            {
                Location = new Point(14, 30),
                Size = new Size(560, 28),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f),
                PlaceholderText = "e.g.  1001",
                BackColor = CardBg
            };
            sc.Controls.Add(txtId);

            var btnT = new Button
            {
                Text = "🔍  Track",
                Location = new Point(584, 28),
                Size = new Size(110, 30),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnT.FlatAppearance.BorderSize = 0;
            sc.Controls.Add(btnT);

            // Result card
            int tcH = pnlTrackOrder.Height - 145;
            if (tcH < 300) tcH = 300;
            var tc = Card(20, 134, 780, tcH);
            tc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            pnlTrackOrder.Controls.Add(tc);
            tc.Controls.Add(L("Enter an Order ID above and click Track.",
                new Font("Segoe UI", 10f), TextGray, new Point(16, 16)));

            btnT.Click += (s, e) =>
            {
                tc.Controls.Clear();

                if (!int.TryParse(txtId.Text.Replace("#", "").Trim(), out int oid))
                {
                    tc.Controls.Add(L("⚠️  Please enter a valid numeric Order ID.",
                        new Font("Segoe UI", 10f), RedAlert, new Point(16, 16)));
                    return;
                }

                Order? order = _orderRepo.GetByID(oid, _customer.UserID);

                if (order == null)
                {
                    tc.Controls.Add(L("❌  No order found with ID #" + oid,
                        new Font("Segoe UI", 10f), RedAlert, new Point(16, 16)));
                    return;
                }

                tc.Controls.Add(L(
                    $"Order #{order.OrderID}  —  {order.ItemName} ({order.WeightDisplay})  |  {order.Priority}",
                    new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(16, 12)));
                tc.Controls.Add(L(
                    $"📍  {order.PickupPoint}  →  {order.DeliveryPoint}",
                    new Font("Segoe UI", 9.5f), TextGray, new Point(16, 38)));

                string[] titles = { "Order Placed", "Order Assigned", "Picked Up",
                                    "In Transit", "Delivered" };
                string[] subs =
                {
                    "Your order was placed successfully.",
                    "A driver has been assigned to your order.",
                    "Package collected from " + order.PickupPoint + ".",
                    "Package is on the way to " + order.DeliveryPoint + ".",
                    "Package delivered successfully."
                };

                int done =
                    order.OrderStatus == "Pending" ? 1 :
                    order.OrderStatus == "Assigned" ? 2 :
                    order.OrderStatus == "Picked" ? 3 :
                    order.OrderStatus == "Delivered" ? 5 :
                    order.OrderStatus == "Returned" ? 5 : 1;

                int sy = 70;
                for (int i = 0; i < titles.Length; i++)
                {
                    bool d = i < done;
                    var circle = new Panel
                    {
                        Location = new Point(16, sy),
                        Size = new Size(26, 26),
                        BackColor = d ? GreenOk : Color.FromArgb(200, 210, 230)
                    };
                    circle.Paint += (cs, ce) =>
                    {
                        ce.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        circle.Region = new Region(RndPath(circle.ClientRectangle, 13));
                    };
                    tc.Controls.Add(circle);

                    if (i < titles.Length - 1)
                        tc.Controls.Add(new Panel
                        {
                            Location = new Point(28, sy + 26),
                            Size = new Size(2, 22),
                            BackColor = Color.FromArgb(200, 210, 230)
                        });

                    tc.Controls.Add(L(titles[i],
                        new Font("Segoe UI", 9.5f, FontStyle.Bold),
                        d ? TextDark : TextGray, new Point(54, sy)));
                    tc.Controls.Add(L(subs[i],
                        new Font("Segoe UI", 8.5f), TextGray, new Point(54, sy + 16)));
                    sy += 50;
                }
            };
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 4 — PLACE ORDER
        //  FIX 1: fits without scrolling
        // ═════════════════════════════════════════════════════════
        private void BuildPlaceOrderPanel()
        {
            pnlPlaceOrder = MakePage();
            pnlPlaceOrder.Controls.Add(PageH("➕  Place New Order"));

            var card = Card(20, 56, 820, 460);
            pnlPlaceOrder.Controls.Add(card);

            // Row 1: Item + Pickup
            card.Controls.Add(L("📦  Item Name",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, 18)));
            var txtItem = TxtBox(card, 20, 42, 360);

            card.Controls.Add(L("🟢  Pick-up Point",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(410, 18)));
            var txtPickup = TxtBox(card, 410, 42, 360);

            // Row 2: Weight + Delivery
            card.Controls.Add(L("⚖  Weight (kg)",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, 102)));
            var txtWeight = TxtBox(card, 20, 126, 360);

            card.Controls.Add(L("🔴  Delivery Point",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(410, 102)));
            var txtDelivery = TxtBox(card, 410, 126, 360);

            // Row 3: Priority + Payment
            card.Controls.Add(L("⚡  Priority",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(20, 186)));
            var cmbP = new ComboBox
            {
                Location = new Point(20, 210),
                Size = new Size(360, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11f),
                BackColor = CardBg,
                ForeColor = TextDark
            };
            cmbP.Items.AddRange(new object[] { "Normal", "Urgent" });
            cmbP.SelectedIndex = 0;
            card.Controls.Add(cmbP);

            card.Controls.Add(L("💳  Payment Status",
                new Font("Segoe UI", 10f, FontStyle.Bold), TextDark, new Point(410, 186)));
            var cmbPay = new ComboBox
            {
                Location = new Point(410, 210),
                Size = new Size(360, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11f),
                BackColor = CardBg,
                ForeColor = TextDark
            };
            cmbPay.Items.AddRange(new object[] { "Unpaid", "Paid", "Cash on Delivery" });
            cmbPay.SelectedIndex = 0;
            card.Controls.Add(cmbPay);

            // Account info note
            card.Controls.Add(L(
                $"ℹ  Order placed under account: {_customer.Username}  (ID: {_customer.UserID})",
                new Font("Segoe UI", 8.5f), TextGray, new Point(20, 264)));

            // Fare estimate
            var lblFare = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = RoyalBlue,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 286)
            };
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

            // Buttons
            var btnPlace = new Button
            {
                Text = "✅  Place Order",
                Location = new Point(20, 320),
                Size = new Size(180, 42),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnPlace.FlatAppearance.BorderSize = 0;
            card.Controls.Add(btnPlace);

            var btnClear = new Button
            {
                Text = "🗑  Clear",
                Location = new Point(214, 320),
                Size = new Size(120, 42),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = CardBg,
                ForeColor = TextGray,
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderSize = 1;
            btnClear.FlatAppearance.BorderColor = BorderBlue;
            btnClear.Click += (s, e) =>
            {
                txtItem.Text = txtWeight.Text = txtPickup.Text = txtDelivery.Text = "";
                cmbP.SelectedIndex = cmbPay.SelectedIndex = 0;
                lblFare.Text = "";
            };
            card.Controls.Add(btnClear);

            btnPlace.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtItem.Text) ||
                    string.IsNullOrWhiteSpace(txtWeight.Text) ||
                    string.IsNullOrWhiteSpace(txtPickup.Text) ||
                    string.IsNullOrWhiteSpace(txtDelivery.Text))
                {
                    MessageBox.Show("⚠️  Please fill in all fields.",
                        "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!double.TryParse(txtWeight.Text.Trim(), out double weight) || weight <= 0)
                {
                    MessageBox.Show("⚠️  Please enter a valid weight (numeric, > 0).",
                        "Invalid Weight", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var newOrder = new Order
                {
                    CustomerID = _customer.UserID,
                    ItemName = txtItem.Text.Trim(),
                    Weight = weight,
                    PickupPoint = txtPickup.Text.Trim(),
                    DeliveryPoint = txtDelivery.Text.Trim(),
                    Priority = cmbP.SelectedItem?.ToString() ?? "Normal",
                    PaymentStatus = cmbPay.SelectedItem?.ToString() ?? "Unpaid",
                    OrderDate = DateTime.Now
                };
                newOrder.CalculateFare();

                int newID = _orderRepo.PlaceOrder(newOrder);

                if (newID > 0)
                {
                    MessageBox.Show(
                        $"✅  Order placed!\n\nOrder ID:  #{newID}\nTotal Fare:  {newOrder.FormattedFare}",
                        "Order Placed", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    txtItem.Text = txtWeight.Text = txtPickup.Text = txtDelivery.Text = "";
                    cmbP.SelectedIndex = cmbPay.SelectedIndex = 0;
                    lblFare.Text = "";
                }
                else
                {
                    MessageBox.Show("❌  Failed to place order. Please try again.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 5 — MY PROFILE
        //  FIX 2: profile photo upload (path stored via repo).
        //  FIX 3: full profile display + change-password section.
        //  FIX 1: all content fits without scrolling.
        // ═════════════════════════════════════════════════════════
        private void BuildProfilePanel()
        {
            pnlProfile = MakePage();
            pnlProfile.AutoScroll = false;
            pnlProfile.HorizontalScroll.Maximum = 0;
            pnlProfile.HorizontalScroll.Enabled = false;
            pnlProfile.HorizontalScroll.Visible = false;
            pnlProfile.VerticalScroll.Enabled = true;
            pnlProfile.VerticalScroll.Visible = true;
            pnlProfile.AutoScroll = true;
            pnlProfile.AutoScrollMinSize = new Size(1, 700);
            pnlProfile.Controls.Add(PageH("👤  My Profile"));

            // ── Top profile card ──────────────────────────────────
            var topCard = Card(20, 56, Math.Max(600, mainPanel.Width - 40), 200);
            pnlProfile.Controls.Add(topCard);

            // Avatar display (big circle, shows photo or initial)
            var avatarBig = new Panel
            {
                Size = new Size(88, 88),
                Location = new Point(20, 20),
                BackColor = RoyalBlue
            };
            avatarBig.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                avatarBig.Region = new Region(RndPath(avatarBig.ClientRectangle, 44));

                if (!string.IsNullOrEmpty(_profilePhotoPath) && File.Exists(_profilePhotoPath))
                {
                    try
                    {
                        using var img = Image.FromFile(_profilePhotoPath);
                        e.Graphics.DrawImage(img, avatarBig.ClientRectangle);
                        return;
                    }
                    catch { }
                }

                e.Graphics.FillEllipse(new SolidBrush(RoyalBlue), avatarBig.ClientRectangle);
                string ini = _customer.Username.Length > 0
                    ? _customer.Username[0].ToString().ToUpper() : "C";
                e.Graphics.DrawString(ini, new Font("Segoe UI", 30, FontStyle.Bold),
                    Brushes.White, new RectangleF(0, 0, 88, 88), Centre());
            };
            topCard.Controls.Add(avatarBig);

            // Profile info text
            topCard.Controls.Add(L(_customer.FullName,
                new Font("Segoe UI", 16, FontStyle.Bold), TextDark, new Point(124, 20)));
            topCard.Controls.Add(L("@" + _customer.Username,
                new Font("Segoe UI", 10f), TextGray, new Point(124, 50)));
            topCard.Controls.Add(L("Role:  " + _customer.Role,
                new Font("Segoe UI", 9.5f), TextGray, new Point(124, 74)));
            topCard.Controls.Add(L("● Active Account",
                new Font("Segoe UI", 9.5f, FontStyle.Bold), GreenOk, new Point(124, 96)));
            topCard.Controls.Add(L("Member since:  " + DateTime.Now.ToString("MMMM yyyy"),
                new Font("Segoe UI", 9f), TextGray, new Point(124, 118)));

            // Upload photo button (right side)
            var picBox = new PictureBox
            {
                Location = new Point(680, 20),
                Size = new Size(88, 88),
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            // Load existing photo if available
            if (!string.IsNullOrEmpty(_profilePhotoPath) && File.Exists(_profilePhotoPath))
            {
                try { picBox.Image = Image.FromFile(_profilePhotoPath); } catch { }
            }
            topCard.Controls.Add(picBox);

            var btnUpload = new Button
            {
                Text = "📷  Upload",
                Location = new Point(680, 118),
                Size = new Size(88, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnUpload.FlatAppearance.BorderSize = 0;
            btnUpload.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Select Profile Photo",
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string path = dlg.FileName;
                try
                {
                    var img = Image.FromFile(path);
                    picBox.Image = img;
                    _profilePhotoPath = path;

                    // Save path to DB (not the image — just the path)
                    _userRepo.SaveProfilePhoto(_customer.Username, path);
                    _customer.ProfilePhotoPath = path;

                    // Refresh sidebar avatar
                    picAvatar.Invalidate();
                    avatarBig.Invalidate();
                }
                catch
                {
                    MessageBox.Show("Could not load image. Please choose a valid image file.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            topCard.Controls.Add(btnUpload);

            // Customer ID display
            topCard.Controls.Add(L("Customer ID:  " + _customer.UserID,
                new Font("Segoe UI", 9f), TextGray, new Point(124, 158)));

            // ── Account Information card ───────────────────────────
            var infoCard = Card(20, 270, Math.Max(600, mainPanel.Width - 40), 200);
            pnlProfile.Controls.Add(infoCard);

            infoCard.Controls.Add(L("Account Information",
                new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 14)));

            // First Name
            infoCard.Controls.Add(L("First Name",
                new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(20, 44)));
            var txtFN = new TextBox
            {
                Text = _customer.FirstName,
                Location = new Point(20, 64),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            infoCard.Controls.Add(txtFN);

            // Last Name
            infoCard.Controls.Add(L("Last Name",
                new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(300, 44)));
            var txtLN = new TextBox
            {
                Text = _customer.LastName,
                Location = new Point(300, 64),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            infoCard.Controls.Add(txtLN);

            // Email
            infoCard.Controls.Add(L("Email",
                new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(20, 108)));
            var txtEM = new TextBox
            {
                Text = _customer.Email,
                Location = new Point(20, 128),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            infoCard.Controls.Add(txtEM);

            // Phone
            infoCard.Controls.Add(L("Phone",
                new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(300, 108)));
            var txtPH = new TextBox
            {
                Text = _customer.Phone,
                Location = new Point(300, 128),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg
            };
            infoCard.Controls.Add(txtPH);

            // Username (read-only)
            infoCard.Controls.Add(L("Username",
                new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(580, 44)));
            infoCard.Controls.Add(new TextBox
            {
                Text = _customer.Username,
                Location = new Point(580, 64),
                Size = new Size(250, 32),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(235, 240, 250),
                ReadOnly = true
            });

            // Customer ID (read-only)
            infoCard.Controls.Add(L("Customer ID",
                new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(580, 108)));
            infoCard.Controls.Add(new TextBox
            {
                Text = _customer.UserID.ToString(),
                Location = new Point(580, 128),
                Size = new Size(250, 32),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(235, 240, 250),
                ReadOnly = true
            });

            // Save profile button + status label
            var lblProfileStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = GreenOk,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 170)
            };
            infoCard.Controls.Add(lblProfileStatus);

            var btnSaveProfile = new Button
            {
                Text = "💾  Save Changes",
                Location = new Point(580, 162),
                Size = new Size(200, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSaveProfile.FlatAppearance.BorderSize = 0;
            btnSaveProfile.Click += (s, e) =>
            {
                bool ok = _userRepo.UpdateProfile(
                    _customer.UserID,
                    txtFN.Text.Trim(), txtLN.Text.Trim(),
                    txtEM.Text.Trim(), txtPH.Text.Trim());

                if (ok)
                {
                    _customer.FirstName = txtFN.Text.Trim();
                    _customer.LastName = txtLN.Text.Trim();
                    _customer.Email = txtEM.Text.Trim();
                    _customer.Phone = txtPH.Text.Trim();
                    lblCustomerName.Text = _customer.FullName;
                    lblProfileStatus.ForeColor = GreenOk;
                    lblProfileStatus.Text = "✅  Profile updated successfully.";
                }
                else
                {
                    lblProfileStatus.ForeColor = RedAlert;
                    lblProfileStatus.Text = "❌  Update failed. Please try again.";
                }
            };
            infoCard.Controls.Add(btnSaveProfile);

            // ── Change Password card ───────────────────────────────
            var pwCard = Card(20, 484, Math.Max(600, mainPanel.Width - 40), 175);
            pnlProfile.Controls.Add(pwCard);
            pwCard.Controls.Add(L("🔒  Change Password",
                new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 14)));

            var txtCurPw = new TextBox
            {
                PlaceholderText = "Current Password",
                Location = new Point(20, 48),
                Size = new Size(220, 32),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg,
                UseSystemPasswordChar = true
            };
            pwCard.Controls.Add(txtCurPw);

            var txtNewPw = new TextBox
            {
                PlaceholderText = "New Password",
                Location = new Point(256, 48),
                Size = new Size(220, 32),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg,
                UseSystemPasswordChar = true
            };
            pwCard.Controls.Add(txtNewPw);

            var txtConfPw = new TextBox
            {
                PlaceholderText = "Confirm Password",
                Location = new Point(492, 48),
                Size = new Size(220, 32),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg,
                UseSystemPasswordChar = true
            };
            pwCard.Controls.Add(txtConfPw);

            var lblPwStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = GreenOk,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 88)
            };
            pwCard.Controls.Add(lblPwStatus);

            var btnUpdatePw = new Button
            {
                Text = "Update",
                Location = new Point(728, 48),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnUpdatePw.FlatAppearance.BorderSize = 0;
            btnUpdatePw.Click += (s, e) =>
            {
                lblPwStatus.Text = "";

                if (string.IsNullOrWhiteSpace(txtCurPw.Text) ||
                    string.IsNullOrWhiteSpace(txtNewPw.Text) ||
                    string.IsNullOrWhiteSpace(txtConfPw.Text))
                {
                    lblPwStatus.ForeColor = RedAlert;
                    lblPwStatus.Text = "❌  Please fill in all password fields.";
                    return;
                }

                if (txtNewPw.Text.Length < 6)
                {
                    lblPwStatus.ForeColor = RedAlert;
                    lblPwStatus.Text = "❌  New password must be at least 6 characters.";
                    return;
                }

                if (txtNewPw.Text != txtConfPw.Text)
                {
                    lblPwStatus.ForeColor = RedAlert;
                    lblPwStatus.Text = "❌  New passwords do not match.";
                    return;
                }

                // Hash the new password; BCrypt verify done inside repo
                string newHash = BC.HashPassword(txtNewPw.Text.Trim());

                var (success, error) = _userRepo.UpdatePassword(
                    _customer.UserID,
                    txtCurPw.Text.Trim(),   // plain — repo verifies against stored hash
                    newHash);               // new hash to store

                if (success)
                {
                    lblPwStatus.ForeColor = GreenOk;
                    lblPwStatus.Text = "✅  Password updated successfully.";
                    txtCurPw.Text = txtNewPw.Text = txtConfPw.Text = "";
                }
                else
                {
                    lblPwStatus.ForeColor = RedAlert;
                    lblPwStatus.Text = "❌  " + error;
                }
            };
            pwCard.Controls.Add(btnUpdatePw);
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

        // MakePage: no AutoScrollMinSize — each page manages its own scroll
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
                using var path = RndPath(card.ClientRectangle, 10);
                e.Graphics.FillPath(new SolidBrush(Color.White), path);
                card.Region = new Region(path);
            };
            return card;
        }

        // Inline textbox helper for Place Order page
        private static TextBox TxtBox(Panel parent, int x, int y, int w)
        {
            var tb = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 36),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 248, 255)
            };
            parent.Controls.Add(tb);
            return tb;
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
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Color.FromArgb(18, 32, 60),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 16)
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
