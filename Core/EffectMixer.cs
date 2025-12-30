using System;
using System.Collections.Generic;
using ThrottleShaker.Audio;

namespace ThrottleShaker.Core;

public sealed class EffectMixer
{
    private readonly List<RumbleEffect> _active = new();

    public void Add(RumbleEffect effect)
    {
        if (effect != null) _active.Add(effect);
    }

    public float Update(float dtSeconds)
    {
        float sum = 0f;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var e = _active[i];
            sum += e.UpdateAndGetAmp(dtSeconds);

            if (e.Finished)
                _active.RemoveAt(i);
        }

        return Math.Clamp(sum, 0f, 1f);
    }

    public void StopAll()
    {
        foreach (var e in _active) e.Stop();
        _active.Clear();
    }
}
