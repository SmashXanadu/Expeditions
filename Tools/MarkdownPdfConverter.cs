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

    public static bool Convert(string inputPath, string outputPath, bool stripLastLine = false)
    {
        string label = Path.GetFileName(outputPath);
        try
        {
            string content = File.ReadAllText(inputPath);
            if (stripLastLine) content = StripLastContentLine(content);

            var document = Markdown.Parse(content, Pipeline);

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
                        foreach (var block in document)
                        {
                            if (block is ParagraphBlock ep &&
                                ep.Inline?.FirstOrDefault() is LiteralInline el &&
                                el.Content.ToString().Trim() == @"\end")
                                break;
                            RenderBlock(col, block);
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

    private static string StripLastContentLine(string content)
    {
        var lines = content.Split('\n').ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                lines.RemoveAt(i);
                break;
            }
        }
        return string.Join('\n', lines);
    }

    private static void RenderBlock(ColumnDescriptor col, Block block)
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
                col.Item().Text(t => RenderInlines(t, para.Inline));
                break;

            case Table table:
                RenderTable(col, table);
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
                            RenderBlock(quoteCol, b);
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
                                RenderBlock(itemCol, b);
                        });
                    });
                }
                break;
        }
    }

    private static void RenderTable(ColumnDescriptor col, Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        int maxCellsPerRow = rows.Any() ? rows.Max(r => r.Count) : 0;

        // Single-column "card" tables (ability/skill cards) — render as plain stacked text
        if (maxCellsPerRow <= 1)
        {
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
                colCount = table.ColumnDefinitions?.Count ?? maxCellsPerRow;
        }
        else
        {
            colCount = table.ColumnDefinitions?.Count ?? maxCellsPerRow;
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

                int cellsAdded = 0;
                foreach (TableCell cell in row)
                {
                    if (cellsAdded >= colCount) break;
                    tbl.Cell()
                        .Background(isHeader ? Colors.Grey.Lighten3 : Colors.White)
                        .Border(0.5f)
                        .Padding(3)
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
                    tbl.Cell().Background(isHeader ? Colors.Grey.Lighten3 : Colors.White).Border(0.5f).Padding(3).Text("");
                    cellsAdded++;
                }
            }
        });
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

        var widths = new float[colCount];
        for (int i = 0; i < colCount; i++)
        {
            int h = headerLengths[i];
            int d = maxDataLengths[i];
            int maxLen = Math.Max(h, d);
            // Header is longer than data and non-trivially long: snap to constant width.
            // 6.5pt/char + 6pt padding comfortably fits bold header text at 8pt.
            if (h > 6 && d <= h)
                widths[i] = -(h * 6.5f + 6f); // negative = ConstantColumn
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
