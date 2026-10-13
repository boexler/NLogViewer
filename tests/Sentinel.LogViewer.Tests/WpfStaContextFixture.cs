using System.Windows;
using System.Windows.Threading;

namespace Sentinel.LogViewer.Tests
{
    /// <summary>
    /// xUnit class fixture that provides a single STA thread with a WPF Application
    /// so that all tests run in the same STA/Application context and share one Dispatcher.
    /// Use for WPF tests that would otherwise see a stale Application.Current when run together.
    /// </summary>
    public class WpfStaContextFixture : IDisposable
    {
        private Dispatcher _dispatcher;
        private readonly Thread _thread;
        private readonly ManualResetEvent _ready = new ManualResetEvent(false);
        private const int StaThreadReadyTimeoutMs = 5000;

        public WpfStaContextFixture()
        {
            _thread = new Thread(StaThreadProc)
            {
                IsBackground = true
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!_ready.WaitOne(StaThreadReadyTimeoutMs))
                throw new InvalidOperationException("STA thread did not signal ready within timeout.");
            if (_dispatcher == null)
                throw new InvalidOperationException("STA thread did not set Dispatcher.");
        }

        private void StaThreadProc()
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _ready.Set();
            Dispatcher.Run();
        }

        /// <summary>
        /// Runs the given action on the shared STA thread (same thread as Application.Current).
        /// Blocks until the action completes.
        /// </summary>
        public void RunOnSta(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            _dispatcher.Invoke(action);
        }

        /// <summary>
        /// Runs the given function on the shared STA thread and returns its result.
        /// Blocks until the function completes.
        /// </summary>
        public T RunOnSta<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            return _dispatcher.Invoke(func);
        }

        /// <summary>
        /// Shuts down the WPF Application on the STA thread and joins the thread.
        /// </summary>
        public void Dispose()
        {
            if (_dispatcher != null && _dispatcher.Thread.IsAlive)
            {
                try
                {
                    _dispatcher.Invoke(() => Application.Current?.Shutdown());
                }
                catch (Exception)
                {
                    // Best effort; thread may already be shutting down
                }
            }
            _thread.Join(TimeSpan.FromSeconds(5));
        }
    }
}
