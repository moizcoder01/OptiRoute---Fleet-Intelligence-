// =============================================================
//  OptiRoute  |  Forms/DriverDashboardForm.cs
//
//  FIXES APPLIED
//   1. All pages fit in viewport — no scroll on Dashboard,
//      Active Delivery, History or Profile pages.
//      (Assignment page keeps scroll for many orders.)
//   2. Stat-card text no longer collides with icon — title at
//      y=70, value at y=94.
//   3. My Assignments shows only real "no assignments" message;
//      removed spurious default message.
//   4. My Profile: photo upload card + info grid + account
//      info block + Change Password card (fully visible).
//   5. Change Password uses UserRepository.UpdatePassword().
//
//  THEME / STYLE : Unchanged from original.
//  PROFILE PHOTO : Loaded from path at runtime — NOT stored in DB.
// =============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using OptiRoute.Core.Data;
using OptiRoute.Core.Models;

namespace OptiRoute.Forms
{
    public class DriverDashboardForm : Form
    {
        // ── Repositories ─────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();
        private readonly DriverRepository _driverRepo = new DriverRepository();

        // ── Driver model ─────────────────────────────────────────
        private Driver _driver = null!;

        // ── Profile photo (in-memory / path, not stored in DB) ───
        private string _profilePhotoPath = "";
        private Image? _profilePhoto = null;

        // ── Layout panels ────────────────────────────────────────
        private Panel sidePanel = null!;
        private Panel mainPanel = null!;
        private Panel headerPanel = null!;

        // ── Sidebar controls ─────────────────────────────────────
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

        // ── Content panels ───────────────────────────────────────
        private Panel pnlDashboard = null!;
        private Panel pnlAssignments = null!;
        private Panel pnlActive = null!;
        private Panel pnlHistory = null!;
        private Panel pnlProfile = null!;

        // ── Profile page avatar display panel ────────────────────
        private Panel pnlProfileAvatar = null!;

        // ── Colours (identical to original) ──────────────────────
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

            BuildLayout();
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

                // Resize all content pages to fill mainPanel
                foreach (Control c in mainPanel.Controls)
                    if (c is Panel p)
                    {
                        p.Size = mainPanel.Size;
                    }

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

            // Driver avatar — Orange accent
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
                {
                    e.Graphics.DrawImage(_profilePhoto, picAvatar.ClientRectangle);
                }
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

            btnDashboard.Click += (s, e) => ShowPanel(pnlDashboard, btnDashboard);
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
        //  PAGE 1 — DASHBOARD HOME  (no scroll — fits viewport)
        // ═════════════════════════════════════════════════════════
        private void BuildDashboardPanel()
        {
            pnlDashboard = MakePage(autoScroll: false);
            int mLeft = 30, mWidth = 1060;

            // Welcome banner
            var banner = new Panel
            {
                Location = new Point(mLeft, 18),
                Size = new Size(mWidth, 80),
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
                new Font("Segoe UI", 15, FontStyle.Bold), Color.White, new Point(24, 12)));
            banner.Controls.Add(L("Check your assignments and manage your deliveries from here.",
                new Font("Segoe UI", 10f), Color.FromArgb(255, 230, 180), new Point(24, 48)));
            pnlDashboard.Controls.Add(banner);

            // ── Stat cards — FIX: title at y=70, value at y=94 ──
            var stats = _driverRepo.GetDriverStats(_driver.UserID);
            int cW = 248, cH = 128, cTop = 114, gap = 16;
            StatCard(pnlDashboard, mLeft, cTop, "📋", "Assigned", stats.Assigned.ToString(), RoyalBlue, cW, cH);
            StatCard(pnlDashboard, mLeft + (cW + gap), cTop, "✅", "Delivered", stats.Delivered.ToString(), GreenOk, cW, cH);
            StatCard(pnlDashboard, mLeft + (cW + gap) * 2, cTop, "⏳", "Pending Pickup", stats.Pending.ToString(), OrangeWarn, cW, cH);
            StatCard(pnlDashboard, mLeft + (cW + gap) * 3, cTop, "↩️", "Returned", stats.Returned.ToString(), RedAlert, cW, cH);

            // ── Vehicle & Fuel card ──
            int row2Y = cTop + cH + 16;
            var vCard = Card(mLeft, row2Y, 490, 152);
            pnlDashboard.Controls.Add(vCard);
            vCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(490, 5), BackColor = OrangeWarn });
            vCard.Controls.Add(L("🚗  Vehicle Info", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            vCard.Controls.Add(L("Number Plate:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 46)));
            vCard.Controls.Add(L(_driver.PlateNumber, new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(20, 62)));
            vCard.Controls.Add(L("Type:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(260, 46)));
            vCard.Controls.Add(L(_driver.VehicleType, new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(260, 62)));
            vCard.Controls.Add(L("⛽  Fuel Level:", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 96)));

            double fuel = Math.Min(100, Math.Max(0, _driver.CurrentFuel));
            Color fuelColor = fuel > 60 ? GreenOk : fuel > 25 ? OrangeWarn : RedAlert;
            var fuelTrack = new Panel { Location = new Point(20, 114), Size = new Size(430, 14), BackColor = Color.FromArgb(220, 228, 245) };
            fuelTrack.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; fuelTrack.Region = new Region(RndPath(fuelTrack.ClientRectangle, 7)); };
            var fuelFill = new Panel { Location = new Point(0, 0), Size = new Size((int)(430 * fuel / 100.0), 14), BackColor = fuelColor };
            fuelFill.Paint += (s, e) => { if (fuelFill.Width > 7) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; fuelFill.Region = new Region(RndPath(fuelFill.ClientRectangle, 7)); } };
            fuelTrack.Controls.Add(fuelFill);
            vCard.Controls.Add(fuelTrack);
            vCard.Controls.Add(L(fuel + "%", new Font("Segoe UI", 9f, FontStyle.Bold), fuelColor, new Point(456, 111)));

            // ── Rating card ──
            var rCard = Card(mLeft + 506, row2Y, 570, 152);
            pnlDashboard.Controls.Add(rCard);
            rCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(570, 5), BackColor = Color.Gold });
            rCard.Controls.Add(L("⭐  My Rating & Performance", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            double avg = _driver.AverageRating;
            rCard.Controls.Add(L(avg.ToString("0.0") + " / 5.0", new Font("Segoe UI", 26, FontStyle.Bold), OrangeWarn, new Point(20, 42)));
            int fullS = (int)Math.Round(avg);
            string stars = new string('★', fullS) + new string('☆', 5 - fullS);
            rCard.Controls.Add(L(stars, new Font("Segoe UI", 17), Color.Gold, new Point(20, 92)));
            rCard.Controls.Add(L("Based on " + stats.TotalRatings + " ratings", new Font("Segoe UI", 9.5f), TextGray, new Point(210, 50)));
            rCard.Controls.Add(L("Total delivered: " + stats.Delivered, new Font("Segoe UI", 9.5f), TextGray, new Point(210, 72)));

            // ── Recent Assignments table ──
            int tblTop = row2Y + 152 + 14;
            int remainH = Math.Max(200, mainPanel.Height - tblTop - 14);
            var tbl = Card(mLeft, tblTop, mWidth, remainH);
            pnlDashboard.Controls.Add(tbl);
            tbl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            tbl.Controls.Add(L("📋  Recent Assignments", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 12)));

            string[] hdrs = { "Order ID", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Status", "Fare" };
            int[] wids = { 90, 130, 80, 100, 180, 210, 130, 130 };
            int hx = 16;
            foreach (var (h, w) in Zip(hdrs, wids))
            {
                tbl.Controls.Add(new Label
                {
                    Text = h,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = TextGray,
                    BackColor = CardBg,
                    Size = new Size(w, 30),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(hx, 46),
                    Padding = new Padding(4, 0, 0, 0)
                });
                hx += w;
            }

            int rowY = 82; bool alt = false;
            foreach (Order o in _driverRepo.GetRecentAssignments(_driver.UserID, 6))
            {
                string[] row = { "#"+o.OrderID, o.ItemName, o.WeightDisplay, o.Priority,
                                  o.PickupPoint, o.DeliveryPoint, o.OrderStatus, o.FormattedFare };
                Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White; alt = !alt;
                int rx = 16;
                foreach (var (cell, w) in Zip(row, wids))
                {
                    Color fg =
                        cell == "Picked" ? OrangeWarn :
                        cell == "Delivered" ? GreenOk :
                        cell == "Urgent" ? RedAlert :
                        cell == "Assigned" ? RoyalBlue : TextDark;
                    tbl.Controls.Add(new Label
                    {
                        Text = cell,
                        Font = new Font("Segoe UI", 9.5f),
                        ForeColor = fg,
                        BackColor = bg,
                        Size = new Size(w, 34),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Location = new Point(rx, rowY),
                        Padding = new Padding(4, 0, 0, 0)
                    });
                    rx += w;
                }
                rowY += 36;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 2 — MY ASSIGNMENTS  (keeps scroll for many orders)
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
            int cardH = 220, cardW = 1060, margin = 30, cardY = 70, gap = 18;
            int panelW = pnlAssignments.Width > 0 ? pnlAssignments.Width : 1280;
            int cardLeft = Math.Max(margin, (panelW - cardW) / 2);

            List<Order> orders = _driverRepo.GetAssignedOrders(_driver.UserID);

            // FIX: show clean empty state — no "place an order" message
            if (orders.Count == 0)
            {
                pnlAssignments.Controls.Add(L("No active assignments.", new Font("Segoe UI", 12f), TextGray, new Point(cardLeft, 80)));
                pnlAssignments.Controls.Add(L("The Admin will assign orders to you shortly.", new Font("Segoe UI", 10f), TextGray, new Point(cardLeft, 110)));
                return;
            }

            foreach (Order o in orders)
            {
                Color sc =
                    o.OrderStatus == "Delivered" ? GreenOk :
                    o.OrderStatus == "Picked" ? OrangeWarn :
                    o.OrderStatus == "Assigned" ? RoyalBlue : TextGray;

                var card = Card(cardLeft, cardY, cardW, cardH);
                pnlAssignments.Controls.Add(card);

                card.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(7, cardH), BackColor = sc });

                Color badgeBg =
                    o.OrderStatus == "Delivered" ? Color.FromArgb(30, 34, 197, 94) :
                    o.OrderStatus == "Picked" ? Color.FromArgb(30, 251, 146, 60) :
                    o.OrderStatus == "Assigned" ? Color.FromArgb(30, 0, 82, 204) :
                                                    Color.FromArgb(30, 120, 140, 170);
                string statusIcon =
                    o.OrderStatus == "Delivered" ? "✅ Delivered" :
                    o.OrderStatus == "Picked" ? "🚚 In Transit" :
                    o.OrderStatus == "Assigned" ? "🔵 Assigned" : "⏳ Pending";

                var badge = new Panel { Location = new Point(cardW - 170, 18), Size = new Size(148, 32), BackColor = badgeBg };
                badge.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; badge.Region = new Region(RndPath(badge.ClientRectangle, 8)); };
                badge.Controls.Add(new Label
                {
                    Text = statusIcon,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = sc,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(badge);

                card.Controls.Add(L("#" + o.OrderID, new Font("Segoe UI", 13, FontStyle.Bold), TextDark, new Point(24, 20)));
                card.Controls.Add(L("📅  " + o.FormattedDate, new Font("Segoe UI", 9.5f), TextGray, new Point(24, 50)));
                card.Controls.Add(L("📦  " + o.ItemName + "     ⚖  " + o.WeightDisplay, new Font("Segoe UI", 10.5f), TextDark, new Point(24, 78)));
                card.Controls.Add(L("💰  " + o.FormattedFare + "     💳  " + o.PaymentStatus, new Font("Segoe UI", 9.5f), TextGray, new Point(24, 106)));

                var pBadge = new Panel { Location = new Point(24, 134), Size = new Size(110, 28), BackColor = o.IsUrgent ? Color.FromArgb(30, 239, 68, 68) : Color.FromArgb(30, 34, 197, 94) };
                pBadge.Controls.Add(new Label
                {
                    Text = o.IsUrgent ? "⚡ URGENT" : "✓ Normal",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = o.IsUrgent ? RedAlert : GreenOk,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(pBadge);

                if (o.PaymentStatus == "Cash on Delivery" || o.PaymentStatus == "Unpaid")
                {
                    var cod = new Panel { Location = new Point(148, 134), Size = new Size(120, 28), BackColor = Color.FromArgb(30, 251, 146, 60) };
                    cod.Controls.Add(new Label
                    {
                        Text = "💵 Collect COD",
                        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                        ForeColor = OrangeWarn,
                        BackColor = Color.Transparent,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    });
                    card.Controls.Add(cod);
                }

                card.Controls.Add(L("🟢  Pick-up:   " + o.PickupPoint, new Font("Segoe UI", 10.5f), TextDark, new Point(420, 46)));
                card.Controls.Add(L("🔴  Delivery:  " + o.DeliveryPoint, new Font("Segoe UI", 10.5f), TextDark, new Point(420, 76)));
                card.Controls.Add(L("📍  Route calculated by Admin", new Font("Segoe UI", 9f), TextGray, new Point(420, 106)));

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
                btnPick.Click += (s, e) =>
                {
                    if (MessageBox.Show("Confirm picking up Order #" + capId + "?",
                        "Confirm Pickup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Picked");
                        if (ok) { MessageBox.Show("✅  Order #" + capId + " marked Picked. Tracking started.", "Picked Up", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshAssignments(); }
                        else MessageBox.Show("❌  Could not update. Try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                btnDeliver.Click += (s, e) =>
                {
                    if (isCOD)
                    {
                        var res = MessageBox.Show("💵  COD Order!\n\nHave you collected " + fareStr + " cash?",
                            "COD Collection", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (res != DialogResult.Yes) return;
                    }
                    if (MessageBox.Show("Confirm delivery of Order #" + capId + "?",
                        "Confirm Delivery", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Delivered");
                        if (ok) { MessageBox.Show("🎉  Order #" + capId + " delivered!", "Delivered", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshAssignments(); }
                        else MessageBox.Show("❌  Could not update. Try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                card.Controls.Add(btnDeliver);

                if (o.IsUrgent && curStatus == "Picked")
                {
                    var urg = new Panel { Location = new Point(24, 174), Size = new Size(cardW - 50, 32), BackColor = Color.FromArgb(30, 239, 68, 68) };
                    urg.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; urg.Region = new Region(RndPath(urg.ClientRectangle, 8)); };
                    urg.Controls.Add(new Label
                    {
                        Text = "⚡  URGENT — Priority delivery. Observe traffic rules and speed limits.",
                        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                        ForeColor = RedAlert,
                        BackColor = Color.Transparent,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    });
                    card.Controls.Add(urg);
                }

                cardY += cardH + gap;
            }
            pnlAssignments.AutoScrollMinSize = new Size(0, cardY + 40);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 3 — ACTIVE DELIVERY  (no scroll)
        // ═════════════════════════════════════════════════════════
        private void BuildActiveDeliveryPanel()
        {
            pnlActive = MakePage(autoScroll: false);
            pnlActive.Controls.Add(PageH("🚚  Active Delivery"));
            LoadActiveDelivery();
        }

        private void RefreshActiveDelivery()
        {
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
                var nc = Card(30, 70, 860, 100);
                pnlActive.Controls.Add(nc);
                nc.Controls.Add(L("🟢  No active delivery right now.", new Font("Segoe UI", 13, FontStyle.Bold), GreenOk, new Point(30, 18)));
                nc.Controls.Add(L("Pick an assigned order from 'My Assignments' to start.", new Font("Segoe UI", 10.5f), TextGray, new Point(30, 52)));
                return;
            }

            int topY = 70;
            if (active.IsUrgent)
            {
                var urgPanel = new Panel { Location = new Point(30, 70), Size = new Size(1060, 44), BackColor = Color.Transparent };
                urgPanel.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    urgPanel.Region = new Region(RndPath(urgPanel.ClientRectangle, 10));
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(30, 239, 68, 68)), RndPath(urgPanel.ClientRectangle, 10));
                };
                urgPanel.Controls.Add(new Label
                {
                    Text = "⚡  URGENT DELIVERY — Handle with priority. Observe all traffic rules and speed limits.",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = RedAlert,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                pnlActive.Controls.Add(urgPanel);
                topY = 128;
            }

            var det = Card(30, topY, 680, 270);
            pnlActive.Controls.Add(det);
            det.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(6, 270), BackColor = OrangeWarn });
            det.Controls.Add(L("📦  Order Details", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            LabelPair(det, "Order ID", "#" + active.OrderID, 24, 50);
            LabelPair(det, "Item", active.ItemName, 24, 92);
            LabelPair(det, "Weight", active.WeightDisplay, 24, 134);
            LabelPair(det, "Priority", active.Priority, 350, 50);
            LabelPair(det, "Fare", active.FormattedFare, 350, 92);
            LabelPair(det, "Payment", active.PaymentStatus, 350, 134);
            LabelPair(det, "Date", active.FormattedDate, 24, 176);
            if (active.PaymentStatus == "Cash on Delivery" || active.PaymentStatus == "Unpaid")
            {
                det.Controls.Add(new Panel { Location = new Point(20, 218), Size = new Size(630, 1), BackColor = BorderBlue });
                det.Controls.Add(L("💵  COLLECT: " + active.FormattedFare + " cash on delivery.",
                    new Font("Segoe UI", 10f, FontStyle.Bold), OrangeWarn, new Point(20, 228)));
            }

            var route = Card(726, topY, 364, 270);
            pnlActive.Controls.Add(route);
            route.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(6, 270), BackColor = RoyalBlue });
            route.Controls.Add(L("📍  Route", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            route.Controls.Add(L("🟢  Pickup", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 50)));
            route.Controls.Add(L(active.PickupPoint, new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 68)));
            route.Controls.Add(new Panel { Location = new Point(20, 106), Size = new Size(2, 36), BackColor = BorderBlue });
            route.Controls.Add(L("🔴  Delivery", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 148)));
            route.Controls.Add(L(active.DeliveryPoint, new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(20, 166)));
            route.Controls.Add(L("Route calculated by Admin (Laptop B)", new Font("Segoe UI", 8.5f), TextGray, new Point(20, 226)));

            int telTop = topY + 286;
            var tel = Card(30, telTop, 1060, 132);
            pnlActive.Controls.Add(tel);
            tel.Controls.Add(L("📡  Live Telemetry", new Font("Segoe UI", 12, FontStyle.Bold), TextDark, new Point(20, 14)));
            double fuel = Math.Min(100, Math.Max(0, _driver.CurrentFuel));
            Color fc = fuel > 60 ? GreenOk : fuel > 25 ? OrangeWarn : RedAlert;
            tel.Controls.Add(L("⛽  Fuel Level", new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(20, 48)));
            var trk = new Panel { Location = new Point(20, 66), Size = new Size(300, 13), BackColor = Color.FromArgb(220, 228, 245) };
            trk.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; trk.Region = new Region(RndPath(trk.ClientRectangle, 6)); };
            var fll = new Panel { Location = new Point(0, 0), Size = new Size((int)(300 * fuel / 100.0), 13), BackColor = fc };
            fll.Paint += (s, e) => { if (fll.Width > 6) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; fll.Region = new Region(RndPath(fll.ClientRectangle, 6)); } };
            trk.Controls.Add(fll);
            tel.Controls.Add(trk);
            tel.Controls.Add(L(fuel + "% remaining", new Font("Segoe UI", 9.5f), fc, new Point(330, 63)));
            tel.Controls.Add(L("🔄  Telemetry updates sent every 5-10 seconds to Admin.", new Font("Segoe UI", 9.5f), TextGray, new Point(20, 94)));
            tel.Controls.Add(L("Last sync: " + DateTime.Now.ToString("hh:mm:ss tt"), new Font("Segoe UI", 9.5f), TextGray, new Point(20, 112)));

            int btnTop = telTop + 148;
            int capId = active.OrderID;
            bool isCOD = active.PaymentStatus == "Cash on Delivery" || active.PaymentStatus == "Unpaid";
            string fareStr = active.FormattedFare;

            var btnDeliver = new Button
            {
                Text = "✅  Mark as Delivered",
                Location = new Point(30, btnTop),
                Size = new Size(240, 48),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 34, 197, 94),
                ForeColor = GreenOk,
                Cursor = Cursors.Hand
            };
            btnDeliver.FlatAppearance.BorderSize = 0;
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
                    if (ok) { MessageBox.Show("🎉  Delivered! Customer will be notified.", "Delivered", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshActiveDelivery(); }
                    else MessageBox.Show("❌  Could not update. Try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            pnlActive.Controls.Add(btnDeliver);

            var btnFailed = new Button
            {
                Text = "↩️  Report Failed",
                Location = new Point(286, btnTop),
                Size = new Size(190, 48),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 239, 68, 68),
                ForeColor = RedAlert,
                Cursor = Cursors.Hand
            };
            btnFailed.FlatAppearance.BorderSize = 0;
            btnFailed.Click += (s, e) =>
            {
                if (MessageBox.Show("Report Order #" + capId + " as failed?\nAdmin will mark it Returned.",
                    "Report Failed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    bool ok = _driverRepo.UpdateOrderStatus(capId, _driver.UserID, "Returned");
                    if (ok) { MessageBox.Show("↩️  Order #" + capId + " reported. Admin notified.", "Reported", MessageBoxButtons.OK, MessageBoxIcon.Information); RefreshActiveDelivery(); }
                    else MessageBox.Show("❌  Could not update. Try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            pnlActive.Controls.Add(btnFailed);
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 4 — DELIVERY HISTORY  (no scroll, table fits)
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

            int tblH = Math.Max(220, 70 + history.Count * 40);
            var tbl = Card(30, 62, 1060, tblH);
            pnlHistory.Controls.Add(tbl);

            string[] hdrs = { "Order ID", "Item", "Weight", "Priority", "Pick-up", "Delivery", "Status", "Fare", "Rating", "Date" };
            int[] wids = { 80, 120, 70, 90, 160, 160, 110, 90, 80, 118 };

            int hx = 16;
            foreach (var (h, w) in Zip(hdrs, wids))
            {
                tbl.Controls.Add(new Label
                {
                    Text = h,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = TextGray,
                    BackColor = CardBg,
                    Size = new Size(w, 30),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(hx, 14),
                    Padding = new Padding(4, 0, 0, 0)
                });
                hx += w;
            }
            tbl.Controls.Add(new Panel { Location = new Point(16, 46), Size = new Size(1020, 1), BackColor = BorderBlue });

            int rowY = 52; bool alt = false;
            foreach (Order o in history)
            {
                string ratingStr = o.Rating > 0
                    ? new string('★', o.Rating) + new string('☆', 5 - o.Rating) : "—";
                string[] row = { "#"+o.OrderID, o.ItemName, o.WeightDisplay, o.Priority,
                                  o.PickupPoint, o.DeliveryPoint, o.OrderStatus, o.FormattedFare,
                                  ratingStr, o.FormattedDate };
                Color bg = alt ? Color.FromArgb(248, 251, 255) : Color.White; alt = !alt;
                int rx = 16;
                foreach (var (cell, w) in Zip(row, wids))
                {
                    Color fg =
                        cell == "Delivered" ? GreenOk :
                        cell == "Returned" ? RedAlert :
                        cell == "Urgent" ? RedAlert :
                        cell.StartsWith("★") ? Color.Gold : TextDark;
                    tbl.Controls.Add(new Label
                    {
                        Text = cell,
                        Font = new Font("Segoe UI", 9.5f),
                        ForeColor = fg,
                        BackColor = bg,
                        Size = new Size(w, 36),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Location = new Point(rx, rowY),
                        Padding = new Padding(4, 0, 0, 0)
                    });
                    rx += w;
                }
                rowY += 38;
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PAGE 5 — MY PROFILE  (no scroll, all cards visible)
        //
        //  Layout (matches Correct_one.jpeg):
        //   Row 1 (top):  [Avatar + photo card]   [Account Info card]
        //   Row 2 (mid):  [Personal Info card (full width)]
        //   Row 3 (bot):  [Vehicle Details card]  [Change Password card]
        // ═════════════════════════════════════════════════════════
        private void BuildProfilePanel()
        {
            pnlProfile = MakePage(autoScroll: false);
            pnlProfile.AutoScroll = true;
            pnlProfile.AutoScrollMinSize = new Size(1224, 730);
            pnlProfile.Controls.Add(PageH("👤  My Profile"));

            int leftX = 30, rightX = 490, cardGap = 14;
            int row1Y = 62, row1H = 195;
            int row2Y = row1Y + row1H + cardGap;
            int row2H = 215;
            int row3Y = row2Y + row2H + cardGap;
            int row3H = 185;
            int avatarW = 450, infoW = 700;
            int vCardW = 450, pwCardW = 700;

            // ────────────────────────────────────────────────────
            //  ROW 1 LEFT — Avatar / Photo Upload card
            // ────────────────────────────────────────────────────
            var avatarCard = Card(leftX, row1Y, avatarW, row1H);
            pnlProfile.Controls.Add(avatarCard);
            avatarCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(avatarW, 5), BackColor = OrangeWarn });

            // Avatar circle in profile page
            pnlProfileAvatar = new Panel
            {
                Size = new Size(100, 100),
                Location = new Point(24, 22),
                BackColor = OrangeWarn
            };
            pnlProfileAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pnlProfileAvatar.Region = new Region(RndPath(pnlProfileAvatar.ClientRectangle, 50));
                if (_profilePhoto != null)
                {
                    e.Graphics.DrawImage(_profilePhoto, pnlProfileAvatar.ClientRectangle);
                }
                else
                {
                    e.Graphics.FillEllipse(new SolidBrush(OrangeWarn), pnlProfileAvatar.ClientRectangle);
                    string init = _driver.Username.Length > 0 ? _driver.Username[0].ToString().ToUpper() : "D";
                    e.Graphics.DrawString(init, new Font("Segoe UI", 30, FontStyle.Bold), Brushes.White,
                        new RectangleF(0, 0, 100, 100), Centre());
                }
            };
            avatarCard.Controls.Add(pnlProfileAvatar);

            // Name / role next to avatar
            var lblPName = new Label
            {
                Text = _driver.FullName,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(140, 28)
            };
            avatarCard.Controls.Add(lblPName);
            avatarCard.Controls.Add(L("@" + _driver.Username, new Font("Segoe UI", 10f), TextGray, new Point(140, 58)));
            avatarCard.Controls.Add(L("Role:  " + _driver.Role, new Font("Segoe UI", 10f), TextGray, new Point(140, 80)));

            var activeChip = new Panel { Location = new Point(140, 102), Size = new Size(130, 26), BackColor = Color.FromArgb(30, 34, 197, 94) };
            activeChip.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; activeChip.Region = new Region(RndPath(activeChip.ClientRectangle, 8)); };
            activeChip.Controls.Add(new Label { Text = "● Active Account", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = GreenOk, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
            avatarCard.Controls.Add(activeChip);

            // Upload button
            var btnUpload = new Button
            {
                Text = "📷  Upload Photo",
                Location = new Point(24, 140),
                Size = new Size(360, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
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
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        _profilePhotoPath = dlg.FileName;
                        _profilePhoto?.Dispose();
                        _profilePhoto = Image.FromFile(_profilePhotoPath);
                        // Refresh both avatar panels
                        pnlProfileAvatar.Invalidate();
                        picAvatar.Invalidate();
                    }
                    catch
                    {
                        MessageBox.Show("Could not load image.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            };
            avatarCard.Controls.Add(btnUpload);

            // ────────────────────────────────────────────────────
            //  ROW 1 RIGHT — Account Info card
            // ────────────────────────────────────────────────────
            var infoCard = Card(rightX, row1Y, infoW, row1H);
            pnlProfile.Controls.Add(infoCard);
            infoCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(infoW, 5), BackColor = RoyalBlue });
            infoCard.Controls.Add(L("Account Information", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(22, 14)));

            LabelPair(infoCard, "Username", _driver.Username, 24, 48);
            LabelPair(infoCard, "Driver ID", _driver.UserID.ToString(), 320, 48);
            LabelPair(infoCard, "Email", _driver.Email, 24, 98);
            LabelPair(infoCard, "Phone", string.IsNullOrEmpty(_driver.Phone) ? "—" : _driver.Phone, 320, 98);
            LabelPair(infoCard, "License No.", _driver.LicenseNumber, 24, 148);
            LabelPair(infoCard, "Member Since", "May 2026", 320, 148);

            // ────────────────────────────────────────────────────
            //  ROW 2 — Personal Information (editable) card
            // ────────────────────────────────────────────────────
            var card = Card(leftX, row2Y, avatarW + cardGap + infoW, row2H);
            pnlProfile.Controls.Add(card);
            card.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(avatarW + cardGap + infoW, 5), BackColor = RoyalBlue });
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

            var btnSave = new Button
            {
                Text = "💾  Save Changes",
                Location = new Point(870, 120),
                Size = new Size(186, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                bool ok = _userRepo.UpdateProfile(_driver.UserID,
                    txtFN.Text.Trim(), txtLN.Text.Trim(),
                    txtEM.Text.Trim(), txtPH.Text.Trim());
                if (ok)
                {
                    _driver.FirstName = txtFN.Text.Trim();
                    _driver.LastName = txtLN.Text.Trim();
                    _driver.Email = txtEM.Text.Trim();
                    _driver.Phone = txtPH.Text.Trim();
                    lblDriverName.Text = _driver.FullName;
                    lblPName.Text = _driver.FullName;
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

            // ────────────────────────────────────────────────────
            //  ROW 3 LEFT — Vehicle Details card
            // ────────────────────────────────────────────────────
            var vCard = Card(leftX, row3Y, vCardW, row3H);
            pnlProfile.Controls.Add(vCard);
            vCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(vCardW, 5), BackColor = OrangeWarn });
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

            var btnSaveV = new Button
            {
                Text = "💾  Save Vehicle",
                Location = new Point(20, 130),
                Size = new Size(180, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = OrangeWarn,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSaveV.FlatAppearance.BorderSize = 0;
            btnSaveV.Click += (s, e) =>
            {
                bool ok = _driverRepo.UpdateVehicle(_driver.UserID,
                    txtPlate.Text.Trim(), cmbType.SelectedItem?.ToString() ?? "Bike");
                if (ok)
                {
                    _driver.PlateNumber = txtPlate.Text.Trim();
                    _driver.VehicleType = cmbType.SelectedItem?.ToString() ?? "Bike";
                    lblVS.ForeColor = GreenOk;
                    lblVS.Text = "✅  Vehicle info updated.";
                }
                else
                {
                    lblVS.ForeColor = RedAlert;
                    lblVS.Text = "❌  Update failed.";
                }
            };
            vCard.Controls.Add(btnSaveV);

            // ────────────────────────────────────────────────────
            //  ROW 3 RIGHT — Change Password card (FIXED LAYOUT)
            // ────────────────────────────────────────────────────
            var pwCard = Card(rightX, row3Y, pwCardW, row3H);
            pnlProfile.Controls.Add(pwCard);

            // Top border line
            pwCard.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(pwCardW, 5), BackColor = RoyalBlue });

            // 1. Fixed Title (Increased X and AutoSize check)
            var lblTitle = L("🔒 Change Password", new Font("Segoe UI", 11, FontStyle.Bold), TextDark, new Point(22, 14));
            lblTitle.AutoSize = true;
            pwCard.Controls.Add(lblTitle);

            // Define common spacing variables
            int labelY = 50;     // Labels ki height
            int inputY = 72;     // Textboxes ki height
            int columnGap = 195; // Ek column se doosre ka distance (180 width + 15 gap)

            // 2. Current Password
            var lblCur = L("Current Password", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(22, labelY));
            lblCur.AutoSize = true;
            pwCard.Controls.Add(lblCur);

            var txtCurPw = new TextBox
            {
                Location = new Point(22, inputY),
                Size = new Size(175, 30),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg,
                UseSystemPasswordChar = true
            };
            pwCard.Controls.Add(txtCurPw);

            // 3. New Password (X shifted to 212)
            var lblNew = L("New Password", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(212, labelY));
            lblNew.AutoSize = true;
            pwCard.Controls.Add(lblNew);

            var txtNewPw = new TextBox
            {
                Location = new Point(212, inputY),
                Size = new Size(175, 30),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg,
                UseSystemPasswordChar = true
            };
            pwCard.Controls.Add(txtNewPw);

            // 4. Confirm Password (X shifted to 402)
            var lblConf = L("Confirm Password", new Font("Segoe UI", 9f, FontStyle.Bold), TextGray, new Point(402, labelY));
            lblConf.AutoSize = true;
            pwCard.Controls.Add(lblConf);

            var txtConfPw = new TextBox
            {
                Location = new Point(402, inputY),
                Size = new Size(175, 30),
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = CardBg,
                UseSystemPasswordChar = true
            };
            pwCard.Controls.Add(txtConfPw);

            // 5. Status Label (Shifted down slightly)
            var lblPwStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = GreenOk,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(22, 115)
            };
            pwCard.Controls.Add(lblPwStatus);

            // 6. Update Button (Aligned with the right edge of the last textbox)
            var btnUpdate = new Button
            {
                Text = "Update",
                Location = new Point(469, 110), // Adjusted to align perfectly
                Size = new Size(108, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = RoyalBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.Click += (s, e) =>
            {
                lblPwStatus.Text = "";
                if (string.IsNullOrWhiteSpace(txtCurPw.Text) ||
                    string.IsNullOrWhiteSpace(txtNewPw.Text) ||
                    string.IsNullOrWhiteSpace(txtConfPw.Text))
                {
                    lblPwStatus.ForeColor = RedAlert;
                    lblPwStatus.Text = "❌  All fields are required.";
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
                string newHash = BCrypt.Net.BCrypt.HashPassword(txtNewPw.Text.Trim());
                var (success, error) = _userRepo.UpdatePassword(_driver.UserID, txtCurPw.Text.Trim(), newHash);
                if (success)
                {
                    lblPwStatus.ForeColor = GreenOk;
                    lblPwStatus.Text = "✅  Password updated successfully.";
                    txtCurPw.Clear(); txtNewPw.Clear(); txtConfPw.Clear();
                }
                else
                {
                    lblPwStatus.ForeColor = RedAlert;
                    lblPwStatus.Text = "❌  " + error;
                }
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

        // FIX: autoScroll parameter — only assignments page uses scroll
        private Panel MakePage(bool autoScroll = false)
        {
            var p = new Panel
            {
                Location = new Point(0, 0),
                Size = mainPanel.Size,
                BackColor = PageBg,
                Visible = false,
                AutoScroll = autoScroll

            };

            if (autoScroll) p.AutoScrollMinSize = new Size(0, 1400);

            mainPanel.Controls.Add(p);

            return p;

        }



        private Panel Card(int x, int y, int w, int h)

        {

            var c = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.White };

            c.Paint += (s, e) =>

            {

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var path = RndPath(c.ClientRectangle, 12);

                e.Graphics.FillPath(new SolidBrush(Color.White), path);

                c.Region = new Region(path);

            };

            return c;

        }



        // FIX: title at y=70, value at y=94 — no collision with icon at y=18

        private void StatCard(Panel parent, int x, int y, string icon, string title, string value, Color accent, int w, int h)

        {

            var c = Card(x, y, w, h);
            c.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(w, 5), BackColor = accent });
            c.Controls.Add(L(icon, new Font("Segoe UI", 22), accent, new Point(16, 16)));
            c.Controls.Add(L(title, new Font("Segoe UI", 10f), TextGray, new Point(16, 70)));
            c.Controls.Add(L(value, new Font("Segoe UI", 24, FontStyle.Bold), accent, new Point(16, 90)));
            parent.Controls.Add(c);
        }

        private void LabelPair(Panel parent, string label, string value, int x, int y)
        {
            parent.Controls.Add(L(label, new Font("Segoe UI", 9.5f, FontStyle.Bold), TextGray, new Point(x, y)));
            parent.Controls.Add(L(value, new Font("Segoe UI", 11f, FontStyle.Bold), TextDark, new Point(x, y + 20)));
        }

        private static Label L(string text, Font font, Color color, Point loc) => new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = loc
        };

        private static Label PageH(string text) => new Label
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

        private static StringFormat Centre() => new StringFormat
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