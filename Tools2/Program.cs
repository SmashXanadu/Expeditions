using System.Collections.Concurrent;
using PdfSharpCore.Pdf.IO;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\"));
string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Outputs");

var sourceFolders = new Dictionary<string, string>
{
    { "Abilities",          Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Abilities") },
    { "General Skills",     Path.Combine(solutionRoot, "Expeditions", "Player Resources", "General Skills") },
    { "Trade Skills",       Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Trade Skills") },
    { "Rules",              Path.Combine(solutionRoot, "Expeditions", "System Rules", "Rules") },
    { "Premade Adventures", Path.Combine(solutionRoot, "Expeditions", "Guide Resources", "Premade Adventures") },
    { "Adversaries",        Path.Combine(solutionRoot, "Expeditions", "Guide Resources", "Adversaries") },
    { "Adversaries - Cards",Path.Combine(solutionRoot, "Expeditions", "Guide Resources", "Adversaries", "Adversaries") },
    { "Equipment Sets",     Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Equipment", "Sets") },
    { "Equipment Weapons",  Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Equipment", "Weapons") },
    { "Equipment Armor",    Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Equipment", "Armor") },
    { "Premade Characters", Path.Combine(solutionRoot, "Expeditions", "Playtest", "Premade Characters") },
    { "Pocket Edition",     Path.Combine(solutionRoot, "Expeditions", "Pocket Edition") },
};

var selected = PromptFolderSelection(sourceFolders);
if (selected.Count == 0)
{
    Console.WriteLine("No folders selected. Exiting.");
    return;
}

Directory.CreateDirectory(outputFolder);

foreach (var (name, path) in selected)
    ProcessFolder(name, path, outputFolder);

static List<(string name, string path)> PromptFolderSelection(Dictionary<string, string> sourceFolders)
{
    var keys = sourceFolders.Keys.ToList();
    Console.WriteLine("\nAvailable asset folders:");
    for (int i = 0; i < keys.Count; i++)
        Console.WriteLine($"  {i + 1,2}. {keys[i]}");
    Console.WriteLine($"   A. All");
    Console.Write("\nEnter selection(s) (e.g. 1,3,5 or A): ");
    string input = Console.ReadLine()?.Trim() ?? "";

    if (input.Equals("A", StringComparison.OrdinalIgnoreCase))
        return sourceFolders.Select(kvp => (kvp.Key, kvp.Value)).ToList();

    var selected = new List<(string, string)>();
    foreach (var part in input.Split(','))
    {
        if (int.TryParse(part.Trim(), out int index) && index >= 1 && index <= keys.Count)
        {
            string key = keys[index - 1];
            selected.Add((key, sourceFolders[key]));
        }
        else
        {
            Console.WriteLine($"  Skipping invalid selection: '{part.Trim()}'");
        }
    }
    return selected;
}

static void ProcessFolder(string folderName, string sourceFolder, string outputFolder)
{
    Console.WriteLine($"\n=== Processing: {folderName} ===");

    if (!Directory.Exists(sourceFolder))
    {
        Console.WriteLine($"  Error: Source directory not found. Skipping.");
        return;
    }

    string[] markdownFiles = Directory.GetFiles(sourceFolder, "*.md", SearchOption.TopDirectoryOnly);

    if (markdownFiles.Length == 0)
    {
        Console.WriteLine("  No Markdown files found. Skipping.");
        return;
    }

    Console.WriteLine($"  Converting {markdownFiles.Length} files...\n");

    bool isSystemRules = sourceFolder.Contains("System Rules");
    var failures = new ConcurrentBag<string>();
    var generated = new ConcurrentBag<string>();

    Parallel.ForEach(markdownFiles.OrderBy(f => f), markdownFile =>
    {
        if (Path.GetFileNameWithoutExtension(markdownFile).StartsWith("-")) return;

        string outputPdfPath = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(markdownFile), ".pdf"));
        bool success = MarkdownPdfConverter.Convert(markdownFile, outputPdfPath, isSystemRules);
        if (success) generated.Add(outputPdfPath);
        else failures.Add(Path.GetFileName(markdownFile));
    });

    if (!failures.IsEmpty)
        Console.WriteLine($"\n  [WARNING] {failures.Count} file(s) failed: {string.Join(", ", failures)}");

    Console.WriteLine();
    CombinePDFs(generated.OrderBy(f => f).ToList(), outputFolder, $"{folderName}.pdf");
}

static void CombinePDFs(List<string> files, string folderPath, string fileName)
{
    string fullOutputPath = Path.Combine(folderPath, fileName);

    using var outputDocument = new PdfSharpCore.Pdf.PdfDocument();

    foreach (var file in files)
    {
        try
        {
            using var inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import);
            for (int i = 0; i < inputDocument.PageCount; i++)
                outputDocument.AddPage(inputDocument.Pages[i]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error merging {Path.GetFileName(file)}: {ex.Message}");
        }
    }

    if (outputDocument.PageCount > 0)
    {
        outputDocument.Save(fullOutputPath);
        Console.WriteLine($"  Combined {files.Count} PDFs -> {Path.GetFileName(fullOutputPath)}");
    }
}
