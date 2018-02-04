using System;
using System.Drawing;
using System.Drawing.Imaging;

public unsafe class FastBitmap
{
    private Bitmap _bitmap;
    private BitmapData _bitmapData;
    private byte* _scan0;

    public int Stride { get; private set; }
    public int Bytes { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public FastBitmap(Bitmap source)
    {
        _bitmap = source;
    }

    public void Lock()
    {
        _bitmapData = _bitmap.LockBits(new Rectangle(0, 0, _bitmap.Width, _bitmap.Height), 
            ImageLockMode.ReadWrite, _bitmap.PixelFormat);

        _scan0 = (byte*)_bitmapData.Scan0;

        Width = _bitmap.Width;
        Height = _bitmap.Height;

        Bytes = Bitmap.GetPixelFormatSize(_bitmap.PixelFormat) / 8;
        Stride = _bitmapData.Stride;
    }

    public void Unlock()
    {
        _bitmap.UnlockBits(_bitmapData);
    }

    public Color GetPixel(int x, int y)
    {
        byte* ptr = _scan0 + (y * Stride);
        int i = x * Bytes;

        byte b = ptr[i];
        byte g = ptr[i + 1];
        byte r = ptr[i + 2];
        byte a = ptr[i + 3];

        return Color.FromArgb(a, r, g, b);
    }
}