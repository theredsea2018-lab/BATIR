using System.Diagnostics;
using System.Text;

namespace BATIR;

internal static class OcrService
{
    public static bool IsAvailable(out string executable)
    {
        executable = "";
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var folder in path.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(folder.Trim(), "tesseract.exe");
            if (File.Exists(candidate)) { executable = candidate; return true; }
        }
        return false;
    }

    public static string ExtractImageText(string imagePath)
    {
        if (!IsAvailable(out var exe)) throw new InvalidOperationException("موتور OCR (Tesseract) روی ویندوز پیدا نشد. آن را نصب و دوباره امتحان کنید.");
        var outputBase = Path.Combine(Path.GetTempPath(), "batir-ocr-" + Guid.NewGuid().ToString("N"));
        var psi = new ProcessStartInfo { FileName = exe, Arguments = Quote(imagePath) + " " + Quote(outputBase) + " -l fas+eng", UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("اجرای موتور OCR ممکن نشد.");
        var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        var textFile = outputBase + ".txt";
        try
        {
            if (process.ExitCode != 0 || !File.Exists(textFile)) throw new InvalidOperationException("OCR انجام نشد: " + error.Trim());
            return File.ReadAllText(textFile, Encoding.UTF8);
        }
        finally { if (File.Exists(textFile)) File.Delete(textFile); }
    }

    static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
