// =============================================================
//  OptiRoute  |  Forms/LoginForm.cs
//  THEME: untouched — every colour, panel, paint event preserved.
//
//  FIX: right-panel top & bottom blue accent bars are now drawn
//       inside RightPanel_Paint so they are always flush with the
//       panel edges regardless of client-size rounding.
// =============================================================
using System;
using BC = BCrypt.Net.BCrypt;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OptiRoute.Core.Data;

namespace OptiRoute.Forms
{
    public class LoginForm : Form
    {
        // ── Repository ────────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();

        // ── Controls ──────────────────────────────────────────────
        private Panel leftPanel = null!;
        private Panel rightPanel = null!;
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private Button btnLogin = null!;
        private CheckBox chkShowPassword = null!;
        private PictureBox picLogo = null!;
        private LinkLabel lnkSignIn = null!;
        private Label lblLoginTitle = null!;
        private Label lblLoginSub = null!;

        // ── Colours ───────────────────────────────────────────────
        readonly Color Navy = Color.FromArgb(10, 35, 90);
        readonly Color RoyalBlue = Color.FromArgb(0, 82, 204);
        readonly Color SkyAccent = Color.FromArgb(0, 163, 255);
        readonly Color IceWhite = Color.FromArgb(220, 232, 248);
        readonly Color BorderBlue = Color.FromArgb(100, 160, 230);
        readonly Color TextLight = Color.White;
        readonly Color TextSub = Color.FromArgb(190, 220, 255);

        public LoginForm()
        {
            Text = "OptiRoute  |  Secure Login";
            Size = new Size(980, 640);
            MinimumSize = new Size(800, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
        }

        // ─────────────────────────────────────────────────────────
        //  LAYOUT
        // ─────────────────────────────────────────────────────────
        private void BuildLayout()
        {
            leftPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(560, ClientSize.Height),
                BackColor = Color.Transparent
            };
            leftPanel.Paint += LeftPanel_Paint;
            Controls.Add(leftPanel);
            BuildLoginForm();

            rightPanel = new Panel
            {
                Location = new Point(560, 0),
                Size = new Size(ClientSize.Width - 560, ClientSize.Height),
                BackColor = Color.White
            };
            // FIX: draw top & bottom blue bars in Paint so they are
            // always pixel-perfect flush with the panel edges
            rightPanel.Paint += RightPanel_Paint;
            Controls.Add(rightPanel);
            BuildBrandPanel();
        }

        // ─────────────────────────────────────────────────────────
        //  LEFT PANEL — LOGIN FORM
        // ─────────────────────────────────────────────────────────
        private void BuildLoginForm()
        {
            lblLoginTitle = new Label
            {
                Text = "Welcome Back",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(70, 80),
                BackColor = Color.Transparent
            };
            leftPanel.Controls.Add(lblLoginTitle);

            lblLoginSub = new Label
            {
                Text = "Sign in to continue to your dashboard",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextSub,
                AutoSize = true,
                Location = new Point(70, 130),
                BackColor = Color.Transparent
            };
            leftPanel.Controls.Add(lblLoginSub);

            leftPanel.Controls.Add(new Panel
            {
                Location = new Point(70, 155),
                Size = new Size(55, 3),
                BackColor = Color.White
            });

            AddFormLabel("Username", 195);
            txtUsername = AddFormInput(223, false);

            AddFormLabel("Password", 310);
            txtPassword = AddFormInput(338, true);

            chkShowPassword = new CheckBox
            {
                Text = "Show Password",
                Location = new Point(72, 397),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSub,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            chkShowPassword.CheckedChanged += (s, e) =>
                txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
            leftPanel.Controls.Add(chkShowPassword);

            btnLogin = new Button
            {
                Text = "SIGN  IN  →",
                Location = new Point(70, 445),
                Size = new Size(420, 52),
                BackColor = Color.White,
                ForeColor = RoyalBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 240, 255);
            btnLogin.Paint += BtnLogin_Paint;
            btnLogin.Click += BtnLogin_Click;
            btnLogin.MouseEnter += (s, e) => { btnLogin.BackColor = Color.FromArgb(230, 240, 255); btnLogin.ForeColor = Navy; };
            btnLogin.MouseLeave += (s, e) => { btnLogin.BackColor = Color.White; btnLogin.ForeColor = RoyalBlue; };
            leftPanel.Controls.Add(btnLogin);

            leftPanel.Controls.Add(new Panel
            {
                Location = new Point(70, 515),
                Size = new Size(420, 1),
                BackColor = Color.FromArgb(80, 255, 255, 255)
            });

            lnkSignIn = new LinkLabel
            {
                Text = "Not a Member?  Sign Up Here",
                Location = new Point(70, 520),
                Size = new Size(420, 28),
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.White,
                ActiveLinkColor = Color.FromArgb(180, 220, 255),
                BackColor = Color.Transparent
            };
            lnkSignIn.LinkClicked += (s, e) => { new SignUpForm().Show(); Hide(); };
            leftPanel.Controls.Add(lnkSignIn);
        }

        // ─────────────────────────────────────────────────────────
        //  RIGHT PANEL — BRANDING
        //  Top & bottom bars are painted in RightPanel_Paint below.
        //  No child Panel bars needed here — avoids off-by-one gaps.
        // ─────────────────────────────────────────────────────────
        private void BuildBrandPanel()
        {
            picLogo = new PictureBox
            {
                Size = new Size(320, 320),
                Location = new Point(45, 60),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                picLogo.Image = Image.FromFile(
                    @"C:\Users\HP\Downloads\Project logo.png");
            }
            catch
            {
                var bmp = new Bitmap(220, 220);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(RoyalBlue), 10, 10, 200, 200);
                g.DrawString("OR", new Font("Segoe UI", 36, FontStyle.Bold),
                    Brushes.White, 65, 75);
                picLogo.Image = bmp;
            }
            rightPanel.Controls.Add(picLogo);

            rightPanel.Controls.Add(new Panel
            {
                Location = new Point(80, 388),
                Size = new Size(260, 2),
                BackColor = BorderBlue
            });

            AddBullet("✦   Real-time Fleet Tracking", 415);
            AddBullet("✦   Smart Route Optimization", 447);
            AddBullet("✦   Driver Performance Analytics", 479);
        }

        // ─────────────────────────────────────────────────────────
        //  PAINT — right panel: white background + flush blue bars
        // ─────────────────────────────────────────────────────────
        private void RightPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            // White fill (panel BackColor is already White, but keep explicit)
            g.FillRectangle(Brushes.White, rightPanel.ClientRectangle);
            // Top bar — flush at y=0
            using var br = new SolidBrush(RoyalBlue);
            g.FillRectangle(br, 0, 0, rightPanel.Width, 6);
            // Bottom bar — flush at bottom edge
            g.FillRectangle(br, 0, rightPanel.Height - 6, rightPanel.Width, 6);
        }

        // ─────────────────────────────────────────────────────────
        //  LOGIN LOGIC
        // ─────────────────────────────────────────────────────────
        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Please enter both username and password.",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // ── 1. TEXTBOX SE INPUT ─────────────────────────
                string username = txtUsername.Text.Trim();
                string passwordInput = txtPassword.Text;

                string storedHash = "";
                string userRole = "";
                bool isValid = false;

                // ── 2. HARDCODED ADMIN BYPASS ──────────────────────────
                if (username == "admin" && passwordInput == "Admin@123")
                {
                    isValid = true; 
                    userRole = "Admin"; 
                }
                else
                {
                    var result = _userRepo.GetHashAndRole(username);
                    storedHash = result.Hash;
                    userRole = result.Role;

                    // Username check
                    if (string.IsNullOrEmpty(storedHash))
                    {
                        MessageBox.Show("❌ Username not found.",
                            "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Password verify via BCrypt
                    isValid = BC.Verify(passwordInput, storedHash);
                }

                // ── 3. FINAL VALIDATION CHECK ──────────────────────────
                if (!isValid)
                {
                    MessageBox.Show("❌ Incorrect password.",
                        "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Hide();
                if (userRole == "Driver")
                    new DriverDashboardForm(userRole, username).Show();
                else if (userRole == "Admin")
                    new AdminDashboardForm(username).Show();
                else
                    new DashboardForm(userRole, username).Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Error: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  PAINT HELPERS
        // ─────────────────────────────────────────────────────────
        private void BtnLogin_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, btn.Width, btn.Height);
            using var path = RoundedPath(rect, 10);
            using var fill = new SolidBrush(btn.BackColor);
            g.FillPath(fill, path);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using var textBrush = new SolidBrush(btn.ForeColor);
            g.DrawString(btn.Text, btn.Font, textBrush, rect, sf);
            btn.Region = new Region(path);
        }

        private void AddBullet(string text, int top)
        {
            rightPanel.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(40, 80, 160),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(55, top)
            });
        }

        private void AddFormLabel(string text, int top)
        {
            leftPanel.Controls.Add(new Label
            {
                Text = text,
                Top = top,
                Left = 70,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent
            });
        }

        private TextBox AddFormInput(int top, bool isPass)
        {
            bool isFocused = false;

            var box = new Panel
            {
                Top = top,
                Left = 70,
                Size = new Size(420, 48),
                BackColor = Color.Transparent
            };

            var tb = new TextBox
            {
                Location = new Point(14, 13),
                Width = 392,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11f),
                BackColor = Color.FromArgb(40, 80, 160),
                ForeColor = Color.White
            };
            if (isPass) tb.UseSystemPasswordChar = true;

            box.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
                using var path = RoundedPath(rect, 8);
                using var fillBrush = new SolidBrush(
                    isFocused
                        ? Color.FromArgb(255, 255, 255, 255)
                        : Color.FromArgb(40, 255, 255, 255));
                e.Graphics.FillPath(fillBrush, path);
                using var pen = new Pen(
                    isFocused ? Color.White : Color.FromArgb(120, 255, 255, 255),
                    isFocused ? 2f : 1.5f);
                e.Graphics.DrawPath(pen, path);
                box.Region = new Region(path);
            };

            tb.Enter += (s, e) =>
            {
                isFocused = true;
                tb.BackColor = Color.White;
                tb.ForeColor = Color.FromArgb(10, 35, 90);
                box.Invalidate();
            };
            tb.Leave += (s, e) =>
            {
                isFocused = false;
                tb.BackColor = Color.FromArgb(40, 80, 160);
                tb.ForeColor = Color.White;
                box.Invalidate();
            };

            box.Controls.Add(tb);
            leftPanel.Controls.Add(box);
            return tb;
        }

        private void LeftPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = leftPanel.ClientRectangle;

            using var bg = new LinearGradientBrush(rect, Navy, RoyalBlue, LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(bg, rect);

            using var c1 = new SolidBrush(Color.FromArgb(18, 255, 255, 255));
            g.FillEllipse(c1, 320, 380, 300, 300);

            using var c2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255));
            g.FillEllipse(c2, -80, -80, 280, 280); 

            using var dot = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                    g.FillEllipse(dot, 380 + col * 22, 22 + row * 22, 7, 7);
        }

        private static GraphicsPath RoundedPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}