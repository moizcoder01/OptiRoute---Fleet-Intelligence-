// =============================================================
//  OptiRoute  |  Forms/DriverVehicleForm.cs
//
//  Step 2 of Driver registration.
//  Called from SignUpForm after personal details are validated.
//  Collects: License Number, Vehicle Plate Number, Vehicle Type.
//  On success registers the driver + vehicle and opens LoginForm.
//
//  Visual style: identical to SignUpForm (same gradient, same
//  input field helper, same brand panel on the right).
//
//  Namespace : OptiRoute.Forms
// =============================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OptiRoute.Core.Data;

namespace OptiRoute.Forms
{
    public class DriverVehicleForm : Form
    {
        // ── Repository ────────────────────────────────────────────
        private readonly UserRepository _userRepo = new UserRepository();

        // ── Personal details passed from SignUpForm ───────────────
        private readonly string _firstName;
        private readonly string _lastName;
        private readonly string _username;
        private readonly string _hashedPassword;
        private readonly string _email;
        private readonly string _phone;

        // ── Controls ──────────────────────────────────────────────
        private Panel leftPanel = null!;
        private Panel rightPanel = null!;
        private TextBox txtLicense = null!;
        private TextBox txtPlate = null!;
        private ComboBox cmbVehicleType = null!;
        private Button btnRegister = null!;
        private Button btnBack = null!;
        private PictureBox picLogo = null!;

        // ── Palette (identical to SignUpForm) ─────────────────────
        private readonly Color Navy = Color.FromArgb(10, 35, 90);
        private readonly Color RoyalBlue = Color.FromArgb(0, 82, 204);
        private readonly Color TextSub = Color.FromArgb(190, 220, 255);
        private readonly Color TextDark = Color.FromArgb(40, 80, 160);

        // ── Constructor ───────────────────────────────────────────
        public DriverVehicleForm(
            string firstName, string lastName, string username,
            string hashedPassword, string email, string phone)
        {
            _firstName = firstName;
            _lastName = lastName;
            _username = username;
            _hashedPassword = hashedPassword;
            _email = email;
            _phone = phone;

            Text = "OptiRoute  |  Driver — Vehicle Details";
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
            BuildVehicleForm();

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

        // ── LEFT PANEL — VEHICLE FORM ─────────────────────────────
        private void BuildVehicleForm()
        {
            // Title
            leftPanel.Controls.Add(new Label
            {
                Text = "Vehicle Details",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(50, 28),
                BackColor = Color.Transparent
            });

            // Underline bar
            leftPanel.Controls.Add(new Panel
            {
                Location = new Point(50, 88),
                Size = new Size(55, 3),
                BackColor = Color.White
            });

            // Subtitle
            leftPanel.Controls.Add(new Label
            {
                Text = "Step 2 of 2  —  Enter your vehicle information",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = TextSub,
                AutoSize = true,
                Location = new Point(50, 98),
                BackColor = Color.Transparent
            });

            // ── Field: License Number ─────────────────────────────
            FLabel("License Number", 140, 50);
            txtLicense = FInput(164, 50, 460, false);

            // ── Field: Vehicle Plate Number ───────────────────────
            FLabel("Vehicle Plate Number", 228, 50);
            txtPlate = FInput(252, 50, 460, false);

            // ── Field: Vehicle Type dropdown ──────────────────────
            FLabel("Vehicle Type", 316, 50);
            BuildVehicleTypeDropdown(340);

            // ── Register button ───────────────────────────────────
            btnRegister = new Button
            {
                Text = "COMPLETE REGISTRATION  →",
                Location = new Point(50, 448),
                Size = new Size(320, 48),
                BackColor = Color.White,
                ForeColor = RoyalBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnRegister.FlatAppearance.BorderSize = 0;
            btnRegister.Paint += BtnPaint;
            btnRegister.Click += BtnRegister_Click;
            btnRegister.MouseEnter += (s, e) =>
            {
                btnRegister.BackColor = Color.FromArgb(220, 235, 255);
                btnRegister.ForeColor = Navy;
            };
            btnRegister.MouseLeave += (s, e) =>
            {
                btnRegister.BackColor = Color.White;
                btnRegister.ForeColor = RoyalBlue;
            };
            leftPanel.Controls.Add(btnRegister);

            // ── Back button ───────────────────────────────────────
            btnBack = new Button
            {
                Text = "← Back to Signup",
                Location = new Point(385, 458),
                Size = new Size(165, 35),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(100, 255, 255, 255);
            btnBack.Click += (s, e) => { new SignUpForm().Show(); Close(); };
            leftPanel.Controls.Add(btnBack);

            // Footer note
            leftPanel.Controls.Add(new Label
            {
                Text = "🔐   OptiRoute v1.0  |  Secure Registration",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(120, 180, 255),
                Size = new Size(540, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(10, 520),
                BackColor = Color.Transparent
            });
        }

        // ── VEHICLE TYPE DROPDOWN ─────────────────────────────────
        private void BuildVehicleTypeDropdown(int top)
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

            cmbVehicleType = new ComboBox
            {
                Location = new Point(10, 4),
                Width = 440,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f),
                BackColor = Color.FromArgb(40, 80, 160),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            cmbVehicleType.Items.AddRange(new object[] { "Bike", "Car", "Van", "Truck" });
            cmbVehicleType.SelectedIndex = 0;

            cmbVehicleType.Enter += (s, e) => { focused = true; box.Invalidate(); };
            cmbVehicleType.Leave += (s, e) => { focused = false; box.Invalidate(); };
            cmbVehicleType.DropDown += (s, e) =>
            {
                cmbVehicleType.BackColor = Color.FromArgb(20, 50, 130);
            };
            cmbVehicleType.DropDownClosed += (s, e) =>
            {
                cmbVehicleType.BackColor = Color.FromArgb(40, 80, 160);
            };

            box.Controls.Add(cmbVehicleType);
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
                    @"C:\Users\HP\Downloads\Project logo.png");
            }
            catch
            {
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

            // Step-by-step guide for vehicle info
            AddStep("①  Enter your driving license number", 458);
            AddStep("②  Enter your vehicle plate number", 492);
            AddStep("③  Select your vehicle type", 526);
            AddStep("④  Click Complete Registration", 560);

            // Info note
            rightPanel.Controls.Add(new Label
            {
                Text = "ℹ  Your vehicle info can be updated from your profile.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(150, 170, 210),
                AutoSize = true,
                Location = new Point(42, 610),
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

        // ── REGISTER LOGIC ────────────────────────────────────────
        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            string license = txtLicense.Text.Trim();
            string plate = txtPlate.Text.Trim();
            string vtype = cmbVehicleType.SelectedItem?.ToString() ?? "Bike";

            // ── 1. License required ───────────────────────────────
            if (string.IsNullOrWhiteSpace(license))
            {
                ShowWarning("Please enter your license number.", "Incomplete Form");
                txtLicense.Focus();
                return;
            }

            // ── 2. Plate required ─────────────────────────────────
            if (string.IsNullOrWhiteSpace(plate))
            {
                ShowWarning("Please enter your vehicle plate number.", "Incomplete Form");
                txtPlate.Focus();
                return;
            }

            // ── 3. Register in DB ─────────────────────────────────
            int newID = _userRepo.RegisterDriver(
                _firstName, _lastName, _username,
                _hashedPassword, _email, _phone,
                license, plate, vtype);

            // ── 4. Result ─────────────────────────────────────────
            if (newID > 0)
            {
                MessageBox.Show(
                    "✅  Driver account created successfully!\nYou can now log in.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                new LoginForm().Show();
                Close();
            }
            else
            {
                ShowError("Registration failed. Please try again.\n" +
                          "The username or plate number may already be in use.",
                          "Registration Error");
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

            // Navy → RoyalBlue gradient (identical to SignUpForm)
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
