using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace FluxAnswer.SystemTray
{
    internal static class BrandingAssets
    {
        private const string BrandOwner = "CYLON NET EIRL";

        public static string OwnerName => BrandOwner;

        public static Icon GetApplicationIcon()
        {
            foreach (var logoPath in GetCandidateLogoPaths())
            {
                if (!File.Exists(logoPath))
                {
                    continue;
                }

                try
                {
                    using var stream = File.OpenRead(logoPath);
                    using var image = Image.FromStream(stream);
                    using var bitmap = CreateTransparentBrandBitmap(image, 64, 64);
                    var hIcon = bitmap.GetHicon();

                    try
                    {
                        using var icon = Icon.FromHandle(hIcon);
                        return (Icon)icon.Clone();
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
                catch
                {
                    // Try the next path.
                }
            }

            return SystemIcons.Application;
        }

        public static Image? GetLogoImage(int width, int height)
        {
            foreach (var logoPath in GetCandidateLogoPaths())
            {
                if (!File.Exists(logoPath))
                {
                    continue;
                }

                try
                {
                    using var stream = File.OpenRead(logoPath);
                    using var image = Image.FromStream(stream);
                    return CreateTransparentBrandBitmap(image, width, height);
                }
                catch
                {
                    // Try the next path.
                }
            }

            return null;
        }

        private static string[] GetCandidateLogoPaths()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TikTokManager",
                "branding",
                "logo.png");

            var localAppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TikTokManager",
                "branding",
                "logo.png");

            var exePath = Path.Combine(AppContext.BaseDirectory, "branding", "logo.png");

            return new[] { appDataPath, localAppDataPath, exePath };
        }

        private static Bitmap CreateTransparentBrandBitmap(Image image, int width, int height)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, width, height);
            }

            RemoveBackgroundConnectedToBorder(bitmap);
            return bitmap;
        }

        private static void RemoveBackgroundConnectedToBorder(Bitmap bitmap)
        {
            if (bitmap.Width == 0 || bitmap.Height == 0)
            {
                return;
            }

            var visited = new bool[bitmap.Width, bitmap.Height];
            var queue = new Queue<Point>();

            EnqueueBorders(bitmap, visited, queue);

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                var color = bitmap.GetPixel(p.X, p.Y);
                if (!IsLikelyBackground(color))
                {
                    continue;
                }

                bitmap.SetPixel(p.X, p.Y, Color.FromArgb(0, color.R, color.G, color.B));

                EnqueueIfNeeded(p.X + 1, p.Y, bitmap, visited, queue);
                EnqueueIfNeeded(p.X - 1, p.Y, bitmap, visited, queue);
                EnqueueIfNeeded(p.X, p.Y + 1, bitmap, visited, queue);
                EnqueueIfNeeded(p.X, p.Y - 1, bitmap, visited, queue);
            }
        }

        private static void EnqueueBorders(Bitmap bitmap, bool[,] visited, Queue<Point> queue)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                EnqueueIfNeeded(x, 0, bitmap, visited, queue);
                EnqueueIfNeeded(x, bitmap.Height - 1, bitmap, visited, queue);
            }

            for (var y = 0; y < bitmap.Height; y++)
            {
                EnqueueIfNeeded(0, y, bitmap, visited, queue);
                EnqueueIfNeeded(bitmap.Width - 1, y, bitmap, visited, queue);
            }
        }

        private static void EnqueueIfNeeded(int x, int y, Bitmap bitmap, bool[,] visited, Queue<Point> queue)
        {
            if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
            {
                return;
            }

            if (visited[x, y])
            {
                return;
            }

            visited[x, y] = true;
            queue.Enqueue(new Point(x, y));
        }

        private static bool IsLikelyBackground(Color color)
        {
            if (color.A < 8)
            {
                return true;
            }

            var max = Math.Max(color.R, Math.Max(color.G, color.B));
            var min = Math.Min(color.R, Math.Min(color.G, color.B));
            var brightness = max;
            var saturation = max == 0 ? 0 : (max - min) * 255 / max;

            // Transparent checker backgrounds are usually light/neutral gray.
            return brightness >= 150 && saturation <= 40;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
