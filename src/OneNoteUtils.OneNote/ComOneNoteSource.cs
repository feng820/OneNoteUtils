using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using OneNoteUtils.Core;

namespace OneNoteUtils.OneNote;

/// <summary>
/// Reads OneNote data via the COM Interop API, using Windows PowerShell 5.1
/// as a bridge. .NET 8's dynamic COM binder fails with TYPE_E_LIBNOTREGISTERED
/// on Microsoft 365 OneNote installations where the type library is not registered.
/// PowerShell 5.1 (.NET Framework) handles IDispatch without a type library.
/// Must be called from an STA thread.
/// </summary>
public class ComOneNoteSource : IOneNoteSource
{
    private const string PowerShell5Path = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
    private readonly ILogger<ComOneNoteSource> _logger;

    public ComOneNoteSource(ILogger<ComOneNoteSource> logger)
    {
        _logger = logger;

        if (!File.Exists(PowerShell5Path))
            throw new InvalidOperationException(
                $"Windows PowerShell 5.1 not found at {PowerShell5Path}. Required for OneNote COM interop.");

        _logger.LogDebug("ComOneNoteSource initialized (PowerShell 5.1 bridge).");
    }

    public string GetHierarchyXml()
    {
        _logger.LogDebug("Fetching hierarchy XML...");

        var script = @"
            $onenote = New-Object -ComObject OneNote.Application
            $xml = ''
            $onenote.GetHierarchy('', 4, [ref]$xml)
            [Console]::OutputEncoding = [Text.Encoding]::UTF8
            Write-Output $xml
        ";

        var result = RunPowerShell5(script);
        _logger.LogDebug("Hierarchy XML received: {Length} characters", result.Length);
        return result;
    }

    public string GetPageContentXml(string pageId)
    {
        // Escape single quotes in pageId for PowerShell
        var escapedId = pageId.Replace("'", "''");

        var script = $@"
            $onenote = New-Object -ComObject OneNote.Application
            $xml = ''
            $onenote.GetPageContent('{escapedId}', [ref]$xml, 1)
            [Console]::OutputEncoding = [Text.Encoding]::UTF8
            Write-Output $xml
        ";

        return RunPowerShell5(script);
    }

    private string RunPowerShell5(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = PowerShell5Path,
            Arguments = "-STA -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start PowerShell 5.1 process.");

        process.StandardInput.Write(script);
        process.StandardInput.Close();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var errorMsg = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException(
                $"OneNote COM call failed (exit code {process.ExitCode}): {errorMsg.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(error))
            _logger.LogWarning("PowerShell stderr: {Error}", error.Trim());

        return output.Trim();
    }
}
