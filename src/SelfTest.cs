using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TiCi
{
    /* 无界面自检：把排版/镜像渲染成 PNG，并把主窗口截图存盘，便于验证 */
    static class SelfTest
    {
        static string LongText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("提词器排版测试 · 中文长段落与混排");
            sb.AppendLine();
            sb.AppendLine("第一段：山不在高，有仙则名；水不在深，有龙则灵。这一段故意写得很长，用来验证自动换行、行距与标点挤压是否符合预期：，。！？；：“”《》（）");
            sb.AppendLine();
            sb.AppendLine("第二段：混合 mixed English text、数字 1234，以及一条足够长的句子，用来触发换行行为，确认窄屏与宽屏下都能正确排版而不溢出边界。");
            sb.AppendLine();
            sb.AppendLine("第三段：这一条应当是独立的一个自然段。");
            sb.AppendLine("同段内第二行（硬换行）。");
            return sb.ToString();
        }
        public static void Run()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string outDir = Path.Combine(dir, "_selftest");
            Directory.CreateDirectory(outDir);
            StringBuilder log = new StringBuilder();

            App app = new App();
            app.S.Scripts.Add(new ScriptItem("陋室铭", App.Demo()));
            app.S.Cur = 0;

            Render(app, 1080, 1920, 0, false, false, Path.Combine(outDir, "副屏_竖屏_正常.png"));
            log.AppendLine("1080x1920 rendered");

            Render(app, 1080, 1920, 0, true, false, Path.Combine(outDir, "副屏_竖屏_水平镜像.png"));
            log.AppendLine("1080x1920 mirrorH rendered");

            Render(app, 1920, 1080, 0, false, false, Path.Combine(outDir, "副屏_1080p_起始.png"));
            log.AppendLine("1920x1080 scrolled rendered");

            TextLayout L = TextLayout.Build(app.S, 1080, 1920);
            log.AppendLine(string.Format("layout: blocks={0} total={1} maxY={2} lineH={3} colW={4}",
                L.Blocks.Count, L.Total, L.MaxY, L.LineH, L.ColW));
            for (int i = 0; i < L.Blocks.Count; i++)
                log.AppendLine(string.Format("  block{0}: y={1} h={2} lines={3}",
                    i, L.Blocks[i].Y, L.Blocks[i].H, L.Blocks[i].Lines.Count));

            // 主窗口截图：按标准 1920×1080 场景的几种窗口尺寸验证自适应
            try
            {
                app.Lay = TextLayout.Build(app.S, 1920, 1080);
                app.S.Y = 0;
                app.S.ScreenIdx = 0;
                app.Main = new MainForm(app);
                app.Main.StartPosition = FormStartPosition.Manual;
                app.Main.Location = new Point(-6000, -6000);
                app.Main.Show();
                app.Main.RefreshUi();
                Application.DoEvents();

                int[][] sizes = new int[][] {
                    new int[]{ 1440, 800 }, new int[]{ 1200, 700 }, new int[]{ 980, 640 }
                };
                foreach (int[] sz in sizes)
                {
                    app.Main.ClientSize = new Size(sz[0], sz[1]);
                    for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
                    Bitmap b = new Bitmap(app.Main.Width, app.Main.Height);
                    app.Main.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
                    int bw = b.Width, bh = b.Height;
                    b.Save(Path.Combine(outDir, string.Format("主窗口_{0}x{1}.png", sz[0], sz[1])),
                           System.Drawing.Imaging.ImageFormat.Png);
                    b.Dispose();
                    log.AppendLine(string.Format("main {0}x{1} -> bitmap {2}x{3}, toolbar h={4}",
                        sz[0], sz[1], bw, bh, app.Main.ToolbarHeight));
                }
                // 点开预览进入编辑态，验证内嵌编辑器
                app.Main.ClientSize = new Size(1200, 700);
                app.Main.EnterPreviewEdit();
                for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
                Bitmap b2 = new Bitmap(app.Main.Width, app.Main.Height);
                app.Main.DrawToBitmap(b2, new Rectangle(0, 0, b2.Width, b2.Height));
                b2.Save(Path.Combine(outDir, "主窗口_编辑态.png"), System.Drawing.Imaging.ImageFormat.Png);
                b2.Dispose();
                log.AppendLine("edit mode captured, editing=" + app.Main.PreviewEditing);
                app.Main.CancelPreviewEdit();

                app.Main.Hide();
            }
            catch (Exception ex) { log.AppendLine("main capture failed: " + ex.ToString()); }
            File.WriteAllText(Path.Combine(outDir, "log.txt"), log.ToString(), Encoding.UTF8);
            Environment.Exit(0);
        }

        /* 界面回归测试：连续改尺寸/改参数，统计预览区还有多少文字像素 */
        public static void UiTest()
        {
            StringBuilder log = new StringBuilder();
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string outDir = Path.Combine(dir, "_selftest");
            Directory.CreateDirectory(outDir);
            int step = 0;
            try
            {
                App.Silent = true;
                App app = new App();
                app.Start();
                Pump(300);
                app.S.Theme = 0; app.S.MirrorH = false; app.S.MirrorV = false; app.S.Guide = true;
                app.S.Scripts.Clear();
                app.S.Scripts.Add(new ScriptItem("测试稿", LongText()));
                app.S.Cur = 0;
                app.S.Y = 0;
                app.Rebuild();
                app.Main.RefreshUi();

                MainForm m = app.Main;
                m.ClientSize = new Size(1440, 800);
                Pump(200);

                log.AppendLine("step  op                         size        preview     brightPx  ratio   Y");
                Snap(m, log, ref step, outDir, "初始");

                Resize(m, 1000, 700); Snap(m, log, ref step, outDir, "缩小到1000x700");
                Resize(m, 1600, 900); Snap(m, log, ref step, outDir, "放大到1600x900");
                Resize(m, 900, 620); Snap(m, log, ref step, outDir, "再缩到900x620");
                Resize(m, 1440, 800); Snap(m, log, ref step, outDir, "回1440x800");

                m.TrkFont.Value = 90; Snap(m, log, ref step, outDir, "字号90");
                m.TrkFont.Value = 30; Snap(m, log, ref step, outDir, "字号30");
                m.TrkFont.Value = 56; Snap(m, log, ref step, outDir, "字号56");

                m.TrkWidth.Value = 50; Snap(m, log, ref step, outDir, "宽度50");
                m.TrkWidth.Value = 88; Snap(m, log, ref step, outDir, "宽度88");
                m.TrkLh.Value = 25; Snap(m, log, ref step, outDir, "行距2.5");
                m.TrkLh.Value = 15; Snap(m, log, ref step, outDir, "行距1.5");

                m.BtnMirrorH.PerformClick(); Snap(m, log, ref step, outDir, "水平镜像开");
                m.BtnMirrorH.PerformClick(); Snap(m, log, ref step, outDir, "水平镜像关");
                m.BtnTheme.PerformClick(); Snap(m, log, ref step, outDir, "主题切换");
                m.BtnTheme.PerformClick();

                app.S.Y = app.Lay.MaxY; app.Pause(); Snap(m, log, ref step, outDir, "滚到底");
                m.TrkFont.Value = 70; Snap(m, log, ref step, outDir, "底部改字号70");
                m.TrkFont.Value = 56; Snap(m, log, ref step, outDir, "再改回56");

                app.S.Y = 0; app.Pause(); Pump(100);
                m.EnterPreviewEdit(); Pump(200); Snap(m, log, ref step, outDir, "进入编辑");
                m.CancelPreviewEdit(); Pump(200); Snap(m, log, ref step, outDir, "退出编辑");

                Resize(m, 1100, 760); Snap(m, log, ref step, outDir, "退出后再缩放");
                m.Hide();
                log.AppendLine("RESULT=OK");
            }
            catch (Exception ex) { log.AppendLine("RESULT=FAIL " + ex); }
            File.WriteAllText(Path.Combine(outDir, "uitest.txt"), log.ToString(), Encoding.UTF8);
            Environment.Exit(0);
        }

        static void Resize(MainForm m, int w, int h)
        {
            m.ClientSize = new Size(w, h);
            Pump(180);
        }

        static void Snap(MainForm m, StringBuilder log, ref int step, string outDir, string op)
        {
            int n = ++step;
            Control pv = m.PreviewCtl;
            int pw = pv.Width, ph = pv.Height;
            int bright = 0;
            double ratio = 0;
            try
            {
                using (Bitmap b = new Bitmap(Math.Max(1, pw), Math.Max(1, ph)))
                {
                    pv.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
                    for (int y = 0; y < b.Height; y += 3)
                        for (int x = 0; x < b.Width; x += 3)
                        {
                            Color c = b.GetPixel(x, y);
                            if (c.R > 140 && c.G > 140 && c.B > 140) bright++;
                        }
                    long total = ((b.Width + 2) / 3) * ((b.Height + 2) / 3);
                    ratio = total > 0 ? (double)bright / total : 0;
                    b.Save(Path.Combine(outDir, string.Format("ui{0:00}_{1}.png", n, op)),
                           System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex) { log.AppendLine("  capture error: " + ex.Message); }
            log.AppendLine(string.Format("{0,4}  {1,-26} {2,-11} {3,4}x{4,-4} {5,8}  {6:P2}  {7}",
                n, op, m.ClientSize.Width + "x" + m.ClientSize.Height, pw, ph, bright, ratio, (int)0));
        }

        /* 启动冒烟测试：真跑一遍 App.Start()、开关副屏、按键，验证不崩 */
        public static void Smoke()
        {
            StringBuilder log = new StringBuilder();
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string outDir = Path.Combine(dir, "_selftest");
            Directory.CreateDirectory(outDir);
            try
            {
                App.Silent = true;
                App app = new App();
                app.Start();
                Pump(300);
                log.AppendLine("start ok; scripts=" + app.S.Scripts.Count + " cur=" + app.S.Cur);
                log.AppendLine("layout blocks=" + app.Lay.Blocks.Count + " maxY=" + app.Lay.MaxY);

                Screen sc = app.TargetScreen();
                log.AppendLine(string.Format("target screen: {0} {1}x{2} primary={3}",
                    sc.DeviceName, sc.Bounds.Width, sc.Bounds.Height, sc.Primary));

                app.ToggleOutput();
                Pump(400);
                bool vis = (app.Out != null && !app.Out.IsDisposed && app.Out.Visible);
                log.AppendLine("output visible=" + vis);
                if (vis)
                {
                    Rectangle b = app.Out.Bounds;
                    log.AppendLine(string.Format("output bounds={0},{1} {2}x{3} expected={4}x{5}",
                        b.X, b.Y, b.Width, b.Height, sc.Bounds.Width, sc.Bounds.Height));
                    Bitmap ob = new Bitmap(app.Out.Width, app.Out.Height);
                    app.Out.DrawToBitmap(ob, new Rectangle(0, 0, ob.Width, ob.Height));
                    ob.Save(Path.Combine(outDir, "实际输出窗口.png"), System.Drawing.Imaging.ImageFormat.Png);
                    ob.Dispose();
                    log.AppendLine("output window captured");
                }

                bool k1 = app.Main.HandleKey(Keys.Space);
                Pump(250);
                log.AppendLine("key space handled=" + k1 + " playing=" + app.S.Playing + " y=" + (int)app.S.Y);
                app.Main.HandleKey(Keys.Space);
                log.AppendLine("paused playing=" + app.S.Playing);

                double before = app.S.Y;
                app.Main.HandleKey(Keys.Right);
                log.AppendLine("nudge right: " + (int)before + " -> " + (int)app.S.Y);

                int cur0 = app.S.Cur;
                app.Main.HandleKey(Keys.D3);
                log.AppendLine("next script: " + cur0 + " -> " + app.S.Cur);

                app.ToggleOutput();
                Pump(200);
                log.AppendLine("output hidden=" + !(app.Out.Visible));
                app.Main.Close();
                log.AppendLine("RESULT=OK");
            }
            catch (Exception ex)
            {
                log.AppendLine("RESULT=FAIL " + ex);
            }
            File.AppendAllText(Path.Combine(outDir, "smoke.txt"), log.ToString(), Encoding.UTF8);
            Environment.Exit(0);
        }

        static void Pump(int ms)
        {
            DateTime end = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < end)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(15);
            }
        }

        static void Render(App app, int w, int h, int scroll, bool mh, bool mv, string path)
        {
            app.S.MirrorH = mh;
            app.S.MirrorV = mv;
            TextLayout L = TextLayout.Build(app.S, w, h);
            app.S.Y = Math.Min(scroll, L.MaxY);

            using (Bitmap buf = new Bitmap(w, h))
            {
                using (Graphics g = Graphics.FromImage(buf))
                {
                    g.Clear(app.S.Bg);
                    L.Paint(g, app.S.Y, app.S.Fg);
                }
                using (Bitmap final = new Bitmap(w, h))
                {
                    using (Graphics gx = Graphics.FromImage(final))
                    {
                        if (mh || mv)
                        {
                            gx.TranslateTransform(mh ? w : 0, mv ? h : 0);
                            gx.ScaleTransform(mh ? -1 : 1, mv ? -1 : 1);
                        }
                        gx.DrawImageUnscaled(buf, 0, 0);
                        gx.ResetTransform();
                        // 导播线（不参与镜像）
                        int y = (int)(h * 0.40);
                        Color red = Color.FromArgb(220, 255, 59, 48);
                        using (Pen pen = new Pen(red, 2)) gx.DrawLine(pen, 0, y, w, y);
                        using (SolidBrush br = new SolidBrush(red))
                        {
                            gx.FillPolygon(br, new Point[] { new Point(0, y - 9), new Point(16, y), new Point(0, y + 9) });
                            gx.FillPolygon(br, new Point[] { new Point(w, y - 9), new Point(w - 16, y), new Point(w, y + 9) });
                        }
                    }
                    final.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
    }
}
