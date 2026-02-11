using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ClassicUO.Game.Managers;

public static class MainThreadQueue
{
    private static int _threadId;
    private static bool _isMainThread => Thread.CurrentThread.ManagedThreadId == _threadId;
    private static ConcurrentQueue<Action> _queuedActions { get; } = new();

    /// <summary>
    /// Must be called from main thread
    /// </summary>
    public static void Load() => _threadId = Thread.CurrentThread.ManagedThreadId;

    /// <summary>
    /// This will not wait for the action to complete.
    /// </summary>
    /// <param name="action"></param>
    public static void EnqueueAction(Action action) => _queuedActions.Enqueue(action);

    /// <summary>
    ///     Wraps the given function with a try/catch, returning any caught exception
    /// </summary>
    /// <param name="callback">The function to wrap</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>A tuple of the Result and Exception (if one occurred)</returns>
    private static Func<(T, Exception)> WrapCallback<T>(Func<T> callback) =>
        () =>
        {
            try
            {
                return (callback(), null);
            }
            catch (Exception e)
            {
                return (default, e);
            }
        };

    /// <summary>
    ///     Dispatches the given function for invocation on the main thread and waits synchronously for the result
    /// </summary>
    /// <param name="func">The function to invoke on the main thread</param>
    /// <param name="cancellationToken">An optional cancellation token to interrupt result wait</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The result of the function's invocation</returns>
    /// <exception cref="Exception">The exception, if any, raised by the function invocation</exception>
    private static T BubblingDispatchToMainThread<T>(Func<T> func, CancellationToken? cancellationToken = null)
    {
        // The MT is so slow there's no real point in spinning; Just wastes CPU.
        var resultEvent = new ManualResetEventSlim(false, 0);

        T mtResult = default;
        Exception ex = null;

        _queuedActions.Enqueue(MtAction);

        // Wait for the main thread to complete the operation
        resultEvent.Wait(cancellationToken ?? CancellationToken.None);

        return ex != null ? throw ex : mtResult;

        void MtAction()
        {
            (T res, Exception e) = WrapCallback(func)();
            mtResult = res;
            ex = e;
            resultEvent.Set();
        }
    }

    /// <summary>
    ///     Dispatches a given function for execution on the MainThread.
    ///     If the current thread is the main thread, the function will run immediately as-is,
    ///     otherwise, the function will be dispatched and waited for.
    ///     Any exceptions raised on the main thread's context will be captured and bubbled back.
    /// </summary>
    /// <param name="func">The function to execute</param>
    /// <param name="cancellationToken">An optional cancellation token to interrupt result wait</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The function's result</returns>
    /// <exception cref="Exception">On any exception thrown by the given function</exception>
    public static T BubblingInvokeOnMainThread<T>(Func<T> func, CancellationToken? cancellationToken = null) =>
        _isMainThread
            ? func()
            : BubblingDispatchToMainThread(func, cancellationToken);

    /// <summary>
    /// This will wait for the returned result.
    /// </summary>
    /// <param name="func"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T InvokeOnMainThread<T>(Func<T> func)
    {
        if (_isMainThread) return func();

        // The MT is so slow there's no real point in spinning; Just wastes CPU.
        var resultEvent = new ManualResetEvent(false);
        T result = default;

        _queuedActions.Enqueue(Action);

        // Wait for the main thread to complete the operation
        resultEvent.WaitOne();

        return result;

        void Action()
        {
            result = func();
            resultEvent.Set();
        }
    }

    /// <summary>
    /// This will not wait for the returned result.
    /// </summary>
    /// <param name="action"></param>
    public static void InvokeOnMainThread(Action action)
    {
        if (_isMainThread)
        {
            action();
            return;
        }

        _queuedActions.Enqueue(action);
    }

    /// <summary>
    /// Must only be called on the main thread
    /// </summary>
    public static void ProcessQueue()
    {
        while (_queuedActions.TryDequeue(out Action action))
        {
            action();
        }
    }

    public static void Reset()
    {
        while (_queuedActions.TryDequeue(out _)) { }
    }
}
