using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Reads the EXIF orientation tag from a JPEG (or TIFF) byte stream and applies the
/// corresponding geometric transform to a decoded Mat. Cameras tag portrait photos with
/// orientation 6/8; without applying it, phone photos open sideways.
/// </summary>
public static class ExifOrientationHelper
{
    /// <summary>
    /// Returns the EXIF orientation tag (1..8) embedded in the image bytes, or 1 ("no
    /// transform needed") when the file is not JPEG/TIFF or has no orientation tag.
    /// </summary>
    public static int ReadOrientation(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 8)
        {
            return 1;
        }

        // TIFF: the whole file is the TIFF structure ("II*\0" or "MM\0*").
        if ((bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 0x2A && bytes[3] == 0)
            || (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0 && bytes[3] == 0x2A))
        {
            return ReadOrientationFromTiff(bytes, 0);
        }

        // JPEG: scan the segment list for an APP1 "Exif\0\0" segment. The scan starts at
        // byte 0 so it also tolerates writers that emit an APP segment before the SOI marker.
        if (bytes[0] != 0xFF)
        {
            return 1;
        }

        int offset = 0;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            byte marker = bytes[offset + 1];
            // Standalone markers carry no length field.
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                offset += 2;
                continue;
            }
            // End of image / start of scan: no more metadata segments.
            if (marker == 0xD9 || marker == 0xDA)
            {
                return 1;
            }

            int segLen = (bytes[offset + 2] << 8) | bytes[offset + 3];
            if (segLen < 2 || offset + 2 + segLen > bytes.Length)
            {
                return 1;
            }

            if (marker == 0xE1 && segLen >= 8)
            {
                int payload = offset + 4;
                if (bytes[payload] == (byte)'E' && bytes[payload + 1] == (byte)'x'
                    && bytes[payload + 2] == (byte)'i' && bytes[payload + 3] == (byte)'f'
                    && bytes[payload + 4] == 0 && bytes[payload + 5] == 0)
                {
                    int orientation = ReadOrientationFromTiff(bytes, payload + 6);
                    if (orientation >= 1 && orientation <= 8)
                    {
                        return orientation;
                    }
                }
                return 1;
            }

            offset += 2 + segLen;
        }

        return 1;
    }

    /// <summary>
    /// Returns a new Mat with the EXIF orientation transform applied (identity for
    /// orientation 1). The caller owns the returned Mat.
    /// </summary>
    public static Mat ApplyOrientation(Mat mat, int orientation)
    {
        return orientation switch
        {
            2 => Flip(mat, FlipMode.Y),   // mirror left-right
            3 => Rotate(mat, RotateFlags.Rotate180),
            4 => Flip(mat, FlipMode.X),   // mirror top-bottom
            5 => Transpose(mat),          // reflect across the top-left/bottom-right diagonal
            6 => Rotate(mat, RotateFlags.Rotate90Clockwise),
            7 => Transverse(mat),         // reflect across the top-right/bottom-left diagonal
            8 => Rotate(mat, RotateFlags.Rotate90Counterclockwise),
            _ => mat.Clone()
        };
    }

    private static Mat Flip(Mat mat, FlipMode mode)
    {
        var result = new Mat();
        Cv2.Flip(mat, result, mode);
        return result;
    }

    private static Mat Rotate(Mat mat, RotateFlags flags)
    {
        var result = new Mat();
        Cv2.Rotate(mat, result, flags);
        return result;
    }

    private static Mat Transpose(Mat mat)
    {
        var result = new Mat();
        Cv2.Transpose(mat, result);
        return result;
    }

    private static Mat Transverse(Mat mat)
    {
        using var transposed = Transpose(mat);
        var result = new Mat();
        Cv2.Flip(transposed, result, FlipMode.Y);
        return result;
    }

    private static int ReadOrientationFromTiff(byte[] data, int start)
    {
        if (start + 8 > data.Length)
        {
            return 1;
        }

        bool littleEndian = data[start] == (byte)'I' && data[start + 1] == (byte)'I';
        bool bigEndian = data[start] == (byte)'M' && data[start + 1] == (byte)'M';
        if (!littleEndian && !bigEndian)
        {
            return 1;
        }

        if (U16(data, start + 2, littleEndian) != 42)
        {
            return 1;
        }

        long ifd0 = U32(data, start + 4, littleEndian);
        if (ifd0 < 8 || start + ifd0 + 2 > data.Length)
        {
            return 1;
        }

        int entryCount = U16(data, (int)(start + ifd0), littleEndian);
        int entry = (int)(start + ifd0 + 2);
        for (int i = 0; i < entryCount; i++)
        {
            if (entry + 12 > data.Length)
            {
                return 1;
            }

            int tag = U16(data, entry, littleEndian);
            int type = U16(data, entry + 2, littleEndian);
            long count = U32(data, entry + 4, littleEndian);
            if (tag == 0x0112 && type == 3 && count >= 1)
            {
                // SHORT values are stored inline in the 4-byte value field.
                return U16(data, entry + 8, littleEndian);
            }
            entry += 12;
        }

        return 1;
    }

    private static ushort U16(byte[] data, int offset, bool littleEndian)
    {
        return littleEndian
            ? (ushort)(data[offset] | (data[offset + 1] << 8))
            : (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint U32(byte[] data, int offset, bool littleEndian)
    {
        return littleEndian
            ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
            : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }
}
