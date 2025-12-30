using System;
using System.Collections.Generic;
using System.Threading;
using SharpDX.DirectInput;

namespace ThrottleShaker.Input;

public sealed class MultiJoystickPoller : IDisposable
{
    public event Action<Guid, JoystickState>? StateReceived;
    public event Action<Exception>? PollError;

    private readonly Dictionary<Guid, Joystick> _sticks = new();

    private Thread? _thread;
    private volatile bool _running;
    private int _intervalMs = 5;

    public void Add(Guid deviceGuid, Joystick joystick)
    {
        _sticks[deviceGuid] = joystick;
    }

    public void Start(int intervalMs = 5)
    {
        _intervalMs = Math.Max(1, intervalMs);
        if (_running) return;

        _running = true;
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "MultiJoystickPoller"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(200);
        _thread = null;
    }

    private void PollLoop()
    {
        while (_running)
        {
            try
            {
                foreach (var kv in _sticks)
                {
                    var guid = kv.Key;
                    var js = kv.Value;

                    js.Poll();
                    var state = js.GetCurrentState();
                    StateReceived?.Invoke(guid, state);
                }
            }
            catch (Exception ex)
            {
                PollError?.Invoke(ex);
            }

            Thread.Sleep(_intervalMs);
        }
    }

    public void Dispose()
    {
        Stop();

        foreach (var js in _sticks.Values)
        {
            try { js.Unacquire(); } catch { }
            js.Dispose();
        }
        _sticks.Clear();
    }
}
