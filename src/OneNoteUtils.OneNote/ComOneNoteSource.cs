using System.Reflection;
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
    private readonly object _onenote;
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
        // Parameters: bstrStartNodeID, hsScope (4 = hsPages), out pbstrHierarchyXmlOut
        var args = new object[] { "", 4 /* hsPages */, "" };
        var modifiers = new[] { new ParameterModifier(3) };
        modifiers[0][2] = true; // third param is out

        _onenote.GetType().InvokeMember(
            "GetHierarchy",
            BindingFlags.InvokeMethod,
            null,
            _onenote,
            args,
            modifiers,
            null,
            null);

        return (string)args[2];
    }

    public string GetPageContentXml(string pageId)
    {
        // Parameters: bstrPageID, out pbstrPageXmlOut, piAll (4 = include binary data)
        var args = new object[] { pageId, "", 4 /* piAll */ };
        var modifiers = new[] { new ParameterModifier(3) };
        modifiers[0][1] = true; // second param is out

        _onenote.GetType().InvokeMember(
            "GetPageContent",
            BindingFlags.InvokeMethod,
            null,
            _onenote,
            args,
            modifiers,
            null,
            null);

        return (string)args[1];
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Marshal.ReleaseComObject(_onenote);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
