using JFlightShaker.Input;
using SharpDX.DirectInput;
using System.Windows;
using System.Windows.Interop;

namespace JFlightShaker.Service;

public sealed class InputPollingService : IDisposable
{
    private sealed class Subscription : IDisposable
    {
        private InputPollingService? _owner;
        private readonly Action<Guid, int> _handler;

        public Subscription(InputPollingService owner, Action<Guid, int> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Unsubscribe(_handler);
        }
    }

    public static InputPollingService Shared { get; } = new();

    private readonly DirectInputService _inputSvc = new();
    private readonly object _sync = new();
    private readonly Dictionary<Guid, bool[]> _prevButtonsByDevice = new();
    private readonly Dictionary<Guid, JoystickState> _latestStateByDevice = new();
    private MultiJoystickPoller? _poller;
    private int _subscriberCount;
    private bool _isDisposed;

    public event Action<Guid, int>? ButtonPressedEdge;

    public IDisposable ListenForButtons(Window window, Action<Guid, int> handler)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return ListenForButtons(hwnd, handler);
    }

    public IDisposable ListenForButtons(IntPtr hwnd, Action<Guid, int> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        lock (_sync)
        {
            ThrowIfDisposed();

            ButtonPressedEdge += handler;
            _subscriberCount++;

            if (_poller == null)
                StartPolling(hwnd);
        }

        return new Subscription(this, handler);
    }

    public void ArmListening(Guid deviceGuid)
    {
        lock (_sync)
        {
            if (_latestStateByDevice.TryGetValue(deviceGuid, out var state))
                SnapshotButtons(deviceGuid, state);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopPolling();
            ButtonPressedEdge = null;
            _inputSvc.Dispose();
        }
    }

    private void StartPolling(IntPtr hwnd)
    {
        _poller = new MultiJoystickPoller();
        _poller.StateReceived += OnState;

        var joys = _inputSvc.ListJoysticks();
        foreach (var dev in joys)
        {
            try
            {
                var js = _inputSvc.Open(dev, hwnd);
                _poller.Add(dev.InstanceGuid, js);
            }
            catch
            {
                // ignore devices that fail to open
            }
        }

        _poller.Start(5);
    }

    private void StopPolling()
    {
        if (_poller == null) return;

        _poller.StateReceived -= OnState;
        _poller.Dispose();
        _poller = null;
        _prevButtonsByDevice.Clear();
        _latestStateByDevice.Clear();
    }

    private void OnState(Guid deviceGuid, JoystickState state)
    {
        lock (_sync)
        {
            _latestStateByDevice[deviceGuid] = state;
            HandleTransitions(deviceGuid, state);
        }
    }

    private void HandleTransitions(Guid deviceGuid, JoystickState state)
    {
        var buttons = state.Buttons ?? Array.Empty<bool>();

        if (!_prevButtonsByDevice.TryGetValue(deviceGuid, out var prev) || prev.Length != buttons.Length)
        {
            prev = SnapshotButtons(deviceGuid, state);
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            bool isDown = buttons[i];
            bool wasDown = prev[i];

            if (isDown && !wasDown)
                ButtonPressedEdge?.Invoke(deviceGuid, i);
        }

        Array.Copy(buttons, prev, buttons.Length);
    }

    private bool[] SnapshotButtons(Guid deviceGuid, JoystickState state)
    {
        var buttons = state.Buttons ?? Array.Empty<bool>();
        var snapshot = new bool[buttons.Length];
        Array.Copy(buttons, snapshot, buttons.Length);
        _prevButtonsByDevice[deviceGuid] = snapshot;
        return snapshot;
    }

    private void Unsubscribe(Action<Guid, int> handler)
    {
        lock (_sync)
        {
            if (_isDisposed) return;

            ButtonPressedEdge -= handler;
            _subscriberCount = Math.Max(0, _subscriberCount - 1);

            if (_subscriberCount == 0)
                StopPolling();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(InputPollingService));
    }
}