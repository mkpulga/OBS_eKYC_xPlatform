namespace OBS.Core;

// Pixels are packed, top-to-bottom 8-bit grayscale. The reader transfers ownership of each frame.
public sealed record FingerprintFrame(int Width, int Height, byte[] Pixels)
{
    public override string ToString() => "[fingerprint frame]";
}

// A transient read has no frame. Fatal errors throw; they never become successful frames.
public readonly record struct FingerprintReadResult(FingerprintFrame? Frame)
{
    public bool IsTransient => Frame is null;
    public static FingerprintReadResult Transient => new(null);
}

public interface IFingerprintScanner
{
    IFingerprintScanDevice Open();
}
public interface IFingerprintScanDevice : IDisposable
{
    FingerprintReadResult ReadFrame();
}

public sealed class FingerprintScannerException(string message) : Exception(message);
public static class FingerprintScannerMessages
{
    public const string MissingLibrary = "Fingerprint scanner library (ftrScanAPI.dll) not found.";
    public const string WrongArchitecture = "ftrScanAPI.dll is not compatible with this app. A 64-bit version is required.";
    public const string NotDetected = "Scanner not detected. Check the USB connection.";
    public const string Disconnected = "Scanner disconnected or stopped responding.";
    public static string For(Exception error) => error switch
    {
        DllNotFoundException => MissingLibrary,
        BadImageFormatException => WrongArchitecture,
        FingerprintScannerException scanner => scanner.Message,
        _ => Disconnected // Do not surface native exception details or any image data.
    };
}

public enum FingerprintScanState { Idle, Scanning, Faulted }
public sealed record FingerprintScanSnapshot(FingerprintScanState State, bool IsRunning, FingerprintFrame? Frame, string? ErrorMessage)
{
    public override string ToString() => $"Fingerprint scan: {State}";
}

public sealed class FingerprintScanSession(IFingerprintScanner scanner)
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task completion = Task.CompletedTask;
    private FingerprintScanState state;
    private FingerprintFrame? latest;
    private string? errorMessage;
    private bool running;
    // Raised on the worker thread. Consumers must marshal to their UI and coalesce updates.
    public event Action? Changed;
    public FingerprintScanSnapshot Snapshot { get { lock (gate) return new(state, running, latest, errorMessage); } }
    public Task Completion { get { lock (gate) return completion; } }

    public bool Start(CancellationToken token = default)
    {
        lock (gate)
        {
            if (running) return false;
            var current = cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            running = true; state = FingerprintScanState.Scanning; latest = null; errorMessage = null;
            // All native calls, including Dispose, stay on this one background thread.
            completion = Task.Factory.StartNew(() => Run(current), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return true;
        }
    }
    public Task StopAsync()
    {
        lock (gate) { cancellation?.Cancel(); return completion; }
    }
    public void ClearFrame() { lock (gate) latest = null; }

    private void Run(CancellationTokenSource current)
    {
        IFingerprintScanDevice? device = null;
        string? failure = null;
        var token = current.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            device = scanner.Open();
            while (!token.IsCancellationRequested)
            {
                var read = device.ReadFrame();
                if (token.IsCancellationRequested) break;
                if (read.IsTransient) { if (token.WaitHandle.WaitOne(20)) break; continue; }
                var frame = read.Frame!;
                GrayscaleBitmap.Validate(frame);
                lock (gate)
                {
                    if (token.IsCancellationRequested) break;
                    latest = frame;
                }
                Changed?.Invoke();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!token.IsCancellationRequested) failure = FingerprintScannerMessages.For(error); }
        finally
        {
            // Cancellation never disposes a handle from underneath an in-flight ReadFrame.
            try { device?.Dispose(); }
            catch (Exception error) { if (!token.IsCancellationRequested) failure ??= FingerprintScannerMessages.For(error); }
            lock (gate)
            {
                errorMessage = failure; state = failure is null ? FingerprintScanState.Idle : FingerprintScanState.Faulted;
                running = false; cancellation = null; current.Dispose();
            }
            Changed?.Invoke();
        }
    }
}
