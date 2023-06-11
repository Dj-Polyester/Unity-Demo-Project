using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Timer
{
    [Tooltip("As the name implies the duration in seconds before time expires.")]
    [SerializeField] float DurationBeforeTimeout = 2.0f;

    public class OnTimeout : UnityEvent { }
    public enum TimerState
    {
        Set,
        Running,
        Timeout,
    };

    public OnTimeout onTimeout;

    public float timeLeft;
    public TimerState state;


    public Timer(UnityAction call)
    {
        SetListener(call);
    }
    public Timer(UnityAction call, float durationBeforeTimeout)
    {
        SetListener(call);
        SetTimeLeft(durationBeforeTimeout);
    }

    public void SetListener(UnityAction call)
    {
        if (onTimeout == null)
            onTimeout = new OnTimeout();
        onTimeout.RemoveAllListeners();
        onTimeout.AddListener(call);
    }
    public void SetTimeLeft(float durationBeforeTimeout)
    {
        DurationBeforeTimeout = durationBeforeTimeout;
        state = TimerState.Set;
        timeLeft = DurationBeforeTimeout;
    }

    public void Reset()
    {
        state = TimerState.Set;
        timeLeft = DurationBeforeTimeout;
    }
    public void Tick(bool timeoutInvokeOnce = true)
    {
        if (timeLeft > 0f)
        {
            if (state == TimerState.Set)
            {
                state = TimerState.Running;
            }
            timeLeft -= Time.deltaTime;
        }
        else if (!timeoutInvokeOnce || state == TimerState.Running)
        {
            state = TimerState.Timeout;
            onTimeout.Invoke();
        }
    }
}
