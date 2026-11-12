using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;

namespace EckTimer
{
    internal sealed class Notifications : IDisposable
    {
        private readonly Stopwatch phase = new Stopwatch();
        private readonly MemoryStream wave = BuildRing();
        private readonly SoundPlayer player;
        private bool taskbarFlashing;

        public Notifications()
        {
            player = new SoundPlayer(wave);
            player.Load();
        }

        public Signal Active { get; private set; }
        public bool Bright { get { return (Active & Signal.Blink) != 0 && phase.ElapsedMilliseconds % 1000 < 500; } }

        public void Raise(Signal signal)
        {
            var added = signal & ~Active;
            Active |= signal;
            if ((added & Signal.Blink) != 0) phase.Restart();
            if ((added & Signal.Sound) != 0) player.PlayLooping();
        }

        public void UpdateTaskbar(IntPtr window, bool minimized)
        {
            bool shouldFlash = minimized && (Active & Signal.Blink) != 0;
            if (shouldFlash == taskbarFlashing) return;
            taskbarFlashing = shouldFlash;
            var flash = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(FlashInfo)),
                Window = window,
                Flags = shouldFlash ? 2u | 4u : 0u
            };
            FlashWindowEx(ref flash);
        }

        public void Dismiss()
        {
            if ((Active & Signal.Sound) != 0) player.Stop();
            Active = Signal.None;
            phase.Reset();
        }

        public void Dispose()
        {
            Dismiss();
            player.Dispose();
            wave.Dispose();
        }

        private static MemoryStream BuildRing()
        {
            const int rate = 22050;
            const int samples = rate * 2;
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(0x46464952);
            writer.Write(36 + samples * 2);
            writer.Write(0x45564157);
            writer.Write(0x20746d66);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(0x61746164);
            writer.Write(samples * 2);
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / rate;
                double note = t < .4 ? t : t - .48;
                double frequency = t < .4 ? 660 : 880;
                double envelope = note >= 0 && note < .36
                    ? Math.Min(1, note / .018) * Math.Min(1, (.36 - note) / .13) : 0;
                writer.Write((short)(9000 * envelope * Math.Sin(2 * Math.PI * frequency * t)));
            }
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashInfo
        {
            public uint Size;
            public IntPtr Window;
            public uint Flags, Count, Timeout;
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FlashInfo info);
    }
}
