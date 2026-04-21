using System.Collections.Concurrent;
using System.Drawing;
using System.Windows.Forms;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

public class MainForm : Form
{
    private readonly TreeView _treeView;
    private readonly RichTextBox _logBox;
    private readonly Button _convertBtn;
    private readonly Button _selectAllBtn;
    private readonly Button _deselectAllBtn;
    private readonly CheckBox _includeIndividualChk;
    private readonly CheckBox _printVersionChk;

    private readonly string _solutionRoot;
    private readonly string _outputFolder;
    private readonly string _expeditionsRoot;

    private bool _suppressCheck;

    public MainForm()
    {
        _solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\"));
        _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Outputs");
        _expeditionsRoot = Path.Combine(_solutionRoot, "Expeditions");

        Text = "Expeditions PDF Converter";
        Size = new Size(900, 680);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;

        // --- Tree panel (left) ---
        var treeLabel = new Label
        {
            Text = "Select files or folders to convert:",
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(2, 4, 0, 0)
        };

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            CheckBoxes = true,
            ShowLines = true,
            ShowPlusMinus = true,
            Font = new Font("Segoe UI", 9f)
        };
        _treeView.AfterCheck += TreeView_AfterCheck;

        var leftPanel = new Panel { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(_treeView);
        leftPanel.Controls.Add(treeLabel);

        // --- Log panel (right) ---
        var logLabel = new Label
        {
            Text = "Output:",
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(2, 4, 0, 0)
        };

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.LightGray,
            Font = new Font("Consolas", 9f),
            BorderStyle = BorderStyle.None
        };

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(_logBox);
        rightPanel.Controls.Add(logLabel);

        // --- Split container ---
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 340,
            Orientation = Orientation.Vertical
        };
        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);

        // --- Button bar (bottom) ---
        _selectAllBtn   = new Button { Text = "Select All",       Width = 95,  Height = 30 };
        _deselectAllBtn = new Button { Text = "Deselect All",     Width = 95,  Height = 30 };
        _convertBtn     = new Button
        {
            Text      = "Convert",
            Width     = 140,
            Height    = 30,
            BackColor = Color.FromArgb(40, 160, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor    = AnchorStyles.Top | AnchorStyles.Right
        };
        _convertBtn.FlatAppearance.BorderSize = 0;

        _includeIndividualChk = new CheckBox
        {
            Text = "Include individual pages in output",
            Checked = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        _printVersionChk = new CheckBox
        {
            Text = "Generate print version (booklet/zine)",
            Checked = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        _selectAllBtn.Click   += (_, _) => SetAllChecked(_treeView.Nodes, true);
        _deselectAllBtn.Click += (_, _) => SetAllChecked(_treeView.Nodes, false);
        _convertBtn.Click     += ConvertBtn_Click;

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 210,
            Padding = new Padding(6, 7, 0, 0)
        };
        leftButtons.Controls.Add(_selectAllBtn);
        leftButtons.Controls.Add(_deselectAllBtn);

        var middleOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 11, 0, 0)
        };
        middleOptions.Controls.Add(_includeIndividualChk);
        middleOptions.Controls.Add(_printVersionChk);

        var rightButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            Padding = new Padding(0, 7, 8, 0)
        };
        rightButtons.Controls.Add(_convertBtn);

        var buttonBar = new Panel { Dock = DockStyle.Bottom, Height = 44 };
        buttonBar.Controls.Add(middleOptions);
        buttonBar.Controls.Add(leftButtons);
        buttonBar.Controls.Add(rightButtons);

        Controls.Add(split);
        Controls.Add(buttonBar);

        Directory.CreateDirectory(_outputFolder);
        foreach (var f in Directory.GetFiles(_outputFolder))
            File.Delete(f);

        PopulateTree();
    }

    // -------------------------------------------------------------------------
    // Tree population
    // -------------------------------------------------------------------------

    private void PopulateTree()
    {
        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();

        if (!Directory.Exists(_expeditionsRoot))
        {
            _treeView.Nodes.Add("Expeditions folder not found: " + _expeditionsRoot);
            _treeView.EndUpdate();
            return;
        }

        var root = new TreeNode("Expeditions") { Tag = _expeditionsRoot };
        AddDirectoryNodes(root, _expeditionsRoot);
        _treeView.Nodes.Add(root);
        root.Expand();

        _treeView.EndUpdate();
    }

    private static void AddDirectoryNodes(TreeNode parent, string path)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
            {
                var name = Path.GetFileName(dir)!;
                if (name.StartsWith('.')) continue;

                var node = new TreeNode(name) { Tag = dir };
                AddDirectoryNodes(node, dir);
                parent.Nodes.Add(node);
            }

            var allFiles = Directory.GetFiles(path, "*.md")
                .Concat(Directory.GetFiles(path, "*.pdf"))
                .OrderBy(f =>
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (n.StartsWith("Front Cover", StringComparison.OrdinalIgnoreCase)) return 0;
                    if (n.StartsWith("Back Cover",  StringComparison.OrdinalIgnoreCase)) return 2;
                    return 1;
                })
                .ThenBy(f =>
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (n.StartsWith("Front Cover", StringComparison.OrdinalIgnoreCase))
                        return n.EndsWith("Outside", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                    if (n.StartsWith("Back Cover", StringComparison.OrdinalIgnoreCase))
                        return n.EndsWith("Inside", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                    return 0;
                })
                .ThenBy(f => f);

            foreach (var file in allFiles)
            {
                bool isPdf = file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                var node = new TreeNode(Path.GetFileNameWithoutExtension(file))
                {
                    Tag       = file,
                    ForeColor = isPdf ? Color.Goldenrod : Color.CornflowerBlue
                };
                parent.Nodes.Add(node);
            }
        }
        catch { /* skip inaccessible dirs */ }
    }

    // -------------------------------------------------------------------------
    // Checkbox logic
    // -------------------------------------------------------------------------

    private void TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_suppressCheck || e.Node is null) return;
        _suppressCheck = true;
        try { SetAllChecked(e.Node.Nodes, e.Node.Checked); }
        finally { _suppressCheck = false; }
    }

    private static bool IsHyphenFile(TreeNode node) =>
        node.Tag is string path && File.Exists(path) &&
        Path.GetFileName(path).StartsWith('-');

    private static bool HasPdfCounterpart(TreeNode node) =>
        node.Tag is string path &&
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
        File.Exists(Path.ChangeExtension(path, ".pdf"));

    private void SetAllChecked(TreeNodeCollection nodes, bool value)
    {
        _suppressCheck = true;
        try
        {
            foreach (TreeNode node in nodes)
            {
                if (value && IsHyphenFile(node)) continue;
                if (value && HasPdfCounterpart(node)) continue;
                node.Checked = value;
                SetAllChecked(node.Nodes, value);
            }
        }
        finally { _suppressCheck = false; }
    }

    private List<string> GetCheckedFiles()
    {
        var result = new List<string>();
        Collect(_treeView.Nodes, result);
        return result;

        static void Collect(TreeNodeCollection nodes, List<string> list)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is string path && File.Exists(path))
                    list.Add(path);
                Collect(node.Nodes, list);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Conversion
    // -------------------------------------------------------------------------

    private async void ConvertBtn_Click(object? sender, EventArgs e)
    {
        var allFiles = GetCheckedFiles()
            .Where(f => !Path.GetFileNameWithoutExtension(f).StartsWith('-'))
            .ToList();

        if (allFiles.Count == 0)
        {
            MessageBox.Show("No Markdown files are selected.", "Nothing to Convert",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetUiEnabled(false);
        _logBox.Clear();
        Directory.CreateDirectory(_outputFolder);
        Log($"Selected {allFiles.Count} file(s):", Color.White);
        foreach (var f in allFiles)
            Log($"  {f}", Color.DarkGray);

        var byFolder = allFiles
            .GroupBy(f => Path.GetDirectoryName(f)!)
            .Select(g =>
            {
                int totalInFolder = Directory.GetFiles(g.Key, "*.md", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(g.Key, "*.pdf", SearchOption.TopDirectoryOnly))
                    .Where(f => !Path.GetFileNameWithoutExtension(f).StartsWith('-'))
                    .Where(f => !(f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                                  File.Exists(Path.ChangeExtension(f, ".pdf"))))
                    .Count();
                bool allSelected = g.Count() >= totalInFolder;
                return (folder: g.Key, files: g.ToList(), combine: allSelected);
            })
            .ToList();

        bool keepIndividual = _includeIndividualChk.Checked;
        bool printVersion   = _printVersionChk.Checked;
        await Task.Run(() =>
        {
            foreach (var (folder, files, combine) in byFolder)
                ProcessFolder(folder, files, combine, keepIndividual, printVersion);
        });

        Log("\n=== All done! ===", Color.LightGreen);
        SetUiEnabled(true);
    }

    private void SetUiEnabled(bool enabled)
    {
        _convertBtn.Enabled     = enabled;
        _selectAllBtn.Enabled   = enabled;
        _deselectAllBtn.Enabled = enabled;
        _treeView.Enabled       = enabled;
    }

    private void ProcessFolder(string sourceFolder, List<string> mdFiles, bool combine, bool keepIndividual, bool printVersion)
    {
        string folderName = Path.GetFileName(sourceFolder)!;
        Log($"\n=== {folderName} ===", Color.Cyan);
        Log($"  Converting {mdFiles.Count} file(s){(combine ? " (will combine)" : "")}...", Color.White);

        bool isSystemRules = sourceFolder.Contains("System Rules");

        // Split checked files: pre-built PDFs are used as-is; markdown files are converted.
        // Source PDFs are never added to `generated` so they are never deleted by cleanup.
        var sourcePdfs    = mdFiles.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        var sourcePdfNames = sourcePdfs.Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var markdownFiles = mdFiles.Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                                            && !sourcePdfNames.Contains(Path.GetFileNameWithoutExtension(f))).ToList();

        foreach (var pdf in sourcePdfs)
            Log($"  Using pre-built {Path.GetFileName(pdf)}", Color.LightGray);

        var failures  = new ConcurrentBag<string>();
        var generated = new ConcurrentBag<string>(); // converted files only — lives in _outputFolder

        Parallel.ForEach(markdownFiles.OrderBy(f => f), mdFile =>
        {
            string fileName = Path.ChangeExtension(Path.GetFileName(mdFile), ".pdf");
            string outPath = combine
                ? Path.Combine(_outputFolder, fileName)
                : Path.Combine(_outputFolder, $"{folderName} - {fileName}");
            string baseName = Path.GetFileNameWithoutExtension(mdFile);
            bool stripLast = isSystemRules && !baseName.StartsWith("Front Cover", StringComparison.OrdinalIgnoreCase);
            bool compact   = baseName.StartsWith("Front Cover Inside", StringComparison.OrdinalIgnoreCase);
            bool ok = MarkdownPdfConverter.Convert(mdFile, outPath, stripLast, compact);

            if (ok)  { generated.Add(outPath); Log($"  OK   {Path.GetFileName(mdFile)}", Color.LightGray); }
            else     { failures.Add(Path.GetFileName(mdFile)); Log($"  FAIL {Path.GetFileName(mdFile)}", Color.Tomato); }
        });

        if (!failures.IsEmpty)
            Log($"  [WARNING] {failures.Count} failed: {string.Join(", ", failures)}", Color.Orange);

        if (combine)
        {
            // Merge converted and pre-built PDFs, pinning front/back covers in order
            var all = generated.Concat(sourcePdfs).OrderBy(f => Path.GetFileNameWithoutExtension(f)).ToList();

            var frontCovers = all
                .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith("Front Cover", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileNameWithoutExtension(f).EndsWith("Outside", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            var backCovers = all
                .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith("Back Cover", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileNameWithoutExtension(f).EndsWith("Inside", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            var middle = all.Where(f => !frontCovers.Contains(f) && !backCovers.Contains(f)).ToList();

            // Build TOC from content pages and generate its PDF
            string? tocPath = null;
            var tocEntries = BuildTocEntries(middle, markdownFiles);
            if (tocEntries.Count > 0)
            {
                tocPath = Path.Combine(_outputFolder, "_TOC.pdf");
                if (MarkdownPdfConverter.GenerateToc(tocEntries, tocPath))
                    Log("  Generated table of contents", Color.LightGray);
                else
                    tocPath = null;
            }

            var orderedFiles = new List<string>();
            orderedFiles.AddRange(frontCovers);
            if (tocPath != null) orderedFiles.Add(tocPath);
            orderedFiles.AddRange(middle);
            orderedFiles.AddRange(backCovers);

            int frontSkip = frontCovers.Count + (tocPath != null ? 1 : 0);
            int backSkip  = backCovers.Count;

            string digitalName = $"{folderName}_Digital.pdf";
            string digitalPath = Path.Combine(_outputFolder, digitalName);
            CombinePdfs(orderedFiles, digitalName); // merge only — no page numbers yet

            if (printVersion)
                CreateBookletPdf(digitalPath, $"{folderName}_Print.pdf", frontSkip, backSkip);

            // Stamp digital page numbers after print imposition so the source is always clean
            StampPageNumbers(digitalPath, frontSkip, backSkip, printStyle: false);

            if (tocPath != null) try { File.Delete(tocPath); } catch { }

            if (!keepIndividual)
            {
                // Only delete files we generated — never touch source PDFs
                foreach (var file in generated)
                {
                    try { File.Delete(file); }
                    catch { /* best effort */ }
                }
            }
        }
    }

    private void CombinePdfs(List<string> files, string outputFileName)
    {
        if (files.Count == 0) return;

        string outPath = Path.Combine(_outputFolder, outputFileName);
        using var combined = new PdfSharpCore.Pdf.PdfDocument();

        foreach (var file in files)
        {
            try
            {
                using var doc = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                for (int i = 0; i < doc.PageCount; i++)
                    combined.AddPage(doc.Pages[i]);
            }
            catch (Exception ex)
            {
                Log($"  Merge error ({Path.GetFileName(file)}): {ex.Message}", Color.Tomato);
            }
        }

        // Strip trailing blank pages (QuestPDF sometimes emits an empty trailing page)
        while (combined.PageCount > 0 && IsPageBlank(combined.Pages[combined.PageCount - 1]))
            combined.Pages.RemoveAt(combined.PageCount - 1);

        if (combined.PageCount > 0)
        {
            combined.Save(outPath);
            Log($"  -> {outputFileName}  ({files.Count} PDFs merged)", Color.LightGreen);
        }
    }

    private static void StampPageNumbers(string path, int frontSkip, int backSkip, bool printStyle)
    {
        using var doc  = PdfReader.Open(path, PdfDocumentOpenMode.Modify);
        var font       = new XFont("Segoe UI", 7, XFontStyle.Regular);
        double margin  = 0.375 * 72;
        int    total   = doc.PageCount;

        for (int i = 0; i < total; i++)
        {
            if (i < frontSkip || i >= total - backSkip) continue;

            int    num        = i - frontSkip + 1; // content pages start at 1
            var    page       = doc.Pages[i];
            double w          = page.Width.Point;
            double h          = page.Height.Point;
            double y          = h - margin * 0.5;
            bool   rightAlign = !printStyle || num % 2 == 0;
            double x          = rightAlign ? w - margin : margin;
            var    format     = rightAlign ? XStringFormats.BaseLineRight : XStringFormats.BaseLineLeft;

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            gfx.DrawString(num.ToString(), font, XBrushes.Black, x, y, format);
        }

        doc.Save(path);
    }

    private static bool IsPageBlank(PdfSharpCore.Pdf.PdfPage page)
    {
        var content = page.Elements["/Contents"];
        if (content == null) return true;

        // Resolve through indirect reference if needed
        var obj = content is PdfSharpCore.Pdf.Advanced.PdfReference r ? r.Value : content;

        if (obj is PdfSharpCore.Pdf.PdfDictionary dict && dict.Stream != null)
            return IsStreamBlank(dict.Stream.Value);

        if (obj is PdfSharpCore.Pdf.PdfArray arr)
        {
            foreach (var item in arr.Elements)
            {
                var resolved = item is PdfSharpCore.Pdf.Advanced.PdfReference pr ? pr.Value : item;
                if (resolved is PdfSharpCore.Pdf.PdfDictionary sd && sd.Stream != null && !IsStreamBlank(sd.Stream.Value))
                    return false;
            }
            return true;
        }

        return false; // unknown structure — assume not blank
    }

    private static bool IsStreamBlank(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return true;
        string s = System.Text.Encoding.Latin1.GetString(bytes).Trim();
        return s.Length == 0 || s == "q Q" || s == "Q";
    }

    private void CreateBookletPdf(string sourcePath, string outputFileName, int frontSkip, int backSkip)
    {
        string outPath = Path.Combine(_outputFolder, outputFileName);
        try
        {
            int pageCount;
            using (var countDoc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
                pageCount = countDoc.PageCount;

            int N    = ((pageCount + 3) / 4) * 4;
            var font = new XFont("Segoe UI", 7, XFontStyle.Regular);

            using var src    = XPdfForm.FromFile(sourcePath);
            using var output = new PdfDocument();

            // Virtual index 0 = Cover, virtual N-1 = Back, interior content fills 1..N-2.
            // Blank padding sits between the last content page and Back.
            for (int p = 0; p < N / 2; p++)
            {
                int leftVirtual  = (p % 2 == 0) ? N - 1 - p : p;
                int rightVirtual = N - 1 - leftVirtual;
                int leftActual   = VirtualToActual(leftVirtual,  pageCount, frontSkip, backSkip, N);
                int rightActual  = VirtualToActual(rightVirtual, pageCount, frontSkip, backSkip, N);

                var sheet = output.AddPage();
                sheet.Width  = XUnit.FromPoint(11 * 72);
                sheet.Height = XUnit.FromPoint(8.5 * 72);

                using var gfx = XGraphics.FromPdfPage(sheet);

                if (leftActual >= 0)
                {
                    src.PageNumber = leftActual + 1;
                    gfx.DrawImage(src, new XRect(0, 0, 5.5 * 72, 8.5 * 72));
                    DrawPrintPageNumber(gfx, font, leftVirtual, new XRect(0, 0, 5.5 * 72, 8.5 * 72), frontSkip, backSkip, N, isLeftPage: true);
                }
                if (rightActual >= 0)
                {
                    src.PageNumber = rightActual + 1;
                    gfx.DrawImage(src, new XRect(5.5 * 72, 0, 5.5 * 72, 8.5 * 72));
                    DrawPrintPageNumber(gfx, font, rightVirtual, new XRect(5.5 * 72, 0, 5.5 * 72, 8.5 * 72), frontSkip, backSkip, N, isLeftPage: false);
                }
            }

            output.Save(outPath);
            Log($"  -> {outputFileName}  (booklet, {N / 2} imposed sheets)", Color.LightGreen);
        }
        catch (Exception ex)
        {
            Log($"  Booklet error: {ex.Message}", Color.Tomato);
        }
    }

    // Maps a virtual booklet slot (0 = Cover, N-1 = Back, interior = content then blanks)
    // to the actual 0-based page index in the source PDF, or -1 for a blank slot.
    // frontSkip = number of non-numbered pages at the front (Cover, TOC, etc.)
    // backSkip  = number of non-numbered pages at the back (Back cover, etc.)
    private static int VirtualToActual(int virtualIdx, int pageCount, int frontSkip, int backSkip, int N)
    {
        // Front special pages map 1:1 to source
        if (virtualIdx < frontSkip)
            return virtualIdx < pageCount ? virtualIdx : -1;

        // Back special pages map 1:1 to the last source pages
        if (virtualIdx >= N - backSkip)
        {
            int src = pageCount - backSkip + (virtualIdx - (N - backSkip));
            return src < pageCount ? src : -1;
        }

        // Interior: content pages fill first, blanks fill the rest
        int offset       = virtualIdx - frontSkip;
        int contentCount = pageCount - frontSkip - backSkip;
        return offset < contentCount ? frontSkip + offset : -1;
    }

    private static void DrawPrintPageNumber(XGraphics gfx, XFont font, int virtualIdx, XRect area,
                                            int frontSkip, int backSkip, int N, bool isLeftPage)
    {
        if (virtualIdx < frontSkip || virtualIdx >= N - backSkip) return;

        int    num    = virtualIdx - frontSkip + 1;
        double margin = 0.375 * 72;
        double x      = isLeftPage ? area.Left + margin : area.Right - margin;
        double y      = area.Bottom - margin * 0.5;
        var    format = isLeftPage ? XStringFormats.BaseLineLeft : XStringFormats.BaseLineRight;

        gfx.DrawString(num.ToString(), font, XBrushes.Black, x, y, format);
    }

    private static List<(string Heading, int Page)> BuildTocEntries(List<string> contentPdfs, List<string> markdownFiles)
    {
        var entries = new List<(string, int)>();
        int page = 1;

        foreach (var pdfPath in contentPdfs)
        {
            string baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var mdFile = markdownFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(baseName, StringComparison.OrdinalIgnoreCase));

            string heading = string.Empty;
            if (mdFile != null)
                heading = MarkdownPdfConverter.ExtractH1(File.ReadAllText(mdFile));
            if (string.IsNullOrEmpty(heading))
            {
                // Strip leading "NN - " numeric prefix from the file name
                heading = System.Text.RegularExpressions.Regex.Replace(baseName, @"^\d+\s*-\s*", "");
            }

            entries.Add((heading, page));

            try
            {
                using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                page += doc.PageCount;
            }
            catch { page++; }
        }

        return entries;
    }

    // -------------------------------------------------------------------------
    // Logging (thread-safe)
    // -------------------------------------------------------------------------

    private void Log(string message, Color color)
    {
        if (InvokeRequired) { Invoke(() => Log(message, color)); return; }

        _logBox.SelectionStart  = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor  = color;
        _logBox.AppendText(message + "\n");
        _logBox.ScrollToCaret();
    }
}
