using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace FingerPrintClient.FP.Utilities;

internal class FPImageUtilities
{
    private struct BITMAPFILEHEADER
    {
        public ushort bfType;
        public int bfSize;
        public ushort bfReserved1;
        public ushort bfReserved2;
        public int bfOffBits;
    }

    private struct MASK
    {
        public byte redmask;
        public byte greenmask;
        public byte bluemask;
        public byte rgbReserved;
    }

    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    //public static Bitmap GetImage(byte[] buffer, int width, int height)
    //{
    //    Bitmap output = new Bitmap(width, height);
    //    Rectangle rect = new Rectangle(0, 0, width, height);
    //    BitmapData bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);
    //    IntPtr ptr = bmpData.Scan0;

    //    Marshal.Copy(buffer, 0, ptr, buffer.Length);
    //    output.UnlockBits(bmpData);

    //    return output;
    //}

    public static Image GetImage(byte[] byteArrayIn, int width, int height)
    {
        MemoryStream ms = new MemoryStream();
        GetBitmap(byteArrayIn, width, height, ref ms);
        Bitmap btm = new Bitmap(ms);
        //btm.Save("thumb1.bmp");
        return btm;
    }

    private static void GetBitmap(byte[] buffer, int nWidth, int nHeight, ref MemoryStream ms)
    {
        int ColorIndex = 0;
        ushort m_nBitCount = 8;
        int m_nColorTableEntries = 256;
        byte[] ResBuf = new byte[nWidth * nHeight * 2];

        try
        {
            BITMAPFILEHEADER BmpHeader = new BITMAPFILEHEADER();
            BITMAPINFOHEADER BmpInfoHeader = new BITMAPINFOHEADER();
            MASK[] ColorMask = new MASK[m_nColorTableEntries];

            int w = (((nWidth + 3) / 4) * 4);

            BmpInfoHeader.biSize = Marshal.SizeOf(BmpInfoHeader);
            BmpInfoHeader.biWidth = nWidth;
            BmpInfoHeader.biHeight = nHeight;
            BmpInfoHeader.biPlanes = 1;
            BmpInfoHeader.biBitCount = m_nBitCount;
            BmpInfoHeader.biCompression = 0;
            BmpInfoHeader.biSizeImage = 0;
            BmpInfoHeader.biXPelsPerMeter = 0;
            BmpInfoHeader.biYPelsPerMeter = 0;
            BmpInfoHeader.biClrUsed = m_nColorTableEntries;
            BmpInfoHeader.biClrImportant = m_nColorTableEntries;

            BmpHeader.bfType = 0x4D42;
            BmpHeader.bfOffBits = 14 + Marshal.SizeOf(BmpInfoHeader) + BmpInfoHeader.biClrUsed * 4;
            BmpHeader.bfSize = BmpHeader.bfOffBits + ((((w * BmpInfoHeader.biBitCount + 31) / 32) * 4) * BmpInfoHeader.biHeight);
            BmpHeader.bfReserved1 = 0;
            BmpHeader.bfReserved2 = 0;

            ms.Write(StructToBytes(BmpHeader, 14), 0, 14);
            ms.Write(StructToBytes(BmpInfoHeader, Marshal.SizeOf(BmpInfoHeader)), 0, Marshal.SizeOf(BmpInfoHeader));

            for (ColorIndex = 0; ColorIndex < m_nColorTableEntries; ColorIndex++)
            {
                ColorMask[ColorIndex].redmask = (byte)ColorIndex;
                ColorMask[ColorIndex].greenmask = (byte)ColorIndex;
                ColorMask[ColorIndex].bluemask = (byte)ColorIndex;
                ColorMask[ColorIndex].rgbReserved = 0;

                ms.Write(StructToBytes(ColorMask[ColorIndex], Marshal.SizeOf(ColorMask[ColorIndex])), 0, Marshal.SizeOf(ColorMask[ColorIndex]));
            }

            RotatePic(buffer, nWidth, nHeight, ref ResBuf);

            byte[] filter = null;
            if (w - nWidth > 0)
            {
                filter = new byte[w - nWidth];
            }
            for (int i = 0; i < nHeight; i++)
            {
                ms.Write(ResBuf, i * nWidth, nWidth);
                if (w - nWidth > 0)
                {
                    ms.Write(ResBuf, 0, w - nWidth);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static void RotatePic(byte[] BmpBuf, int width, int height, ref byte[] ResBuf)
    {
        int RowLoop = 0;
        int ColLoop = 0;
        int BmpBuflen = width * height;

        try
        {
            for (RowLoop = 0; RowLoop < BmpBuflen;)
            {
                for (ColLoop = 0; ColLoop < width; ColLoop++)
                {
                    ResBuf[RowLoop + ColLoop] = BmpBuf[BmpBuflen - RowLoop - width + ColLoop];
                }

                RowLoop += width;
            }
        }
        catch (Exception)
        {
            //ZKCE.SysException.ZKCELogger logger = new ZKCE.SysException.ZKCELogger(ex);
            //logger.Append();
        }
    }

    private static byte[] StructToBytes(object StructObj, int Size)
    {
        int StructSize = Marshal.SizeOf(StructObj);
        byte[] GetBytes = new byte[StructSize];

        try
        {
            IntPtr StructPtr = Marshal.AllocHGlobal(StructSize);
            Marshal.StructureToPtr(StructObj, StructPtr, false);
            Marshal.Copy(StructPtr, GetBytes, 0, StructSize);
            Marshal.FreeHGlobal(StructPtr);

            if (Size == 14)
            {
                byte[] NewBytes = new byte[Size];
                int Count = 0;
                int Loop = 0;

                for (Loop = 0; Loop < StructSize; Loop++)
                {
                    if (Loop != 2 && Loop != 3)
                    {
                        NewBytes[Count] = GetBytes[Loop];
                        Count++;
                    }
                }

                return NewBytes;
            }
            else
            {
                return GetBytes;
            }
        }
        catch (Exception)
        {
            //ZKCE.SysException.ZKCELogger logger = new ZKCE.SysException.ZKCELogger(ex);
            //logger.Append();

            return GetBytes;
        }
    }
}
