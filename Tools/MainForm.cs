using System.Collections.Concurrent;
using System.Drawing;
using System.Windows.Forms;
using PdfSharpCore.Pdf.IO;

public class MainForm : Form
{
    private readonly TreeView _treeView;
    private readonly RichTextBox _logBox;
    private readonly Button _convertBtn;
    private readonly Button _selectAllBtn;
    private readonly Button _deselectAllBtn;

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

        var rightButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            Padding = new Padding(0, 7, 8, 0)
        };
        rightButtons.Controls.Add(_convertBtn);

        var buttonBar = new Panel { Dock = DockStyle.Bottom, Height = 44 };
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

            foreach (var file in Directory.GetFiles(path, "*.md").OrderBy(f => f))
            {
                var node = new TreeNode(Path.GetFileNameWithoutExtension(file))
                {
                    Tag       = file,
                    ForeColor = Color.CornflowerBlue
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

    private void SetAllChecked(TreeNodeCollection nodes, bool value)
    {
        _suppressCheck = true;
        try
        {
            foreach (TreeNode node in nodes)
            {
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
                    .Count(f => !Path.GetFileNameWithoutExtension(f).StartsWith('-'));
                bool allSelected = g.Count() >= totalInFolder;
                return (folder: g.Key, files: g.ToList(), combine: allSelected);
            })
            .ToList();

        await Task.Run(() =>
        {
            foreach (var (folder, files, combine) in byFolder)
                ProcessFolder(folder, files, combine);
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

    private void ProcessFolder(string sourceFolder, List<string> mdFiles, bool combine)
    {
        string folderName = Path.GetFileName(sourceFolder)!;
        Log($"\n=== {folderName} ===", Color.Cyan);
        Log($"  Converting {mdFiles.Count} file(s){(combine ? " (will combine)" : "")}...", Color.White);

        bool isSystemRules = sourceFolder.Contains("System Rules");
        var failures  = new ConcurrentBag<string>();
        var generated = new ConcurrentBag<string>();

        Parallel.ForEach(mdFiles.OrderBy(f => f), mdFile =>
        {
            string fileName = Path.ChangeExtension(Path.GetFileName(mdFile), ".pdf");
            string outPath = combine
                ? Path.Combine(_outputFolder, fileName)
                : Path.Combine(_outputFolder, $"{folderName} - {fileName}");
            bool ok = MarkdownPdfConverter.Convert(mdFile, outPath, isSystemRules);

            if (ok)  { generated.Add(outPath); Log($"  OK   {Path.GetFileName(mdFile)}", Color.LightGray); }
            else     { failures.Add(Path.GetFileName(mdFile)); Log($"  FAIL {Path.GetFileName(mdFile)}", Color.Tomato); }
        });

        if (!failures.IsEmpty)
            Log($"  [WARNING] {failures.Count} failed: {string.Join(", ", failures)}", Color.Orange);

        if (combine)
            CombinePdfs(generated.OrderBy(f => f).ToList(), $"{folderName}.pdf");
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

        if (combined.PageCount > 0)
        {
            combined.Save(outPath);
            Log($"  -> {outputFileName}  ({files.Count} PDFs merged)", Color.LightGreen);
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
