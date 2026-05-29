// =============================================================
//  OptiRoute  |  Forms/SignUpForm.cs
//
//  Roles available in signup: Customer, Driver
//
//  ┌─ WHY ADMIN IS NOT HERE ───────────────────────────────────┐
//  │  Admin is a PRESET account created directly in the DB.    │
//  │  Self-registration as Admin would be a security hole —    │
//  │  anyone could grant themselves full system access.        │
//  │  The Admin account is seeded in the SQL schema:           │
//  │    Username : admin                                       │
//  │    Password : Admin@123   (change after first login)      │
//  │  Admin simply logs in — no signup needed.                 │
//  └───────────────────────────────────────────────────────────┘
//
//  Logo path : C:\Users\dell\Downloads\optiroute_logo.jpeg
//  Namespace : OptiRoute.Forms
// =============================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using BC = BCrypt.Net.BCrypt;
using OptiRoute.Core.Data;

namespace OptiRoute.Forms
{
    public class SignUpForm : Form
    {
        // ── Repository ────────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();

        // ── Controls ──────────────────────────────────────────────
        private Panel leftPanel = null!;
        private Panel rightPanel = null!;
        private TextBox txtFirstName = null!;
        private TextBox txtLastName = null!;
        private TextBox txtEmail = null!;
        private TextBox txtPhone = null!;
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private TextBox txtConfirmPassword = null!;
        private ComboBox cmbRole = null!;
        private Button btnSignUp = null!;
        private Button btnBack = null!;
        private CheckBox chkShowPassword = null!;
        private Label lblTitle = null!;
        private PictureBox picLogo = null!;

        // ── Palette ───────────────────────────────────────────────
        private readonly Color Navy = Color.FromArgb(10, 35, 90);
        private readonly Color RoyalBlue = Color.FromArgb(0, 82, 204);
        private readonly Color TextSub = Color.FromArgb(190, 220, 255);
        private readonly Color TextDark = Color.FromArgb(40, 80, 160);

        // ── Constructor ───────────────────────────────────────────
        public SignUpForm()
        {
            Text = "OptiRoute  |  Create Account";
            Size = new Size(980, 860);
            MinimumSize = new Size(900, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
        }

        // ── LAYOUT ────────────────────────────────────────────────
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
            BuildSignUpForm();

            rightPanel = new Panel
            {
                Location = new Point(560, 0),
                Size = new Size(420, ClientSize.Height),
                BackColor = Color.White
            };
            Controls.Add(rightPanel);
            BuildBrandPanel();

            Resize += (s, e) => 
            {
                leftPanel.Size = new Size(560, ClientSize.Height);
                rightPanel.Size = new Size(420, ClientSize.Height);
                leftPanel.Invalidate(); 
            }; 
        }

        // ── LEFT PANEL — SIGN UP FORM ─────────────────────────────
        private void BuildSignUpForm()
        {
            lblTitle = new Label
            {
                Text = "Create Account",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(50, 28),
                BackColor = Color.Transparent
            };
            leftPanel.Controls.Add(lblTitle);

            
            leftPanel.Controls.Add(new Panel
            {
                Location = new Point(50, 88),
                Size = new Size(55, 3),
                BackColor = Color.White
            });

            // Row 1 — First Name | Last Name
            FLabel("First Name", 108, 50);
            txtFirstName = FInput(132, 50, 230, false);

            FLabel("Last Name", 108, 295);
            txtLastName = FInput(132, 295, 215, false);

            // Row 2 — Email (full width)
            FLabel("Email Address", 196, 50);
            txtEmail = FInputFull(220, false);

            // Row 3 — Phone (full width)
            FLabel("Phone Number", 280, 50);
            txtPhone = FInputFull(304, false);

            // Row 4 — Username (full width)
            FLabel("Username", 364, 50);
            txtUsername = FInputFull(388, false);

            // Row 5 — Role (full width dropdown)
            FLabel("Role", 448, 50);
            BuildRoleDropdown(472);

            // Row 6 — Password | Confirm Password
            FLabel("Password", 528, 50);
            txtPassword = FInput(552, 50, 230, true);

            FLabel("Confirm Password", 528, 295);
            txtConfirmPassword = FInput(552, 295, 215, true);

            // Show passwords checkbox
            chkShowPassword = new CheckBox
            {
                Text = "Show Passwords",
                Location = new Point(50, 608),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSub,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            chkShowPassword.CheckedChanged += (s, e) =>
            {
                txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
                txtConfirmPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
            };
            leftPanel.Controls.Add(chkShowPassword);

            // Create Account button
            btnSignUp = new Button
            {
                Text = "CREATE ACCOUNT  →",
                Location = new Point(50, 640),
                Size = new Size(300, 48),
                BackColor = Color.White,
                ForeColor = RoyalBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnSignUp.FlatAppearance.BorderSize = 0;
            btnSignUp.Paint += BtnPaint;
            btnSignUp.Click += BtnSignUp_Click;
            btnSignUp.MouseEnter += (s, e) =>
            {
                btnSignUp.BackColor = Color.FromArgb(220, 235, 255);
                btnSignUp.ForeColor = Navy;
            };
            btnSignUp.MouseLeave += (s, e) =>
            {
                btnSignUp.BackColor = Color.White;
                btnSignUp.ForeColor = RoyalBlue;
            };
            leftPanel.Controls.Add(btnSignUp);

            // Back to login button
            btnBack = new Button
            {
                Text = "← Back to Login",
                Location = new Point(365, 650),
                Size = new Size(160, 35),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(100, 255, 255, 255);
            btnBack.Click += (s, e) => { new LoginForm().Show(); Close(); };
            leftPanel.Controls.Add(btnBack);

            // Footer note
            leftPanel.Controls.Add(new Label
            {
                Text = "🔐   OptiRoute v1.0  |  Secure Registration",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(120, 180, 255),
                Size = new Size(540, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(10, 704),
                BackColor = Color.Transparent
            });
        }

        // ── ROLE DROPDOWN ─────────────────────────────────────────
        // Admin is deliberately excluded — it is a preset DB account.
        private void BuildRoleDropdown(int top)
        {
            bool focused = false;

            var box = new Panel
            {
                Top = top,
                Left = 50,
                Size = new Size(460, 44),
                BackColor = Color.Transparent
            };

            box.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
                using var path = RndPath(rect, 8);
                using var fill = new SolidBrush(
                    focused ? Color.White : Color.FromArgb(40, 255, 255, 255));
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(
                    focused ? Color.White : Color.FromArgb(120, 255, 255, 255),
                    focused ? 2f : 1.5f);
                e.Graphics.DrawPath(pen, path);
                box.Region = new Region(path);
            };

            cmbRole = new ComboBox
            {
                Location = new Point(10, 4),
                Width = 440,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f),
                BackColor = Color.FromArgb(40, 80, 160),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Only Customer and Driver — Admin logs in directly (no self-registration)
            cmbRole.Items.AddRange(new object[] { "Select Role...", "Customer", "Driver" });
            cmbRole.SelectedIndex = 0;

            cmbRole.Enter += (s, e) => { focused = true; box.Invalidate(); };
            cmbRole.Leave += (s, e) => { focused = false; box.Invalidate(); };

            // Darken background when dropdown is active
            cmbRole.DropDown += (s, e) =>
            {
                cmbRole.BackColor = Color.FromArgb(20, 50, 130);
            };
            cmbRole.DropDownClosed += (s, e) =>
            {
                cmbRole.BackColor = Color.FromArgb(40, 80, 160);
            };

            box.Controls.Add(cmbRole);
            leftPanel.Controls.Add(box);
        }

        // ── RIGHT PANEL — BRANDING ────────────────────────────────
        private void BuildBrandPanel()
        {
            // Top accent bar
            rightPanel.Controls.Add(new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(420, 6),
                BackColor = RoyalBlue
            });

            // Logo
            picLogo = new PictureBox
            {
                Size = new Size(320, 320),
                Location = new Point(50, 60),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                picLogo.Image = Image.FromFile(
                    @"C:\Users\dell\Downloads\optiroute_logo.jpeg");
            }
            catch
            {
                // Fallback: draw "OR" initials in a blue circle
                var bmp = new Bitmap(280, 280);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(RoyalBlue), 10, 10, 260, 260);
                g.DrawString("OR",
                    new Font("Segoe UI", 40, FontStyle.Bold),
                    Brushes.White, 88, 100);
                picLogo.Image = bmp;
            }
            rightPanel.Controls.Add(picLogo);

            // Divider
            rightPanel.Controls.Add(new Panel
            {
                Location = new Point(80, 432),
                Size = new Size(260, 2),
                BackColor = Color.FromArgb(180, 210, 245)
            });

            // Step-by-step guide
            AddStep("①  Fill in your personal details", 458);
            AddStep("②  Enter your phone number", 492);
            AddStep("③  Choose a unique username", 526);
            AddStep("④  Select your role", 560);
            AddStep("⑤  Set a strong password", 594);
            AddStep("⑥  Click Create Account", 628);

            // Admin info note
            rightPanel.Controls.Add(new Label
            {
                Text = "ℹ  Admins log in directly — no signup required.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(150, 170, 210),
                AutoSize = true,
                Location = new Point(42, 672),
                BackColor = Color.Transparent
            });

            // Bottom accent bar
            rightPanel.Controls.Add(new Panel
            {
                Location = new Point(0, 716),
                Size = new Size(420, 6),
                BackColor = RoyalBlue
            });
        }

        private void AddStep(string text, int top)
        {
            rightPanel.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = TextDark,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(55, top)
            });
        }

        // ── SIGN UP LOGIC ─────────────────────────────────────────
        private void BtnSignUp_Click(object? sender, EventArgs e)
        {
            string firstName = txtFirstName.Text.Trim();
            string lastName = txtLastName.Text.Trim();
            string email = txtEmail.Text.Trim();
            string phone = txtPhone.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            // ── 1. All fields filled ──────────────────────────────
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ShowWarning("Please fill in all fields.", "Incomplete Form");
                return;
            }

            // ── 2. Valid phone ────────────────────────────────────
            if (phone.Length < 10 || phone.Length > 13 ||
                !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9\+\-]+$"))
            {
                ShowWarning("Please enter a valid phone number.\nExample: 03001234567",
                    "Invalid Phone");
                return;
            }

            // ── 3. Role selected ──────────────────────────────────
            if (cmbRole.SelectedIndex == 0)
            {
                ShowWarning("Please select a role.", "Role Required");
                return;
            }

            // ── 4. Valid email ────────────────────────────────────
            if (!email.Contains('@') || !email.Contains('.'))
            {
                ShowWarning("Please enter a valid email address.", "Invalid Email");
                return;
            }

            // ── 5. Password strength ──────────────────────────────
            if (password.Length < 6)
            {
                ShowWarning("Password must be at least 6 characters.", "Weak Password");
                return;
            }

            // ── 6. Passwords match ────────────────────────────────
            if (password != confirmPassword)
            {
                ShowError("Passwords do not match.", "Password Mismatch");
                return;
            }

            // ── 7. Username availability ──────────────────────────
            if (_userRepo.UsernameExists(username))
            {
                ShowError("Username already taken. Please choose another.", "Username Exists");
                return;
            }

            // ── 8. Hash & register ────────────────────────────────
            string hashedPassword = BC.HashPassword(password);
            string role = cmbRole.SelectedItem!.ToString()!;

            if (role == "Customer")
            {
                int newID = _userRepo.RegisterCustomer(
                    firstName, lastName, username,
                    hashedPassword, email, phone);

                // ── 9. Result (Customer) ──────────────────────────
                if (newID > 0)
                {
                    MessageBox.Show(
                        "✅  Account created successfully!\nYou can now log in.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    new LoginForm().Show();
                    Close();
                }
                else
                {
                    ShowError("Registration failed. Please try again.\n" +
                              "The username may already be in use.",
                              "Registration Error");
                }
                return;
            }
            else // Driver — open vehicle details form (step 2)
            {
                // Pass all personal details to the vehicle form.
                // The vehicle form handles the actual DB registration.
                var vehicleForm = new DriverVehicleForm(
                    firstName, lastName, username,
                    hashedPassword, email, phone);
                vehicleForm.Show();
                Close();
                return;
            }
        }

        // ── HELPERS — MESSAGES ────────────────────────────────────
        private static void ShowWarning(string msg, string title) =>
            MessageBox.Show("⚠️  " + msg, title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private static void ShowError(string msg, string title) =>
            MessageBox.Show("❌  " + msg, title,
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        // ── HELPERS — INPUT FIELDS ────────────────────────────────
        private void FLabel(string text, int top, int left)
        {
            leftPanel.Controls.Add(new Label
            {
                Text = text,
                Top = top,
                Left = left,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent
            });
        }

        private TextBox FInputFull(int top, bool isPass) =>
            FInput(top, 50, 460, isPass);

        private TextBox FInput(int top, int left, int width, bool isPass)
        {
            bool focused = false;

            var box = new Panel
            {
                Top = top,
                Left = left,
                Size = new Size(width, 44),
                BackColor = Color.Transparent
            };

            box.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
                using var path = RndPath(rect, 8);
                using var fill = new SolidBrush(
                    focused ? Color.White : Color.FromArgb(40, 255, 255, 255));
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(
                    focused ? Color.White : Color.FromArgb(120, 255, 255, 255),
                    focused ? 2f : 1.5f);
                e.Graphics.DrawPath(pen, path);
                box.Region = new Region(path);
            };

            var tb = new TextBox
            {
                Location = new Point(10, 11),
                Width = width - 22,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.FromArgb(40, 80, 160),
                ForeColor = Color.White,
                UseSystemPasswordChar = isPass
            };

            tb.Enter += (s, e) =>
            {
                focused = true;
                tb.BackColor = Color.White;
                tb.ForeColor = Color.FromArgb(10, 35, 90);
                box.Invalidate();
            };
            tb.Leave += (s, e) =>
            {
                focused = false;
                tb.BackColor = Color.FromArgb(40, 80, 160);
                tb.ForeColor = Color.White;
                box.Invalidate();
            };

            box.Controls.Add(tb);
            leftPanel.Controls.Add(box);
            return tb;
        }

        // ── PAINT EVENTS ──────────────────────────────────────────
        private void BtnPaint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, btn.Width, btn.Height);
            using var path = RndPath(rect, 10);
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

        private void LeftPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = leftPanel.ClientRectangle;

            // Navy → RoyalBlue gradient
            using var bg = new LinearGradientBrush(rect, Navy, RoyalBlue,
                LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(bg, rect);

            // Decorative circle — bottom-right
            using var c1 = new SolidBrush(Color.FromArgb(18, 255, 255, 255));
            g.FillEllipse(c1, 320, 380, 300, 300);

            // Decorative circle — top-left
            using var c2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255));
            g.FillEllipse(c2, -80, -80, 280, 280);

            // Dot grid (top-right)
            using var dot = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                    g.FillEllipse(dot, 380 + col * 22, 22 + row * 22, 7, 7);
        }

        // ── ROUNDED RECTANGLE PATH ────────────────────────────────
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
    } 
}
