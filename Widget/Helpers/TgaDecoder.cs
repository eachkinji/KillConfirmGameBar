using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TestXboxGameBar.Helpers
{
    public static class TgaDecoder
    {
        public static async Task<SoftwareBitmap> GetSoftwareBitmapAsync(StorageFile tgaFile)
        {
            try
            {
                using (var stream = await tgaFile.OpenReadAsync())
                using (var reader = new BinaryReader(stream.AsStreamForRead()))
                {
                    byte idLength = reader.ReadByte();
                    byte colorMapType = reader.ReadByte();
                    byte imageType = reader.ReadByte(); 
                    reader.ReadBytes(9);
                    short width = reader.ReadInt16();
                    short height = reader.ReadInt16();
                    byte bpp = reader.ReadByte();
                    byte descriptor = reader.ReadByte();

                    if (imageType != 2 && imageType != 10) return null;
                    if (bpp != 24 && bpp != 32) return null;

                    if (idLength > 0) reader.ReadBytes(idLength);

                    int pixelCount = width * height;
                    byte[] pixelData = new byte[pixelCount * 4];

                    bool isRle = (imageType == 10);
                    bool isTopDown = (descriptor & 0x20) != 0;

                    if (isRle)
                    {
                        int currentPixel = 0;
                        while (currentPixel < pixelCount)
                        {
                            byte chunkHeader = reader.ReadByte();
                            int count = (chunkHeader & 0x7F) + 1;
                            if ((chunkHeader & 0x80) != 0)
                            {
                                byte b = reader.ReadByte();
                                byte g = reader.ReadByte();
                                byte r = reader.ReadByte();
                                byte a = (bpp == 32) ? reader.ReadByte() : (byte)255;
                                for (int i = 0; i < count && currentPixel < pixelCount; i++)
                                    SetPixel(pixelData, currentPixel++, b, g, r, a, width, height, isTopDown);
                            }
                            else
                            {
                                for (int i = 0; i < count && currentPixel < pixelCount; i++)
                                {
                                    byte b = reader.ReadByte();
                                    byte g = reader.ReadByte();
                                    byte r = reader.ReadByte();
                                    byte a = (bpp == 32) ? reader.ReadByte() : (byte)255;
                                    SetPixel(pixelData, currentPixel++, b, g, r, a, width, height, isTopDown);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < pixelCount; i++)
                        {
                            byte b = reader.ReadByte();
                            byte g = reader.ReadByte();
                            byte r = reader.ReadByte();
                            byte a = (bpp == 32) ? reader.ReadByte() : (byte)255;
                            SetPixel(pixelData, i, b, g, r, a, width, height, isTopDown);
                        }
                    }

                    SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
                    softwareBitmap.CopyFromBuffer(pixelData.AsBuffer());
                    return softwareBitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<StorageFile> ConvertTgaToPngAsync(StorageFile tgaFile, StorageFolder targetFolder, string targetName)
        {
            try
            {
                using (var stream = await tgaFile.OpenReadAsync())
                using (var reader = new BinaryReader(stream.AsStreamForRead()))
                {
                    // TGA Header (18 bytes)
                    byte idLength = reader.ReadByte();
                    byte colorMapType = reader.ReadByte();
                    byte imageType = reader.ReadByte(); // 2 = Uncompressed RGB, 10 = RLE RGB
                    reader.ReadBytes(9); // Skip color map info and origin
                    short width = reader.ReadInt16();
                    short height = reader.ReadInt16();
                    byte bpp = reader.ReadByte();
                    byte descriptor = reader.ReadByte();

                    if (imageType != 2 && imageType != 10)
                        throw new NotSupportedException("Only uncompressed or RLE TrueColor TGA is supported.");

                    if (bpp != 24 && bpp != 32)
                        throw new NotSupportedException("Only 24-bit or 32-bit TGA is supported.");

                    if (idLength > 0) reader.ReadBytes(idLength);

                    int pixelCount = width * height;
                    byte[] pixelData = new byte[pixelCount * 4]; // We'll output BGRA8

                    bool isRle = (imageType == 10);
                    bool isTopDown = (descriptor & 0x20) != 0;

                    if (isRle)
                    {
                        int currentPixel = 0;
                        while (currentPixel < pixelCount)
                        {
                            byte chunkHeader = reader.ReadByte();
                            int count = (chunkHeader & 0x7F) + 1;
                            if ((chunkHeader & 0x80) != 0) // RLE packet
                            {
                                byte b = reader.ReadByte();
                                byte g = reader.ReadByte();
                                byte r = reader.ReadByte();
                                byte a = (bpp == 32) ? reader.ReadByte() : (byte)255;

                                for (int i = 0; i < count && currentPixel < pixelCount; i++)
                                {
                                    SetPixel(pixelData, currentPixel++, b, g, r, a, width, height, isTopDown);
                                }
                            }
                            else // Raw packet
                            {
                                for (int i = 0; i < count && currentPixel < pixelCount; i++)
                                {
                                    byte b = reader.ReadByte();
                                    byte g = reader.ReadByte();
                                    byte r = reader.ReadByte();
                                    byte a = (bpp == 32) ? reader.ReadByte() : (byte)255;
                                    SetPixel(pixelData, currentPixel++, b, g, r, a, width, height, isTopDown);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < pixelCount; i++)
                        {
                            byte b = reader.ReadByte();
                            byte g = reader.ReadByte();
                            byte r = reader.ReadByte();
                            byte a = (bpp == 32) ? reader.ReadByte() : (byte)255;
                            SetPixel(pixelData, i, b, g, r, a, width, height, isTopDown);
                        }
                    }

                    // Create PNG
                    StorageFile pngFile = await targetFolder.CreateFileAsync(targetName, CreationCollisionOption.ReplaceExisting);
                    using (var outStream = await pngFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
                        encoder.SetPixelData(
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Premultiplied,
                            (uint)width,
                            (uint)height,
                            96, 96,
                            pixelData);
                        await encoder.FlushAsync();
                    }

                    return pngFile;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TGA Conversion failed: {ex.Message}");
                return null;
            }
        }

        private static void SetPixel(byte[] data, int index, byte b, byte g, byte r, byte a, int width, int height, bool isTopDown)
        {
            int x = index % width;
            int y = index / width;

            int targetY = isTopDown ? y : (height - 1 - y);
            int targetIndex = (targetY * width + x) * 4;

            data[targetIndex] = b;
            data[targetIndex + 1] = g;
            data[targetIndex + 2] = r;
            data[targetIndex + 3] = a;
        }
    }
}
