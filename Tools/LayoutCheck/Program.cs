using System.Drawing;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using QuestPDF.Infrastructure;

// Layout checks for the zine PDFs, without opening the converter app.
//
//   render <vaultRoot> <outDir> <file.md...>
//       Builds each file's PDF plus one PNG per page into outDir and prints its
//       page count. Use it to confirm a spread still fits (e.g. every Setting
//       Guide location/faction chapter must be exactly 2 pages).
//
//   width <limitPt> <file.md...>
//       Measures every table cell that comes after a "\smalltables" directive in
//       Atkinson Hyperlegible at the small-table font size, and lists any cell
//       wider than limitPt (those would wrap). 312 is the safe limit for a
//       two-column "| # | Text |" table; the text cell holds about 321pt.

QuestPDF.Settings.License = LicenseType.Community;

if (args.Length < 3)
{
    Console.WriteLine("usage: render <vaultRoot> <outDir> <file.md...>");
    Console.WriteLine("       width <limitPt> <file.md...>");
    return 1;
}

return args[0] switch
{
    "render" => Render(args[1], args[2], args.Skip(3)),
    "width" => Width(float.Parse(args[1]), args.Skip(2)),
    _ => Fail($"unknown mode '{args[0]}'"),
};

static int Render(string vaultRoot, string outDir, IEnumerable<string> files)
{
    Directory.CreateDirectory(outDir);
    int failures = 0;
    foreach (var md in files)
    {
        string pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(md) + ".pdf");
        if (!MarkdownPdfConverter.Convert(md, pdf, vaultRoot, previewImageDir: outDir)) { failures++; continue; }
        int pages = Regex.Matches(File.ReadAllText(pdf, System.Text.Encoding.Latin1), @"/Type\s*/Page[^s]").Count;
        Console.WriteLine($"PAGES {pages}  {Path.GetFileName(md)}");
    }
    Console.WriteLine($"Output: {outDir}");
    return failures == 0 ? 0 : 1;
}

static int Width(float limit, IEnumerable<string> files)
{
    // The font is installed per-user, not in C:\Windows\Fonts.
    string fontFile = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts\Atkinson-Hyperlegible-Regular-102.otf"),
        @"C:\Windows\Fonts\Atkinson-Hyperlegible-Regular-102.otf",
    }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Atkinson Hyperlegible Regular is not installed");

    using var fonts = new PrivateFontCollection();
    fonts.AddFontFile(fontFile);
    using var font = new Font(fonts.Families[0], MarkdownPdfConverter.SmallTableFontSize, GraphicsUnit.Point);
    using var bmp = new Bitmap(1, 1);
    using var g = Graphics.FromImage(bmp);
    g.PageUnit = GraphicsUnit.Point;
    g.TextRenderingHint = TextRenderingHint.AntiAlias;

    int over = 0, cells = 0;
    float widest = 0;
    foreach (var md in files)
    {
        bool small = false;
        foreach (var line in File.ReadAllLines(md))
        {
            if (line.Trim() == @"\smalltables") { small = true; continue; }
            if (!small || !line.StartsWith('|') || Regex.IsMatch(line, @"^\|[\s:|-]+$")) continue;

            foreach (var raw in line.Split('|'))
            {
                // Strip markdown emphasis so "**Bold**" measures as rendered text.
                string text = raw.Trim().Replace("**", "").Replace("*", "");
                if (text.Length == 0 || text == "#" || text.All(char.IsDigit)) continue;
                float w = g.MeasureString(text, font, 10000, StringFormat.GenericTypographic).Width;
                cells++;
                widest = Math.Max(widest, w);
                if (w > limit) { over++; Console.WriteLine($"{w,6:F1}pt  {Path.GetFileName(md)}: {text}"); }
            }
        }
    }
    Console.WriteLine($"{cells} cells checked, widest {widest:F1}pt, {over} over {limit}pt");
    return over == 0 ? 0 : 1;
}

static int Fail(string message)
{
    Console.WriteLine(message);
    return 1;
}
