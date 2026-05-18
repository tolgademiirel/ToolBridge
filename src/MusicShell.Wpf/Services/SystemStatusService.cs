using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MusicShell.Models;

namespace MusicShell.Services;

public static class SystemStatusService
{
    public static IReadOnlyList<SystemStatusItem> GetConvertEngineStatuses()
    {
        var sumatraPath = FindSumatraPdfExecutable();
        var libreOfficePath = FindLibreOfficeExecutable();
        var imageMagickPath = FindImageMagickExecutable();
        var officeAvailable = IsMicrosoftOfficeAutomationAvailable();

        return new[]
        {
            new SystemStatusItem("Dahili görsel dönüştürücü", "JPG, PNG, BMP, GIF, TIFF ve ICO işlemleri", true, "Uygulama içinde hazır.", Environment.Version.ToString()),
            new SystemStatusItem("PDFium / Docnet", "PDF sayfalarını görsele çevirme ve PDF önizleme", ArePackagedToolsAvailable("pdfium.dll", "Docnet.Core.dll"), GetPackagedToolDetail("pdfium.dll", "Docnet.Core.dll"), GetPackagedToolVersion("Docnet.Core.dll")),
            new SystemStatusItem("SumatraPDF", "PDF dosyalarını sessiz yazdırma", IsExecutableAvailable(sumatraPath), GetExecutableDetail("SumatraPDF.exe", sumatraPath), GetFileVersionText(sumatraPath)),
            new SystemStatusItem("LibreOffice", "DOCX, XLSX, PPTX, ODT, ODS ve belge dönüşümleri", IsExecutableAvailable(libreOfficePath), GetLibreOfficeDetail(libreOfficePath), GetProcessVersionText(libreOfficePath, "--version")),
            new SystemStatusItem("ImageMagick", "Gelişmiş görsel format dönüştürmeleri", IsExecutableAvailable(imageMagickPath), GetImageMagickDetail(imageMagickPath), IsExecutableAvailable(imageMagickPath) ? GetProcessVersionText(imageMagickPath, "--version") : string.Empty),
            new SystemStatusItem("Microsoft Office", "Word, Excel ve PowerPoint COM dışa aktarma", officeAvailable, officeAvailable ? "Office COM otomasyonu algılandı." : "Office COM otomasyonu algılanmadı. LibreOffice yedek motor olarak kullanılabilir.", officeAvailable ? "COM hazır" : string.Empty)
        };
    }

    private static bool ArePackagedToolsAvailable(params string[] fileNames)
    {
        return fileNames.All(fileName => File.Exists(Path.Combine(AppContext.BaseDirectory, "Tools", fileName)) || File.Exists(Path.Combine(AppContext.BaseDirectory, fileName)));
    }

    private static string GetPackagedToolDetail(params string[] fileNames)
    {
        var missing = fileNames
            .Where(fileName => !File.Exists(Path.Combine(AppContext.BaseDirectory, "Tools", fileName)) && !File.Exists(Path.Combine(AppContext.BaseDirectory, fileName)))
            .ToList();

        return missing.Count == 0
            ? "Paket içindeki araç dosyaları hazır."
            : $"Eksik: {string.Join(", ", missing)}";
    }

    private static string GetPackagedToolVersion(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tools", fileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, fileName);
        }

        return GetFileVersionText(path);
    }

    private static string GetFileVersionText(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return string.Empty;
            }

            var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
            return string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetProcessVersionText(string? executablePath, string arguments)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return string.Empty;
            }

            if (!RunHiddenProcess(executablePath, arguments, 8000, out var standardOutput, out var standardError, out var exitCode) || exitCode != 0)
            {
                return string.IsNullOrWhiteSpace(standardError)
                    ? string.Empty
                    : standardError.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
            }

            return standardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsExecutableAvailable(string? resolvedPath)
    {
        return !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath);
    }

    private static string GetExecutableDetail(string executableName, string? resolvedPath)
    {
        return !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath)
            ? resolvedPath
            : $"{executableName} bulunamadı.";
    }

    private static string GetLibreOfficeDetail(string? resolvedPath)
    {
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            return resolvedPath.Contains(Path.Combine("Tools", "LibreOfficePortable"), StringComparison.OrdinalIgnoreCase)
                ? $"Portable LibreOffice hazır: {resolvedPath}"
                : resolvedPath;
        }

        return "soffice.exe bulunamadı. Portable LibreOffice Tools\\LibreOfficePortable klasöründe olmalı veya PATH üzerinden erişilebilir olmalı.";
    }

    private static string GetImageMagickDetail(string? resolvedPath)
    {
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            return resolvedPath.Contains(Path.Combine("Tools", "ImageMagick"), StringComparison.OrdinalIgnoreCase)
                ? $"Portable ImageMagick hazır: {resolvedPath}"
                : resolvedPath;
        }

        return "magick.exe bulunamadı. Portable ImageMagick Tools\\ImageMagick klasöründe olmalı veya PATH üzerinden erişilebilir olmalı.";
    }

    private static string? FindSumatraPdfExecutable()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "SumatraPDF.exe"),
            Path.Combine(AppContext.BaseDirectory, "SumatraPDF.exe"),
            Path.Combine(AppContext.BaseDirectory, "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SumatraPDF", "SumatraPDF.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF", "SumatraPDF.exe")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("SumatraPDF.exe");
    }

    private static string? FindLibreOfficeExecutable()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "LibreOfficePortable", "App", "LibreOffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "LibreOffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
        };

        AddLibreOfficeProgramFilesCandidates(possiblePaths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddLibreOfficeProgramFilesCandidates(possiblePaths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("soffice.exe");
    }

    private static void AddLibreOfficeProgramFilesCandidates(List<string> possiblePaths, string programRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(programRoot) || !Directory.Exists(programRoot))
            {
                return;
            }

            possiblePaths.AddRange(Directory.EnumerateDirectories(programRoot, "LibreOffice*", SearchOption.TopDirectoryOnly)
                .Select(folder => Path.Combine(folder, "program", "soffice.exe")));
        }
        catch
        {
            // Program Files taranamazsa PATH kontrolü devam eder.
        }
    }

    private static string? FindImageMagickExecutable()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "magick.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "ImageMagick", "magick.exe"),
            Path.Combine(AppContext.BaseDirectory, "ImageMagick", "magick.exe"),
            Path.Combine(AppContext.BaseDirectory, "magick.exe")
        };

        AddImageMagickProgramFilesCandidates(possiblePaths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddImageMagickProgramFilesCandidates(possiblePaths, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        return possiblePaths.FirstOrDefault(File.Exists) ?? FindExecutableInPath("magick.exe");
    }

    private static void AddImageMagickProgramFilesCandidates(List<string> possiblePaths, string programRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(programRoot) || !Directory.Exists(programRoot))
            {
                return;
            }

            possiblePaths.AddRange(Directory.EnumerateDirectories(programRoot, "ImageMagick*", SearchOption.TopDirectoryOnly)
                .Select(folder => Path.Combine(folder, "magick.exe")));
        }
        catch
        {
            // Program Files okunamazsa yok sayılır.
        }
    }

    private static bool IsMicrosoftOfficeAutomationAvailable()
    {
        try
        {
            return Type.GetTypeFromProgID("Word.Application") is not null ||
                   Type.GetTypeFromProgID("Excel.Application") is not null ||
                   Type.GetTypeFromProgID("PowerPoint.Application") is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindExecutableInPath(string executableName)
    {
        try
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var folder in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(folder.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // PATH okunamazsa yok sayılır.
        }

        return null;
    }

    private static string GetSafeWorkingDirectory(string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(fileName);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : AppContext.BaseDirectory;
        }
        catch
        {
            return AppContext.BaseDirectory;
        }
    }

    private static bool RunHiddenProcess(string fileName, string arguments, int timeoutMilliseconds, out string standardOutput, out string standardError, out int exitCode)
    {
        standardOutput = string.Empty;
        standardError = string.Empty;
        exitCode = -1;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = GetSafeWorkingDirectory(fileName),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                standardOutput = GetCompletedProcessOutput(outputTask);
                standardError = "İşlem zaman aşımına uğradı.";
                exitCode = -1;
                return true;
            }

            Task.WaitAll(new Task[] { outputTask, errorTask }, TimeSpan.FromSeconds(2));
            standardOutput = GetCompletedProcessOutput(outputTask);
            standardError = GetCompletedProcessOutput(errorTask);
            exitCode = process.ExitCode;
            return true;
        }
        catch (Exception ex)
        {
            standardError = ex.Message;
            return false;
        }
    }

    private static string GetCompletedProcessOutput(Task<string> outputTask)
    {
        if (!outputTask.IsCompletedSuccessfully)
        {
            return string.Empty;
        }

        try
        {
            return outputTask.Result.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

}
