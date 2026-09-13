using System;
using System.Threading;
using System.Windows.Threading;

namespace IconGrid.Helpers.Launcher;

/// <summary>
/// Ensures only ONE IconGrid launcher runs per user session.
///
/// Without this, a second launch (for example while the first instance sits in
/// floating-icon mode, or after an abnormal close) started a second launcher
/// process. That second process could not take over the hardware-monitor agent
/// mutex ("Hardware monitor agent is already running...") and therefore inherited
/// the previous session's stale FPS target — which made the gaming overlay appear
/// at startup with no game running.
///
/// Behaviour:
///  - The first instance acquires the mutex and listens for an activation signal.
///  - Any later instance signals the running instance to come to the foreground,
///    then exits immediately, so no duplicate launcher/agent is ever created.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\IconGrid.Launcher.SingleInstance";
    private const string ActivateEventName = @"Local\IconGrid.Launcher.Activate";

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _ownsMutex;

    /// <summary>
    /// Attempts to become the single running launcher instance.
    /// Returns true when this process is the first (owning) instance.
    /// </summary>
    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                // Another launcher already owns the single-instance mutex.
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            return true;
        }
        catch
        {
            // If the guard itself fails we must not block the app from starting.
            _mutex = null;
            _ownsMutex = false;
            return true;
        }
    }

    /// <summary>
    /// Called by a later instance: asks the already-running instance to show
    /// itself. Best-effort — a missing event simply means the running instance is
    /// still starting up, and this process exits either way.
    /// </summary>
    public static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var handle))
            {
                using (handle)
                {
                    handle.Set();
                }
            }
        }
        catch
        {
            // Best effort.
        }
    }

    /// <summary>
    /// Started by the owning instance: invokes <paramref name="onActivate"/> on the
    /// UI thread whenever another launch asks this instance to come to the foreground.
    /// </summary>
    public void ListenForActivation(Dispatcher dispatcher, Action onActivate)
    {
        try
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                _activateEvent,
                (_, _) =>
                {
                    try
                    {
                        dispatcher.BeginInvoke(onActivate, DispatcherPriority.Normal);
                    }
                    catch
                    {
                        // The dispatcher may already be shutting down.
                    }
                },
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);
        }
        catch
        {
            // Best effort: single-instance protection still works without activation.
        }
    }

    public void Dispose()
    {
        try
        {
            _registeredWait?.Unregister(null);
        }
        catch
        {
            // ignore
        }

        try
        {
            _activateEvent?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            if (_ownsMutex && _mutex != null)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            _mutex?.Dispose();
        }
        catch
        {
            // ignore
        }

        _registeredWait = null;
        _activateEvent = null;
        _mutex = null;
        _ownsMutex = false;
    }
}
