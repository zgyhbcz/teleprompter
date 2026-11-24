using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TiCi
{
    /* ================= 底部进度条 ================= */
    class ProgressEx : Control
    {
        public double Pct = 0;
        public ProgressEx()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(30, 30, 30);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int w = ClientSize.Width, h = ClientSize.Height;
            int barH = 8, y = (h - barH) / 2;
            int txtW = 46;
            int bw = w - txtW - 8;
            if (bw < 20) return;

            using (SolidBrush b = new SolidBrush(Color.FromArgb(56, 56, 56)))
                g.FillRectangle(b, 0, y, bw, barH);

            int fw = (int)(bw * Math.Max(0, Math.Min(1, Pct)));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 182, 76)))
                g.FillRectangle(b, 0, y, fw, barH);

            using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 170, 170)))
            using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
                g.DrawString(((int)(Pct * 100)) + "%", f, b, new PointF(bw + 8, y - 4));
        }
    }

    /* ================= 预览区（同步显示 + 点击直接编辑） ================= */
    class PreviewPanel : Control
    {
        App app;
        TextBox ed;
        Button bSave, bCancel;
        bool editing = false;
        bool hover = false;

        static readonly Color C_FRAME_OUT = Color.FromArgb(14, 14, 14);
        static readonly Color C_BORDER = Color.FromArgb(60, 60, 60);
        static readonly Color C_ACC = Color.FromArgb(232, 182, 76);

        public bool Editing { get { return editing; } }

        public PreviewPanel(App a)
        {
            app = a;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = C_FRAME_OUT;
            Cursor = Cursors.IBeam;

            ed = new TextBox();
            ed.Multiline = true;
            ed.ScrollBars = ScrollBars.Vertical;
            ed.BorderStyle = BorderStyle.None;
            ed.BackColor = Color.FromArgb(22, 22, 22);
            ed.ForeColor = Color.FromArgb(238, 238, 238);
            ed.Font = new Font("Microsoft YaHei", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
            ed.AcceptsReturn = true;
            ed.Visible = false;
            ed.KeyDown += EdKeyDown;
            Controls.Add(ed);

            bSave = MkMini("保存并应用", true);
            bSave.Click += delegate { Commit(); };
            Controls.Add(bSave);

            bCancel = MkMini("取消", false);
            bCancel.Click += delegate { Cancel(); };
            Controls.Add(bCancel);
        }

        static Button MkMini(string t, bool accent)
        {
            Button b = new Button();
            b.Text = t;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent ? C_ACC : Color.FromArgb(70, 70, 70);
            b.BackColor = accent ? C_ACC : Color.FromArgb(44, 44, 44);
            b.ForeColor = accent ? Color.FromArgb(26, 18, 4) : Color.FromArgb(226, 226, 226);
            b.Font = new Font("Microsoft YaHei", 12f, accent ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            b.Size = new Size(accent ? 96 : 56, 28);
            b.Visible = false;
            b.TabStop = false;
            b.Cursor = Cursors.Hand;
            return b;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
            Invalidate();
        }

        void LayoutChildren()
        {
            int pad = 8;
            ed.SetBounds(pad, pad, Math.Max(40, ClientSize.Width - pad * 2),
                         Math.Max(40, ClientSize.Height - pad * 2 - 34));
            bSave.Location = new Point(ClientSize.Width - bSave.Width - 10, ClientSize.Height - bSave.Height - 8);
            bCancel.Location = new Point(bSave.Left - bCancel.Width - 8, bSave.Top);
        }

        public void EnterEdit()
        {
            if (editing) return;
            app.Pause();
            editing = true;
            LayoutChildren();
            ed.Text = (app.S.CurrentText ?? "").Replace("\n", "\r\n");
            ed.Visible = true;
            bSave.Visible = true;
            bCancel.Visible = true;
            ed.Focus();
            ed.SelectionStart = 0;
            ed.SelectionLength = 0;
            Invalidate();
        }
        public void Cancel()
        {
            editing = false;
            ed.Visible = false;
            bSave.Visible = false;
            bCancel.Visible = false;
            Invalidate();
        }

        public void Commit()
        {
            string raw = ed.Text.Replace("\r\n", "\n");
            if (raw.Trim().Length == 0) return;
            if (app.S.Cur >= 0 && app.S.Cur < app.S.Scripts.Count)
                app.S.Scripts[app.S.Cur].Raw = raw;
            else
            {
                app.S.Scripts.Add(new ScriptItem("手动输入 " + (app.S.Scripts.Count + 1), raw));
                app.S.Cur = app.S.Scripts.Count - 1;
            }
            app.S.Y = 0;
            app.Pause();
            app.Rebuild();
            Cancel();
            app.SaveData();
            if (app.Main != null) app.Main.RefreshUi();
        }

        void EdKeyDown(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Cancel(); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Enter) { Commit(); e.Handled = true; e.SuppressKeyPress = true; }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!editing) EnterEdit();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (editing) return;
            Graphics g = e.Graphics;
            g.Clear(C_FRAME_OUT);

            TextLayout L = app.Lay;
            if (L == null || ClientSize.Width < 40 || ClientSize.Height < 40) return;

            // 先按副屏 1:1 渲染成一帧位图（与输出窗口同一套绘制），再缩放着色。
            // 不能让文字直接走 GDI+ 的缩放/镜像变换 —— 那样会整片画不出来或错位。
            EnsureFrame(L.ViewW, L.ViewH);
            using (Graphics gf = Graphics.FromImage(frame))
            {
                gf.Clear(app.S.Bg);
                L.Paint(gf, app.S.Y, app.S.Fg);
            }

            float s = Math.Min(1f, (float)ClientSize.Width / L.ViewW);
            float dw = L.ViewW * s;
            float dh = L.ViewH * s;
            float dx = (ClientSize.Width - dw) / 2f;
            float dy = (ClientSize.Height - dh) / 2f;   // 负数=上下裁切，只显示中间带

            g.InterpolationMode = (Math.Abs(s - 1f) < 0.001f)
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            GraphicsState st = g.Save();
            if (app.S.MirrorH) { g.TranslateTransform(ClientSize.Width, 0); g.ScaleTransform(-1, 1); }
            if (app.S.MirrorV) { g.TranslateTransform(0, ClientSize.Height); g.ScaleTransform(1, -1); }
            g.DrawImage(frame, dx, dy, dw, dh);
            g.Restore(st);

            // 导播线（与副屏同位置，不参与镜像）
            if (app.S.Guide)
            {
                float gy = dy + 0.40f * dh;
                if (gy > -4 && gy < ClientSize.Height + 4)
                {
                    using (Pen p = new Pen(Color.FromArgb(200, 255, 59, 48), 1))
                        g.DrawLine(p, 0, gy, ClientSize.Width, gy);
                }
            }

            using (Pen p = new Pen(C_BORDER, 1))
                g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            // 空闲悬停提示
            if (!app.S.Playing && hover)
            {
                string tip = "点击可直接编辑文字";
                using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 190, 190)))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(210, 10, 10, 10)))
                {
                    SizeF tsz = g.MeasureString(tip, f);
                    RectangleF r = new RectangleF(ClientSize.Width - tsz.Width - 18, 8, tsz.Width + 12, tsz.Height + 4);
                    g.FillRectangle(bg, r);
                    g.DrawString(tip, f, b, r.X + 6, r.Y + 2);
                }
            }
        }

        Bitmap frame;
        int fw, fh;

        void EnsureFrame(int w, int h)
        {
            w = Math.Max(2, w); h = Math.Max(2, h);
            if (frame != null && fw == w && fh == h) return;
            if (frame != null) { frame.Dispose(); frame = null; }
            frame = new Bitmap(w, h);
            fw = w; fh = h;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && frame != null) { frame.Dispose(); frame = null; }
            base.Dispose(disposing);
        }
    }

    /* ================= 主窗口 ================= */
    class MainForm : Form
    {
        App app;
        bool suppress = false;

        FlowLayoutPanel flow;
        PreviewPanel preview;
        ProgressEx prog;
        Label lblStatus;

        Button btnOpen, btnPlay, btnTop, btnDel, btnOut, btnHelp, btnReset, btnTheme;
        Button chkBold, chkMirrorH, chkMirrorV, chkGuide;
        ComboBox cmbScripts, cmbScreen;
        TrackBar trkSpeed, trkFont, trkWidth, trkLh;
        Label lblSpeedV, lblFontV, lblWidthV, lblLhV;

        static readonly Color C_BG = Color.FromArgb(22, 22, 22);
        static readonly Color C_CTRL = Color.FromArgb(42, 42, 42);
        static readonly Color C_TXT = Color.FromArgb(228, 228, 228);
        static readonly Color C_SUB = Color.FromArgb(148, 148, 148);
        static readonly Color C_LINE = Color.FromArgb(58, 58, 58);
        static readonly Color C_ACC = Color.FromArgb(232, 182, 76);

        static readonly Font F_UI = new Font("Microsoft YaHei", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        static readonly Font F_UIB = new Font("Microsoft YaHei", 12f, FontStyle.Bold, GraphicsUnit.Pixel);

        public MainForm(App a)
        {
            app = a;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            Text = "提词器";
            BackColor = C_BG;
            ForeColor = C_TXT;
            Font = F_UI;
            KeyPreview = true;
            DoubleBuffered = true;
            MinimumSize = new Size(760, 460);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w = Math.Min(1460, wa.Width - 80);
            int h = Math.Min(880, wa.Height - 80);
            ClientSize = new Size(Math.Max(760, w), Math.Max(460, h));
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            LoadScreens();
        }

        /* ---------- 构造控件 ---------- */
        Button MkBtn(string text, bool accent, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent ? C_ACC : C_LINE;
            b.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(250, 205, 110) : Color.FromArgb(60, 60, 60);
            b.BackColor = accent ? C_ACC : C_CTRL;
            b.ForeColor = accent ? Color.FromArgb(26, 18, 4) : C_TXT;
            b.Font = accent ? F_UIB : F_UI;
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            b.AutoSize = false;
            int tw = TextRenderer.MeasureText(text, accent ? F_UIB : F_UI).Width;
            b.Size = new Size(tw + 26, 30);
            b.Margin = new Padding(0, 3, 8, 3);
            b.Click += onClick;
            return b;
        }

        Button MkToggle(string text, EventHandler onClick)
        {
            Button b = MkBtn(text, false, onClick);
            b.BackColor = Color.FromArgb(52, 52, 52);
            return b;
        }

        Panel MkSlider(string label, int min, int max, int val, int width, EventHandler onCh,
                       out TrackBar trk, out Label valLbl)
        {
            int lw = TextRenderer.MeasureText(label, F_UI).Width + 8;
            Panel p = new Panel();
            p.BackColor = Color.Transparent;
            p.Size = new Size(lw + width + 52, 34);
            p.Margin = new Padding(0, 1, 12, 1);

            Label l = new Label();
            l.Text = label; l.Font = F_UI; l.ForeColor = C_SUB;
            l.AutoSize = false; l.TextAlign = ContentAlignment.MiddleLeft;
            l.SetBounds(0, 8, lw, 18);
            p.Controls.Add(l);

            trk = new TrackBar();
            trk.Minimum = min; trk.Maximum = max;
            trk.Value = Math.Max(min, Math.Min(max, val));
            trk.TickStyle = TickStyle.None;
            trk.SmallChange = 1; trk.LargeChange = 1;
            trk.TabStop = false;
            trk.SetBounds(lw, 3, width, 26);
            trk.ValueChanged += onCh;
            p.Controls.Add(trk);

            valLbl = new Label();
            valLbl.Font = F_UI; valLbl.ForeColor = C_ACC;
            valLbl.AutoSize = false; valLbl.TextAlign = ContentAlignment.MiddleLeft;
            valLbl.SetBounds(lw + width + 6, 8, 44, 18);
            p.Controls.Add(valLbl);
            return p;
        }

        void BuildUi()
        {
            flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Top;
            flow.AutoSize = true;
            flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flow.WrapContents = true;
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.BackColor = Color.FromArgb(26, 26, 26);
            flow.Padding = new Padding(10, 5, 6, 5);
            flow.Height = 84;

            // ---- 稿件 ----
            btnOpen = MkBtn("打开", true, OnOpen);
            flow.Controls.Add(btnOpen);

            cmbScripts = new ComboBox();
            cmbScripts.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScripts.FlatStyle = FlatStyle.Flat;
            cmbScripts.Font = F_UI;
            cmbScripts.Size = new Size(200, 26);
            cmbScripts.Margin = new Padding(0, 5, 4, 3);
            cmbScripts.TabStop = false;
            cmbScripts.SelectedIndexChanged += OnScriptChanged;
            MakeDarkCombo(cmbScripts);
            flow.Controls.Add(cmbScripts);

            btnDel = MkBtn("×", false, OnDeleteScript);
            btnDel.Size = new Size(30, 30);
            flow.Controls.Add(btnDel);

            // ---- 播放 ----
            btnPlay = MkBtn("▶ 开始", true, OnPlay);
            flow.Controls.Add(btnPlay);
            btnTop = MkBtn("首行", false, OnTop);
            flow.Controls.Add(btnTop);

            // ---- 滑杆 ----
            Panel g;
            TrackBar t;
            Label v;
            g = MkSlider("速度", 1, 60, 12, 110, OnSpeed, out t, out v);
            trkSpeed = t; lblSpeedV = v; flow.Controls.Add(g);

            g = MkSlider("字号", 20, 140, 56, 110, OnFont, out t, out v);
            trkFont = t; lblFontV = v; flow.Controls.Add(g);

            g = MkSlider("宽度", 40, 100, 88, 90, OnWidth, out t, out v);
            trkWidth = t; lblWidthV = v; flow.Controls.Add(g);

            g = MkSlider("行距", 10, 30, 15, 90, OnLh, out t, out v);
            trkLh = t; lblLhV = v; flow.Controls.Add(g);

            // ---- 开关组（整体换行，不被拆散） ----
            FlowLayoutPanel grpT = new FlowLayoutPanel();
            grpT.AutoSize = true;
            grpT.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grpT.WrapContents = false;
            grpT.FlowDirection = FlowDirection.LeftToRight;
            grpT.Margin = new Padding(0, 0, 4, 0);
            grpT.BackColor = Color.FromArgb(26, 26, 26);

            chkBold = MkToggle("加粗", OnBold);
            chkBold.Margin = new Padding(0, 3, 5, 3);
            grpT.Controls.Add(chkBold);
            chkMirrorH = MkToggle("水平镜像", OnMirrorH);
            chkMirrorH.Margin = new Padding(0, 3, 5, 3);
            grpT.Controls.Add(chkMirrorH);
            chkMirrorV = MkToggle("垂直镜像", OnMirrorV);
            chkMirrorV.Margin = new Padding(0, 3, 5, 3);
            grpT.Controls.Add(chkMirrorV);
            chkGuide = MkToggle("导播线", OnGuide);
            chkGuide.Margin = new Padding(0, 3, 5, 3);
            grpT.Controls.Add(chkGuide);
            flow.Controls.Add(grpT);

            // ---- 副屏组 ----
            FlowLayoutPanel grpS = new FlowLayoutPanel();
            grpS.AutoSize = true;
            grpS.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grpS.WrapContents = false;
            grpS.FlowDirection = FlowDirection.LeftToRight;
            grpS.Margin = new Padding(0, 0, 4, 0);
            grpS.BackColor = Color.FromArgb(26, 26, 26);

            Label capScreen = new Label();
            capScreen.Text = "副屏";
            capScreen.Font = F_UI;
            capScreen.ForeColor = C_SUB;
            capScreen.AutoSize = true;
            capScreen.Margin = new Padding(0, 10, 6, 0);
            grpS.Controls.Add(capScreen);

            cmbScreen = new ComboBox();
            cmbScreen.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScreen.FlatStyle = FlatStyle.Flat;
            cmbScreen.Font = F_UI;
            cmbScreen.Size = new Size(176, 26);
            cmbScreen.Margin = new Padding(0, 6, 6, 3);
            cmbScreen.TabStop = false;
            cmbScreen.SelectedIndexChanged += OnScreenChanged;
            MakeDarkCombo(cmbScreen);
            grpS.Controls.Add(cmbScreen);

            btnOut = MkBtn("副屏全屏", false, OnToggleOutput);
            btnOut.Margin = new Padding(0, 3, 5, 3);
            grpS.Controls.Add(btnOut);
            flow.Controls.Add(grpS);

            // ---- 其他 ----
            btnTheme = MkBtn("黑色", false, OnTheme);
            flow.Controls.Add(btnTheme);
            btnReset = MkBtn("恢复默认", false, OnReset);
            flow.Controls.Add(btnReset);
            btnHelp = MkBtn("帮助", false, OnHelp);
            flow.Controls.Add(btnHelp);

            // ---- 预览区 ----
            preview = new PreviewPanel(app);
            preview.Dock = DockStyle.Fill;
            preview.Margin = new Padding(0);

            // ---- 底栏 ----
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 30;
            bottom.BackColor = Color.FromArgb(30, 30, 30);
            bottom.Padding = new Padding(10, 0, 10, 0);

            prog = new ProgressEx();
            prog.Dock = DockStyle.Fill;
            bottom.Controls.Add(prog);

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Left;
            lblStatus.Width = 210;
            lblStatus.Font = new Font("Microsoft YaHei", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
            lblStatus.ForeColor = C_SUB;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.BackColor = Color.FromArgb(30, 30, 30);
            bottom.Controls.Add(lblStatus);

            // 停靠顺序：先 Fill，再 Top/Bottom
            Controls.Add(preview);
            Controls.Add(bottom);
            Controls.Add(flow);
        }

        void MakeDarkCombo(ComboBox cb)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = 20;
            cb.DrawItem += DarkComboDraw;
        }

        void DarkComboDraw(object sender, DrawItemEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush bg = new SolidBrush(sel ? Color.FromArgb(74, 74, 74) : C_CTRL))
                e.Graphics.FillRectangle(bg, e.Bounds);
            string t = (e.Index >= 0) ? Convert.ToString(cb.Items[e.Index]) : cb.Text;
            if (!string.IsNullOrEmpty(t))
            {
                using (SolidBrush fg = new SolidBrush(C_TXT))
                using (StringFormat sf = new StringFormat())
                {
                    sf.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(t, cb.Font, fg,
                        new RectangleF(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), sf);
                }
            }
        }

        void LoadScreens()
        {
            suppress = true;
            cmbScreen.Items.Clear();
            Screen[] all = Screen.AllScreens;
            int sel = 0;
            for (int i = 0; i < all.Length; i++)
            {
                cmbScreen.Items.Add(string.Format("显示器{0} {1}×{2}{3}",
                    i + 1, all[i].Bounds.Width, all[i].Bounds.Height, all[i].Primary ? " 主" : ""));
                if (app.S.ScreenIdx == i) sel = i;
            }
            if (app.S.ScreenIdx < 0)
                for (int i = 0; i < all.Length; i++) if (!all[i].Primary) { sel = i; break; }
            app.S.ScreenIdx = sel;
            cmbScreen.SelectedIndex = sel;
            suppress = false;
        }

        public int ToolbarHeight { get { return (flow == null) ? 0 : flow.Height; } }
        public bool PreviewEditing { get { return preview != null && preview.Editing; } }
        public void EnterPreviewEdit() { if (preview != null) preview.EnterEdit(); }
        public void CancelPreviewEdit() { if (preview != null) preview.Cancel(); }
        public Control PreviewCtl { get { return preview; } }
        public double CurrentY { get { return app.S.Y; } }
        public TrackBar TrkFont { get { return trkFont; } }
        public TrackBar TrkWidth { get { return trkWidth; } }
        public TrackBar TrkLh { get { return trkLh; } }
        public Button BtnTheme { get { return btnTheme; } }
        public Button BtnMirrorH { get { return chkMirrorH; } }

        /* ---------- 刷新 ---------- */
        public void TickRefresh()
        {
            if (IsDisposed) return;
            if (prog != null && app.Lay != null)
                prog.Pct = app.Lay.MaxY > 0 ? app.S.Y / app.Lay.MaxY : 0;
            if (prog != null) prog.Invalidate();
            if (preview != null && !preview.Editing) preview.Invalidate();
        }

        public void RefreshUi()
        {
            if (IsDisposed) return;
            suppress = true;
            try
            {
                cmbScripts.Items.Clear();
                for (int i = 0; i < app.S.Scripts.Count; i++)
                    cmbScripts.Items.Add((i + 1) + ". " + app.S.Scripts[i].Name);
                if (app.S.Cur >= 0 && app.S.Cur < cmbScripts.Items.Count)
                    cmbScripts.SelectedIndex = app.S.Cur;

                trkSpeed.Value = Clamp((int)Math.Round(app.S.Speed * 10), 1, 60);
                trkFont.Value = Clamp(app.S.FontSize, 20, 140);
                trkWidth.Value = Clamp(app.S.WidthPct, 40, 100);
                trkLh.Value = Clamp((int)Math.Round(app.S.LineSpacing * 10), 10, 30);

                lblSpeedV.Text = app.S.Speed.ToString("0.0") + "×";
                lblFontV.Text = app.S.FontSize.ToString();
                lblWidthV.Text = app.S.WidthPct + "%";
                lblLhV.Text = app.S.LineSpacing.ToString("0.0");

                btnPlay.Text = app.S.Playing ? "⏸ 暂停" : "▶ 开始";
                SetToggle(chkBold, app.S.Bold);
                SetToggle(chkMirrorH, app.S.MirrorH);
                SetToggle(chkMirrorV, app.S.MirrorV);
                SetToggle(chkGuide, app.S.Guide);
                btnTheme.Text = State.ThemeNames[app.S.Theme];

                bool outOn = (app.Out != null && !app.Out.IsDisposed && app.Out.Visible);
                btnOut.Text = outOn ? "隐藏副屏" : "副屏全屏";

                Screen sc = app.TargetScreen();
                int paras = (app.Lay != null) ? app.Lay.Blocks.Count : 0;
                int chars = app.S.CurrentText.Replace("\n", "").Replace(" ", "").Length;
                lblStatus.Text = string.Format("{0} · {1}段 · {2}字",
                    outOn ? "副屏输出中" : "副屏未开", paras, chars);
            }
            finally { suppress = false; }
            TickRefresh();
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        static void SetToggle(Button b, bool on)
        {
            b.BackColor = on ? C_ACC : Color.FromArgb(52, 52, 52);
            b.ForeColor = on ? Color.FromArgb(26, 18, 4) : C_TXT;
            b.FlatAppearance.BorderColor = on ? C_ACC : C_LINE;
        }

        /* ---------- 事件 ---------- */
        void OnOpen(object s, EventArgs e)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "选择配音稿";
                d.Filter = "稿件文件|*.docx;*.txt;*.md|Word 文档 (*.docx)|*.docx|文本文件 (*.txt;*.md)|*.txt;*.md|所有文件|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string raw = Util.ReadFileText(d.FileName);
                    if (raw.Trim().Length == 0) { Warn("文件是空的。"); return; }
                    app.S.Scripts.Add(new ScriptItem(Path.GetFileNameWithoutExtension(d.FileName), raw));
                    app.S.Cur = app.S.Scripts.Count - 1;
                    app.S.Y = 0;
                    app.Pause();
                    app.Rebuild();
                    RefreshUi();
                    app.SaveData();
                }
                catch (Exception ex) { Warn("读取失败：" + ex.Message); }
            }
        }

        void OnDeleteScript(object s, EventArgs e)
        {
            if (app.S.Cur < 0) return;
            app.S.Scripts.RemoveAt(app.S.Cur);
            if (app.S.Scripts.Count == 0)
                app.S.Scripts.Add(new ScriptItem("示例稿件", App.Demo()));
            app.S.Cur = Math.Min(app.S.Cur, app.S.Scripts.Count - 1);
            app.S.Y = 0;
            app.Pause();
            app.Rebuild();
            RefreshUi();
            app.SaveData();
        }

        void OnPlay(object s, EventArgs e) { app.Toggle(); }

        void OnTop(object s, EventArgs e)
        {
            app.Pause();
            app.S.Y = 0;
            TickRefresh();
            if (app.Out != null && !app.Out.IsDisposed) app.Out.Invalidate();
        }

        void OnToggleOutput(object s, EventArgs e) { app.ToggleOutput(); }
        void OnHelp(object s, EventArgs e) { ShowHelp(); }

        void OnScriptChanged(object s, EventArgs e)
        {
            if (suppress) return;
            if (cmbScripts.SelectedIndex >= 0) app.SelectScript(cmbScripts.SelectedIndex);
        }

        void OnScreenChanged(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.ScreenIdx = cmbScreen.SelectedIndex;
            app.Rebuild();
            if (app.Out != null && !app.Out.IsDisposed && app.Out.Visible)
                app.Out.ShowOnScreen(app.TargetScreen());
            RefreshUi();
            app.SaveData();
        }

        void OnSpeed(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.Speed = trkSpeed.Value / 10.0;
            lblSpeedV.Text = app.S.Speed.ToString("0.0") + "×";
            app.SaveData();
        }

        void OnFont(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.FontSize = trkFont.Value;
            lblFontV.Text = app.S.FontSize.ToString();
            app.Rebuild(); app.SaveData();
        }

        void OnWidth(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.WidthPct = trkWidth.Value;
            lblWidthV.Text = app.S.WidthPct + "%";
            app.Rebuild(); app.SaveData();
        }

        void OnLh(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.LineSpacing = trkLh.Value / 10.0;
            lblLhV.Text = app.S.LineSpacing.ToString("0.0");
            app.Rebuild(); app.SaveData();
        }

        void OnBold(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.Bold = !app.S.Bold;
            SetToggle(chkBold, app.S.Bold);
            app.Rebuild(); app.SaveData();
        }

        void OnMirrorH(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.MirrorH = !app.S.MirrorH;
            SetToggle(chkMirrorH, app.S.MirrorH);
            Redraw(); app.SaveData();
        }

        void OnMirrorV(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.MirrorV = !app.S.MirrorV;
            SetToggle(chkMirrorV, app.S.MirrorV);
            Redraw(); app.SaveData();
        }

        void OnGuide(object s, EventArgs e)
        {
            if (suppress) return;
            app.S.Guide = !app.S.Guide;
            SetToggle(chkGuide, app.S.Guide);
            Redraw(); app.SaveData();
        }

        void Redraw()
        {
            if (app.Out != null && !app.Out.IsDisposed) app.Out.Invalidate();
            if (preview != null) preview.Invalidate();
        }

        void OnTheme(object s, EventArgs e)
        {
            app.S.Theme = (app.S.Theme + 1) % 4;
            btnTheme.Text = State.ThemeNames[app.S.Theme];
            Redraw(); app.SaveData();
        }

        void OnReset(object s, EventArgs e)
        {
            app.S.Speed = 1.2; app.S.FontSize = 56; app.S.WidthPct = 88; app.S.LineSpacing = 1.5;
            app.S.Bold = true; app.S.MirrorH = false; app.S.MirrorV = false;
            app.S.Guide = true; app.S.Theme = 0;
            app.Rebuild(); RefreshUi(); Redraw(); app.SaveData();
        }

        void Warn(string msg) { MessageBox.Show(this, msg, "提词器", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        void ShowHelp()
        {
            string t =
                "快捷键（与脚踏板 / 遥控器一致）\n" +
                "--------------------------------\n" +
                "空格        开始 / 暂停\n" +
                "↑  ↓        加速 / 减速\n" +
                "←  →        手动向上滚 / 向下滚\n" +
                "Home        回到首行\n" +
                "Tab         打开稿件列表\n" +
                "1  3        上一篇 / 下一篇\n" +
                "2  或  F11  开关副屏输出\n" +
                "M           水平镜像开关\n" +
                "Esc         退出编辑\n\n" +
                "编辑文字\n" +
                "--------------------------------\n" +
                "直接点主窗口中间的预览画面即可编辑，\n" +
                "改完点「保存并应用」或按 Ctrl+Enter。\n\n" +
                "界面自适应任意分辨率，接双屏时\n" +
                "副屏自动全屏显示纯文字。";
            MessageBox.Show(this, t, "提词器 · 帮助", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /* ---------- 键盘 ---------- */
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleKey(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public bool HandleKey(Keys keyData)
        {
            if (preview != null && preview.Editing) return false;
            bool comboFocus = (ActiveControl is ComboBox);

            switch (keyData)
            {
                case Keys.Space: app.Toggle(); return true;
                case Keys.Up:
                    if (comboFocus) return false;
                    SetSpeed(0.1); return true;
                case Keys.Down:
                    if (comboFocus) return false;
                    SetSpeed(-0.1); return true;
                case Keys.Left: app.Nudge(-60); return true;
                case Keys.Right: app.Nudge(60); return true;
                case Keys.Home: app.Nudge(-100000000); return true;
                case Keys.End: app.Nudge(100000000); return true;
                case Keys.Tab:
                    cmbScripts.Focus();
                    cmbScripts.DroppedDown = true; return true;
                case Keys.D1: app.SelectScript(app.S.Cur - 1); return true;
                case Keys.D3: app.SelectScript(app.S.Cur + 1); return true;
                case Keys.D2:
                case Keys.F11: app.ToggleOutput(); return true;
                case Keys.M:
                    app.S.MirrorH = !app.S.MirrorH;
                    SetToggle(chkMirrorH, app.S.MirrorH);
                    Redraw(); app.SaveData(); return true;
                case Keys.F2:
                    if (preview != null && !preview.Editing) preview.EnterEdit();
                    return true;
            }
            return false;
        }

        void SetSpeed(double delta)
        {
            double v = Math.Round((app.S.Speed + delta) * 10) / 10.0;
            if (v < 0.1) v = 0.1;
            if (v > 6.0) v = 6.0;
            app.S.Speed = v;
            suppress = true;
            trkSpeed.Value = Clamp((int)Math.Round(v * 10), 1, 60);
            suppress = false;
            lblSpeedV.Text = v.ToString("0.0") + "×";
            app.SaveData();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            app.SaveData();
            Application.ExitThread();
        }
    }
}
