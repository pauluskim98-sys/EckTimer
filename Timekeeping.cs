using System;
using System.Diagnostics;

namespace EckTimer
{
    [Flags]
    internal enum Signal { None = 0, Sound = 1, Blink = 2 }

    internal sealed class Timekeeping
    {
        private readonly Func<TimeSpan> monotonic;
        private readonly Func<DateTime> localNow;
        private TimeSpan countdownStarted, stopwatchStarted, remaining, elapsed;
        private DateTime? alarmDue;

        public TimeSpan Duration { get; private set; }
        public bool CountdownRunning { get; private set; }
        public bool StopwatchRunning { get; private set; }
        public DateTime? AlarmDue { get { return alarmDue; } }
        public Signal CountdownSignal { get; set; }
        public Signal AlarmSignal { get; set; }

        public Timekeeping() : this(Stopwatch.StartNew(), () => DateTime.Now) { }
        private Timekeeping(Stopwatch clock, Func<DateTime> now) : this(() => clock.Elapsed, now) { }
        internal Timekeeping(Func<TimeSpan> clock, Func<DateTime> now)
        {
            monotonic = clock;
            localNow = now;
        }

        public TimeSpan Remaining
        {
            get
            {
                var value = CountdownRunning ? remaining - (monotonic() - countdownStarted) : remaining;
                return value > TimeSpan.Zero ? value : TimeSpan.Zero;
            }
        }

        public TimeSpan Elapsed
        {
            get { return StopwatchRunning ? elapsed + (monotonic() - stopwatchStarted) : elapsed; }
        }

        public void SetDuration(TimeSpan duration)
        {
            Duration = duration;
            ResetCountdown();
        }

        public void ToggleCountdown()
        {
            if (CountdownRunning)
            {
                remaining = Remaining;
                CountdownRunning = false;
            }
            else
            {
                if (remaining == TimeSpan.Zero) remaining = Duration;
                if (remaining == TimeSpan.Zero) return;
                countdownStarted = monotonic();
                CountdownRunning = true;
            }
        }

        public void ResetCountdown()
        {
            CountdownRunning = false;
            remaining = Duration;
        }

        public void ToggleStopwatch()
        {
            if (StopwatchRunning) elapsed = Elapsed;
            else stopwatchStarted = monotonic();
            StopwatchRunning = !StopwatchRunning;
        }

        public void ResetStopwatch()
        {
            StopwatchRunning = false;
            elapsed = TimeSpan.Zero;
        }

        public void ArmAlarm(TimeSpan time)
        {
            var now = localNow();
            var due = now.Date + time;
            alarmDue = due > now ? due : due.AddDays(1);
        }

        public void DisarmAlarm() { alarmDue = null; }

        public Signal Check()
        {
            var signal = Signal.None;
            if (CountdownRunning && Remaining == TimeSpan.Zero)
            {
                CountdownRunning = false;
                remaining = TimeSpan.Zero;
                signal |= CountdownSignal;
            }
            if (alarmDue.HasValue && localNow() >= alarmDue.Value)
            {
                alarmDue = null;
                signal |= AlarmSignal;
            }
            return signal;
        }
    }
}
