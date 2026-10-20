using System.Buffers.Binary;

namespace OBS.Core;

public static class GrayscaleBitmap
{
    private const int PixelOffset = 14 + 40 + 256 * 4;
    internal static void Validate(FingerprintFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(frame.Pixels);
        if (frame.Width <= 0 || frame.Height <= 0 || (long)frame.Width * frame.Height != frame.Pixels.Length)
            throw new ArgumentException("A frame must contain one grayscale byte per pixel.", nameof(frame));
    }
    // Positive BMP height means bottom-up rows. Input values are copied without inversion or enhancement.
    public static byte[] Encode(FingerprintFrame frame)
    {
        Validate(frame);
        var stride = checked((frame.Width + 3) & ~3);
        var imageSize = checked(stride * frame.Height);
        var bitmap = new byte[checked(PixelOffset + imageSize)];
        var header = bitmap.AsSpan();
        bitmap[0] = (byte)'B'; bitmap[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(header[2..], bitmap.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[10..], PixelOffset);
        BinaryPrimitives.WriteInt32LittleEndian(header[14..], 40);
        BinaryPrimitives.WriteInt32LittleEndian(header[18..], frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(header[22..], frame.Height);
        BinaryPrimitives.WriteInt16LittleEndian(header[26..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[28..], 8);
        BinaryPrimitives.WriteInt32LittleEndian(header[34..], imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[46..], 256);
        for (var shade = 0; shade < 256; shade++)
        {
            var entry = 54 + shade * 4;
            bitmap[entry] = bitmap[entry + 1] = bitmap[entry + 2] = (byte)shade;
        }
        for (var row = 0; row < frame.Height; row++)
            frame.Pixels.AsSpan(row * frame.Width, frame.Width).CopyTo(header.Slice(PixelOffset + (frame.Height - 1 - row) * stride, frame.Width));
        return bitmap;
    }
}
