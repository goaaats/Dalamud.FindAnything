using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;

namespace Dalamud.FindAnything;

public record RepeatPolicy {
    public readonly long InitialDelay;
    public readonly long RepeatInterval;

    public RepeatPolicy(long initialDelay, long repeatInterval) {
        if (initialDelay < 0) throw new ArgumentException("initialDelay must be non-negative");
        if (repeatInterval < 1) throw new ArgumentException("repeatInterval must be positive");

        InitialDelay = long.Max(initialDelay, repeatInterval);
        RepeatInterval = repeatInterval;
    }
}

public class RepeatEngine(RepeatPolicy policy) {
    private bool isHeld;
    private long nextEmitTime;

    protected long Update(bool isDown, long now) {
        if (!isDown) {
            isHeld = false;
            return 0;
        }

        if (!isHeld) {
            isHeld = true;
            nextEmitTime = now + policy.InitialDelay;
            return 1;
        }

        if (now < nextEmitTime)
            return 0;

        var count = 1 + (now - nextEmitTime) / policy.RepeatInterval;
        nextEmitTime += count * policy.RepeatInterval;
        return count;
    }
}

public class ActionRepeater(RepeatPolicy policy, Action action) : RepeatEngine(policy) {
    public new void Update(bool isDown, long now) {
        var count = base.Update(isDown, now);
        if (count > 0) {
            for (var i = 0; i < count; i++) {
                action();
            }
        }
    }
}

public class DoubleTapEngine(long maxDelay) {
    private enum Phase {
        Idle,
        WaitingForRelease,
        WaitingForSecondPress,
    }

    private Phase phase;
    private long firstPressMark;

    protected bool Update(bool isDown, long now) {
        switch (phase) {
            case Phase.Idle:
                if (isDown) {
                    phase = Phase.WaitingForRelease;
                    firstPressMark = now;
                }
                return false;

            case Phase.WaitingForRelease:
                if (!isDown) {
                    phase = Phase.WaitingForSecondPress;
                }
                return false;

            case Phase.WaitingForSecondPress:
                if (!isDown)
                    return false;

                if (now - firstPressMark > maxDelay) {
                    // Too late, treat as first press again
                    phase = Phase.WaitingForRelease;
                    firstPressMark = now;
                    return false;
                }

                phase = Phase.Idle;
                return true;
        }

        throw new InvalidOperationException($"Unknown phase: {phase}");
    }

    public void Reset() {
        phase = Phase.Idle;
    }
}

public interface IDoubleTapDetector {
    bool Update(bool isDown);
}

public class TimeBasedDoubleTapDetector(long maxDelay) : DoubleTapEngine(maxDelay), IDoubleTapDetector {
    public bool Update(bool isDown) {
        return base.Update(isDown, Environment.TickCount64);
    }
}

public class FrameBasedDoubleTapDetector(long maxDelay) : DoubleTapEngine(maxDelay), IDoubleTapDetector {
    public unsafe bool Update(bool isDown) {
        return base.Update(isDown, Framework.Instance()->FrameCounter);
    }
}
