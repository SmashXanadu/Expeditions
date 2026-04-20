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
                    if (n.Equals("Cover", StringComparison.OrdinalIgnoreCase)) return 0;
                    if (n.Equals("Back",  StringComparison.OrdinalIgnoreCase)) return 2;
                    return 1;
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

    private void SetAllChecked(TreeNodeCollection nodes, bool value)
    {
        _suppressCheck = true;
        try
        {
            foreach (TreeNode node in nodes)
            {
                if (value && IsHyphenFile(node)) continue;
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
                    .Count(f => !Path.GetFileNameWithoutExtension(f).StartsWith('-'));
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
        var markdownFiles = mdFiles.Where(f => f.EndsWith(".md",  StringComparison.OrdinalIgnoreCase)).ToList();

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
            bool stripLast = isSystemRules && !baseName.Equals("Cover", StringComparison.OrdinalIgnoreCase);
            bool ok = MarkdownPdfConverter.Convert(mdFile, outPath, stripLast);

            if (ok)  { generated.Add(outPath); Log($"  OK   {Path.GetFileName(mdFile)}", Color.LightGray); }
            else     { failures.Add(Path.GetFileName(mdFile)); Log($"  FAIL {Path.GetFileName(mdFile)}", Color.Tomato); }
        });

        if (!failures.IsEmpty)
            Log($"  [WARNING] {failures.Count} failed: {string.Join(", ", failures)}", Color.Orange);

        if (combine)
        {
            // Merge converted and pre-built PDFs, then pin Cover first and Back last
            var all   = generated.Concat(sourcePdfs).OrderBy(f => Path.GetFileNameWithoutExtension(f)).ToList();
            var cover = all.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("Cover", StringComparison.OrdinalIgnoreCase));
            var back  = all.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("Back",  StringComparison.OrdinalIgnoreCase));
            var middle = all.Where(f => f != cover && f != back).ToList();
            var orderedFiles = new List<string>();
            if (cover != null) orderedFiles.Add(cover);
            orderedFiles.AddRange(middle);
            if (back  != null) orderedFiles.Add(back);
            string digitalName = $"{folderName}_Digital.pdf";
            CombinePdfs(orderedFiles, digitalName);

            if (printVersion)
                CreateBookletPdf(Path.Combine(_outputFolder, digitalName), $"{folderName}_Print.pdf");

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

    private void CreateBookletPdf(string sourcePath, string outputFileName)
    {
        string outPath = Path.Combine(_outputFolder, outputFileName);
        try
        {
            int pageCount;
            using (var countDoc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
                pageCount = countDoc.PageCount;

            // Pad total to next multiple of 4 so the booklet folds evenly
            int N = ((pageCount + 3) / 4) * 4;

            using var src    = XPdfForm.FromFile(sourcePath);
            using var output = new PdfDocument();

            // Each pair p produces one landscape 8.5×11 sheet with two 5.5×8.5 pages.
            // Booklet order: even p → [N-1-p | p], odd p → [p | N-1-p]
            for (int p = 0; p < N / 2; p++)
            {
                int leftIdx  = (p % 2 == 0) ? N - 1 - p : p;
                int rightIdx = N - 1 - leftIdx;

                var sheet = output.AddPage();
                sheet.Width  = XUnit.FromPoint(11 * 72);
                sheet.Height = XUnit.FromPoint(8.5 * 72);

                using var gfx = XGraphics.FromPdfPage(sheet);

                if (leftIdx < pageCount)
                {
                    src.PageNumber = leftIdx + 1;
                    gfx.DrawImage(src, new XRect(0, 0, 5.5 * 72, 8.5 * 72));
                }
                if (rightIdx < pageCount)
                {
                    src.PageNumber = rightIdx + 1;
                    gfx.DrawImage(src, new XRect(5.5 * 72, 0, 5.5 * 72, 8.5 * 72));
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
