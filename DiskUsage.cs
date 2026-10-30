// DiskUsage - portable folder-size explorer for Windows.
// Single file, no dependencies (WinForms / .NET Framework that ships with Windows).
// Build: run build.bat  (uses the csc.exe included in Windows)
// Usage: DiskUsage.exe [folder]
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using System.Windows.Forms;

// ---------------------------------------------------------------- native
static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindFirstFileExW(string lpFileName, int fInfoLevelId,
        out WIN32_FIND_DATA lpFindFileData, int fSearchOp, IntPtr lpSearchFilter, int dwAdditionalFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll")]
    public static extern bool FindClose(IntPtr hFindFile);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern int StrCmpLogicalW(string a, string b);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hWnd, string app, string idlist);
}

// ---------------------------------------------------------------- languages
static class L
{
    public static readonly string[] Codes = { "en", "es", "fr", "de", "it", "pt" };
    public static readonly string[] Names = { "English", "Español", "Français", "Deutsch", "Italiano", "Português" };
    public static int Cur = 0;

    static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
    {
        // en, es, fr, de, it, pt
        { "back", new[] { "Back", "Atrás", "Retour", "Zurück", "Indietro", "Voltar" } },
        { "up", new[] { "Up", "Subir", "Dossier parent", "Nach oben", "Su", "Subir" } },
        { "refresh", new[] { "Refresh", "Actualizar", "Actualiser", "Aktualisieren", "Aggiorna", "Atualizar" } },
        { "explorer", new[] { "Open in Explorer", "Abrir en el Explorador", "Ouvrir dans l'Explorateur", "Im Explorer öffnen", "Apri in Esplora risorse", "Abrir no Explorador" } },
        { "showfiles", new[] { "Show files", "Mostrar archivos", "Afficher les fichiers", "Dateien anzeigen", "Mostra file", "Mostrar arquivos" } },
        { "lang", new[] { "Language:", "Idioma:", "Langue :", "Sprache:", "Lingua:", "Idioma:" } },
        { "name", new[] { "Name", "Nombre", "Nom", "Name", "Nome", "Nome" } },
        { "size", new[] { "Size", "Tamaño", "Taille", "Größe", "Dimensione", "Tamanho" } },
        { "percent", new[] { "%", "%", "%", "%", "%", "%" } },
        { "files", new[] { "[ {0} files ]", "[ {0} archivos ]", "[ {0} fichiers ]", "[ {0} Dateien ]", "[ {0} file ]", "[ {0} arquivos ]" } },
        { "calculating", new[] { "calculating…", "calculando…", "calcul…", "berechne…", "calcolo…", "calculando…" } },
        { "total", new[] { "Total", "Total", "Total", "Gesamt", "Totale", "Total" } },
        { "free", new[] { "Free: {0} of {1}", "Libre: {0} de {1}", "Libre : {0} sur {1}", "Frei: {0} von {1}", "Libero: {0} su {1}", "Livre: {0} de {1}" } },
        { "others", new[] { "Others", "Otros", "Autres", "Andere", "Altri", "Outros" } },
        { "scanning", new[] { "Scanning…", "Calculando…", "Analyse…", "Analysiere…", "Analisi…", "Analisando…" } },
        { "done", new[] { "Done in {0:0.0} s", "Terminado en {0:0.0} s", "Terminé en {0:0.0} s", "Fertig in {0:0.0} s", "Completato in {0:0.0} s", "Concluído em {0:0.0} s" } },
        { "denied", new[] {
            "Cannot open this folder (access denied or not found).",
            "No se puede abrir esta carpeta (acceso denegado o no existe).",
            "Impossible d'ouvrir ce dossier (accès refusé ou introuvable).",
            "Ordner kann nicht geöffnet werden (Zugriff verweigert oder nicht vorhanden).",
            "Impossibile aprire la cartella (accesso negato o inesistente).",
            "Não é possível abrir esta pasta (acesso negado ou inexistente)." } },
        { "copypath", new[] { "Copy path", "Copiar ruta", "Copier le chemin", "Pfad kopieren", "Copia percorso", "Copiar caminho" } },
        { "open", new[] { "Open", "Abrir", "Ouvrir", "Öffnen", "Apri", "Abrir" } },
        { "show", new[] { "Show in Explorer", "Mostrar en el Explorador", "Afficher dans l'Explorateur", "Im Explorer anzeigen", "Mostra in Esplora risorse", "Mostrar no Explorador" } },
        { "delete", new[] { "Delete", "Eliminar", "Supprimer", "Löschen", "Elimina", "Excluir" } },
        { "deleteperm", new[] { "Delete permanently", "Eliminar permanentemente", "Supprimer définitivement", "Endgültig löschen", "Elimina definitivamente", "Excluir permanentemente" } },
        { "confirmbin", new[] {
            "Move to the Recycle Bin?\n\n{0}", "¿Mover a la Papelera de reciclaje?\n\n{0}", "Déplacer vers la Corbeille ?\n\n{0}",
            "In den Papierkorb verschieben?\n\n{0}", "Spostare nel Cestino?\n\n{0}", "Mover para a Lixeira?\n\n{0}" } },
        { "confirmperm", new[] {
            "This will PERMANENTLY delete the item. It cannot be undone.\n\n{0}",
            "Se eliminará DEFINITIVAMENTE. No se puede deshacer.\n\n{0}",
            "L'élément sera supprimé DÉFINITIVEMENT. Action irréversible.\n\n{0}",
            "Das Element wird ENDGÜLTIG gelöscht. Dies kann nicht rückgängig gemacht werden.\n\n{0}",
            "L'elemento verrà eliminato DEFINITIVAMENTE. Operazione irreversibile.\n\n{0}",
            "O item será excluído PERMANENTEMENTE. Não é possível desfazer.\n\n{0}" } },
        { "deleting", new[] { "Deleting…", "Eliminando…", "Suppression…", "Lösche…", "Eliminazione…", "Excluindo…" } },
        { "deleteerror", new[] { "Could not delete: {0}", "No se pudo eliminar: {0}", "Impossible de supprimer : {0}", "Löschen nicht möglich: {0}", "Impossibile eliminare: {0}", "Não foi possível excluir: {0}" } },
        { "items", new[] { "{0} items", "{0} elementos", "{0} éléments", "{0} Elemente", "{0} elementi", "{0} itens" } },
    };

    public static string Get(string key) { return T[key][Cur]; }
}

// ---------------------------------------------------------------- helpers
static class Fmt
{
    public static string Size(long b)
    {
        if (b < 1024) return b.ToString("N0") + " B";
        string[] u = { "KB", "MB", "GB", "TB", "PB" };
        double v = b; int i = -1;
        do { v /= 1024; i++; } while (v >= 1024 && i < u.Length - 1);
        return v.ToString(v >= 100 ? "0" : (v >= 10 ? "0.0" : "0.00")) + " " + u[i];
    }
}

static class Ini
{
    static string FilePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DiskUsage.ini"); } }

    public static Dictionary<string, string> Load()
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        try
        {
            if (File.Exists(FilePath))
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
        }
        catch { }
        return d;
    }

    public static void Save(Dictionary<string, string> d)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in d) sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }
}

// ---------------------------------------------------------------- scanner
class DirInfo { public long Total; public long Stamp; }

class Entry
{
    public string Name;
    public string Path;
    public long Size = -1;       // -1 = still calculating
    public bool IsDir;
    public bool IsAgg;           // "[ N files ]" row
    public int Count;

    public string DisplayName()
    {
        return IsAgg ? string.Format(L.Get("files"), Count) : Name;
    }
}

static class Scanner
{
    public static readonly ConcurrentDictionary<string, DirInfo> Cache =
        new ConcurrentDictionary<string, DirInfo>(StringComparer.OrdinalIgnoreCase);

    const int CacheDepth = 3;                                   // levels below the scanned folder kept in cache
    const long CacheTtl = 120L * TimeSpan.TicksPerSecond;       // F5 clears it anyway
    const uint SkipFile = 0x441000;                             // offline / recall-on-open / recall-on-data-access (cloud placeholders)
    static readonly IntPtr Invalid = new IntPtr(-1);
    static readonly ParallelOptions PO = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount * 2) };

    public static ParallelOptions Options { get { return PO; } }

    public static string Join(string p, string n) { return p.TrimEnd('\\') + "\\" + n; }
    static string Key(string p) { return p.TrimEnd('\\'); }

    static string Query(string path)
    {
        string p = path.TrimEnd('\\');
        if (p.StartsWith(@"\\?\")) return p + @"\*";
        if (p.StartsWith(@"\\")) return @"\\?\UNC\" + p.Substring(2) + @"\*";
        return @"\\?\" + p + @"\*";
    }

    public static bool TryGet(string path, out DirInfo di)
    {
        if (Cache.TryGetValue(Key(path), out di) && DateTime.UtcNow.Ticks - di.Stamp < CacheTtl) return true;
        di = null;
        return false;
    }

    // Drop cache entries affected by a change in 'path' (itself, its descendants and its ancestors)
    public static void Invalidate(string path)
    {
        string k = Key(path);
        foreach (string key in Cache.Keys)
        {
            if (key.Equals(k, StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith(k + "\\", StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(key + "\\", StringComparison.OrdinalIgnoreCase))
            {
                DirInfo d;
                Cache.TryRemove(key, out d);
            }
        }
    }

    public static void Store(string path, long total)
    {
        Cache[Key(path)] = new DirInfo { Total = total, Stamp = DateTime.UtcNow.Ticks };
    }

    // One level only: sub folder names + file names/sizes.
    public static bool ListLevel(string path, List<string> dirs, List<string> fileNames, List<long> fileSizes)
    {
        Native.WIN32_FIND_DATA fd;
        IntPtr h = Native.FindFirstFileExW(Query(path), 1 /*basic*/, out fd, 0, IntPtr.Zero, 2 /*large fetch*/);
        if (h == Invalid) return false;
        try
        {
            do
            {
                uint a = fd.dwFileAttributes;
                string n = fd.cFileName;
                if ((a & 0x10) != 0)
                {
                    if ((a & 0x400) != 0) continue;                 // junction / symlink: skip
                    if (n == "." || n == "..") continue;
                    dirs.Add(n);
                }
                else
                {
                    if ((a & SkipFile) != 0) continue;
                    fileNames.Add(n);
                    fileSizes.Add(((long)fd.nFileSizeHigh << 32) | fd.nFileSizeLow);
                }
            } while (Native.FindNextFileW(h, out fd));
        }
        finally { Native.FindClose(h); }
        return true;
    }

    // Recursive total size. depth: 1 = direct child of the folder being displayed.
    public static long Scan(string path, int depth, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return 0;
        long own = 0;
        List<string> subs = null;
        Native.WIN32_FIND_DATA fd;
        IntPtr h = Native.FindFirstFileExW(Query(path), 1, out fd, 0, IntPtr.Zero, 2);
        if (h == Invalid) return 0;
        try
        {
            do
            {
                uint a = fd.dwFileAttributes;
                if ((a & 0x10) != 0)
                {
                    if ((a & 0x400) != 0) continue;
                    string n = fd.cFileName;
                    if (n == "." || n == "..") continue;
                    if (subs == null) subs = new List<string>();
                    subs.Add(Join(path, n));
                }
                else
                {
                    if ((a & SkipFile) != 0) continue;
                    own += ((long)fd.nFileSizeHigh << 32) | fd.nFileSizeLow;
                }
            } while (Native.FindNextFileW(h, out fd));
        }
        finally { Native.FindClose(h); }

        long total = own;
        if (subs != null)
        {
            if (depth < 3 && subs.Count > 1)
            {
                long sum = 0;
                Parallel.ForEach(subs, PO, delegate(string d)
                {
                    long s = Scan(d, depth + 1, ct);
                    Interlocked.Add(ref sum, s);
                });
                total += sum;
            }
            else
            {
                foreach (string d in subs) total += Scan(d, depth + 1, ct);
            }
        }
        if (depth <= CacheDepth && !ct.IsCancellationRequested) Store(path, total);
        return total;
    }
}

// ---------------------------------------------------------------- controls
class FastListView : ListView
{
    public FastListView() { DoubleBuffered = true; }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { Native.SetWindowTheme(Handle, "explorer", null); } catch { }
    }
}

class Slice
{
    public Entry E;
    public string Name;
    public long Size;
    public double Pct;
    public Color Col;
}

class PieControl : Control
{
    public List<Slice> Slices = new List<Slice>();
    public string Title = "";
    public Entry Selected;
    public float Sc = 1f;
    public event Action<Slice> SliceClicked;
    public event Action<Slice> SliceDoubleClicked;

    int hover = -1;
    ToolTip tip = new ToolTip();
    int state;   // 0 = nothing, 1 = loading (spinner), 2 = loaded (green dot)
    int step;
    System.Windows.Forms.Timer anim;

    public PieControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = SystemColors.Window;
        anim = new System.Windows.Forms.Timer { Interval = 70 };
        anim.Tick += delegate { step = (step + 1) % 12; Invalidate(IndicatorRect()); };
    }

    public void SetState(int s)
    {
        state = s;
        if (s == 1) anim.Start(); else anim.Stop();
        Invalidate();
    }

    Rectangle IndicatorRect()
    {
        int sz = (int)(18 * Sc), pad = (int)(12 * Sc);
        return new Rectangle(Width - pad - sz, (int)(pad * 0.6f + (24 * Sc - sz) / 2f), sz, sz);
    }

    void DrawIndicator(Graphics g)
    {
        Rectangle ir = IndicatorRect();
        float cx = ir.X + ir.Width / 2f, cy = ir.Y + ir.Height / 2f;
        if (state == 1)
        {
            float r1 = ir.Width * 0.24f, r2 = ir.Width * 0.5f;
            for (int i = 0; i < 12; i++)
            {
                int a = 255 * (((i - step + 12) % 12) + 1) / 12;
                double ang = i * 30 * Math.PI / 180.0;
                float sn = (float)Math.Sin(ang), cs = (float)Math.Cos(ang);
                using (Pen p = new Pen(Color.FromArgb(a, 70, 130, 200), 2f * Sc))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, cx + sn * r1, cy - cs * r1, cx + sn * r2, cy - cs * r2);
                }
            }
        }
        else if (state == 2)
        {
            float d = 10 * Sc;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 170, 70)))
                g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
        }
    }

    public void SetData(List<Slice> s, string title)
    {
        Slices = s; Title = title;
        if (hover >= Slices.Count) hover = -1;
        Invalidate();
    }

    void Geo(out Rectangle pie, out int legendTop, out int rowH)
    {
        rowH = (int)(22 * Sc);
        int titleH = (int)(28 * Sc);
        int pad = (int)(12 * Sc);
        int legendH = Slices.Count * rowH;
        int d = Math.Min(Width - 2 * pad, Height - titleH - legendH - 2 * pad);
        if (d < (int)(60 * Sc)) d = (int)(60 * Sc);
        pie = new Rectangle((Width - d) / 2, titleH + pad, d, d);
        legendTop = pie.Bottom + pad;
    }

    double Sum()
    {
        double s = 0;
        foreach (Slice x in Slices) s += x.Size;
        return s;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        int pad = (int)(12 * Sc);

        using (Font bold = new Font(Font, FontStyle.Bold))
        using (StringFormat tf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(Title, bold, SystemBrushes.ControlText, new RectangleF(pad, pad * 0.6f, Width - 2 * pad - 24 * Sc, 24 * Sc), tf);

        DrawIndicator(g);

        double sum = Sum();
        if (sum <= 0 || Slices.Count == 0) return;

        Rectangle pr; int lt, rh;
        Geo(out pr, out lt, out rh);

        float start = -90f;
        using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (Font small = new Font(Font.FontFamily, Font.Size - 0.5f, FontStyle.Bold))
        {
            for (int i = 0; i < Slices.Count; i++)
            {
                Slice sl = Slices[i];
                float sweep = (float)(sl.Size * 360.0 / sum);
                if (sweep < 0.05f) continue;
                double mid = (start + sweep / 2) * Math.PI / 180.0;
                Rectangle r = pr;
                bool ex = (i == hover) || (sl.E != null && sl.E == Selected);
                if (ex)
                {
                    int off = (int)(6 * Sc);
                    r.Offset((int)Math.Round(Math.Cos(mid) * off), (int)Math.Round(Math.Sin(mid) * off));
                }
                using (SolidBrush b = new SolidBrush(sl.Col)) g.FillPie(b, r, start, sweep);
                if (Slices.Count > 1 && sweep < 359.9f)
                    using (Pen p = new Pen(BackColor, 1.5f * Sc)) g.DrawPie(p, r, start, sweep);

                if (sweep >= 18f)
                {
                    float rad = r.Width * 0.34f;
                    float cx = r.X + r.Width / 2f + (float)Math.Cos(mid) * rad;
                    float cy = r.Y + r.Height / 2f + (float)Math.Sin(mid) * rad;
                    string t = sl.Pct.ToString("0.#") + "%";
                    g.DrawString(t, small, Brushes.Black, cx + 1, cy + 1, center);
                    g.DrawString(t, small, Brushes.White, cx, cy, center);
                }
                start += sweep;
            }
        }

        // legend
        int box = (int)(12 * Sc);
        int sizeW = (int)(78 * Sc), pctW = (int)(54 * Sc);
        using (StringFormat nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
        using (StringFormat rf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        using (Font bold = new Font(Font, FontStyle.Bold))
        {
            for (int i = 0; i < Slices.Count; i++)
            {
                Slice sl = Slices[i];
                int y = lt + i * rh;
                Font f = (i == hover) ? bold : Font;
                if (i == hover)
                    using (SolidBrush hb = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                        g.FillRectangle(hb, 0, y, Width, rh);
                using (SolidBrush b = new SolidBrush(sl.Col))
                    g.FillRectangle(b, pad, y + (rh - box) / 2, box, box);
                int nx = pad + box + (int)(6 * Sc);
                int nw = Math.Max(10, Width - pad - pctW - sizeW - nx);
                g.DrawString(sl.Name, f, SystemBrushes.ControlText, new RectangleF(nx, y, nw, rh), nf);
                g.DrawString(Fmt.Size(sl.Size), f, SystemBrushes.ControlText, new RectangleF(Width - pad - pctW - sizeW, y, sizeW, rh), rf);
                g.DrawString(sl.Pct.ToString("0.0") + "%", f, SystemBrushes.ControlText, new RectangleF(Width - pad - pctW, y, pctW, rh), rf);
            }
        }
    }

    int HitTest(Point p)
    {
        if (Slices.Count == 0) return -1;
        Rectangle pr; int lt, rh;
        Geo(out pr, out lt, out rh);
        if (p.Y >= lt && p.Y < lt + Slices.Count * rh) return (p.Y - lt) / rh;
        double cx = pr.X + pr.Width / 2.0, cy = pr.Y + pr.Height / 2.0;
        double dx = p.X - cx, dy = p.Y - cy, r = pr.Width / 2.0;
        if (dx * dx + dy * dy > r * r) return -1;
        double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI + 90.0;
        if (ang < 0) ang += 360.0;
        double sum = Sum(), acc = 0;
        for (int i = 0; i < Slices.Count; i++)
        {
            acc += Slices[i].Size * 360.0 / sum;
            if (ang < acc) return i;
        }
        return Slices.Count - 1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitTest(e.Location);
        if (h != hover)
        {
            hover = h;
            Cursor = (h >= 0 && Slices[h].E != null && Slices[h].E.IsDir) ? Cursors.Hand : Cursors.Default;
            Invalidate();
            if (h >= 0)
                tip.Show(Slices[h].Name + "\n" + Fmt.Size(Slices[h].Size) + "  (" + Slices[h].Pct.ToString("0.0") + "%)",
                         this, e.X + (int)(16 * Sc), e.Y + (int)(16 * Sc), 4000);
            else tip.Hide(this);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = -1; tip.Hide(this); Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int h = HitTest(e.Location);
        if (h >= 0 && SliceClicked != null) SliceClicked(Slices[h]);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int h = HitTest(e.Location);
        if (h >= 0 && SliceDoubleClicked != null) SliceDoubleClicked(Slices[h]);
    }
}

// ---------------------------------------------------------------- main form
class MainForm : Form
{
    float sc;
    int S(int v) { return (int)Math.Round(v * sc); }

    TableLayoutPanel bar;
    Button btnBack, btnUp, btnRefresh, btnExplorer;
    ComboBox cmbDrive, cmbLang;
    TextBox txtPath;
    CheckBox chkFiles;
    Label lblLang;
    SplitContainer split;
    FastListView lv;
    PieControl pie;
    StatusStrip status;
    ToolStripStatusLabel lblStatus, lblTotal, lblFree;
    ContextMenuStrip menu;
    ToolStripMenuItem miOpen, miShow, miCopy, miDelete, miDeletePerm;
    bool deleting;
    System.Windows.Forms.Timer timer;

    string curPath;
    List<Entry> entries = new List<Entry>();
    CancellationTokenSource cts = new CancellationTokenSource();
    bool scanning;
    int scanId;
    int sortCol = 1;
    bool sortAsc = false;
    Stack<string> history = new Stack<string>();
    Stopwatch sw = new Stopwatch();
    double doneSecs = -1;
    long totalSize;
    string freeText = "";
    bool suppress;
    string pendingSelect;
    Dictionary<string, string> ini;

    static readonly Color[] Palette = {
        Color.FromArgb(78,121,167), Color.FromArgb(242,142,43), Color.FromArgb(225,87,89),
        Color.FromArgb(118,183,178), Color.FromArgb(89,161,79), Color.FromArgb(214,176,30),
        Color.FromArgb(176,122,161), Color.FromArgb(240,120,150), Color.FromArgb(156,117,95) };
    static readonly Color OthersColor = Color.FromArgb(150, 150, 150);

    public MainForm(string startArg)
    {
        using (Graphics g0 = Graphics.FromHwnd(IntPtr.Zero)) sc = g0.DpiX / 96f;
        ini = Ini.Load();

        string code = ini.ContainsKey("lang") ? ini["lang"] : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        int li = Array.IndexOf(L.Codes, code);
        L.Cur = li >= 0 ? li : 0;

        Font = SystemFonts.MessageBoxFont;
        Text = "DiskUsage";
        StartPosition = FormStartPosition.CenterScreen;
        int w = S(1000), h = S(620);
        int iw, ih;
        if (ini.ContainsKey("w") && int.TryParse(ini["w"], out iw) && iw > 400) w = iw;
        if (ini.ContainsKey("h") && int.TryParse(ini["h"], out ih) && ih > 300) h = ih;
        ClientSize = new Size(w, h);
        MinimumSize = new Size(S(720), S(420));
        if (ini.ContainsKey("max") && ini["max"] == "1") WindowState = FormWindowState.Maximized;
        int sCol;
        if (ini.ContainsKey("sortcol") && int.TryParse(ini["sortcol"], out sCol) && sCol >= 0 && sCol <= 2) sortCol = sCol;
        if (ini.ContainsKey("asc")) sortAsc = ini["asc"] == "1";

        // ---- toolbar
        bar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 9, RowCount = 1, Padding = new Padding(S(4), S(4), S(4), S(2)) };
        for (int i = 0; i < 9; i++)
            bar.ColumnStyles.Add(i == 5 ? new ColumnStyle(SizeType.Percent, 100f) : new ColumnStyle(SizeType.AutoSize));

        btnBack = MkButton(); btnUp = MkButton(); btnRefresh = MkButton(); btnExplorer = MkButton();
        cmbDrive = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(64), Anchor = AnchorStyles.Left };
        foreach (DriveInfo d in DriveInfo.GetDrives()) cmbDrive.Items.Add(d.Name);
        txtPath = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        chkFiles = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, Checked = ini.ContainsKey("files") && ini["files"] == "1" };
        lblLang = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(S(8), 0, 0, 0) };
        cmbLang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(105), Anchor = AnchorStyles.Left };
        foreach (string n in L.Names) cmbLang.Items.Add(n);
        cmbLang.SelectedIndex = L.Cur;

        bar.Controls.Add(btnBack, 0, 0); bar.Controls.Add(btnUp, 1, 0); bar.Controls.Add(btnRefresh, 2, 0);
        bar.Controls.Add(btnExplorer, 3, 0); bar.Controls.Add(cmbDrive, 4, 0); bar.Controls.Add(txtPath, 5, 0);
        bar.Controls.Add(chkFiles, 6, 0); bar.Controls.Add(lblLang, 7, 0); bar.Controls.Add(cmbLang, 8, 0);

        // ---- status bar
        status = new StatusStrip { SizingGrip = true };
        lblStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        lblTotal = new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left };
        lblFree = new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left };
        status.Items.Add(lblStatus); status.Items.Add(lblTotal); status.Items.Add(lblFree);

        // ---- list
        ImageList imgs = new ImageList { ImageSize = new Size(S(16), S(16)), ColorDepth = ColorDepth.Depth32Bit };
        imgs.Images.Add(MakeIcon(0)); imgs.Images.Add(MakeIcon(1)); imgs.Images.Add(MakeIcon(2));
        lv = new FastListView
        {
            Dock = DockStyle.Fill, View = View.Details, VirtualMode = true, FullRowSelect = true,
            HideSelection = false, MultiSelect = false, SmallImageList = imgs, BorderStyle = BorderStyle.None
        };
        lv.Columns.Add("", S(300));
        lv.Columns.Add("", S(100), HorizontalAlignment.Right);
        lv.Columns.Add("", S(70), HorizontalAlignment.Right);
        lv.RetrieveVirtualItem += OnRetrieve;
        lv.ColumnClick += OnColClick;
        lv.ItemActivate += delegate { OpenSelected(); };
        lv.SelectedIndexChanged += OnSelChanged;
        lv.SizeChanged += delegate { FitNameColumn(); };
        lv.KeyDown += delegate(object s, KeyEventArgs ev)
        {
            if (ev.KeyCode == Keys.Delete) { ev.Handled = true; DeleteSelected(ev.Shift); }
        };

        menu = new ContextMenuStrip();
        miOpen = new ToolStripMenuItem(); miShow = new ToolStripMenuItem(); miCopy = new ToolStripMenuItem();
        miDelete = new ToolStripMenuItem { ShortcutKeyDisplayString = "Del" };
        miDeletePerm = new ToolStripMenuItem { ShortcutKeyDisplayString = "Shift+Del" };
        miDelete.Click += delegate { DeleteSelected(false); };
        miDeletePerm.Click += delegate { DeleteSelected(true); };
        miOpen.Click += delegate { OpenSelected(); };
        miShow.Click += delegate { ShowSelectedInExplorer(); };
        miCopy.Click += delegate { Entry en = SelectedEntry(); if (en != null) try { Clipboard.SetText(en.Path); } catch { } };
        menu.Items.AddRange(new ToolStripItem[] { miOpen, miShow, miCopy, new ToolStripSeparator(), miDelete, miDeletePerm });
        menu.Opening += delegate(object s, System.ComponentModel.CancelEventArgs ev)
        {
            Entry en = SelectedEntry();
            if (en == null) { ev.Cancel = true; return; }
            miOpen.Enabled = en.IsDir;
            miDelete.Enabled = miDeletePerm.Enabled = !en.IsAgg && !deleting;
        };
        lv.ContextMenuStrip = menu;

        // ---- pie
        pie = new PieControl { Dock = DockStyle.Fill, Sc = sc };
        pie.SliceClicked += delegate(Slice sl) { if (sl.E != null) SelectEntry(sl.E); };
        pie.SliceDoubleClicked += delegate(Slice sl) { if (sl.E != null && sl.E.IsDir) Navigate(sl.E.Path, true, null); };

        split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, SplitterWidth = S(5) };
        split.Panel1.Controls.Add(lv);
        split.Panel2.Controls.Add(pie);

        Controls.Add(split);
        Controls.Add(status);
        Controls.Add(bar);

        // ---- events
        btnBack.Click += delegate { GoBack(); };
        btnUp.Click += delegate { GoUp(); };
        btnRefresh.Click += delegate { DoRefresh(); };
        btnExplorer.Click += delegate { try { Process.Start(curPath); } catch { } };
        cmbDrive.SelectionChangeCommitted += delegate { if (cmbDrive.SelectedItem != null) Navigate((string)cmbDrive.SelectedItem, true, null); };
        txtPath.KeyDown += delegate(object s, KeyEventArgs ev)
        {
            if (ev.KeyCode == Keys.Enter) { ev.SuppressKeyPress = true; Navigate(txtPath.Text.Trim().Trim('"'), true, null); lv.Focus(); }
            else if (ev.KeyCode == Keys.Escape) { txtPath.Text = curPath; lv.Focus(); }
        };
        chkFiles.CheckedChanged += delegate { if (curPath != null) Navigate(curPath, false, null); };
        cmbLang.SelectedIndexChanged += delegate { if (suppress) return; L.Cur = cmbLang.SelectedIndex; ApplyLanguage(); };

        timer = new System.Windows.Forms.Timer { Interval = 120 };
        timer.Tick += OnTick;

        Shown += delegate
        {
            split.Panel1MinSize = S(280);
            split.Panel2MinSize = S(300);
            split.SplitterDistance = Math.Max(S(280), split.Width - S(440));
            FitNameColumn();
        };
        FormClosing += delegate { CancelScan(); SaveIni(); };

        ApplyLanguage();

        string start = startArg;
        if (start == null || !Directory.Exists(start))
        {
            start = ini.ContainsKey("path") ? ini["path"] : null;
            if (start == null || !Directory.Exists(start))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dl = Path.Combine(home, "Downloads");
                start = Directory.Exists(dl) ? dl : home;
            }
        }
        Navigate(start, false, null);
        lv.Focus();
    }

    Button MkButton()
    {
        return new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left, Padding = new Padding(S(3), S(1), S(3), S(1)) };
    }

    Bitmap MakeIcon(int kind)
    {
        int sz = S(16);
        Bitmap bmp = new Bitmap(sz, sz);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(sz / 16f, sz / 16f);
            if (kind == 0)
            {
                using (Brush b1 = new SolidBrush(Color.FromArgb(222, 170, 50)))
                using (Brush b2 = new SolidBrush(Color.FromArgb(250, 205, 90)))
                {
                    g.FillRectangle(b1, 1, 2, 6, 3);
                    g.FillRectangle(b2, 1, 4, 14, 10);
                }
            }
            else
            {
                if (kind == 2) { g.FillRectangle(Brushes.White, 2, 1, 9, 11); g.DrawRectangle(Pens.SlateGray, 2, 1, 9, 11); }
                int ox = kind == 2 ? 4 : 3, oy = kind == 2 ? 4 : 1;
                g.FillRectangle(Brushes.White, ox, oy, 9, 11);
                g.DrawRectangle(Pens.SlateGray, ox, oy, 9, 11);
                g.DrawLine(Pens.LightSteelBlue, ox + 2, oy + 3, ox + 7, oy + 3);
                g.DrawLine(Pens.LightSteelBlue, ox + 2, oy + 6, ox + 7, oy + 6);
            }
        }
        return bmp;
    }

    // ------------------------------------------------------------ language
    void ApplyLanguage()
    {
        btnBack.Text = "←  " + L.Get("back");
        btnUp.Text = "↑  " + L.Get("up");
        btnRefresh.Text = "↻  " + L.Get("refresh");
        btnExplorer.Text = "↗  " + L.Get("explorer");
        chkFiles.Text = L.Get("showfiles");
        lblLang.Text = L.Get("lang");
        miOpen.Text = L.Get("open");
        miShow.Text = L.Get("show");
        miCopy.Text = L.Get("copypath");
        miDelete.Text = L.Get("delete");
        miDeletePerm.Text = L.Get("deleteperm");
        UpdateHeaders();
        lv.Invalidate();
        if (curPath != null) { UpdatePie(); freeText = ComputeFree(); UpdateStatus(); }
    }

    void UpdateHeaders()
    {
        string[] k = { "name", "size", "percent" };
        for (int i = 0; i < 3; i++)
            lv.Columns[i].Text = L.Get(k[i]) + (i == sortCol ? (sortAsc ? " ▲" : " ▼") : "");
    }

    // ------------------------------------------------------------ navigation
    void Navigate(string path, bool push, string selectAfter)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
            if (full.Length == 2 && full[1] == ':') full += "\\";
        }
        catch { lblStatus.Text = L.Get("denied"); return; }

        List<string> dirs = new List<string>();
        List<string> fn = new List<string>();
        List<long> fs = new List<long>();
        if (!Scanner.ListLevel(full, dirs, fn, fs))
        {
            lblStatus.Text = L.Get("denied");
            if (curPath != null) txtPath.Text = curPath;
            return;
        }

        CancelScan();
        if (push && curPath != null && !string.Equals(curPath, full, StringComparison.OrdinalIgnoreCase)) history.Push(curPath);
        curPath = full;
        txtPath.Text = full;
        Text = full + "  -  DiskUsage";
        SyncDrive(full);
        pendingSelect = selectAfter;

        // build rows
        List<Entry> list = new List<Entry>(dirs.Count + (chkFiles.Checked ? fn.Count : 1));
        List<Entry> pending = new List<Entry>();
        foreach (string d in dirs)
        {
            Entry e = new Entry { Name = d, Path = Scanner.Join(full, d), IsDir = true };
            DirInfo di;
            if (Scanner.TryGet(e.Path, out di)) e.Size = di.Total; else pending.Add(e);
            list.Add(e);
        }
        if (chkFiles.Checked)
        {
            for (int i = 0; i < fn.Count; i++)
                list.Add(new Entry { Name = fn[i], Path = Scanner.Join(full, fn[i]), Size = fs[i] });
        }
        else if (fn.Count > 0)
        {
            long own = 0;
            for (int i = 0; i < fs.Count; i++) own += fs[i];
            list.Add(new Entry { IsAgg = true, Count = fn.Count, Size = own, Path = full, Name = "" });
        }

        suppress = true;
        lv.SelectedIndices.Clear();
        entries = list;
        lv.VirtualListSize = entries.Count;
        suppress = false;

        doneSecs = -1;
        freeText = ComputeFree();
        RecalcTotal();
        Resort();
        SelectPending();
        UpdatePie();

        if (pending.Count == 0)
        {
            Scanner.Store(full, totalSize);
            sw.Reset();
            doneSecs = 0;
            pie.SetState(2);
            UpdateStatus();
            return;
        }

        scanning = true;
        pie.SetState(1);
        sw.Restart();
        timer.Start();
        UpdateStatus();
        CancellationToken token = cts.Token;
        int id = scanId;
        Task.Factory.StartNew(delegate
        {
            try
            {
                Parallel.ForEach(pending, Scanner.Options, delegate(Entry e)
                {
                    long s = Scanner.Scan(e.Path, 1, token);
                    if (!token.IsCancellationRequested) e.Size = s;
                });
            }
            catch { }
            if (token.IsCancellationRequested) return;
            try { BeginInvoke(new Action(delegate { OnScanDone(id); })); } catch { }
        }, TaskCreationOptions.LongRunning);
    }

    void CancelScan()
    {
        cts.Cancel();
        cts = new CancellationTokenSource();
        scanning = false;
        scanId++;
        timer.Stop();
    }

    void OnScanDone(int id)
    {
        if (id != scanId) return;
        scanning = false;
        timer.Stop();
        sw.Stop();
        doneSecs = sw.Elapsed.TotalSeconds;
        pie.SetState(2);
        RecalcTotal();
        if (sortCol != 0) Resort();
        lv.Invalidate();
        UpdatePie();
        Scanner.Store(curPath, totalSize);
        freeText = ComputeFree();
        UpdateStatus();
    }

    void OnTick(object s, EventArgs e)
    {
        if (!scanning) { timer.Stop(); return; }
        RecalcTotal();
        if (sortCol != 0) Resort();
        lv.Invalidate();
        UpdatePie();
        UpdateStatus();
    }

    void GoUp()
    {
        if (curPath == null) return;
        string t = curPath.TrimEnd('\\');
        string parent = null;
        try { parent = Path.GetDirectoryName(t); } catch { }
        if (string.IsNullOrEmpty(parent)) return;
        Navigate(parent, true, t);
    }

    void GoBack()
    {
        if (history.Count == 0) return;
        string from = curPath != null ? curPath.TrimEnd('\\') : null;
        Navigate(history.Pop(), false, from);
    }

    void DoRefresh()
    {
        Scanner.Cache.Clear();
        Entry sel = SelectedEntry();
        Navigate(curPath, false, sel != null ? sel.Path : null);
    }

    void SyncDrive(string full)
    {
        string root = Path.GetPathRoot(full);
        suppress = true;
        int idx = -1;
        for (int i = 0; i < cmbDrive.Items.Count; i++)
            if (string.Equals((string)cmbDrive.Items[i], root, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
        cmbDrive.SelectedIndex = idx;
        suppress = false;
    }

    void OpenSelected()
    {
        Entry en = SelectedEntry();
        if (en != null && en.IsDir) Navigate(en.Path, true, null);
    }

    void ShowSelectedInExplorer()
    {
        Entry en = SelectedEntry();
        if (en == null) return;
        try
        {
            if (en.IsAgg) Process.Start(curPath);
            else Process.Start("explorer.exe", "/select,\"" + en.Path + "\"");
        }
        catch { }
    }

    void DeleteSelected(bool permanent)
    {
        if (deleting) return;
        Entry en = SelectedEntry();
        if (en == null || en.IsAgg) return;
        string what = en.Name + "  (" + (en.Size >= 0 ? Fmt.Size(en.Size) : "…") + ")";
        DialogResult dr = MessageBox.Show(this, string.Format(L.Get(permanent ? "confirmperm" : "confirmbin"), what),
            L.Get(permanent ? "deleteperm" : "delete"), MessageBoxButtons.YesNo,
            permanent ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (dr != DialogResult.Yes) return;

        string path = en.Path, parent = curPath, name = en.Name;
        bool isDir = en.IsDir;
        CancelScan();
        pie.SetState(1);
        deleting = true;
        bar.Enabled = false; split.Enabled = false;
        lblStatus.Text = L.Get("deleting") + " " + name;
        RecycleOption ro = permanent ? RecycleOption.DeletePermanently : RecycleOption.SendToRecycleBin;

        Task.Factory.StartNew(delegate
        {
            string err = null;
            try
            {
                if (isDir) FileSystem.DeleteDirectory(path, UIOption.AllDialogs, ro);
                else FileSystem.DeleteFile(path, UIOption.AllDialogs, ro);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { err = ex.Message; }
            try { BeginInvoke(new Action(delegate { OnDeleted(path, parent, err); })); } catch { }
        }, TaskCreationOptions.LongRunning);
    }

    void OnDeleted(string path, string parent, string err)
    {
        deleting = false;
        bar.Enabled = true; split.Enabled = true;
        Scanner.Invalidate(path);
        Navigate(parent, false, null);
        lv.Focus();
        if (err != null) lblStatus.Text = string.Format(L.Get("deleteerror"), err);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { DoRefresh(); return true; }
        if (keyData == (Keys.Alt | Keys.Up)) { GoUp(); return true; }
        if (keyData == (Keys.Alt | Keys.Left)) { GoBack(); return true; }
        if (keyData == (Keys.Control | Keys.L) || keyData == Keys.F4) { txtPath.Focus(); txtPath.SelectAll(); return true; }
        if (keyData == Keys.Back && lv.Focused) { GoUp(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ------------------------------------------------------------ list
    void FitNameColumn()
    {
        if (lv.Columns.Count < 3) return;
        lv.Columns[0].Width = Math.Max(S(120), lv.ClientSize.Width - lv.Columns[1].Width - lv.Columns[2].Width);
    }

    void OnRetrieve(object s, RetrieveVirtualItemEventArgs e)
    {
        Entry en = entries[e.ItemIndex];
        string sz = en.Size < 0 ? L.Get("calculating") : Fmt.Size(en.Size);
        string pc = (en.Size < 0 || totalSize <= 0) ? "" : (100.0 * en.Size / totalSize).ToString("0.0") + " %";
        ListViewItem it = new ListViewItem(en.DisplayName());
        it.SubItems.Add(sz);
        it.SubItems.Add(pc);
        it.ImageIndex = en.IsAgg ? 2 : (en.IsDir ? 0 : 1);
        e.Item = it;
    }

    void OnColClick(object s, ColumnClickEventArgs e)
    {
        if (e.Column == sortCol) sortAsc = !sortAsc;
        else { sortCol = e.Column; sortAsc = (e.Column == 0); }
        UpdateHeaders();
        Resort();
    }

    int Cmp(Entry a, Entry b)
    {
        int r;
        if (sortCol == 0) r = Native.StrCmpLogicalW(a.DisplayName(), b.DisplayName());
        else
        {
            r = a.Size.CompareTo(b.Size);
            if (r == 0) r = Native.StrCmpLogicalW(a.DisplayName(), b.DisplayName());
        }
        return sortAsc ? r : -r;
    }

    Entry SelectedEntry()
    {
        if (lv.SelectedIndices.Count == 0) return null;
        int i = lv.SelectedIndices[0];
        return (i >= 0 && i < entries.Count) ? entries[i] : null;
    }

    void Resort()
    {
        Entry sel = SelectedEntry();
        entries.Sort(Cmp);
        if (sel != null)
        {
            int idx = entries.IndexOf(sel);
            if (idx != lv.SelectedIndices[0])
            {
                suppress = true;
                lv.SelectedIndices.Clear();
                if (idx >= 0) lv.SelectedIndices.Add(idx);
                suppress = false;
            }
        }
        lv.Invalidate();
    }

    void SelectEntry(Entry en)
    {
        int idx = entries.IndexOf(en);
        if (idx < 0) return;
        lv.SelectedIndices.Clear();
        lv.SelectedIndices.Add(idx);
        lv.EnsureVisible(idx);
        lv.Focus();
    }

    void SelectPending()
    {
        if (pendingSelect == null) return;
        foreach (Entry en in entries)
            if (string.Equals(en.Path.TrimEnd('\\'), pendingSelect, StringComparison.OrdinalIgnoreCase)) { SelectEntry(en); break; }
        pendingSelect = null;
    }

    void OnSelChanged(object s, EventArgs e)
    {
        if (suppress) return;
        pie.Selected = SelectedEntry();
        pie.Invalidate();
    }

    // ------------------------------------------------------------ totals / pie / status
    void RecalcTotal()
    {
        long t = 0;
        foreach (Entry e in entries) if (e.Size > 0) t += e.Size;
        totalSize = t;
    }

    void UpdatePie()
    {
        Entry[] top = new Entry[8];
        int n = 0;
        foreach (Entry e in entries)
        {
            if (e.Size <= 0) continue;
            int pos;
            if (n < 8) pos = n++;
            else if (e.Size > top[7].Size) pos = 7;
            else continue;
            top[pos] = e;
            while (pos > 0 && top[pos].Size > top[pos - 1].Size)
            {
                Entry tmp = top[pos]; top[pos] = top[pos - 1]; top[pos - 1] = tmp; pos--;
            }
        }
        List<Slice> slices = new List<Slice>();
        long used = 0;
        for (int i = 0; i < n; i++)
        {
            used += top[i].Size;
            slices.Add(new Slice { E = top[i], Name = top[i].DisplayName(), Size = top[i].Size, Pct = 100.0 * top[i].Size / totalSize, Col = Palette[i] });
        }
        if (totalSize - used > 0)
            slices.Add(new Slice { E = null, Name = L.Get("others"), Size = totalSize - used, Pct = 100.0 * (totalSize - used) / totalSize, Col = OthersColor });
        pie.Selected = SelectedEntry();
        pie.SetData(slices, L.Get("total") + ": " + Fmt.Size(totalSize));
    }

    string ComputeFree()
    {
        try
        {
            string root = Path.GetPathRoot(curPath);
            if (root == null || root.StartsWith(@"\\")) return "";
            DriveInfo di = new DriveInfo(root);
            return string.Format(L.Get("free"), Fmt.Size(di.AvailableFreeSpace), Fmt.Size(di.TotalSize));
        }
        catch { return ""; }
    }

    void UpdateStatus()
    {
        if (scanning) lblStatus.Text = L.Get("scanning") + " " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s";
        else lblStatus.Text = doneSecs >= 0 ? string.Format(L.Get("done"), doneSecs) : "";
        lblTotal.Text = L.Get("total") + ": " + Fmt.Size(totalSize) + "   |   " + string.Format(L.Get("items"), entries.Count);
        lblFree.Text = freeText;
    }

    void SaveIni()
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        d["lang"] = L.Codes[L.Cur];
        if (curPath != null) d["path"] = curPath;
        d["files"] = chkFiles.Checked ? "1" : "0";
        d["sortcol"] = sortCol.ToString();
        d["asc"] = sortAsc ? "1" : "0";
        bool max = WindowState == FormWindowState.Maximized;
        d["max"] = max ? "1" : "0";
        if (!max) { d["w"] = ClientSize.Width.ToString(); d["h"] = ClientSize.Height.ToString(); }
        else { if (ini.ContainsKey("w")) d["w"] = ini["w"]; if (ini.ContainsKey("h")) d["h"] = ini["h"]; }
        Ini.Save(d);
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ThreadPool.SetMinThreads(64, 64);
        try { Native.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(args.Length > 0 ? args[0].Trim('"') : null));
    }
}
