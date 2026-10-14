// Instalador de Pandora's Tower para PC (port nativo no oficial).
//
// Un asistente de Windows de los de siempre: bienvenida, aviso, carpeta, ISO,
// instalación con su barra de progreso y fin. No lleva el juego: lleva los
// archivos del port (port.zip, recurso incrustado) y, en tu equipo:
//   - los copia a la carpeta elegida;
//   - instala MSYS2 (las herramientas de compilación, gratuitas) si no está;
//   - instala el compilador y las librerías (pacman);
//   - compila el juego a partir de la ISO de tu copia (setup.sh);
//   - prepara el lanzador y los accesos directos (lanzador.sh);
//   - se registra en "Aplicaciones instaladas" para poder desinstalarlo.
// Desinstalar.exe (una copia de este programa) con /desinstalar lo quita.
//
// Se compila con tools/instalador/compilar.sh (csc de Windows o mcs de Mono).
// C# 5, como el lanzador.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PandoraInstaller
{
    static class L
    {
        public static readonly bool Es = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
        public static string T(string es, string en) { return Es ? es : en; }
    }

    static class Info
    {
        public const string Version = "1.0";
        public const string Name = "Pandora's Tower";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PandorasTowerPC";
        public const string Msys = @"C:\msys64";
        public const string MsysUrl = "https://github.com/msys2/msys2-installer/releases/latest/download/msys2-x86_64-latest.exe";
        public const string Packages = "git mingw-w64-clang-x86_64-clang mingw-w64-clang-x86_64-cmake mingw-w64-clang-x86_64-ninja " +
                                       "mingw-w64-clang-x86_64-python mingw-w64-clang-x86_64-python-pycryptodome mingw-w64-clang-x86_64-sdl3";
        public static string Bash { get { return Path.Combine(Msys, @"usr\bin\bash.exe"); } }

        // una ruta que las herramientas de compilación admiten: letras, cifras, _ - . y nada más
        public static bool CleanPath(string p) { return p.Length > 3 && Regex.IsMatch(p, @"^[A-Za-z]:\\[A-Za-z0-9_\-\.\\]*$"); }

        public static Stream Resource(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

        // la versión del juego de una ISO: su identificador (los 6 primeros bytes) y la marca de disco de Wii
        public static string GameId(string iso, out string error)
        {
            error = null;
            try
            {
                using (var f = File.OpenRead(iso))
                {
                    var b = new byte[0x20];
                    if (f.Read(b, 0, b.Length) != b.Length) { error = L.T("el archivo es demasiado pequeño", "the file is too small"); return null; }
                    uint magic = (uint)(b[0x18] << 24 | b[0x19] << 16 | b[0x1A] << 8 | b[0x1B]);
                    if (magic != 0x5D1C9EA3) { error = L.T("no es una ISO de Wii (una WBFS o RVZ se convierte a ISO con Dolphin)", "not a Wii ISO (convert a WBFS or RVZ to ISO with Dolphin)"); return null; }
                    string id = Encoding.ASCII.GetString(b, 0, 6);
                    if (id != "SX3EXJ" && id != "SX3P01" && id != "SX3J01") { error = L.T("es otro juego (", "it is another game (") + id + ")"; return null; }
                    return id;
                }
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        public static string GameName(string id)
        {
            switch (id)
            {
                case "SX3EXJ": return L.T("norteamericana (SX3EXJ)", "North American (SX3EXJ)");
                case "SX3P01": return L.T("europea (SX3P01)", "European (SX3P01)");
                case "SX3J01": return L.T("japonesa (SX3J01)", "Japanese (SX3J01)");
            }
            return id;
        }

        // una instalación anterior: la del registro, o C:\pandora-port si existe
        public static string Previous()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    string p = k != null ? k.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, "setup.sh"))) return p;
                }
            }
            catch (Exception) { }
            if (File.Exists(@"C:\pandora-port\setup.sh")) return @"C:\pandora-port";
            return null;
        }

        public static bool Compiled(string dir)
        {
            return File.Exists(Path.Combine(dir, @"build\recomp-build\wiiboot.exe")) || File.Exists(Path.Combine(dir, @"build\jp\recomp-build\wiiboot.exe"));
        }

        public static void Shortcut(string lnk, string target, string args, string work, string icon, string desc)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object s = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = s.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, s, new object[] { target });
            st.InvokeMember("Arguments", BindingFlags.SetProperty, null, s, new object[] { args });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, s, new object[] { work });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, s, new object[] { icon });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, s, new object[] { desc });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, s, null);
        }

        public static string StartMenuDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name); } }
        public static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk"); } }
    }

    // ---- aspecto: el del lanzador (oscuro, dorado, letra gótica en los títulos) -------------------
    static class Look
    {
        public static readonly Color Back = Color.FromArgb(18, 13, 16);
        public static readonly Color Panel = Color.FromArgb(26, 20, 23);
        public static readonly Color Gold = Color.FromArgb(214, 170, 96);
        public static readonly Color GoldDim = Color.FromArgb(140, 110, 66);
        public static readonly Color Text = Color.FromArgb(232, 224, 212);
        public static readonly Color TextDim = Color.FromArgb(160, 150, 140);
        public static readonly Color Bad = Color.FromArgb(230, 120, 100);
        public static FontFamily Gothic, Body;
        static PrivateFontCollection fonts = new PrivateFontCollection();
        public static float S = 1f;                                  // escala de la pantalla (ppp / 96)
        public static int P(float v) { return (int)Math.Round(v * S); }

        public static void Init(float dpi)
        {
            S = Math.Max(1f, dpi / 96f);
            Body = Pick("Segoe UI", "DejaVu Sans") ?? FontFamily.GenericSansSerif;
            Gothic = Pick("Pirata One");
            if (Gothic == null) try
            {
                using (Stream s = Info.Resource("PirataOne.ttf"))
                {
                    if (s != null)
                    {
                        string tmp = Path.Combine(Path.GetTempPath(), "pt-instalador-PirataOne.ttf");
                        using (var f = File.Create(tmp)) s.CopyTo(f);
                        fonts.AddFontFile(tmp);
                        Gothic = fonts.Families[0];
                    }
                }
            }
            catch (Exception) { }
            if (Gothic == null) Gothic = Pick("Palatino Linotype", "Georgia", "DejaVu Serif") ?? FontFamily.GenericSerif;
        }

        static FontFamily Pick(params string[] names)
        {
            foreach (string n in names)
                foreach (FontFamily f in FontFamily.Families)
                    if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        public static Font F(float size, FontStyle st = FontStyle.Regular) { return new Font(Body, size * S, st, GraphicsUnit.Pixel); }
        public static Font G(float size) { return new Font(Gothic, size * S, FontStyle.Regular, GraphicsUnit.Pixel); }

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // un botón dorado, como los del lanzador
    class GoldButton : Control
    {
        bool hover, down;
        public bool Primary;
        public GoldButton(string text, bool primary)
        {
            Text = text; Primary = primary;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Look.F(15, FontStyle.Bold);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath p = Look.Round(r, Look.P(6)))
            {
                Color top = Primary ? Color.FromArgb(120, 84, 40) : Color.FromArgb(44, 34, 36);
                Color bot = Primary ? Color.FromArgb(70, 46, 22) : Color.FromArgb(30, 23, 26);
                if (hover && Enabled) { top = ControlPaint.Light(top, 0.25f); bot = ControlPaint.Light(bot, 0.2f); }
                if (down) { Color t = top; top = bot; bot = t; }
                using (var b = new LinearGradientBrush(r, top, bot, LinearGradientMode.Vertical)) g.FillPath(b, p);
                using (var pen = new Pen(Enabled ? Look.Gold : Look.GoldDim, Math.Max(1f, Look.S))) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Font, r, Enabled ? Look.Text : Look.TextDim,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    // la barra de progreso
    class Bar : Control
    {
        public float Value;                                          // 0..1
        public Bar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Look.Back);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Look.Round(r, Height / 2 - 1))
            {
                using (var b = new SolidBrush(Look.Panel)) g.FillPath(b, p);
                int w = (int)(r.Width * Math.Max(0f, Math.Min(1f, Value)));
                if (w > Height)
                {
                    var f = new Rectangle(0, 0, w, Height - 1);
                    using (GraphicsPath pf = Look.Round(f, Height / 2 - 1))
                    using (var b = new LinearGradientBrush(f, Color.FromArgb(232, 190, 110), Color.FromArgb(150, 100, 44), LinearGradientMode.Vertical))
                        g.FillPath(b, pf);
                }
                using (var pen = new Pen(Look.GoldDim, Math.Max(1f, Look.S))) g.DrawPath(pen, p);
            }
        }
    }

    // ---- el asistente ------------------------------------------------------------------------------
    class Wizard : Form
    {
        enum Page { Welcome, Notice, Folder, Iso, Install, Done }
        Page page = Page.Welcome;
        readonly Dictionary<Page, Panel> pages = new Dictionary<Page, Panel>();
        Image side;
        GoldButton back, next, cancel;
        string stepTitle = "";
        Point dragFrom;

        // lo elegido
        TextBox folderBox, isoBox, noticeBox, logBox;
        CheckBox accept, desktopLink, startLink, playNow;
        Label isoInfo, folderInfo, toolsInfo, spaceInfo, status, percent, elapsed, doneTitle, doneText;
        Bar bar;
        GoldButton showLog;
        string isoId;

        // la instalación
        Thread worker;
        volatile bool cancelled;
        Process running;
        StreamWriter log;
        bool ok;
        string failure;

        const int Wd = 920, Ht = 580, SideW = 330;

        public Wizard()
        {
            Text = L.T("Instalar ", "Install ") + Info.Name;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Look.Back;
            ClientSize = new Size(Look.P(Wd), Look.P(Ht));
            DoubleBuffered = true;
            try { using (Stream s = Info.Resource("pandora.ico")) if (s != null) Icon = new Icon(s); } catch (Exception) { }
            try { using (Stream s = Info.Resource("lateral.jpg")) if (s != null) side = Image.FromStream(s); } catch (Exception) { }

            int x0 = Look.P(SideW) + Look.P(36), w = ClientSize.Width - x0 - Look.P(36);

            int by = ClientSize.Height - Look.P(62), bw = Look.P(128), bh = Look.P(38);
            cancel = new GoldButton(L.T("Cancelar", "Cancel"), false) { Size = new Size(bw, bh), Location = new Point(x0, by) };
            next = new GoldButton(L.T("Siguiente", "Next"), true) { Size = new Size(bw, bh), Location = new Point(ClientSize.Width - Look.P(36) - bw, by) };
            back = new GoldButton(L.T("Atrás", "Back"), false) { Size = new Size(bw, bh), Location = new Point(next.Left - Look.P(12) - bw, by) };
            cancel.Click += delegate { Cancel(); };
            next.Click += delegate { Next(); };
            back.Click += delegate { Back(); };
            Controls.Add(cancel); Controls.Add(next); Controls.Add(back);

            Rectangle area = new Rectangle(x0, Look.P(100), w, by - Look.P(116));
            BuildWelcome(area); BuildNotice(area); BuildFolder(area); BuildIso(area); BuildInstall(area); BuildDone(area);
            Go(Page.Welcome);

            // Intro sigue, Esc cancela
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; Cancel(); }
                else if (e.KeyCode == Keys.Enter && next.Visible && next.Enabled && !(ActiveControl is TextBox && ((TextBox)ActiveControl).Multiline)) { e.Handled = true; Next(); }
            };
            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragFrom = e.Location; };
            MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) Location = new Point(Location.X + e.X - dragFrom.X, Location.Y + e.Y - dragFrom.Y); };
        }

        // sin marco, pero con sombra y con su botón en la barra de tareas para minimizarlo
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000 | 0x80000;                       // WS_MINIMIZEBOX | WS_SYSMENU
                cp.ClassStyle |= 0x20000;                            // CS_DROPSHADOW
                return cp;
            }
        }

        Panel NewPage(Page p, Rectangle area)
        {
            var pn = new Panel { Location = area.Location, Size = area.Size, BackColor = Look.Back, Visible = false };
            pages[p] = pn;
            Controls.Add(pn);
            return pn;
        }

        // un párrafo a su altura justa, debajo del anterior
        Label Flow(Panel pn, string text, ref int y, bool dim = false, float size = 15, int gap = 14)
        {
            Label l = Para(pn, text, y, 10, dim, size);
            l.Height = TextRenderer.MeasureText(text, l.Font, new Size(pn.Width, 10000), TextFormatFlags.WordBreak).Height + Look.P(4);
            y += l.Height + Look.P(gap);
            return l;
        }

        Label Para(Panel pn, string text, int y, int h, bool dim = false, float size = 15)
        {
            var l = new Label { Text = text, ForeColor = dim ? Look.TextDim : Look.Text, BackColor = Look.Back, Font = Look.F(size),
                                AutoSize = false, Location = new Point(0, y), Size = new Size(pn.Width, h) };
            pn.Controls.Add(l);
            return l;
        }

        TextBox Field(Panel pn, int y, int w)
        {
            var t = new TextBox { Location = new Point(0, y), Width = w, BackColor = Look.Panel, ForeColor = Look.Text, BorderStyle = BorderStyle.FixedSingle,
                                  Font = Look.F(15) };
            pn.Controls.Add(t);
            return t;
        }

        CheckBox Check(Panel pn, string text, int y, bool on)
        {
            var c = new CheckBox { Text = text, Checked = on, ForeColor = Look.Text, BackColor = Look.Back, Font = Look.F(15), AutoSize = true,
                                   Location = new Point(0, y), FlatStyle = FlatStyle.Standard };
            pn.Controls.Add(c);
            return c;
        }

        void BuildWelcome(Rectangle a)
        {
            Panel pn = NewPage(Page.Welcome, a);
            int y = 0;
            Flow(pn, L.T(
                "Este asistente instala el port nativo no oficial de Pandora's Tower (Wii) para Windows: 60 fps y más, " +
                "altas resoluciones, pantalla panorámica sin bandas negras, mando de Xbox o PlayStation, o ratón y teclado, " +
                "y un lanzador con todas las opciones.",
                "This wizard installs the unofficial native port of Pandora's Tower (Wii) for Windows: 60 fps and more, " +
                "high resolutions, widescreen without black bars, Xbox or PlayStation gamepad, or mouse and keyboard, " +
                "and a launcher with every option."), ref y);
            Flow(pn, L.T(
                "No incluye el juego: lo prepara en tu equipo a partir de la ISO de tu propia copia (versión norteamericana " +
                "SX3EXJ, europea SX3P01 o japonesa SX3J01).",
                "The game is not included: it is built on your PC from the ISO of your own copy (North American SX3EXJ, " +
                "European SX3P01 or Japanese SX3J01)."), ref y);
            Flow(pn, L.T(
                "Necesitarás conexión a Internet y unos 6 GB libres. Compilar el juego lleva unos 5 minutos en un PC actual; " +
                "la primera vez, si no tienes las herramientas de compilación (MSYS2, gratuitas), descargarlas añade otros 5 o 10, " +
                "según tu conexión. Puedes seguir usando el equipo mientras tanto.",
                "You will need an Internet connection and about 6 GB of free space. Building the game takes about 5 minutes on a " +
                "modern PC; the first time, if you don't have the build tools (MSYS2, free), downloading them adds another 5 to 10, " +
                "depending on your connection. You can keep using your PC meanwhile."), ref y, true, 14);
            int fy = 0;
            Label foot = Flow(pn, L.T("Versión ", "Version ") + Info.Version + L.T(" · proyecto de aficionados, gratuito, sin relación con Nintendo, Ganbarion ni XSEED Games.",
                                                                                 " · free fan project, not affiliated with Nintendo, Ganbarion or XSEED Games."), ref fy, true, 12);
            foot.Top = pn.Height - foot.Height;
        }

        void BuildNotice(Rectangle a)
        {
            Panel pn = NewPage(Page.Notice, a);
            noticeBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Look.Panel, ForeColor = Look.Text,
                                      BorderStyle = BorderStyle.FixedSingle, Font = Look.F(13), Location = new Point(0, 0), Size = new Size(pn.Width, pn.Height - Look.P(44)) };
            string lic = "";
            try { using (Stream s = Info.Resource("LICENSE")) if (s != null) using (var r = new StreamReader(s)) lic = r.ReadToEnd(); } catch (Exception) { }
            lic = Regex.Replace(lic.Replace("\r", ""), @"(?<!\n)\n(?!\n)", " ");
            noticeBox.Text = (L.T(
                "AVISO\r\n\r\nEste programa no contiene ningún archivo del juego (ni su ejecutable, ni sus gráficos, sonidos o textos, ni imágenes oficiales, " +
                "ni claves de cifrado de la Wii). Todo se genera en tu equipo a partir de la imagen de disco de una copia que tengas legalmente.\r\n\r\n" +
                "Pandora's Tower es una marca de sus propietarios. Este es un proyecto de aficionados, gratuito y sin ánimo de lucro, sin relación con " +
                "Nintendo, Ganbarion ni XSEED Games. No compartas tu ISO ni el juego compilado.\r\n\r\n" +
                "El port usa wiikit (MIT) y descarga MSYS2 y las herramientas de compilación de sus sitios oficiales. La letra de los títulos es " +
                "Pirata One (SIL Open Font License 1.1). Los demás componentes y sus licencias están en TERCEROS.md, en la carpeta del port.\r\n\r\nLICENCIA DEL PORT\r\n\r\n",
                "NOTICE\r\n\r\nThis program contains no game files (no executable, graphics, sounds or text, no official artwork, no Wii encryption keys). " +
                "Everything is generated on your PC from the disc image of a copy you legally own.\r\n\r\n" +
                "Pandora's Tower is a trademark of its owners. This is a free, non-profit fan project, not affiliated with Nintendo, Ganbarion or " +
                "XSEED Games. Do not share your ISO or the built game.\r\n\r\n" +
                "The port uses wiikit (MIT) and downloads MSYS2 and the build tools from their official sites. The title font is " +
                "Pirata One (SIL Open Font License 1.1). Other components and their licenses are listed in TERCEROS.md, in the port folder.\r\n\r\nPORT LICENSE\r\n\r\n") + lic).Replace("\n", "\r\n").Replace("\r\r", "\r");
            pn.Controls.Add(noticeBox);
            accept = Check(pn, L.T("Acepto, y tengo una copia original del juego", "I accept, and I own an original copy of the game"), pn.Height - Look.P(32), false);
            accept.CheckedChanged += delegate { UpdateButtons(); };
        }

        void BuildFolder(Rectangle a)
        {
            Panel pn = NewPage(Page.Folder, a);
            Para(pn, L.T("Carpeta de instalación", "Install folder"), 0, Look.P(24), false, 15);
            folderBox = Field(pn, Look.P(28), pn.Width - Look.P(140));
            string prev = Info.Previous();
            folderBox.Text = prev ?? @"C:\PandorasTower";
            var browse = new GoldButton(L.T("Examinar…", "Browse…"), false) { Location = new Point(pn.Width - Look.P(128), Look.P(24)), Size = new Size(Look.P(128), Look.P(34)) };
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog { Description = L.T("Carpeta del port (sin espacios ni acentos)", "Port folder (no spaces or accents)"), ShowNewFolderButton = true })
                    if (d.ShowDialog(this) == DialogResult.OK)
                        folderBox.Text = Info.CleanPath(d.SelectedPath) ? d.SelectedPath : Path.Combine(d.SelectedPath.Substring(0, 3), "PandorasTower");
            };
            pn.Controls.Add(browse);
            folderInfo = Para(pn, "", Look.P(66), Look.P(44), true, 13);
            folderBox.TextChanged += delegate { UpdateFolderInfo(); UpdateButtons(); };

            desktopLink = Check(pn, L.T("Acceso directo en el escritorio", "Desktop shortcut"), Look.P(124), true);
            startLink = Check(pn, L.T("Acceso directo en el menú Inicio", "Start menu shortcut"), Look.P(156), true);

            toolsInfo = Para(pn, "", Look.P(206), Look.P(56), true, 13);
            spaceInfo = Para(pn, "", Look.P(268), Look.P(44), true, 13);
            toolsInfo.Text = File.Exists(Info.Bash)
                ? L.T("Herramientas de compilación: MSYS2 ya está instalado en C:\\msys64; se le añade lo que falte.",
                      "Build tools: MSYS2 is already installed in C:\\msys64; whatever is missing is added.")
                : L.T("Herramientas de compilación: se descargará e instalará MSYS2 (gratuito, de msys2.org) en C:\\msys64, con el compilador Clang.",
                      "Build tools: MSYS2 (free, from msys2.org) will be downloaded and installed in C:\\msys64, with the Clang compiler.");
            UpdateFolderInfo();
        }

        void UpdateFolderInfo()
        {
            string p = folderBox.Text.Trim();
            if (!Info.CleanPath(p))
                folderInfo.Text = L.T("Sin espacios, acentos ni signos: las herramientas de compilación no los admiten (por ejemplo C:\\PandorasTower).",
                                      "No spaces, accents or symbols: the build tools don't accept them (for example C:\\PandorasTower).");
            else if (File.Exists(Path.Combine(p, "setup.sh")))
                folderInfo.Text = L.T("Ya hay una instalación aquí: se actualizará, conservando tus partidas, opciones y paquetes de texturas.",
                                      "There is an installation here: it will be updated, keeping your saves, options and texture packs.");
            else
                folderInfo.Text = L.T("Se creará la carpeta.", "The folder will be created.");
            folderInfo.ForeColor = Info.CleanPath(p) ? Look.TextDim : Look.Bad;
            // el espacio: el que hace falta y el que hay
            string need = L.T("Espacio necesario: unos 3 GB", "Space required: about 3 GB") +
                          (File.Exists(Info.Bash) ? "" : L.T(" (y unos 3 GB en C: para MSYS2)", " (plus about 3 GB on C: for MSYS2)"));
            string free = "";
            try
            {
                if (p.Length >= 2 && p[1] == ':')
                {
                    var d = new DriveInfo(p.Substring(0, 1));
                    if (d.IsReady) free = L.T("Disponible en ", "Available on ") + p.Substring(0, 2) + " " + (d.AvailableFreeSpace / 1e9).ToString("0.0", CultureInfo.CurrentCulture) + " GB";
                    spaceInfo.ForeColor = d.IsReady && d.AvailableFreeSpace < 6e9 ? Look.Bad : Look.TextDim;
                }
            }
            catch (Exception) { }
            spaceInfo.Text = need + (free.Length > 0 ? "\r\n" + free : "");
        }

        void BuildIso(Rectangle a)
        {
            Panel pn = NewPage(Page.Iso, a);
            Para(pn, L.T("La ISO de tu copia del juego", "The ISO of your copy of the game"), 0, Look.P(24), false, 15);
            isoBox = Field(pn, Look.P(28), pn.Width - Look.P(140));
            var browse = new GoldButton(L.T("Examinar…", "Browse…"), false) { Location = new Point(pn.Width - Look.P(128), Look.P(24)), Size = new Size(Look.P(128), Look.P(34)) };
            browse.Click += delegate
            {
                using (var d = new OpenFileDialog { Title = L.T("Elige la ISO de Pandora's Tower", "Choose the Pandora's Tower ISO"),
                                                    Filter = L.T("Imagen ISO de Wii (*.iso)|*.iso", "Wii ISO image (*.iso)|*.iso") })
                    if (d.ShowDialog(this) == DialogResult.OK) isoBox.Text = d.FileName;
            };
            pn.Controls.Add(browse);
            isoInfo = Para(pn, "", Look.P(66), Look.P(48), true, 13);
            isoBox.TextChanged += delegate { CheckIso(); UpdateButtons(); };
            Para(pn, L.T(
                "Vale la de cualquier región. Si tienes el juego en WBFS o RVZ, conviértelo a ISO con Dolphin (clic derecho en el juego > " +
                "Convertir archivo > ISO). El juego la lee directamente cada vez que juegas: déjala donde está.",
                "Any region works. If your game is a WBFS or RVZ, convert it to ISO with Dolphin (right click on the game > Convert File > ISO). " +
                "The game reads it directly every time you play: leave it where it is."), Look.P(128), Look.P(90), true, 13);
        }

        void CheckIso()
        {
            string p = isoBox.Text.Trim();
            isoId = null;
            if (p.Length == 0)
            {
                bool upd = Info.Compiled(folderBox.Text.Trim());
                isoInfo.Text = upd ? L.T("Vacío: solo se actualiza la versión ya compilada.", "Empty: only the version already built is updated.") : "";
                isoInfo.ForeColor = Look.TextDim;
                return;
            }
            if (!File.Exists(p)) { isoInfo.Text = L.T("No existe ese archivo.", "That file doesn't exist."); isoInfo.ForeColor = Look.Bad; return; }
            string err;
            isoId = Info.GameId(p, out err);
            if (isoId != null) { isoInfo.Text = "✓ Pandora's Tower, " + L.T("versión ", "") + Info.GameName(isoId) + L.T("", " version"); isoInfo.ForeColor = Look.Gold; }
            else { isoInfo.Text = L.T("Esa imagen no sirve: ", "That image can't be used: ") + err + "."; isoInfo.ForeColor = Look.Bad; }
        }

        void BuildInstall(Rectangle a)
        {
            Panel pn = NewPage(Page.Install, a);
            status = Para(pn, "", 0, Look.P(26), false, 15);
            bar = new Bar { Location = new Point(0, Look.P(34)), Size = new Size(pn.Width - Look.P(70), Look.P(18)) };
            pn.Controls.Add(bar);
            percent = new Label { ForeColor = Look.Gold, BackColor = Look.Back, Font = Look.F(15, FontStyle.Bold), AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                                  Location = new Point(pn.Width - Look.P(64), Look.P(28)), Size = new Size(Look.P(64), Look.P(28)) };
            pn.Controls.Add(percent);
            elapsed = new Label { ForeColor = Look.TextDim, BackColor = Look.Back, Font = Look.F(13), AutoSize = false,
                                  Location = new Point(0, Look.P(58)), Size = new Size(pn.Width, Look.P(20)) };
            pn.Controls.Add(elapsed);
            logBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, BackColor = Look.Panel,
                                   ForeColor = Look.TextDim, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 12 * Look.S, GraphicsUnit.Pixel),
                                   Location = new Point(0, Look.P(84)), Size = new Size(pn.Width, pn.Height - Look.P(84)) };
            pn.Controls.Add(logBox);
        }

        void BuildDone(Rectangle a)
        {
            Panel pn = NewPage(Page.Done, a);
            doneTitle = Para(pn, "", 0, Look.P(30), false, 18);
            doneText = Para(pn, "", Look.P(40), Look.P(150), true, 15);
            playNow = Check(pn, L.T("Abrir Pandora's Tower ahora", "Open Pandora's Tower now"), Look.P(200), true);
            showLog = new GoldButton(L.T("Ver registro", "View log"), false) { Location = new Point(0, Look.P(240)), Size = new Size(Look.P(150), Look.P(36)), Visible = false };
            showLog.Click += delegate { try { Process.Start("notepad.exe", "\"" + LogPath + "\""); } catch (Exception) { } };
            pn.Controls.Add(showLog);
        }

        string Root { get { return folderBox.Text.Trim().TrimEnd(new[] { '\\' }); } }
        string LogPath { get { return Path.Combine(Root, @"build\instalar.log"); } }

        void Go(Page p)
        {
            page = p;
            foreach (var kv in pages) kv.Value.Visible = kv.Key == p;
            switch (p)
            {
                case Page.Welcome: stepTitle = L.T("Bienvenido", "Welcome"); break;
                case Page.Notice: stepTitle = L.T("Aviso y licencia", "Notice and license"); break;
                case Page.Folder: stepTitle = L.T("Dónde instalarlo", "Where to install"); UpdateFolderInfo(); break;
                case Page.Iso: stepTitle = L.T("Tu copia del juego", "Your copy of the game"); CheckIso(); break;
                case Page.Install: stepTitle = L.T("Instalando", "Installing"); break;
                case Page.Done: stepTitle = ok ? L.T("Listo", "Done") : L.T("No se pudo terminar", "Could not finish"); break;
            }
            next.Text = p == Page.Iso ? L.T("Instalar", "Install") : p == Page.Done ? L.T("Terminar", "Finish") : L.T("Siguiente", "Next");
            back.Visible = p == Page.Notice || p == Page.Folder || p == Page.Iso;
            next.Visible = p != Page.Install;
            cancel.Visible = p != Page.Done;
            UpdateButtons();
            Invalidate();
        }

        void UpdateButtons()
        {
            bool can = true;
            if (page == Page.Notice) can = accept.Checked;
            if (page == Page.Folder) can = Info.CleanPath(Root);
            if (page == Page.Iso) can = isoId != null || (isoBox.Text.Trim().Length == 0 && Info.Compiled(Root));
            if (page == Page.Install) can = false;
            next.Enabled = can;
        }

        void Next()
        {
            switch (page)
            {
                case Page.Welcome: Go(Page.Notice); break;
                case Page.Notice: Go(Page.Folder); break;
                case Page.Folder: Go(Page.Iso); break;
                case Page.Iso: Go(Page.Install); StartInstall(); break;
                case Page.Done:
                    if (ok && playNow.Checked)
                    {
                        string exe = Path.Combine(Root, "PandorasTower.exe");
                        try
                        {
                            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Root });
                            else Process.Start(new ProcessStartInfo(Path.Combine(Root, "Jugar.cmd")) { WorkingDirectory = Root });
                        }
                        catch (Exception) { }
                    }
                    Close();
                    break;
            }
        }

        void Back()
        {
            if (page == Page.Notice) Go(Page.Welcome);
            else if (page == Page.Folder) Go(Page.Notice);
            else if (page == Page.Iso) Go(Page.Folder);
        }

        void Cancel()
        {
            if (page != Page.Install) { Close(); return; }
            if (MessageBox.Show(this, L.T("¿Cancelar la instalación? Puedes volver a abrir el instalador más tarde: seguirá donde lo dejó.",
                                          "Cancel the installation? You can run the installer again later: it picks up where it left off."),
                                Info.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            cancelled = true;
            Process p = running;
            if (p != null) { try { Process.Start(new ProcessStartInfo("taskkill.exe", "/T /F /PID " + p.Id) { CreateNoWindow = true, UseShellExecute = false }); } catch (Exception) { } }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (page == Page.Install && worker != null && worker.IsAlive) { e.Cancel = true; Cancel(); }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int sw = Look.P(SideW);
            if (side != null) g.DrawImage(side, new Rectangle(0, 0, sw, ClientSize.Height));
            // sombra arriba y abajo, para que se lean el nombre y los pasos
            var top = new Rectangle(0, 0, sw, Look.P(150));
            using (var b = new LinearGradientBrush(top, Color.FromArgb(200, 10, 7, 9), Color.FromArgb(0, 10, 7, 9), LinearGradientMode.Vertical)) g.FillRectangle(b, top);
            var low = new Rectangle(0, ClientSize.Height - Look.P(230), sw, Look.P(230));
            using (var b = new LinearGradientBrush(low, Color.FromArgb(0, 10, 7, 9), Color.FromArgb(225, 10, 7, 9), LinearGradientMode.Vertical))
                g.FillRectangle(b, new Rectangle(low.X, low.Y + 1, low.Width, low.Height - 1));
            // el título de la página
            using (var f = Look.G(36))
            using (var b = new SolidBrush(Look.Gold))
                g.DrawString(stepTitle, f, b, sw + Look.P(32), Look.P(30));
            var rule = new Rectangle(sw + Look.P(36), Look.P(86), ClientSize.Width - sw - Look.P(72), Math.Max(1, Look.P(1)));
            using (var b = new LinearGradientBrush(new Rectangle(rule.X, rule.Y, rule.Width + 1, rule.Height + 1), Color.FromArgb(150, Look.Gold), Color.FromArgb(0, Look.Gold), LinearGradientMode.Horizontal))
                g.FillRectangle(b, rule);
            using (var f = Look.G(46))
            using (var sh = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
            using (var b = new SolidBrush(Look.Gold))
            {
                g.DrawString(Info.Name, f, sh, Look.P(26), Look.P(26));
                g.DrawString(Info.Name, f, b, Look.P(24), Look.P(24));
            }
            using (var f = Look.F(14))
            using (var b = new SolidBrush(Look.Text))
                g.DrawString(L.T("Port para PC · versión ", "PC port · version ") + Info.Version, f, b, Look.P(28), Look.P(84));
            // los pasos, abajo a la izquierda
            string[] steps = { L.T("Bienvenida", "Welcome"), L.T("Aviso", "Notice"), L.T("Carpeta", "Folder"), "ISO", L.T("Instalación", "Installation"), L.T("Fin", "Finish") };
            using (var f = Look.F(14))
                for (int i = 0; i < steps.Length; i++)
                {
                    bool cur = i == (int)page, past = i < (int)page;
                    int y = ClientSize.Height - Look.P(30) * (steps.Length - i) - Look.P(14);
                    using (var b = new SolidBrush(cur ? Look.Gold : past ? Look.Text : Look.TextDim))
                    {
                        g.FillEllipse(b, Look.P(30), y + Look.P(6), Look.P(8), Look.P(8));
                        g.DrawString(steps[i], f, b, Look.P(46), y);
                    }
                }
            // el marco dorado y la línea entre el arte y las páginas
            using (var pen = new Pen(Look.GoldDim, Math.Max(1f, Look.S)))
            {
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                g.DrawLine(pen, sw, 0, sw, ClientSize.Height);
            }
            // minimizar y cerrar
            using (var f = Look.F(18))
            using (var b = new SolidBrush(Look.TextDim))
            {
                g.DrawString("×", f, b, ClientSize.Width - Look.P(30), Look.P(6));
                g.FillRectangle(b, ClientSize.Width - Look.P(60), Look.P(19), Look.P(11), Math.Max(1, Look.P(2)));
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Y < Look.P(34) && e.X > ClientSize.Width - Look.P(36)) Cancel();
            else if (e.Y < Look.P(34) && e.X > ClientSize.Width - Look.P(68)) WindowState = FormWindowState.Minimized;
            base.OnMouseClick(e);
        }

        // ---- la instalación, en otro hilo -----------------------------------------------------------
        float progressFrom, progressTo, progressNow, creepTo;
        System.Windows.Forms.Timer creep;

        void StartInstall()
        {
            cancelled = false;
            next.Enabled = false;
            creep = new System.Windows.Forms.Timer { Interval = 500 };
            DateTime t0 = DateTime.Now;
            creep.Tick += delegate
            {
                TimeSpan t = DateTime.Now - t0;
                elapsed.Text = L.T("Tiempo: ", "Time: ") + (int)t.TotalMinutes + ":" + t.Seconds.ToString("00") +
                               L.T("  ·  puedes seguir usando el equipo", "  ·  you can keep using your PC");
                // en un paso largo sin noticias, la barra avanza despacio para que se vea que sigue vivo
                if (progressNow < creepTo) SetBar(progressNow + (creepTo - progressNow) * 0.006f);
            };
            creep.Start();
            string root = Root, iso = isoBox.Text.Trim();
            bool desk = desktopLink.Checked, start = startLink.Checked;
            worker = new Thread(() => Work(root, iso, desk, start)) { IsBackground = true };
            worker.Start();
        }

        void SetBar(float v)
        {
            progressNow = v;
            bar.Value = v;
            bar.Invalidate();
            percent.Text = (int)(v * 100) + " %";
        }

        void Ui(Action a) { try { if (IsHandleCreated) BeginInvoke(a); } catch (Exception) { } }

        // un paso de la instalación: su parte de la barra (de from a to). quiet: cuánto de esa parte
        // puede avanzar la barra sola, si el paso no da noticias de su avance
        DateTime started = DateTime.Now;
        string Clock()
        {
            TimeSpan t = DateTime.Now - started;
            return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
        }

        void Phase(string text, float from, float to, float quiet = 0.9f)
        {
            Ui(() =>
            {
                status.Text = text;
                progressFrom = from; progressTo = to; creepTo = from + (to - from) * quiet;
                SetBar(Math.Max(progressNow, from));
            });
            Log("");
            Log("== " + text + "   [" + Clock() + "]");
        }

        void At(float v)
        {
            Ui(() => { if (v > progressNow) { SetBar(v); creepTo = Math.Max(creepTo, Math.Min(progressTo, v + (progressTo - progressFrom) * 0.04f)); } });
        }

        readonly object logLock = new object();
        readonly StringBuilder pending = new StringBuilder();
        void Log(string line)
        {
            lock (logLock)
            {
                if (log != null) { log.WriteLine(line); log.Flush(); }
                pending.AppendLine(line);
            }
            Ui(() =>
            {
                string add;
                lock (logLock) { add = pending.ToString(); pending.Length = 0; }
                if (add.Length == 0) return;
                if (logBox.TextLength > 200000) logBox.Text = logBox.Text.Substring(logBox.TextLength - 100000);
                logBox.AppendText(add);
            });
        }

        void Fail(string why) { throw new InstallError(why); }
        class InstallError : Exception { public InstallError(string m) : base(m) { } }

        void Work(string root, string iso, bool desk, bool start)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "build"));
                log = new StreamWriter(Path.Combine(root, @"build\instalar.log"), false, new UTF8Encoding(false));
                started = DateTime.Now;
                Log(Info.Name + " " + Info.Version + " — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " — " + root);
                bool hadBuild = Info.Compiled(root);

                // 1. los archivos del port
                Phase(L.T("Copiando los archivos del port", "Copying the port files"), 0.00f, 0.02f);
                Extract(root);
                Register(root);

                // 2. MSYS2 (si no está)
                float t = 0.02f;
                if (!File.Exists(Info.Bash))
                {
                    Phase(L.T("Descargando MSYS2 (herramientas de compilación)", "Downloading MSYS2 (build tools)"), t, t + 0.10f, 0f);
                    string inst = Path.Combine(Path.GetTempPath(), "msys2-x86_64-latest.exe");
                    for (int n = 1; ; ++n)
                    {
                        try { Download(Info.MsysUrl, inst, t, t + 0.10f); break; }
                        catch (InstallError) { if (n == 3 || cancelled) throw; Log(L.T("aviso: la descarga se cortó; se vuelve a intentar", "warning: the download was cut; trying again")); Thread.Sleep(3000 * n); }
                    }
                    Phase(L.T("Instalando MSYS2", "Installing MSYS2"), t + 0.10f, t + 0.18f);
                    int rc = RunShell(inst, "in --confirm-command --accept-messages --root C:/msys64");
                    if (!File.Exists(Info.Bash)) Fail(L.T("MSYS2 no se instaló (código " + rc + "). Instálalo a mano desde https://www.msys2.org en C:\\msys64 y vuelve a abrir el instalador.",
                                                          "MSYS2 was not installed (code " + rc + "). Install it from https://www.msys2.org into C:\\msys64 and run the installer again."));
                    try { File.Delete(inst); } catch (Exception) { }
                    t += 0.18f;
                }

                // 3. el compilador y las librerías: si ya están todos, no se toca nada (ni se actualiza)
                Phase(L.T("Comprobando las herramientas de compilación", "Checking the build tools"), t, t + 0.01f);
                if (Bash(root, "pacman -Q " + Info.Packages + " >/dev/null 2>&1", null) == 0)
                    Log(L.T("Ya están todas instaladas.", "All of them are already installed."));
                else
                {
                    Phase(L.T("Actualizando MSYS2", "Updating MSYS2"), t, t + 0.08f);
                    Bash(root, "pacman -Syu --noconfirm", null);      // la primera puede reiniciar el propio pacman
                    Bash(root, "pacman -Syu --noconfirm", null);
                    Phase(L.T("Instalando el compilador y las librerías", "Installing the compiler and libraries"), t + 0.08f, t + 0.20f);
                    if (Bash(root, "pacman -S --needed --noconfirm " + Info.Packages, null) != 0 &&
                        Bash(root, "pacman -Sy --needed --noconfirm " + Info.Packages, null) != 0)   // una vez más, por si la conexión falló
                        Fail(L.T("no se pudieron instalar los paquetes de MSYS2 (¿hay conexión a Internet?).", "the MSYS2 packages could not be installed (is there an Internet connection?)."));
                    t += 0.20f;
                }
                t += 0.01f;

                // 4. el juego (setup.sh cuenta sus pasos y ninja los suyos: la barra los sigue)
                float c0 = t, c1 = 0.96f;
                if (iso.Length > 0)
                {
                    float mid = hadBuild ? c0 + (c1 - c0) * 0.8f : c1;
                    Phase(L.T("Compilando el juego, versión ", "Building the game, ") + Info.GameName(isoId) + L.T("", " version"), c0, mid, 0.15f);
                    if (Bash(root, "./setup.sh \"$(cygpath -u \"$PT_ISO\")\"", iso) != 0)
                        Fail(SetupFailed());
                    c0 = mid;
                }
                if (iso.Length == 0 || hadBuild)
                {
                    // también las demás versiones ya compiladas, con los arreglos de este port
                    Phase(L.T("Recompilando con esta versión del port", "Rebuilding with this version of the port"), c0, c1, 0.15f);
                    if (Bash(root, "./setup.sh", null) != 0)
                        Fail(SetupFailed());
                }

                // 5. el lanzador y los accesos directos
                Phase(L.T("Preparando el lanzador", "Preparing the launcher"), c1, 0.995f);
                if (Bash(root, "./lanzador.sh", null) != 0) Fail(L.T("no se pudo preparar el lanzador (lanzador.sh).", "the launcher could not be prepared (lanzador.sh)."));
                Links(root, desk, start);
                Ui(() => SetBar(1f));
                Log("");
                Log(L.T("Instalación terminada en ", "Installation finished in ") + Clock() + L.T(" (minutos:segundos).", " (minutes:seconds)."));
                ok = true;
            }
            catch (InstallError ex) { failure = ex.Message; }
            catch (Exception ex) { failure = cancelled ? L.T("cancelada.", "cancelled.") : ex.Message; }
            if (!ok && failure != null) Log("ERROR: " + failure);
            lock (logLock) { if (log != null) { log.Dispose(); log = null; } }
            Ui(Finish);
        }

        void Finish()
        {
            if (creep != null) creep.Stop();
            doneTitle.Text = ok ? L.T("Pandora's Tower está listo para jugar.", "Pandora's Tower is ready to play.")
                                : L.T("La instalación no se pudo terminar", "The installation could not be finished");
            doneTitle.ForeColor = ok ? Look.Gold : Look.Bad;
            doneText.Text = ok
                ? L.T("Ábrelo desde el acceso directo «Pandora's Tower»: elige ahí los gráficos, los fotogramas, los controles y el idioma, y pulsa Jugar.\r\n\r\n" +
                      "Para actualizar a una versión nueva del port, abre su instalador y elige esta misma carpeta: tus partidas y opciones se conservan.",
                      "Open it from the \"Pandora's Tower\" shortcut: choose graphics, frame rate, controls and language there, and press Play.\r\n\r\n" +
                      "To update to a new version of the port, run its installer and choose this same folder: your saves and options are kept.")
                : (cancelled ? L.T("Has cancelado la instalación. ", "You cancelled the installation. ") : L.T("Motivo: ", "Reason: ") + failure + "\r\n\r\n") +
                  L.T("Puedes volver a abrir el instalador: sigue donde lo dejó. Si pides ayuda, adjunta el registro (build\\instalar.log).",
                      "You can run the installer again: it picks up where it left off. If you ask for help, attach the log (build\\instalar.log).");
            playNow.Visible = ok;
            showLog.Visible = !ok;
            Go(Page.Done);
        }

        // para la vista previa (--captura): una página con datos de ejemplo
        public void Preview(int p)
        {
            Page pg = (Page)Math.Max(0, Math.Min(5, p));
            if (pg >= Page.Folder) accept.Checked = true;
            if (pg == Page.Iso)
            {
                isoBox.Text = @"D:\Juegos\Wii\PandorasTower.iso";
                Go(pg);
                isoId = "SX3P01";
                isoInfo.Text = "✓ Pandora's Tower, " + L.T("versión ", "") + Info.GameName(isoId) + L.T("", " version"); isoInfo.ForeColor = Look.Gold;
                UpdateButtons();
                return;
            }
            if (pg == Page.Install)
            {
                Go(Page.Install);
                status.Text = L.T("Compilando el juego, versión europea (SX3P01)", "Building the game, European (SX3P01) version");
                SetBar(0.62f);
                elapsed.Text = L.T("Tiempo: 14:07  ·  puedes seguir usando el equipo", "Time: 14:07  ·  you can keep using your PC");
                logBox.Text = "== 5/7 Recompilando el ejecutable a C++\r\n\r\n== 6/7 Fuentes del sistema y tabla del DSP (las libres de Dolphin)\r\n\r\n" +
                              "== 7/7 Compilando (varios minutos la primera vez)\r\n" +
                              "[1180/2071] Building CXX object CMakeFiles/game.dir/recomp/funcs_0412.cpp.obj\r\n" +
                              "[1181/2071] Building CXX object CMakeFiles/game.dir/recomp/funcs_0413.cpp.obj\r\n" +
                              "[1182/2071] Building CXX object CMakeFiles/game.dir/recomp/funcs_0414.cpp.obj";
                return;
            }
            if (pg == Page.Done) { ok = true; Finish(); return; }
            Go(pg);
        }

        // port.zip -> la carpeta (sin su primera carpeta); lo tuyo (opciones, partidas, texturas, build) no se toca
        void Extract(string root)
        {
            using (Stream s = Info.Resource("port.zip"))
            {
                if (s == null) Fail(L.T("al instalador le faltan los archivos del port.", "the installer is missing the port files."));
                using (var z = new ZipArchive(s, ZipArchiveMode.Read))
                {
                    int n = 0, same = 0;
                    foreach (ZipArchiveEntry e in z.Entries)
                    {
                        string name = e.FullName.Replace('/', '\\');
                        int cut = name.IndexOf('\\');
                        if (cut < 0) continue;
                        name = name.Substring(cut + 1);
                        if (name.Length == 0 || name.Contains("..") || Path.IsPathRooted(name)) continue;
                        string dst = Path.Combine(root, name);
                        if (name.EndsWith("\\")) { Directory.CreateDirectory(dst); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        byte[] data;
                        using (Stream es = e.Open()) using (var ms = new MemoryStream()) { es.CopyTo(ms); data = ms.ToArray(); }
                        // lo que no ha cambiado se deja como está, con su fecha: así no se recompila de más
                        if (File.Exists(dst) && new FileInfo(dst).Length == data.Length && Same(File.ReadAllBytes(dst), data)) { ++same; continue; }
                        if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal);
                        File.WriteAllBytes(dst, data);
                        ++n;
                    }
                    Log(n + L.T(" archivos nuevos o cambiados en ", " new or changed files in ") + root + (same > 0 ? " (" + same + L.T(" iguales)", " unchanged)") : ""));
                }
            }
            // este instalador, como desinstalador
            try { File.Copy(Application.ExecutablePath, Path.Combine(root, "Desinstalar.exe"), true); } catch (Exception ex) { Log(ex.Message); }
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        void Register(string root)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Info.UninstallKey))
                {
                    k.SetValue("DisplayName", Info.Name + L.T(" (port para PC)", " (PC port)"));
                    k.SetValue("DisplayVersion", Info.Version);
                    k.SetValue("Publisher", L.T("RecompPort (proyecto de aficionados)", "RecompPort (fan project)"));
                    k.SetValue("InstallLocation", root);
                    k.SetValue("DisplayIcon", Path.Combine(root, "Desinstalar.exe"));
                    k.SetValue("UninstallString", "\"" + Path.Combine(root, "Desinstalar.exe") + "\" /desinstalar");
                    k.SetValue("URLInfoAbout", "https://github.com/RecompPort/pandoras-tower-pc");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", 2500 * 1024, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { Log(ex.Message); }
        }

        void Links(string root, bool desk, bool start)
        {
            string exe = Path.Combine(root, "PandorasTower.exe");
            string target = File.Exists(exe) ? exe : Path.Combine(root, "Jugar.cmd");
            string icon = File.Exists(exe) ? exe + ",0" : Path.Combine(root, @"tools\lanzador\pandora.ico");
            try
            {
                // lanzador.sh ya deja el del escritorio
                if (!desk && File.Exists(Info.DesktopLink)) File.Delete(Info.DesktopLink);
                if (desk && !File.Exists(Info.DesktopLink)) Info.Shortcut(Info.DesktopLink, target, "", root, icon, Info.Name);
                if (start)
                {
                    Directory.CreateDirectory(Info.StartMenuDir);
                    Info.Shortcut(Path.Combine(Info.StartMenuDir, Info.Name + ".lnk"), target, "", root, icon, Info.Name);
                    Info.Shortcut(Path.Combine(Info.StartMenuDir, L.T("Desinstalar ", "Uninstall ") + Info.Name + ".lnk"),
                                  Path.Combine(root, "Desinstalar.exe"), "/desinstalar", root, Path.Combine(root, "Desinstalar.exe") + ",0", L.T("Desinstalar ", "Uninstall ") + Info.Name);
                }
            }
            catch (Exception ex) { Log(L.T("aviso: accesos directos: ", "warning: shortcuts: ") + ex.Message); }
        }

        void Download(string url, string dst, float from, float to)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;     // TLS 1.2
            using (var done = new ManualResetEvent(false))
            using (var wc = new WebClient())
            {
                Exception err = null;
                wc.DownloadProgressChanged += (s, e) =>
                {
                    if (e.TotalBytesToReceive > 0) At(from + (to - from) * (float)e.BytesReceived / e.TotalBytesToReceive);
                    if (cancelled) wc.CancelAsync();
                };
                wc.DownloadFileCompleted += (s, e) => { err = e.Error; if (e.Cancelled) err = new Exception(L.T("cancelada", "cancelled")); done.Set(); };
                wc.DownloadFileAsync(new Uri(url), dst);
                done.WaitOne();
                if (err != null) Fail(L.T("no se pudo descargar MSYS2 (", "MSYS2 could not be downloaded (") + err.Message + L.T("). Comprueba la conexión, o instálalo a mano desde https://www.msys2.org.", "). Check the connection, or install it from https://www.msys2.org."));
            }
        }

        // una orden en la terminal CLANG64 de MSYS2, en la carpeta del port. Va en un archivo
        // (build\paso.sh) para que las comillas y los apóstrofos de las rutas lleguen tal cual.
        int Bash(string root, string cmd, string iso)
        {
            string paso = Path.Combine(root, @"build\paso.sh");
            // exec </dev/null: este programa no tiene consola, así que bash y lo que lanza no tienen entrada
            // estándar válida, y algunas herramientas de MSYS2 (sha1sum) fallan sin ella
            File.WriteAllText(paso, "exec </dev/null\ncd \"$(cygpath -u \"$PT_ROOT\")\" || exit 1\n" + cmd + "\n", new UTF8Encoding(false));
            var env = new Dictionary<string, string>();
            env["MSYSTEM"] = "CLANG64";
            env["CHERE_INVOKING"] = "1";
            env["PT_ROOT"] = root;
            if (iso != null) env["PT_ISO"] = iso;
            return Run(Info.Bash, "-l \"" + paso.Replace('\\', '/') + "\"", env);
        }

        static readonly Regex StepRx = new Regex(@"^== ([1-7])/7 "), NinjaRx = new Regex(@"^\[(\d+)/(\d+)\] ");
        // dónde empieza cada uno de los 7 pasos de setup.sh, en su parte de la barra
        static readonly float[] StepAt = { 0f, 0.01f, 0.10f, 0.16f, 0.17f, 0.45f, 0.47f };

        // un programa con su propia ventana (el instalador de MSYS2): así Windows puede pedir permiso si hace falta
        int RunShell(string exe, string args)
        {
            if (cancelled) Fail(L.T("cancelada.", "cancelled."));
            using (Process p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized }))
            {
                running = p;
                p.WaitForExit();
                running = null;
                if (cancelled) Fail(L.T("cancelada.", "cancelled."));
                return p.ExitCode;
            }
        }

        string lastError;                                    // the last "ERROR: ..." of setup.sh / lanzador.sh

        // why setup.sh stopped, in its own words when it said so
        string SetupFailed()
        {
            return lastError != null
                ? L.T("falló la compilación del juego: ", "building the game failed: ") + lastError
                : L.T("falló la compilación del juego (setup.sh). Las últimas líneas del registro dicen por qué.", "building the game failed (setup.sh). The last lines of the log say why.");
        }

        int Run(string exe, string args, Dictionary<string, string> env)
        {
            if (cancelled) Fail(L.T("cancelada.", "cancelled."));
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            if (env != null) foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
            using (var p = new Process { StartInfo = psi })
            {
                DataReceivedEventHandler h = (s, e) =>
                {
                    if (e.Data == null) return;
                    if (e.Data.StartsWith("ERROR: ")) lastError = e.Data.Substring(7).Trim();
                    Log(e.Data.StartsWith("== ") ? e.Data + "   [" + Clock() + "]" : e.Data);
                    // el avance de setup.sh: sus pasos (== N/7) y los de ninja ([a/b]) en el último
                    // (la compilación con ninja, el paso 7, es con mucho el más largo)
                    Match m = StepRx.Match(e.Data);
                    if (m.Success) At(progressFrom + (progressTo - progressFrom) * StepAt[int.Parse(m.Groups[1].Value) - 1]);
                    m = NinjaRx.Match(e.Data);
                    if (m.Success)
                    {
                        float a = float.Parse(m.Groups[1].Value), b = float.Parse(m.Groups[2].Value);
                        if (b > 0) At(progressFrom + (progressTo - progressFrom) * (StepAt[6] + (1f - StepAt[6]) * a / b));
                    }
                };
                p.OutputDataReceived += h;
                p.ErrorDataReceived += h;
                p.Start();
                try { p.StandardInput.Close(); } catch (Exception) { }     // entrada vacía, pero válida
                running = p;
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                running = null;
                if (cancelled) Fail(L.T("cancelada.", "cancelled."));
                return p.ExitCode;
            }
        }
    }

    // ---- desinstalar -------------------------------------------------------------------------------
    static class Uninstaller
    {
        public static void Run(string root)
        {
            if (root == null)
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(Info.UninstallKey)) root = k != null ? k.GetValue("InstallLocation") as string : null; } catch (Exception) { }
                if (string.IsNullOrEmpty(root)) root = Path.GetDirectoryName(Application.ExecutablePath);
            }
            root = root.Trim().TrimEnd(new[] { '\\' });
            // solo una carpeta del port: nunca la raíz de un disco ni una carpeta cualquiera
            if (!Info.CleanPath(root) || root.Length <= 3 || !File.Exists(Path.Combine(root, "setup.sh")) || !File.Exists(Path.Combine(root, @"tools\pandora.cpp")))
            {
                MessageBox.Show(L.T("No encuentro la instalación del port en\n" + root + "\n\nBorra su carpeta a mano.",
                                    "I can't find the port installation in\n" + root + "\n\nDelete its folder by hand."), Info.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                try { Registry.CurrentUser.DeleteSubKeyTree(Info.UninstallKey, false); } catch (Exception) { }
                return;
            }
            // desde dentro de la carpeta no se puede borrar: una copia en Temp lo hace
            string self = Application.ExecutablePath;
            if (self.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string tmp = Path.Combine(Path.GetTempPath(), "pt-desinstalar.exe");
                File.Copy(self, tmp, true);
                Process.Start(tmp, "/desinstalar \"" + root + "\"");
                return;
            }
            if (MessageBox.Show(L.T("¿Desinstalar Pandora's Tower (port para PC)?\n\nSe borra la carpeta " + root + " entera, con tus opciones y tus paquetes de texturas.\n" +
                                    "Tu ISO no se toca. MSYS2 (C:\\msys64) se queda: puedes quitarlo aparte.",
                                    "Uninstall Pandora's Tower (PC port)?\n\nThe whole folder " + root + " is deleted, with your options and texture packs.\n" +
                                    "Your ISO is not touched. MSYS2 (C:\\msys64) stays: you can remove it separately."),
                                Info.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string nand = Path.Combine(root, @"build\nand");
            if (Directory.Exists(nand) &&
                MessageBox.Show(L.T("¿Guardar una copia de tus partidas en Documentos antes de borrar?", "Keep a copy of your saves in Documents before deleting?"),
                                Info.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                string dst = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pandora's Tower - " + L.T("partidas", "saves") + " " + DateTime.Now.ToString("yyyy-MM-dd HHmm"));
                try { CopyDir(nand, dst); } catch (Exception ex) { MessageBox.Show(ex.Message, Info.Name); return; }
            }
            Thread.Sleep(800);                                           // que se cierre el desinstalador de la carpeta
            var kept = new List<string>();
            try { DeleteTree(root, kept); }
            catch (Exception ex) { MessageBox.Show(L.T("No se pudo borrar todo (¿está el juego abierto?): ", "Could not delete everything (is the game running?): ") + ex.Message, Info.Name); }
            if (kept.Count > 0)
                MessageBox.Show(L.T("Tus imágenes de disco se quedan donde estaban:\n", "Your disc images stay where they were:\n") + string.Join("\n", kept.ToArray()), Info.Name);
            try { if (File.Exists(Info.DesktopLink)) File.Delete(Info.DesktopLink); } catch (Exception) { }
            try { if (Directory.Exists(Info.StartMenuDir)) Directory.Delete(Info.StartMenuDir, true); } catch (Exception) { }
            try { Registry.CurrentUser.DeleteSubKeyTree(Info.UninstallKey, false); } catch (Exception) { }
            MessageBox.Show(L.T("Pandora's Tower (port para PC) se ha desinstalado.", "Pandora's Tower (PC port) has been uninstalled."), Info.Name);
        }

        // como Directory.Delete(dir, true), pero también con los archivos de solo lectura (los de git),
        // y dejando las imágenes de disco (tu ISO, si la tenías dentro) con sus carpetas
        static readonly string[] Discs = { ".iso", ".wbfs", ".rvz", ".wia", ".gcz", ".ciso" };
        static void DeleteTree(string dir, List<string> kept)
        {
            foreach (string d in Directory.GetDirectories(dir))
            {
                if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) { Directory.Delete(d); continue; }   // un enlace: no se sigue
                DeleteTree(d, kept);
            }
            foreach (string f in Directory.GetFiles(dir))
            {
                if (Array.IndexOf(Discs, Path.GetExtension(f).ToLowerInvariant()) >= 0) { kept.Add(f); continue; }
                File.SetAttributes(f, FileAttributes.Normal);
                File.Delete(f);
            }
            if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir, false);
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] argv)
        {
            try { SetProcessDPIAware(); } catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (argv.Length >= 1 && argv[0] == "/desinstalar") { Uninstaller.Run(argv.Length >= 2 ? argv[1] : null); return; }
            float dpi = 96;
            using (var b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b)) dpi = g.DpiX;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            Look.Init(dpi);
            if (argv.Length >= 2 && argv[0] == "--captura")
            {
                // para ver el diseño: --captura FICHERO.png [página 0..5] abre la ventana, la fotografía y sale
                var w = new Wizard();
                w.Preview(argv.Length >= 3 ? int.Parse(argv[2]) : 0);
                var t = new System.Windows.Forms.Timer { Interval = 1200 };
                t.Tick += delegate
                {
                    t.Stop();
                    using (var bmp = new Bitmap(w.Width, w.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(w.Location, Point.Empty, w.Size);
                        bmp.Save(argv[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    w.Hide();
                    Application.ExitThread();
                };
                w.Shown += delegate { t.Start(); };
                Application.Run(w);
                return;
            }
            Application.Run(new Wizard());
        }
    }
}
