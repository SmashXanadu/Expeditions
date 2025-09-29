using PdfSharpCore.Pdf.IO;
using System.Diagnostics;

public class Program
{
    public static void Main(string[] args)
    {
        string currentDirectory = AppContext.BaseDirectory;
        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloadsPath = Path.Combine(userProfilePath, "Downloads");
        string solutionRoot = Path.GetFullPath(Path.Combine(currentDirectory, @"..\..\..\..\"));
        //TODO Wrap All of these into a list to do full Asset Prints
        //string sourceFolder = Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Abilities");
        //string sourceFolder = Path.Combine(solutionRoot, "Expeditions", "Player Resources", "General Skills");
        //string sourceFolder = Path.Combine(solutionRoot, "Expeditions", "Player Resources", "Trade Skills");
        string sourceFolder = Path.Combine(solutionRoot, "Expeditions", "System Rules", "Rules");

        string tempInputFolder = Path.Combine(@"C:\\Users\\willi\\AppData\\Local\\Pandoc\\Temp", Path.GetRandomFileName());
        string inputFolder = tempInputFolder;
        string outputFolder = downloadsPath + "\\Outputs";
        Directory.CreateDirectory(outputFolder);

        bool cleanupSuccess = false;

        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string pdfEnginePath = Path.Combine(
            localAppDataPath,
            "Programs",
            "MiKTeX",
            "miktex",
            "bin",
            "x64",
            "xelatex.exe"
        );

        string pandocPath = Path.Combine(
            localAppDataPath,
            "Pandoc",
            "pandoc.exe"
        );

        if (!File.Exists(pandocPath))
        {
            Console.WriteLine($"\nCRITICAL ERROR: Pandoc executable NOT FOUND at the calculated path. Please verify the installation folder.");
            return;
        }

        try
        {
            Console.WriteLine("--- File Preparation ---");
            Console.WriteLine($"Source Folder: {sourceFolder}");
            Console.WriteLine($"Temporary Working Folder: {tempInputFolder}\n");

            if (!Directory.Exists(sourceFolder))
            {
                Console.WriteLine($"Error: Source directory not found at {sourceFolder}.");
                return;
            }

            CopyDirectory(sourceFolder, tempInputFolder, true);
            Console.WriteLine($"Successfully copied folder contents to temporary location.\n");

            string[] markdownFiles = Directory.GetFiles(inputFolder, "*.md", SearchOption.TopDirectoryOnly);

            if (markdownFiles.Length == 0)
            {
                Console.WriteLine("No Markdown files found in the temporary input directory. Exiting.");
                cleanupSuccess = true; 
                return;
            }

            Console.WriteLine($"--- Starting Conversion of {markdownFiles.Length} Individual Files ---");

            foreach (string markdownFile in markdownFiles.OrderBy(f => f))
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(markdownFile);
                if (fileNameWithoutExtension.StartsWith("-")) { continue; }
                string outputPdfPath = Path.Combine(outputFolder, $"{fileNameWithoutExtension}.pdf");

                if (sourceFolder.Contains("System Rules")) { RemoveLastLine(markdownFile); }

                ConvertMarkdownToPdf(markdownFile, outputPdfPath, pdfEnginePath, pandocPath);
            }

            cleanupSuccess = true; 

            Console.WriteLine("\n--- Conversion Process Complete ---");

            CombinePDFs(outputFolder, sourceFolder.Split("\\").ToList().Last() + ".pdf");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL ERROR] An unexpected exception occurred during processing: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempInputFolder))
            {
                try
                {
                    Directory.Delete(tempInputFolder, true); // true = recursive delete
                    Console.WriteLine($"\nSuccessfully cleaned up temporary folder: {tempInputFolder}");
                }
                catch (Exception ex)
                {
                    // Catch cleanup errors but don't stop the program from exiting
                    Console.WriteLine($"\n[CLEANUP ERROR] Failed to delete temporary folder. You may need to delete it manually: {ex.Message}");
                }
            }
        }

    }

    /// <summary>
    /// Executes the Pandoc process to convert a single Markdown file to a PDF.
    /// </summary>
    /// <param name="inputMarkdownPath">The full path to the input Markdown file.</param>
    /// <param name="outputPdfPath">The full path for the output PDF file.</param>
    /// <param name="pdfEnginePath">The full path to the PDF engine (pdflatex.exe).</param>
    /// <summary>
    /// Executes the Pandoc process to convert multiple Markdown files to a single PDF.
    /// </summary>
    /// <param name="inputFiles">A space-separated string of all quoted input Markdown file paths.</param>
    /// <param name="outputPdfPath">The full path for the single output PDF file.</param>
    // Note the updated parameter name for clarity
    public static void ConvertMarkdownToPdf(string inputMarkdownPath, string outputPdfPath, string pdfEnginePath, string fullPandocPath)
    {
        //string geometryOption = "-V geometry:paperwidth=5.5in -V geometry:paperheight=8.5in -V geometry:margin=0.25in";
        string fontName = "Atkinson Hyperlegible";

        string fontOption = $"-V mainfont=\"{fontName}\"";
        string geometryOption = "-V geometry:paperwidth=5.5in -V geometry:paperheight=8.5in -V geometry:margin=0.25in";
        string fontSizeOption = "-V fontsize=8pt";

        string arguments = $"-s \"{inputMarkdownPath}\" -o \"{outputPdfPath}\" --pdf-engine=xelatex {geometryOption} {fontOption} {fontSizeOption}";


        // Extract the directory of pdflatex.exe (the MiKTeX bin folder)
        string pdfEngineDirectory = Path.GetDirectoryName(pdfEnginePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = fullPandocPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Temporarily inject the MiKTeX directory into the PATH for this process
        startInfo.EnvironmentVariables["PATH"] =
            $"{pdfEngineDirectory};{Environment.GetEnvironmentVariable("PATH")}";

        try
        {
            Console.Write($"Generating {inputMarkdownPath.Split("\\").ToList().Last().Replace(".md",".pdf")} ...");
            // ... (rest of the try/catch block for process execution) ...
            using (var process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine(" [SUCCESS]");
                }
                else
                {
                    Console.WriteLine(" [FAILED]");
                    Console.WriteLine($"   Error: {error.Trim()}");
                    throw new Exception(error);
                }
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"\nAn exception occurred: Pandoc executable not found at: {fullPandocPath}. Please verify the path.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn unexpected exception occurred: {ex.Message}");
        }
    }
    private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);

        // Create all necessary directories
        Directory.CreateDirectory(destinationDir);

        // Copy all files
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // Copy sub-directories
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
    private static void CombinePDFs(string folderPath, string fileName)
    {
        string fullOutputFilePath = Path.Combine(folderPath, fileName);
        var outputDocument = new PdfSharpCore.Pdf.PdfDocument();

        var files = Directory.GetFiles(folderPath)
                   .Where(file => Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(file => file)
                   .ToList();

        foreach (var file in files)
        {
            if (file.Equals(fullOutputFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using (var inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                {
                    for (int i = 0; i < inputDocument.PageCount; i++)
                    {
                        outputDocument.AddPage(inputDocument.Pages[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (outputDocument.PageCount > 0)
        {
            outputDocument.Save(fullOutputFilePath);
            Console.WriteLine($"Successfully combined {files.Count} files into {Path.GetFileName(fullOutputFilePath)}.");
        }
    }
    private static void RemoveLastLine(string filePath)
    {
        try
        {
            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length == 0) return;

            int lastContentLineIndex = -1;

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    lastContentLineIndex = i;
                    break;
                }
            }

            if (lastContentLineIndex == -1)
            {
                File.WriteAllText(filePath, string.Empty);
                return;
            }

            // 4. Select all lines up to (but not including) the last content line
            // This array now contains the content with trailing empty lines removed AND the last content line removed.
            string[] linesToKeep = lines.Take(lastContentLineIndex).ToArray();

            // 5. Write the truncated content back to the file
            File.WriteAllLines(filePath, linesToKeep);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing last line from {Path.GetFileName(filePath)}: {ex.Message}");
        }
    }
}