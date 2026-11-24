using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TiCi
{
    /* ============================================================
       提词器 —— 单文件 exe（.NET Framework 自带运行时，零依赖）
       主窗口：主屏控制条
       输出窗口：副屏全屏、无边框、无菜单、纯文字
       两窗口共用同一份滚动状态，天然同步
       ============================================================ */

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--render-test") { SelfTest.Run(); return; }
            if (args.Length > 0 && args[0] == "--smoke") { SelfTest.Smoke(); return; }
            if (args.Length > 0 && args[0] == "--ui-test") { SelfTest.UiTest(); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            App app = new App();
            app.Start();
            Application.Run(app);
        }
    }

    class ScriptItem
    {
        public string Name = "";
        public string Raw = "";
        public ScriptItem() { }
        public ScriptItem(string n, string r) { Name = n; Raw = r; }
    }

    class State
    {
        public List<ScriptItem> Scripts = new List<ScriptItem>();
        public int Cur = -1;
        public double Speed = 1.2;
        public int FontSize = 56;
        public int WidthPct = 88;
        public double LineSpacing = 1.5;
        public bool Bold = true;
        public bool MirrorH = false;
        public bool MirrorV = false;
        public bool Guide = true;
        public int Theme = 0;
        public int ScreenIdx = -1;
        public double Y = 0;
        public bool Playing = false;

        public static readonly Color[][] Themes = new Color[][] {
            new Color[]{ Color.FromArgb(10,10,10),    Color.FromArgb(242,242,242) },
            new Color[]{ Color.FromArgb(27,27,27),    Color.FromArgb(245,245,245) },
            new Color[]{ Color.FromArgb(255,255,255), Color.FromArgb(17,17,17) },
            new Color[]{ Color.FromArgb(0,177,64),    Color.FromArgb(255,255,255) }
        };
        public static readonly string[] ThemeNames = { "黑色", "深灰", "白色", "绿幕" };
        public Color Bg { get { return Themes[Theme][0]; } }
        public Color Fg { get { return Themes[Theme][1]; } }

        public ScriptItem Current
        {
            get { return (Cur >= 0 && Cur < Scripts.Count) ? Scripts[Cur] : null; }
        }
        public string CurrentText
        {
            get { ScriptItem s = Current; return s == null ? "" : s.Raw; }
        }
    }

    /* ---------------- 排版引擎 ---------------- */
    class Block
    {
        public List<string> Lines = new List<string>();
        public int Y;
        public int H;
    }

    class TextLayout
    {
        public int ViewW, ViewH, ColW, LineH, PadTop, PadBottom, Total;
        public Font Font;
        public List<Block> Blocks = new List<Block>();

        public int MaxY { get { return Math.Max(0, Total - ViewH); } }
        public int X0 { get { return (ViewW - ColW) / 2; } }
        public float LineDY { get { return (LineH - Font.Height) / 2f; } }

        public static StringFormat MakeFormat()
        {
            StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            sf.Trimming = StringTrimming.None;
            return sf;
        }

        public static TextLayout Build(State s, int viewW, int viewH)
        {
            TextLayout L = new TextLayout();
            L.ViewW = Math.Max(200, viewW);
            L.ViewH = Math.Max(200, viewH);
            L.ColW = (int)(L.ViewW * (s.WidthPct / 100.0));
            if (L.ColW < 120) L.ColW = 120;
            L.Font = new Font("Microsoft YaHei", (float)s.FontSize,
                              s.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            L.LineH = (int)Math.Round(s.FontSize * s.LineSpacing);
            L.PadTop = (int)(L.ViewH * 0.42);
            L.PadBottom = (int)(L.ViewH * 0.55);   // 结尾停在导播线附近，不会滚成白板

            string raw = (s.CurrentText ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
            string[] paras = Regex.Split(raw, "\n[ \t]*\n");

            int y = L.PadTop;
            int gap = (int)Math.Round(s.FontSize * 0.55);
            using (Bitmap bmp = new Bitmap(8, 8))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                StringFormat sf = MakeFormat();
                foreach (string para in paras)
                {
                    string p = para.Trim('\n');
                    if (p.Trim().Length == 0) continue;
                    Block b = new Block();
                    b.Lines = Wrap(g, p, L.Font, L.ColW, sf);
                    b.Y = y;
                    b.H = Math.Max(L.LineH, b.Lines.Count * L.LineH);
                    L.Blocks.Add(b);
                    y += b.H + gap;
                }
                sf.Dispose();
            }
            if (L.Blocks.Count > 0) y -= gap;
            y += L.PadBottom;
            L.Total = y;
            return L;
        }

        static float WidthOf(Graphics g, string s, Font font, StringFormat sf)
        {
            if (s == null || s.Length == 0) return 0f;
            return g.MeasureString(s, font, new SizeF(1000000f, 1000000f), sf).Width;
        }

        static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }

        /* 二分法找出「恰好不超过 maxW」的字符数，保证测量宽度 = 实际绘制宽度 */
        static int FitChars(Graphics g, string s, Font font, float maxW, StringFormat sf)
        {
            int lo = 1, hi = s.Length, best = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (WidthOf(g, s.Substring(0, mid), font, sf) <= maxW) { best = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return best;
        }

        static List<string> Wrap(Graphics g, string text, Font font, float maxW, StringFormat sf)
        {
            List<string> lines = new List<string>();
            string[] hard = text.Split('\n');
            foreach (string hseg in hard)
            {
                string s = hseg;
                if (s.Length == 0) { lines.Add(""); continue; }
                int guard = 0;
                while (s.Length > 0 && guard++ < 20000)
                {
                    if (WidthOf(g, s, font, sf) <= maxW) { lines.Add(s.TrimEnd()); break; }

                    int k = FitChars(g, s, font, maxW, sf);
                    if (k <= 0) k = 1;

                    // 西文单词尽量不从中间断开
                    if (k < s.Length && IsAsciiLetter(s[k - 1]) && IsAsciiLetter(s[k]))
                    {
                        int sp = s.LastIndexOf(' ', k - 1);
                        if (sp > 2) k = sp;
                    }

                    lines.Add(s.Substring(0, k).TrimEnd());
                    s = s.Substring(k);
                    if (s.Length > 0 && s[0] == ' ') s = s.Substring(1);
                }
            }
            return lines;
        }

        public void Paint(Graphics g, double scrollY, Color fg)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.None;
            float dy = LineDY;
            using (SolidBrush br = new SolidBrush(fg))
            using (StringFormat sf = MakeFormat())
            {
                double top = scrollY - 80;
                double bottom = scrollY + ViewH + 80;
                foreach (Block b in Blocks)
                {
                    if (b.Y + b.H < top) continue;
                    if (b.Y > bottom) break;
                    for (int i = 0; i < b.Lines.Count; i++)
                    {
                        float yy = (float)(b.Y + i * LineH - scrollY + dy);
                        if (yy > ViewH + 60 || yy < -LineH - 60) continue;
                        g.DrawString(b.Lines[i], Font, br, new PointF(X0, yy), sf);
                    }
                }
            }
        }
    }

    /* ---------------- 工具 ---------------- */
    static class Util
    {
        public static string DecodeText(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
                return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch
            {
                try { return Encoding.GetEncoding(936).GetString(b); }
                catch { return Encoding.UTF8.GetString(b); }
            }
        }

        public static string ReadFileText(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (Path.GetExtension(path).ToLowerInvariant() == ".docx") return DocxToText(b);
            return DecodeText(b);
        }

        /* 极简 ZIP 读取：只取所需条目，避免额外程序集依赖 */
        public static byte[] ZipEntry(byte[] zip, string wantName)
        {
            int eocd = -1;
            int start = Math.Max(0, zip.Length - 66000);
            for (int i = zip.Length - 22; i >= start; i--)
            {
                if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x05 && zip[i + 3] == 0x06)
                { eocd = i; break; }
            }
            if (eocd < 0) throw new Exception("不是有效的 docx 文件");
            int count = BitConverter.ToUInt16(zip, eocd + 10);
            int off = (int)BitConverter.ToUInt32(zip, eocd + 16);

            for (int i = 0; i < count; i++)
            {
                if (BitConverter.ToUInt32(zip, off) != 0x02014b50) break;
                int method = BitConverter.ToUInt16(zip, off + 10);
                int csize = (int)BitConverter.ToUInt32(zip, off + 20);
                int nameLen = BitConverter.ToUInt16(zip, off + 28);
                int extraLen = BitConverter.ToUInt16(zip, off + 30);
                int cmtLen = BitConverter.ToUInt16(zip, off + 32);
                int lho = (int)BitConverter.ToUInt32(zip, off + 42);
                string name = Encoding.UTF8.GetString(zip, off + 46, nameLen);
                if (name == wantName)
                {
                    int nl = BitConverter.ToUInt16(zip, lho + 26);
                    int el = BitConverter.ToUInt16(zip, lho + 28);
                    int dataStart = lho + 30 + nl + el;
                    if (method == 0)
                    {
                        byte[] raw0 = new byte[csize];
                        Array.Copy(zip, dataStart, raw0, 0, csize);
                        return raw0;
                    }
                    using (MemoryStream ms = new MemoryStream(zip, dataStart, csize))
                    using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                    using (MemoryStream outMs = new MemoryStream())
                    {
                        byte[] buf = new byte[8192];
                        int n;
                        while ((n = ds.Read(buf, 0, buf.Length)) > 0) outMs.Write(buf, 0, n);
                        return outMs.ToArray();
                    }
                }
                off += 46 + nameLen + extraLen + cmtLen;
            }
            throw new Exception("docx 中未找到 " + wantName);
        }

        public static string DocxToText(byte[] zip)
        {
            byte[] xmlBytes = ZipEntry(zip, "word/document.xml");
            string xml = Encoding.UTF8.GetString(xmlBytes);
            xml = xml.Replace("<w:tab/>", "\t").Replace("<w:br/>", "\n");
            xml = Regex.Replace(xml, @"<w:p\s[^>]*/>", "\n");
            xml = xml.Replace("</w:p>", "\n");
            xml = Regex.Replace(xml, @"<[^>]+>", "");
            xml = xml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"")
                     .Replace("&apos;", "'").Replace("&amp;", "&");
            xml = Regex.Replace(xml, @"[ \t]+\n", "\n");
            xml = Regex.Replace(xml, @"\n{3,}", "\n\n");
            return xml.Trim();
        }
    }

    /* ---------------- 控制器 ---------------- */
    class App : ApplicationContext
    {
        public static bool Silent = false;
        public State S = new State();
        public TextLayout Lay;
        public MainForm Main;
        public OutputForm Out;
        Timer tick;
        Stopwatch sw = new Stopwatch();
        double lastSec = 0;

        public Screen TargetScreen()
        {
            Screen[] all = Screen.AllScreens;
            if (S.ScreenIdx >= 0 && S.ScreenIdx < all.Length) return all[S.ScreenIdx];
            for (int i = 0; i < all.Length; i++) if (!all[i].Primary) return all[i];
            return all[0];
        }

        public void Start()
        {
            LoadData();
            if (S.Scripts.Count == 0) S.Scripts.Add(new ScriptItem("示例稿件", Demo()));
            if (S.Cur < 0 || S.Cur >= S.Scripts.Count) S.Cur = 0;

            Main = new MainForm(this);
            if (Silent)
            {
                Main.StartPosition = FormStartPosition.Manual;
                Main.Location = new Point(-6000, -6000);
            }
            Main.Show();
            Rebuild();
            Main.RefreshUi();

            sw.Start();
            tick = new Timer();
            tick.Interval = 15;
            tick.Tick += OnTick;
            tick.Start();
        }

        public static string Demo()
        {
            return "陋室铭 · 唐 · 刘禹锡\n\n" +
                   "山不在高，有仙则名。\n" +
                   "水不在深，有龙则灵。\n" +
                   "斯是陋室，惟吾德馨。\n\n" +
                   "苔痕上阶绿，草色入帘青。\n" +
                   "谈笑有鸿儒，往来无白丁。\n\n" +
                   "可以调素琴，阅金经。\n" +
                   "无丝竹之乱耳，无案牍之劳形。\n\n" +
                   "南阳诸葛庐，西蜀子云亭。\n" +
                   "孔子云：何陋之有？";
        }
        public void Rebuild()
        {
            Screen sc = TargetScreen();
            double oldMax = (Lay != null) ? Lay.MaxY : 0;
            double oldY = S.Y;
            int oldW = (Lay != null) ? Lay.ViewW : 0;
            Lay = TextLayout.Build(S, sc.Bounds.Width, sc.Bounds.Height);
            if (oldW == Lay.ViewW && oldMax > 1 && Lay.MaxY > 1)
                S.Y = oldY / oldMax * Lay.MaxY;      // 改字号/行距后保持阅读进度不跳
            if (S.Y > Lay.MaxY) S.Y = Lay.MaxY;
            if (S.Y < 0) S.Y = 0;
            if (Out != null && !Out.IsDisposed) Out.Invalidate();
            if (Main != null && !Main.IsDisposed) Main.TickRefresh();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (!S.Playing) return;
            double now = sw.Elapsed.TotalSeconds;
            double dt = now - lastSec;
            lastSec = now;
            if (dt <= 0 || dt > 0.5 || Lay == null) return;
            double pxPerSec = S.Speed * S.FontSize * 1.8;
            S.Y += pxPerSec * dt;
            if (S.Y >= Lay.MaxY) { S.Y = Lay.MaxY; Pause(); }
            if (Out != null && !Out.IsDisposed) Out.Invalidate();
            if (Main != null && !Main.IsDisposed) Main.TickRefresh();
        }

        public void Play()
        {
            if (Lay == null || Lay.MaxY <= 0) return;
            if (S.Y >= Lay.MaxY) S.Y = 0;
            S.Playing = true;
            lastSec = sw.Elapsed.TotalSeconds;
            Main.RefreshUi();
        }

        public void Pause()
        {
            S.Playing = false;
            if (Main != null) Main.RefreshUi();
            if (Out != null && !Out.IsDisposed) Out.Invalidate();
        }

        public void Toggle() { if (S.Playing) Pause(); else Play(); }

        public void Nudge(double px)
        {
            if (Lay == null) return;
            Pause();
            S.Y = Math.Max(0, Math.Min(Lay.MaxY, S.Y + px));
            if (Out != null && !Out.IsDisposed) Out.Invalidate();
            if (Main != null && !Main.IsDisposed) Main.TickRefresh();
        }

        public void SelectScript(int i)
        {
            if (i < 0 || i >= S.Scripts.Count) return;
            S.Cur = i; S.Y = 0;
            Pause();
            Rebuild();
            Main.RefreshUi();
            SaveData();
        }

        public void ToggleOutput()
        {
            if (Out != null && !Out.IsDisposed && Out.Visible) Out.Hide();
            else ShowOutput();
            Main.RefreshUi();
        }

        public void ShowOutput()
        {
            if (Out == null || Out.IsDisposed) Out = new OutputForm(this);
            Out.ShowOnScreen(TargetScreen());
            Main.RefreshUi();
        }

        public string DataPath()
        {
            try { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "提词器数据.txt"); }
            catch { return null; }
        }

        public void SaveData()
        {
            string p = DataPath();
            if (p == null) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("speed=" + S.Speed.ToString("0.0"));
                sb.AppendLine("font=" + S.FontSize);
                sb.AppendLine("width=" + S.WidthPct);
                sb.AppendLine("lh=" + S.LineSpacing.ToString("0.0"));
                sb.AppendLine("bold=" + (S.Bold ? 1 : 0));
                sb.AppendLine("mh=" + (S.MirrorH ? 1 : 0));
                sb.AppendLine("mv=" + (S.MirrorV ? 1 : 0));
                sb.AppendLine("guide=" + (S.Guide ? 1 : 0));
                sb.AppendLine("theme=" + S.Theme);
                sb.AppendLine("screen=" + S.ScreenIdx);
                sb.AppendLine("cur=" + S.Cur);
                foreach (ScriptItem sc in S.Scripts)
                    sb.AppendLine("script=" + B64(sc.Name) + "|" + B64(sc.Raw));
                File.WriteAllText(p, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }

        static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s == null ? "" : s)); }
        static string UnB64(string s)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
            catch { return ""; }
        }

        public void LoadData()
        {
            string p = DataPath();
            if (p == null || !File.Exists(p)) return;
            try
            {
                string[] lines = File.ReadAllLines(p, Encoding.UTF8);
                foreach (string ln in lines)
                {
                    int eq = ln.IndexOf('=');
                    if (eq < 0) continue;
                    string k = ln.Substring(0, eq).Trim();
                    string v = ln.Substring(eq + 1);
                    if (k == "script")
                    {
                        int bar = v.IndexOf('|');
                        if (bar > 0)
                            S.Scripts.Add(new ScriptItem(UnB64(v.Substring(0, bar)), UnB64(v.Substring(bar + 1))));
                        continue;
                    }
                    double d; int n;
                    if (k == "speed" && double.TryParse(v, out d)) S.Speed = d;
                    else if (k == "font" && int.TryParse(v, out n)) S.FontSize = n;
                    else if (k == "width" && int.TryParse(v, out n)) S.WidthPct = n;
                    else if (k == "lh" && double.TryParse(v, out d)) S.LineSpacing = d;
                    else if (k == "bold") S.Bold = v == "1";
                    else if (k == "mh") S.MirrorH = v == "1";
                    else if (k == "mv") S.MirrorV = v == "1";
                    else if (k == "guide") S.Guide = v == "1";
                    else if (k == "theme" && int.TryParse(v, out n)) S.Theme = Math.Max(0, Math.Min(3, n));
                    else if (k == "screen" && int.TryParse(v, out n)) S.ScreenIdx = n;
                    else if (k == "cur" && int.TryParse(v, out n)) S.Cur = n;
                }
            }
            catch { }
        }

        protected override void ExitThreadCore()
        {
            try { if (Out != null && !Out.IsDisposed) Out.Close(); } catch { }
            SaveData();
            base.ExitThreadCore();
        }
    }

    /* ---------------- 副屏输出窗口 ---------------- */
    class OutputForm : Form
    {
        App app;
        Bitmap buf;
        int bw, bh;

        public OutputForm(App a)
        {
            app = a;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "提词输出";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        public void ShowOnScreen(Screen sc)
        {
            Bounds = sc.Bounds;
            if (!Visible) Show();
            BringToFront();
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (app.Main != null && app.Main.HandleKey(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (app.Lay == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            if (buf == null || bw != w || bh != h)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(w, h);
                bw = w; bh = h;
            }

            using (Graphics g = Graphics.FromImage(buf))
            {
                g.Clear(app.S.Bg);
                app.Lay.Paint(g, app.S.Y, app.S.Fg);
            }

            Graphics gx = e.Graphics;
            bool fh = app.S.MirrorH, fv = app.S.MirrorV;
            Matrix old = gx.Transform;
            if (fh || fv)
            {
                gx.TranslateTransform(fh ? w : 0, fv ? h : 0);
                gx.ScaleTransform(fh ? -1 : 1, fv ? -1 : 1);
            }
            gx.InterpolationMode = InterpolationMode.NearestNeighbor;
            gx.PixelOffsetMode = PixelOffsetMode.Half;
            gx.DrawImageUnscaled(buf, 0, 0);
            gx.Transform = old;

            if (app.S.Guide) DrawGuide(gx, w, h);
        }

        void DrawGuide(Graphics g, int w, int h)
        {
            int y = (int)(h * 0.40);
            Color red = Color.FromArgb(220, 255, 59, 48);
            using (Pen pen = new Pen(red, 2)) g.DrawLine(pen, 0, y, w, y);
            using (SolidBrush br = new SolidBrush(red))
            {
                g.FillPolygon(br, new Point[] { new Point(0, y - 9), new Point(16, y), new Point(0, y + 9) });
                g.FillPolygon(br, new Point[] { new Point(w, y - 9), new Point(w - 16, y), new Point(w, y + 9) });
            }
        }
    }
}
