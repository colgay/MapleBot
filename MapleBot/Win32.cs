using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public struct Rect
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}

public class Win32
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string strClassName, string strWindowName);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    public static Bitmap ConvertBitmap(Bitmap src, PixelFormat format, bool disposeSrc = true)
    {
        Bitmap clone = new Bitmap(src.Width, src.Height, format);

        using (Graphics gr = Graphics.FromImage(clone))
        {
            gr.DrawImage(src, new Rectangle(0, 0, clone.Width, clone.Height));
        }

        src.Dispose();
        return clone;
    }

    public static Bitmap CopyFromScreen(int x, int y, int width, int height, PixelFormat format)
    {
        Bitmap bmp = new Bitmap(width, height, format);

        using (Graphics gr = Graphics.FromImage(bmp))
        {
            gr.CopyFromScreen(x, y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        }

        return bmp;
    }

    public static bool FindImage(Bitmap src, Bitmap bmp, int posX, int posY, int width, int height, ref Point point)
    {
        posX = Math.Min(Math.Max(posX, 0), src.Width);
        posY = Math.Min(Math.Max(posY, 0), src.Height);

        int maxWidth = (posX + width) - bmp.Width;
        int maxHeight = (posY + height) - bmp.Height;

        if (posX + bmp.Width >= maxWidth || posY + bmp.Height >= maxHeight)
            return false;

        FastBitmap srcData = new FastBitmap(src);
        FastBitmap bmpData = new FastBitmap(bmp);

        srcData.Lock();
        bmpData.Lock();

        for (int y = posY; y < maxHeight; y++)
        {
            for (int x = posX; x < maxWidth; x++)
            {
                if (CompareImage(srcData, bmpData, x, y))
                {
                    srcData.Unlock();
                    bmpData.Unlock();

                    point.X = x;
                    point.Y = y;

                    return true;
                }
            }
        }

        srcData.Unlock();
        bmpData.Unlock();

        return false;
    }

    public static bool FindImageColor(Bitmap src, Bitmap bmp, Color color, int posX, int posY, int width, int height, ref Point point)
    {
        posX = Math.Min(Math.Max(posX, 0), src.Width);
        posY = Math.Min(Math.Max(posY, 0), src.Height);

        int maxWidth = (posX + width) - bmp.Width;
        int maxHeight = (posY + height) - bmp.Height;

        if (posX + bmp.Width >= maxWidth || posY + bmp.Height >= maxHeight)
            return false;

        maxWidth += posX;
        maxHeight += posY;

        FastBitmap srcData = new FastBitmap(src);
        FastBitmap bmpData = new FastBitmap(bmp);

        srcData.Lock();
        bmpData.Lock();

        for (int y = posY; y < maxHeight; y++)
        {
            for (int x = posX; x < maxWidth; x++)
            {
                if (CompareImageColor(srcData, bmpData, color, x, y))
                {
                    srcData.Unlock();
                    bmpData.Unlock();

                    point.X = x;
                    point.Y = y;

                    return true;
                }
            }
        }

        srcData.Unlock();
        bmpData.Unlock();

        return false;
    }

    private static bool CompareImage(FastBitmap srcData, FastBitmap bmpData, int posX, int posY)
    {
        int width = bmpData.Width;
        int height = bmpData.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color colorSrc = srcData.GetPixel(x + posX, y + posY);
                Color colorBmp = bmpData.GetPixel(x, y);

                if (colorSrc != colorBmp)
                    return false;
            }
        }

        return true;
    }

    private static bool CompareImageColor(FastBitmap srcData, FastBitmap bmpData, Color color, int posX, int posY)
    {
        Color colorBlack = Color.FromArgb(255, 0, 0, 0);

        int width = bmpData.Width;
        int height = bmpData.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color colorSrc = srcData.GetPixel(x + posX, y + posY);
                Color colorBmp = bmpData.GetPixel(x, y);

                if (colorBmp == colorBlack)
                {
                    if (colorSrc == color)
                        return false;
                }
                else if (colorSrc != colorBmp)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
