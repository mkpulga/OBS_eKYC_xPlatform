namespace OBS.Core;

public enum ClientPlatform { Windows, Android, Web }
public enum CaptureKind { FacePhoto, Fingerprint, Signature }
public sealed record Capability(bool Available, string Reason);
public interface ICaptureAdapter
{
    CaptureKind Kind { get; }
    Capability Availability { get; }
    Task<byte[]> CaptureAsync(CancellationToken cancellationToken);
}
public sealed class BlockedCaptureAdapter(ClientPlatform platform, CaptureKind kind) : ICaptureAdapter
{
    public CaptureKind Kind => kind;
    public Capability Availability => new(false, platform == ClientPlatform.Web ? "Capture is excluded on web."
        : platform == ClientPlatform.Android && kind == CaptureKind.Fingerprint ? "Fingerprint capture is excluded on Android."
        : "Capture is blocked pending vendor SDK, format, quality rules, license, and device validation.");
    public Task<byte[]> CaptureAsync(CancellationToken cancellationToken) => throw new NotSupportedException(Availability.Reason);
}
