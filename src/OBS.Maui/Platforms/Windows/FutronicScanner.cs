using System.Runtime.InteropServices;
using OBS.Core;

namespace OBS.Maui.Platforms.Windows;

public sealed class FutronicScanner : IFingerprintScanner
{
    // Vendor-authored ftrScanAPI.h (2003–2013), signatures, packing and error definitions:
    // https://github.com/MuhammdAli/Futuronic_C_Sharp_wrapper/blob/master/ftrScanAPI.h
    private const int EmptyFrame = 4306, MovableFinger = 0x20000001, NoFrame = 0x20000002;

    public IFingerprintScanDevice Open()
    {
        if (!Environment.Is64BitProcess) throw new BadImageFormatException();
        var handle = Native.ftrScanOpenDevice();
        if (handle == IntPtr.Zero)
        {
            _ = Marshal.GetLastWin32Error();
            throw new FingerprintScannerException(FingerprintScannerMessages.NotDetected);
        }
        try
        {
            if (!Native.ftrScanGetImageSize(handle, out var size))
            {
                _ = Marshal.GetLastWin32Error();
                throw new FingerprintScannerException(FingerprintScannerMessages.Disconnected);
            }
            if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height != size.ImageSize)
                throw new FingerprintScannerException(FingerprintScannerMessages.Disconnected);
            return new Device(handle, size);
        }
        catch { Native.ftrScanCloseDevice(handle); throw; }
    }

    private sealed class Device(IntPtr handle, ScanImageSize size) : IFingerprintScanDevice
    {
        private IntPtr device = handle;
        public FingerprintReadResult ReadFrame()
        {
            ObjectDisposedException.ThrowIf(device == IntPtr.Zero, this);
            var pixels = new byte[size.ImageSize];
            // No finger-present test, quality processing, image inversion or capture persistence.
            if (!Native.ftrScanGetFrame(device, pixels, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                if (error is EmptyFrame or MovableFinger or NoFrame) return FingerprintReadResult.Transient;
                throw new FingerprintScannerException(FingerprintScannerMessages.Disconnected);
            }
            return new(new FingerprintFrame(size.Width, size.Height, pixels));
        }
        public void Dispose()
        {
            if (device == IntPtr.Zero) return;
            var handle = device; device = IntPtr.Zero; Native.ftrScanCloseDevice(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScanImageSize { public int Width, Height, ImageSize; }

    private static class Native
    {
        // FTRHANDLE = void*, FTR_BOOL = int, FTR_API = __stdcall on Windows.
        [DllImport("ftrScanAPI.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr ftrScanOpenDevice();
        [DllImport("ftrScanAPI.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ftrScanGetImageSize(IntPtr handle, out ScanImageSize size);
        [DllImport("ftrScanAPI.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ftrScanGetFrame(IntPtr handle, [Out] byte[] pixels, IntPtr frameParameters);
        [DllImport("ftrScanAPI.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
        internal static extern void ftrScanCloseDevice(IntPtr handle);
    }
}
