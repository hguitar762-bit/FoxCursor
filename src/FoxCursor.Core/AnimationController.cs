namespace FoxCursor.Core;

public enum FoxState { Idle, Moving, FastMoving, LeftClick, RightClick, DoubleClick, Charging, Dragging, KeyboardTyping, Sleeping }
public enum PointerAction { LeftDown, LeftUp, RightDown, RightUp, Wheel }
public readonly record struct Pose(FoxState State, double ScaleX, double ScaleY, double Tilt,
    double Bounce, double TailAngle, double TailStretch, double HandPhase, bool Blink);

public sealed class AnimationController
{
    private double lastActivity, lastMove = -100, lastKey = -100, lastStep = -1, downAt;
    private double lastClick = -100, clickX, clickY, effectUntil, keyRate = 5;
    private double x, y, velocityX, velocityY, tail, tailVelocity, wheelImpulse;
    private bool positioned, held, dragging;
    private FoxState clickState;
    public FoxState State { get; private set; }
    public double Speed => Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
    public bool Held => held;

    // Physical coordinates converted to 96-DPI units by the input adapter.
    public void Move(double px, double py, double now)
    {
        if (positioned)
        {
            double elapsed = now - lastMove;
            // Reinitialise after inactivity instead of dividing a large jump by a tiny dt.
            if (elapsed > 0 && elapsed < .25)
            {
                double dt = Math.Max(.001, elapsed);
                double a = 1 - Math.Exp(-dt / .045);
                velocityX += (Math.Clamp((px - x) / dt, -9000, 9000) - velocityX) * a;
                velocityY += (Math.Clamp((py - y) / dt, -9000, 9000) - velocityY) * a;
            }
            else velocityX = velocityY = 0;
        }
        positioned = true; x = px; y = py; lastMove = lastActivity = now;
    }

    public void Pointer(PointerAction action, double now, double px, double py,
        double doubleClickSeconds = .5, double doubleClickWidth = 4, double doubleClickHeight = 4, int wheel = 120)
    {
        lastActivity = now;
        switch (action)
        {
            case PointerAction.LeftDown:
                held = true; downAt = now;
                bool twice = now - lastClick <= doubleClickSeconds &&
                    Math.Abs(px - clickX) <= doubleClickWidth / 2 && Math.Abs(py - clickY) <= doubleClickHeight / 2;
                clickState = twice ? FoxState.DoubleClick : FoxState.LeftClick;
                effectUntil = now + (twice ? .42 : .24);
                lastClick = twice ? -100 : now; clickX = px; clickY = py;
                break;
            case PointerAction.RightDown:
                clickState = FoxState.RightClick; effectUntil = now + .3;
                break;
            case PointerAction.LeftUp:
                held = false;
                if (dragging) EndDrag(now);
                break;
            case PointerAction.Wheel:
                wheelImpulse = Math.Clamp(wheelImpulse + Math.Sign(wheel) * 100, -180, 180);
                break;
        }
    }
    public void Keyboard(double now, int count = 1)
    {
        double interval = now - lastKey;
        if (interval > .01 && interval < 1) keyRate = .65 * keyRate + .35 * Math.Clamp(count / interval, 2, 16);
        lastKey = lastActivity = now;
    }
    public void BeginDrag(double now) { dragging = true; lastActivity = now; }
    public void EndDrag(double now)
    {
        if (!dragging) return;
        dragging = false; clickState = FoxState.DoubleClick; effectUntil = now + .35; lastActivity = now;
    }
    public void Cancel(double now) { dragging = held = false; effectUntil = now; lastActivity = now; }
    public void Reset(double now)
    {
        held = dragging = positioned = false; velocityX = velocityY = tail = tailVelocity = wheelImpulse = 0;
        lastActivity = now; lastMove = lastKey = lastClick = -100; effectUntil = now; lastStep = now;
    }
    public Pose Step(double now, Preferences preferences)
    {
        double dt = lastStep < 0 ? 1d / 60 : Math.Clamp(now - lastStep, 0, .05);
        lastStep = now;
        if (now - lastMove > .035) { double decay = Math.Exp(-dt * 14); velocityX *= decay; velocityY *= decay; }
        State = dragging && preferences.DragAnimation ? FoxState.Dragging :
            now < effectUntil ? clickState : held && now - downAt > .45 ? FoxState.Charging :
            now - lastKey < .65 && preferences.KeyboardAnimation ? FoxState.KeyboardTyping :
            Speed > 1100 ? FoxState.FastMoving : now - lastMove < .12 ? FoxState.Moving :
            now - lastActivity > 8 && preferences.IdleAnimation ? FoxState.Sleeping : FoxState.Idle;
        if (!preferences.Animations)
        {
            tail = tailVelocity = wheelImpulse = 0;
            return new(State, 1, 1, 0, 0, 0, 1, 0, false);
        }
        double intensity = preferences.Intensity;
        double idleEnvelope = IdleBurst(now) ? Math.Sin(IdlePhase(now) / 1.8 * Math.PI) : 0;
        // Horizontal motion changes leaf compression; vertical motion bends the leaves.
        double target = preferences.TailPhysics ? Math.Clamp(-velocityY * .018 - velocityX * .006, -27, 27) : 0;
        if (preferences.TailPhysics)
        {
            // Small substeps keep the spring stable across delayed frames.
            int steps = Math.Max(1, (int)Math.Ceiling(dt / .008));
            for (int i = 0; i < steps; i++)
            {
                double h = dt / steps;
                tailVelocity += ((target - tail) * 190 - tailVelocity * 23 + wheelImpulse) * h;
                tail += tailVelocity * h;
            }
        }
        else tail = tailVelocity = 0;
        wheelImpulse *= Math.Exp(-dt * 8);
        double sx = 1, sy = 1, tilt = 0, bounce = 0;
        double pulse = Math.Sin(Math.Clamp((effectUntil - now) / .3, 0, 1) * Math.PI);
        switch (State)
        {
            case FoxState.LeftClick: sx += .11 * pulse; sy -= .12 * pulse; break;
            case FoxState.RightClick: tilt = -10 * pulse; break;
            case FoxState.DoubleClick: bounce = -12 * Math.Abs(Math.Sin((effectUntil - now) * 10)); break;
            case FoxState.Charging: sx += .06; sy -= .07; bounce = Math.Sin(now * 32); break;
            case FoxState.Dragging: sx = sy = .92; tilt = 5; break;
            case FoxState.FastMoving: tilt = Math.Clamp(velocityX * .002, -5, 5); break;
            case FoxState.Sleeping when IdleBurst(now):
                sy += Math.Sin(now * 1.8) * .016 * idleEnvelope; bounce = Math.Sin(now * 1.5) * 1.5 * idleEnvelope;
                break;
        }
        double leafIdle = State == FoxState.Sleeping ? Math.Sin(now * 1.4) * 2 * idleEnvelope : 0;
        bool blink = State == FoxState.Sleeping && IdleBurst(now) && IdlePhase(now) is > .45 and < .62;
        return new(State, 1 + (sx - 1) * intensity, 1 + (sy - 1) * intensity, tilt * intensity,
            bounce * intensity, (tail + leafIdle) * intensity,
            1 - Math.Clamp(velocityX / 20000, -.13, .13) * (preferences.TailPhysics ? intensity : 0),
            now * Math.Clamp(keyRate, 2, 16) * Math.PI, blink);
    }
    // Deterministic jitter from the last activity time avoids a rigid metronome without a random timer.
    private double IdlePhase(double now) => (now - lastActivity - 8) % (11.3 + Math.Abs(Math.Sin(lastActivity * 7.13)) * 4);
    private bool IdleBurst(double now) => now - lastActivity > 8 && IdlePhase(now) < 1.8;
    public bool NeedsFrames(double now, Preferences p) => p.Animations && p.Intensity > 0 &&
        (now - lastMove < .18 || Speed > 2 || Math.Abs(tail) > .05 || Math.Abs(tailVelocity) > .05 ||
        Math.Abs(wheelImpulse) > .05 || now < effectUntil || held || dragging ||
        (p.KeyboardAnimation && now - lastKey < .7) || (p.IdleAnimation && IdleBurst(now)));
}
