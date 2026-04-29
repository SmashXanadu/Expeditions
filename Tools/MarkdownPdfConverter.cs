using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public static class MarkdownPdfConverter
{
    private const string FontFamily = "Atkinson Hyperlegible";
    private const float BaseFontSize = 8f;
    private const float H1FontSize = 12f;
    private const float H2FontSize = 10f;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseYamlFrontMatter()
        .Build();

    public static string ExtractH1(string markdownContent)
    {
        var doc = Markdown.Parse(markdownContent, Pipeline);
        var h1  = doc.OfType<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        return h1?.Inline != null ? ExtractPlainText(h1.Inline) : string.Empty;
    }

    public static bool GenerateToc(List<(string Heading, int Page)> entries, string outputPath,
                                   string? templatePath = null, string vaultRoot = "")
    {
        try
        {
            // Parse template and split at \toc marker if provided
            List<Block>? beforeToc = null;
            List<Block>? afterToc  = null;
            if (templatePath != null && File.Exists(templatePath))
            {
                string raw = StripSiteBaseUrlLine(File.ReadAllText(templatePath));
                var allBlocks = Markdown.Parse(raw, Pipeline).ToList();
                int tocIdx = allBlocks.FindIndex(b =>
                    b is ParagraphBlock p &&
                    p.Inline?.FirstOrDefault() is LiteralInline lit &&
                    lit.Content.ToString().Trim() == @"\toc");
                if (tocIdx >= 0)
                {
                    beforeToc = allBlocks.Take(tocIdx).ToList();
                    afterToc  = allBlocks.Skip(tocIdx + 1).ToList();
                }
                else
                {
                    beforeToc = allBlocks;
                    afterToc  = new List<Block>();
                }
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(5.5f * 72, 8.5f * 72);
                    page.Margin(0.375f * 72);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(BaseFontSize));

                    page.Content().Column(col =>
                    {
                        col.Spacing(4);

                        if (beforeToc != null)
                            foreach (var b in beforeToc)
                                RenderBlock(col, b, vaultRoot);
                        else
                            col.Item().Text("Contents").Bold().FontSize(H1FontSize);

                        foreach (var (heading, pageNum) in entries)
                        {
                            col.Item().Row(row =>
                            {
                                row.AutoItem().PaddingRight(4).Text(heading);
                                row.RelativeItem().PaddingBottom(2).AlignBottom().BorderBottom(0.5f).BorderColor(Colors.Black);
                                row.AutoItem().PaddingLeft(4).Text(pageNum.ToString());
                            });
                        }

                        if (afterToc != null)
                            foreach (var b in afterToc)
                                RenderBlock(col, b, vaultRoot);
                    });
                });
            }).GeneratePdf(outputPath);
            return true;
        }
        catch { return false; }
    }

    private static string ExtractPlainText(ContainerInline inlines)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    sb.Append(lit.Content.ToString());
                    break;
                case ContainerInline container:
                    sb.Append(ExtractPlainText(container));
                    break;
            }
        }
        return sb.ToString().Trim();
    }

    public static bool Convert(string inputPath, string outputPath, string vaultRoot, bool compact = false)
    {
        string label = Path.GetFileName(outputPath);
        try
        {
            string content = File.ReadAllText(inputPath);
            content = StripSiteBaseUrlLine(content);

            var document = Markdown.Parse(content, Pipeline);
            float fontSize = compact ? 5.5f : BaseFontSize;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(5.5f * 72, 8.5f * 72);
                    page.Margin(0.375f * 72);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(fontSize));

                    page.Content().Column(col =>
                    {
                        col.Spacing(compact ? 1 : 4);
                        foreach (var block in document)
                        {
                            if (block is ParagraphBlock ep &&
                                ep.Inline?.FirstOrDefault() is LiteralInline el &&
                                el.Content.ToString().Trim() == @"\end")
                                break;
                            RenderBlock(col, block, vaultRoot, compact);
                        }
                    });
                });
            }).GeneratePdf(outputPath);

            Console.WriteLine($"  {label} ... [SUCCESS]");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {label} ... [FAILED] {ex.Message}");
            return false;
        }
    }

    private static string StripSiteBaseUrlLine(string content)
    {
        var lines = content.Split('\n').ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                if (lines[i].Contains("site.baseurl", StringComparison.OrdinalIgnoreCase))
                    lines.RemoveAt(i);
                break;
            }
        }
        return string.Join('\n', lines);
    }

    private static void RenderBlock(ColumnDescriptor col, Block block, string vaultRoot, bool compact = false)
    {
        switch (block)
        {
            case YamlFrontMatterBlock:
                break;

            case HeadingBlock heading:
                float fontSize = heading.Level == 1 ? H1FontSize : H2FontSize;
                col.Item().Text(t =>
                {
                    t.DefaultTextStyle(x => x.Bold().FontSize(fontSize));
                    RenderInlines(t, heading.Inline);
                });
                break;

            case ThematicBreakBlock:
                col.Item().PaddingVertical(2).LineHorizontal(0.5f);
                break;

            case ParagraphBlock para:
                if (para.Inline?.FirstOrDefault() is LiteralInline { } lit &&
                    lit.Content.ToString().Trim() == @"\newpage")
                {
                    col.Item().PageBreak();
                    break;
                }
                // Standalone image: first inline is an image, rest are only whitespace/soft-breaks
                var inlineList = para.Inline?.ToList();
                if (inlineList?.Count > 0 && inlineList[0] is LinkInline { IsImage: true } imgLink &&
                    inlineList.Skip(1).All(i => i is LineBreakInline ||
                        (i is LiteralInline wl && wl.Content.ToString().Trim() == "")))
                {
                    RenderImage(col, imgLink, vaultRoot);
                    break;
                }
                col.Item().Text(t => RenderInlines(t, para.Inline));
                break;

            case HtmlBlock html:
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    html.Lines.ToString(), @"<!--\s*vspace:(\d+)\s*-->");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int pts))
                    col.Item().Height(pts);
                break;
            }

            case Table table:
                RenderTable(col, table, compact);
                break;

            case QuoteBlock quote:
                col.Item().Row(row =>
                {
                    row.ConstantItem(3).Background("#888888");
                    row.ConstantItem(6);
                    row.RelativeItem().Column(quoteCol =>
                    {
                        quoteCol.Spacing(2);
                        foreach (var b in quote)
                            RenderBlock(quoteCol, b, vaultRoot, compact);
                    });
                });
                break;

            case ListBlock list:
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(10).Text("•");
                        row.RelativeItem().Column(itemCol =>
                        {
                            itemCol.Spacing(2);
                            foreach (var b in item)
                                RenderBlock(itemCol, b, vaultRoot, compact);
                        });
                    });
                }
                break;
        }
    }

    private static void RenderImage(ColumnDescriptor col, LinkInline img, string vaultRoot)
    {
        string url = img.Url ?? "";
        // Strip any {{site.baseurl}} or similar Liquid tag prefix
        url = System.Text.RegularExpressions.Regex.Replace(url, @"\{\{[^}]+\}\}", "");
        url = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(vaultRoot, url);
        if (!File.Exists(fullPath)) return;
        col.Item().Image(fullPath).FitWidth();
    }

    private static void RenderTable(ColumnDescriptor col, Table table, bool compact = false)
    {
        var rows = table.OfType<TableRow>().ToList();
        int maxCellsPerRow = rows.Any() ? rows.Max(r => r.Count) : 0;

        // Single-column tables: named boxes (e.g. "| Gold |") or plain card text
        if (maxCellsPerRow <= 1)
        {
            var singleHeader = rows.FirstOrDefault(r => r.IsHeader);
            var headerLabel = new System.Text.StringBuilder();
            if (singleHeader != null)
                foreach (TableCell cell in singleHeader)
                    foreach (var para in cell.OfType<ParagraphBlock>())
                        if (para.Inline != null)
                            headerLabel.Append(ExtractPlainText(para.Inline));
            string label = headerLabel.ToString().Trim();

            if (!string.IsNullOrEmpty(label))
            {
                // Render as a named bordered box (header label + empty writable cell)
                col.Item().Table(tbl =>
                {
                    tbl.ColumnsDefinition(cols => cols.RelativeColumn());
                    tbl.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(compact ? 1 : 3)
                        .Text(t => t.Span(label).Bold());
                    foreach (var _ in rows.Where(r => !r.IsHeader))
                        tbl.Cell().Border(0.5f).Padding(compact ? 1 : 3).MinHeight(12).Text("");
                });
                return;
            }

            // No header — render as plain stacked text (ability/skill cards)
            foreach (var row in rows)
            {
                if (row.IsHeader) continue;
                foreach (TableCell cell in row)
                {
                    var paras = cell.OfType<ParagraphBlock>().ToList();
                    if (!paras.Any(p => p.Inline?.Any() == true)) continue;

                    col.Item().Text(t =>
                    {
                        foreach (var para in paras)
                            RenderInlines(t, para.Inline);
                    });
                }
            }
            col.Item().LineHorizontal(0.5f);
            return;
        }

        // Multi-column tables — render with borders
        // Derive column count from the header row, trimming any trailing empty cell
        // that Markdig adds for the closing pipe character.
        int colCount;
        var headerRow = rows.FirstOrDefault(r => r.IsHeader);
        if (headerRow != null)
        {
            var headerCells = headerRow.OfType<TableCell>().ToList();
            colCount = headerCells.Count;
            while (colCount > 0 && GetCellTextLength(headerCells[colCount - 1]) == 0)
                colCount--;
            if (colCount == 0)
                colCount = DeriveColCountFromData(table, rows, maxCellsPerRow);
        }
        else
        {
            colCount = DeriveColCountFromData(table, rows, maxCellsPerRow);
        }

        var colWidths = ComputeColumnWidths(rows, colCount);

        col.Item().Table(tbl =>
        {
            tbl.ColumnsDefinition(cols =>
            {
                foreach (var w in colWidths)
                {
                    if (w < 0) cols.ConstantColumn(-w); // negative = constant pt width
                    else cols.RelativeColumn(w);
                }
            });

            foreach (TableRow row in table)
            {
                bool isHeader = row.IsHeader;

                // Skip header rows where all cells are empty (markdown table syntax artifact)
                if (isHeader && row.OfType<TableCell>().All(cell => GetCellTextLength(cell) == 0))
                    continue;

                bool isShaded = isHeader || IsRowAllBold(row);

                int cellsAdded = 0;
                foreach (TableCell cell in row)
                {
                    if (cellsAdded >= colCount) break;
                    float pad = compact ? 1 : 3;
                    tbl.Cell()
                        .Background(isShaded ? Colors.Grey.Lighten3 : Colors.White)
                        .Border(0.5f)
                        .Padding(pad)
                        .Text(t =>
                        {
                            if (isHeader) t.DefaultTextStyle(x => x.Bold());
                            foreach (var b in cell)
                                if (b is ParagraphBlock para)
                                    RenderInlines(t, para.Inline);
                        });
                    cellsAdded++;
                }
                // Pad missing cells so the QuestPDF grid stays aligned
                while (cellsAdded < colCount)
                {
                    tbl.Cell().Background(isShaded ? Colors.Grey.Lighten3 : Colors.White).Border(0.5f).Padding(compact ? 1 : 3).Text("");
                    cellsAdded++;
                }
            }
        });
    }

    private static bool IsRowAllBold(TableRow row)
    {
        bool hasBoldContent = false;
        foreach (TableCell cell in row)
        {
            foreach (var block in cell)
            {
                if (block is not ParagraphBlock para || para.Inline == null) continue;
                foreach (var inline in para.Inline)
                {
                    if (inline is EmphasisInline em && em.DelimiterCount == 2) { hasBoldContent = true; continue; }
                    if (inline is LiteralInline lit && lit.Content.ToString().Trim() == "") continue;
                    return false;
                }
            }
        }
        return hasBoldContent;
    }

    private static int DeriveColCountFromData(Table table, List<TableRow> rows, int maxCellsPerRow)
    {
        // Use data rows as the source of truth: count max non-empty cells in any
        // data row. Trailing empty cells added by Markdig for the closing pipe
        // have length 0 and are excluded. This also handles whitespace-only headers
        // and the case where ColumnDefinitions mistakenly includes the trailing pipe.
        int dataMax = rows.Where(r => !r.IsHeader)
                          .Select(r => r.OfType<TableCell>().Count(c => GetCellTextLength(c) > 0))
                          .DefaultIfEmpty(0).Max();
        if (dataMax > 0) return dataMax;

        // No data rows — fall back to ColumnDefinitions then raw cell count
        return table.ColumnDefinitions?.Count ?? maxCellsPerRow;
    }

    // Compute column widths. Returns float[] where:
    //   negative value = ConstantColumn, absolute value = width in pt
    //   positive value = RelativeColumn weight
    //
    // Strategy: if the header text is longer than the data (header-bound), snap to a
    // constant width sized to the header. Otherwise use sqrt of data length so long
    // content columns get proportionally more space without crushing shorter ones.
    private static float[] ComputeColumnWidths(List<TableRow> rows, int colCount)
    {
        var headerLengths = new int[colCount];
        var maxDataLengths = new int[colCount];

        foreach (var row in rows)
        {
            int i = 0;
            foreach (TableCell cell in row)
            {
                if (i >= colCount) break;
                int len = GetCellTextLength(cell);
                if (row.IsHeader)
                    headerLengths[i] = Math.Max(headerLengths[i], len);
                else
                    maxDataLengths[i] = Math.Max(maxDataLengths[i], len);
                i++;
            }
        }

        // If all columns are short labels of similar length, equalize them.
        // This prevents one column with a short header but long data (or vice versa)
        // from absorbing all remaining space when neighbours snap to constants.
        int[] perColMax = Enumerable.Range(0, colCount)
            .Select(i => Math.Max(headerLengths[i], maxDataLengths[i]))
            .ToArray();
        int tableMax = perColMax.Length > 0 ? perColMax.Max() : 0;
        int tableMin = perColMax.Length > 0 ? perColMax.Min() : 0;
        if (tableMax <= 12 && (tableMin == 0 || tableMax / (float)tableMin <= 3f))
        {
            var equal = new float[colCount];
            for (int i = 0; i < colCount; i++) equal[i] = 1f;
            return equal;
        }

        var widths = new float[colCount];
        for (int i = 0; i < colCount; i++)
        {
            int h = headerLengths[i];
            int d = maxDataLengths[i];
            int maxLen = Math.Max(h, d);
            // Header is longer than (or close to) data and non-trivially long: snap to constant width.
            // Allow d up to h+2 so bold data that is 1-2 chars longer than the header still snaps.
            // Use max(h,d) so the constant accommodates whichever is wider.
            if (h > 6 && d <= h + 2)
                widths[i] = -(Math.Max(h, d) * 5.0f + 8f); // negative = ConstantColumn
            // Short columns (e.g. TN, Mod): snap to a small constant rather than
            // letting sqrt(8) inflate them to the same weight as medium columns.
            else if (maxLen <= 4)
                widths[i] = -(maxLen * 7f + 16f); // negative = ConstantColumn
            else
                widths[i] = (float)Math.Pow(Math.Max(maxLen, 8), 0.65f); // positive = RelativeColumn
        }
        return widths;
    }

    private static int GetCellTextLength(TableCell cell)
    {
        int total = 0;
        foreach (var block in cell)
            if (block is ParagraphBlock para && para.Inline != null)
                total += CountInlineLength(para.Inline);
        return total;
    }

    private static int CountInlineLength(ContainerInline inlines)
    {
        int total = 0;
        foreach (var inline in inlines)
        {
            total += inline switch
            {
                LiteralInline lit => lit.Content.Length,
                ContainerInline container => CountInlineLength(container),
                _ => 0
            };
        }
        return total;
    }

    private static void RenderInlines(TextDescriptor text, ContainerInline? inlines, bool inheritBold = false)
    {
        if (inlines == null) return;
        foreach (var inline in inlines)
            RenderInline(text, inline, inheritBold);
    }

    private static void RenderInline(TextDescriptor text, Inline inline, bool inheritBold = false)
    {
        switch (inline)
        {
            case LiteralInline literal:
                var span = text.Span(literal.Content.ToString());
                if (inheritBold) span.Bold();
                break;

            case EmphasisInline emphasis:
                bool bold = emphasis.DelimiterCount == 2 || inheritBold;
                bool italic = emphasis.DelimiterCount == 1;
                foreach (var child in emphasis)
                {
                    // Pass bold down; italic is handled per-span
                    RenderEmphasisChild(text, child, bold, italic);
                }
                break;

            case LinkInline link:
                foreach (var child in link)
                    RenderInline(text, child, inheritBold);
                break;

            case LineBreakInline:
                text.Span("\n");
                break;

            case HtmlInline html when html.Tag.Trim().ToLower() is "<br>" or "<br/>" or "<br />":
                text.Span("\n");
                break;

            case ContainerInline container:
                foreach (var child in container)
                    RenderInline(text, child, inheritBold);
                break;
        }
    }

    private static void RenderEmphasisChild(TextDescriptor text, Inline inline, bool bold, bool italic)
    {
        switch (inline)
        {
            case LiteralInline literal:
                var span = text.Span(literal.Content.ToString());
                if (bold) span.Bold();
                if (italic) span.Italic();
                break;

            case EmphasisInline nested:
                bool nestedBold = nested.DelimiterCount == 2 || bold;
                bool nestedItalic = nested.DelimiterCount == 1 || italic;
                foreach (var child in nested)
                    RenderEmphasisChild(text, child, nestedBold, nestedItalic);
                break;

            case ContainerInline container:
                foreach (var child in container)
                    RenderEmphasisChild(text, child, bold, italic);
                break;

            default:
                RenderInline(text, inline, bold);
                break;
        }
    }
}
