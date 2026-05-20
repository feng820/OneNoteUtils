using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneNoteUtils.Core;
using OneNoteUtils.OneNote.Interop;

namespace OneNoteUtils.OneNote;

/// <summary>
/// Reads OneNote data via direct COM Interop with a dedicated STA thread.
/// All COM calls are dispatched to the STA thread via a work queue.
/// Requires OneNote desktop (Microsoft 365 / 2016+) to be installed.
/// </summary>
public class ComOneNoteSource : IOneNoteSource, IDisposable
{
    private readonly Thread _staThread;
    private readonly BlockingCollection<Action> _workQueue = new();
    private readonly ManualResetEventSlim _initialized = new(false);
    private readonly ILogger<ComOneNoteSource> _logger;
    private IOneNoteApplication? _app;
    private Exception? _initError;
    private bool _disposed;

    public ComOneNoteSource(ILogger<ComOneNoteSource> logger)
    {
        _logger = logger;

        _staThread = new Thread(StaThreadLoop) { IsBackground = true };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();

        if (!_initialized.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("OneNote COM initialization timed out.");

        if (_initError != null)
            throw new InvalidOperationException(
                $"Failed to initialize OneNote COM: {_initError.Message}", _initError);

        _logger.LogDebug("ComOneNoteSource initialized (direct COM, STA thread).");
    }

    public string GetHierarchyXml()
    {
        _logger.LogDebug("Fetching hierarchy XML...");
        var result = RunOnStaThread(() =>
        {
            _app!.GetHierarchy("", HierarchyScope.Pages, out var xml);
            return xml;
        });
        _logger.LogDebug("Hierarchy XML received: {Length} characters", result.Length);
        return result;
    }

    public string GetPageContentXml(string pageId)
    {
        return RunOnStaThread(() =>
        {
            _app!.GetPageContent(pageId, out var xml, PageInfo.BinaryData);
            return xml;
        });
    }

    private void StaThreadLoop()
    {
        try
        {
            _app = (IOneNoteApplication)new OneNoteApplicationClass();
        }
        catch (Exception ex)
        {
            _initError = ex;
            _initialized.Set();
            return;
        }

        _initialized.Set();

        foreach (var action in _workQueue.GetConsumingEnumerable())
        {
            action();
        }

        // Cleanup COM on the STA thread where it was created
        if (_app != null)
        {
            try { Marshal.ReleaseComObject((object)_app); }
            catch { /* best-effort cleanup */ }
        }
    }

    private T RunOnStaThread<T>(Func<T> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        T result = default!;
        Exception? error = null;
        using var done = new ManualResetEventSlim(false);

        _workQueue.Add(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();
        if (error != null) throw error;
        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _workQueue.CompleteAdding();
            _staThread.Join(TimeSpan.FromSeconds(5));
            _workQueue.Dispose();
            _initialized.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
