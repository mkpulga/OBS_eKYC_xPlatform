using System.Buffers.Binary;
using System.Collections.Concurrent;
using OBS.Core;

static class FingerprintScannerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    public static void Register(Action<string, Func<Task>> add, Action<bool, string> check)
    {
        add("Grayscale BMP has an 8-bit header, palette, bottom-up rows and padding", () =>
        {
            var bitmap = GrayscaleBitmap.Encode(new(3, 2, [1, 2, 3, 4, 5, 6]));
            int Int32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bitmap.AsSpan(offset));
            short Int16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bitmap.AsSpan(offset));
            check(bitmap[0] == 'B' && bitmap[1] == 'M', "BMP signature");
            check(Int32(2) == bitmap.Length && bitmap.Length == 1086 && Int32(10) == 1078, "BMP file size/pixel offset");
            check(Int32(14) == 40 && Int32(18) == 3 && Int32(22) == 2, "DIB size/dimensions");
            check(Int16(26) == 1 && Int16(28) == 8 && Int32(30) == 0 && Int32(34) == 8 && Int32(46) == 256, "8-bit uncompressed header");
            for (var shade = 0; shade < 256; shade++)
            {
                var offset = 54 + shade * 4;
                check(bitmap[offset] == shade && bitmap[offset + 1] == shade && bitmap[offset + 2] == shade && bitmap[offset + 3] == 0, "Grayscale palette changed");
            }
            check(bitmap.AsSpan(1078).SequenceEqual(new byte[] { 4, 5, 6, 0, 1, 2, 3, 0 }), "Row order or padding");
            return Task.CompletedTask;
        });
        add("Grayscale BMP preserves blank frames and aligned/one-pixel rows", () =>
        {
            var aligned = GrayscaleBitmap.Encode(new(4, 2, [0, 10, 20, 255, 40, 50, 60, 70]));
            check(aligned.Length == 1086 && aligned.AsSpan(1078).SequenceEqual(new byte[] { 40, 50, 60, 70, 0, 10, 20, 255 }), "Aligned pixels altered");
            var narrow = GrayscaleBitmap.Encode(new(1, 3, [10, 20, 30]));
            check(narrow.AsSpan(1078).SequenceEqual(new byte[] { 30, 0, 0, 0, 20, 0, 0, 0, 10, 0, 0, 0 }), "One-pixel row padding");
            foreach (var blank in new byte[] { 0, 255 })
                check(GrayscaleBitmap.Encode(new(4, 1, [blank, blank, blank, blank])).AsSpan(1078).ToArray().All(pixel => pixel == blank), "Blank frame filtered or inverted");
            return Task.CompletedTask;
        });
        add("Grayscale BMP rejects invalid dimensions and pixel lengths", () =>
        {
            foreach (var frame in new FingerprintFrame[] { new(0, 1, []), new(-1, 1, []), new(2, 2, [1]), new(int.MaxValue, 2, []), new(1, 1, null!) })
            {
                var rejected = false;
                try { GrayscaleBitmap.Encode(frame); } catch (ArgumentException) { rejected = true; }
                check(rejected, "Invalid raw frame accepted");
            }
            check(!new FingerprintFrame(1, 1, [42]).ToString().Contains("42"), "Frame logged pixel data");
            return Task.CompletedTask;
        });
        add("Fingerprint Start/Stop ignores a second Start and closes after the native read", async () =>
        {
            var device = new FakeDevice();
            var scanner = new FakeScanner(() => device); var session = new FingerprintScanSession(scanner);
            check(session.Snapshot.State == FingerprintScanState.Idle, "Initial state");
            check(session.Start(), "Start rejected");
            await device.WaitForRead();
            check(session.Snapshot is { State: FingerprintScanState.Scanning, IsRunning: true } && !session.Start() && scanner.Opens == 1, "Repeated Start opened another device");
            var stopped = session.StopAsync();
            check(!stopped.IsCompleted && device.Closes == 0 && session.Snapshot.IsRunning, "Stop closed during native read");
            device.Return(FingerprintReadResult.Transient); await stopped.WaitAsync(Timeout);
            check(session.Snapshot is { State: FingerprintScanState.Idle, IsRunning: false, ErrorMessage: null }, "Stop state");
            check(device.Closes == 1 && !device.ClosedDuringRead && scanner.OpenThread == device.ReadThread && device.ReadThread == device.CloseThread, "Device lifetime crossed threads or closed twice");
            await session.StopAsync(); check(device.Closes == 1, "Repeated Stop closed twice");
        });
        add("Fingerprint preview retains only the latest frame and freezes it on Stop", async () =>
        {
            var device = new FakeDevice(); var session = new FingerprintScanSession(new FakeScanner(() => device));
            var first = new FingerprintFrame(2, 1, [0, 0]); var latest = new FingerprintFrame(2, 1, [255, 255]);
            session.Start(); await device.WaitForRead();
            device.Return(new(first)); await device.WaitForRead();
            check(ReferenceEquals(session.Snapshot.Frame, first), "Blank frame not published");
            device.Return(new(latest)); await device.WaitForRead();
            check(ReferenceEquals(session.Snapshot.Frame, latest), "Latest frame not retained");
            var stopped = session.StopAsync();
            device.Return(new(new FingerprintFrame(2, 1, [9, 9]))); await stopped.WaitAsync(Timeout);
            check(ReferenceEquals(session.Snapshot.Frame, latest), "In-flight frame published after Stop");
            session.ClearFrame(); check(session.Snapshot.Frame is null && device.Closes == 1, "Preview not cleared");
        });
        add("Fingerprint transient reads are skipped without a fault", async () =>
        {
            var device = new FakeDevice(); var session = new FingerprintScanSession(new FakeScanner(() => device));
            var failures = 0; session.Changed += () => { if (session.Snapshot.State == FingerprintScanState.Faulted) Interlocked.Increment(ref failures); };
            session.Start(); await device.WaitForRead();
            for (var i = 0; i < 3; i++)
            {
                device.Return(FingerprintReadResult.Transient); await device.WaitForRead();
                check(session.Snapshot is { State: FingerprintScanState.Scanning, Frame: null } && failures == 0, "Transient read treated as fatal");
            }
            var frame = new FingerprintFrame(1, 1, [23]); device.Return(new(frame)); await device.WaitForRead();
            check(ReferenceEquals(session.Snapshot.Frame, frame), "Scanning did not resume after transients");
            var stopped = session.StopAsync(); device.Return(FingerprintReadResult.Transient); await stopped.WaitAsync(Timeout);
            check(device.Closes == 1 && failures == 0, "Transient session cleanup");
        });
        add("Fingerprint fatal read closes exactly once and reports one failure", async () =>
        {
            var device = new FakeDevice(); var session = new FingerprintScanSession(new FakeScanner(() => device));
            var failures = 0; session.Changed += () => { if (session.Snapshot.State == FingerprintScanState.Faulted) Interlocked.Increment(ref failures); };
            session.Start(); await device.WaitForRead();
            device.Throw(new FingerprintScannerException(FingerprintScannerMessages.Disconnected)); await session.Completion.WaitAsync(Timeout);
            check(session.Snapshot is { State: FingerprintScanState.Faulted, IsRunning: false, ErrorMessage: FingerprintScannerMessages.Disconnected }, "Fatal state/message");
            await session.StopAsync(); await session.StopAsync();
            check(failures == 1 && device.Closes == 1 && !device.ClosedDuringRead, "Failure repeated or device leaked");
        });
        add("Fingerprint external cancellation closes once without a failure", async () =>
        {
            var device = new FakeDevice(); using var cancel = new CancellationTokenSource();
            var session = new FingerprintScanSession(new FakeScanner(() => device));
            session.Start(cancel.Token); await device.WaitForRead(); cancel.Cancel();
            check(device.Closes == 0 && !session.Completion.IsCompleted, "Cancel disposed during native read");
            device.Throw(new IOException("Native call failed after cancellation")); await session.Completion.WaitAsync(Timeout);
            check(session.Snapshot is { State: FingerprintScanState.Idle, IsRunning: false, ErrorMessage: null } && device.Closes == 1, "Cancellation became a failure or leaked the device");
        });
        add("Fingerprint pre-cancelled Start opens no device", async () =>
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var scanner = new FakeScanner(() => throw new InvalidOperationException("Must not open"));
            var session = new FingerprintScanSession(scanner); session.Start(cancel.Token); await session.Completion.WaitAsync(Timeout);
            check(scanner.Opens == 0 && session.Snapshot is { State: FingerprintScanState.Idle, IsRunning: false }, "Cancelled session opened a device");
        });
        add("Fingerprint open failures use safe library, architecture and device messages", async () =>
        {
            foreach (var (error, message) in new (Exception, string)[]
            {
                (new DllNotFoundException("private detail"), FingerprintScannerMessages.MissingLibrary),
                (new BadImageFormatException("private detail"), FingerprintScannerMessages.WrongArchitecture),
                (new FingerprintScannerException(FingerprintScannerMessages.NotDetected), FingerprintScannerMessages.NotDetected),
                (new EntryPointNotFoundException("private detail"), FingerprintScannerMessages.Disconnected)
            })
            {
                var failures = 0; var session = new FingerprintScanSession(new FakeScanner(() => throw error));
                session.Changed += () => { if (session.Snapshot.State == FingerprintScanState.Faulted) Interlocked.Increment(ref failures); };
                session.Start(); await session.Completion.WaitAsync(Timeout);
                check(session.Snapshot.ErrorMessage == message && failures == 1 && !session.Snapshot.IsRunning, "Unsafe or repeated error");
            }
        });
        add("Fingerprint invalid frames and disposal failures cannot simulate success", async () =>
        {
            foreach (var disposeFails in new[] { false, true })
            {
                var device = new FakeDevice { DisposeFails = disposeFails };
                var session = new FingerprintScanSession(new FakeScanner(() => device)); session.Start(); await device.WaitForRead();
                if (disposeFails) device.Throw(new FingerprintScannerException(FingerprintScannerMessages.Disconnected));
                else device.Return(new(new FingerprintFrame(4, 4, [1])));
                await session.Completion.WaitAsync(Timeout);
                check(session.Snapshot is { State: FingerprintScanState.Faulted, Frame: null } && device.Closes == 1, "Invalid frame accepted or device closed twice");
                await session.StopAsync(); check(device.Closes == 1, "Disposed a failed device twice");
            }
        });
        add("Fingerprint retry after a failure starts a fresh device and clears old errors", async () =>
        {
            var first = new FakeDevice(); var second = new FakeDevice();
            var devices = new Queue<FakeDevice>([first, second]);
            var scanner = new FakeScanner(() => devices.Dequeue()); var session = new FingerprintScanSession(scanner);
            session.Start(); await first.WaitForRead(); first.Throw(new IOException()); await session.Completion.WaitAsync(Timeout);
            check(session.Start(), "Retry rejected"); await second.WaitForRead();
            check(session.Snapshot is { State: FingerprintScanState.Scanning, Frame: null, ErrorMessage: null } && scanner.Opens == 2, "Retry retained previous state");
            var stopped = session.StopAsync(); second.Return(FingerprintReadResult.Transient); await stopped.WaitAsync(Timeout);
            check(first.Closes == 1 && second.Closes == 1, "Retry lifetime");
        });
    }

    // Test-only scanner: reads block until a test supplies a result, like an in-flight native call.
    private sealed class FakeScanner(Func<IFingerprintScanDevice> open) : IFingerprintScanner
    {
        public int Opens, OpenThread;
        public IFingerprintScanDevice Open() { Interlocked.Increment(ref Opens); OpenThread = Environment.CurrentManagedThreadId; return open(); }
    }
    private sealed class FakeDevice : IFingerprintScanDevice
    {
        private readonly BlockingCollection<Func<FingerprintReadResult>> reads = new();
        private readonly SemaphoreSlim entered = new(0);
        private int reading;
        public int Closes, ReadThread, CloseThread;
        public bool ClosedDuringRead, DisposeFails;
        public async Task WaitForRead() { if (!await entered.WaitAsync(Timeout)) throw new TimeoutException("Scanner read not entered"); }
        public void Return(FingerprintReadResult read) => reads.Add(() => read);
        public void Throw(Exception error) => reads.Add(() => throw error);
        public FingerprintReadResult ReadFrame()
        {
            ReadThread = Environment.CurrentManagedThreadId; Interlocked.Exchange(ref reading, 1); entered.Release();
            try { return reads.Take()(); }
            finally { Interlocked.Exchange(ref reading, 0); }
        }
        public void Dispose()
        {
            CloseThread = Environment.CurrentManagedThreadId;
            ClosedDuringRead |= Volatile.Read(ref reading) != 0; Interlocked.Increment(ref Closes);
            if (DisposeFails) throw new IOException("private native failure detail");
        }
    }
}
