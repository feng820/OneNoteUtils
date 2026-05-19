using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneNoteUtils.Core;

namespace OneNoteUtils.OneNote;

/// <summary>
/// Reads OneNote data via the COM Interop API.
/// Requires OneNote desktop (Win32) to be installed.
/// </summary>
public class ComOneNoteSource : IOneNoteSource, IDisposable
{
    private readonly dynamic _onenote;
    private readonly ILogger<ComOneNoteSource> _logger;
    private bool _disposed;

    public ComOneNoteSource(ILogger<ComOneNoteSource> logger)
    {
        _logger = logger;

        try
        {
            var onenoteType = Type.GetTypeFromProgID("OneNote.Application")
                ?? throw new InvalidOperationException(
                    "OneNote COM class not registered. Ensure OneNote (desktop) is installed.");

            _onenote = Activator.CreateInstance(onenoteType)
                ?? throw new InvalidOperationException(
                    "Failed to create OneNote COM object.");

            _logger.LogDebug("OneNote COM object created successfully.");
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                "Failed to create OneNote COM object. Ensure OneNote (desktop) is installed.", ex);
        }
    }

    public string GetHierarchyXml()
    {
        string hierarchyXml = "";
        _onenote.GetHierarchy("", 4 /* hsPages */, out hierarchyXml);
        return hierarchyXml;
    }

    public string GetPageContentXml(string pageId)
    {
        string pageXml = "";
        _onenote.GetPageContent(pageId, out pageXml, 1 /* piBasic with BinaryData */);
        return pageXml;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_onenote != null)
                Marshal.ReleaseComObject(_onenote);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
