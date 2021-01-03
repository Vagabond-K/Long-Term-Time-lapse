using Accord.Imaging;
using Accord.Imaging.Filters;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LongTermTimeLapse
{
    public class TimeLapseFrame : NotifyPropertyChangeObject
    {
        internal TimeLapseFrame(TimeLapse timeLapse, string filePath)
        {
            TimeLapse = timeLapse;
            OriginalFilePath = filePath;
            IsBusy = true;
        }

        private Guid id = Guid.NewGuid();
        public string OriginalFilePath { get; }
        public string AdjustedFilePath { get => Get<string>(); private set => Set(value); }
        public string PreProcessedFilePath { get => Get<string>(); private set => Set(value); }

        public string ResizedOriginalFilePath { get => Get<string>(); private set => Set(value); }
        public string ResizedAdjustedFilePath { get => Get<string>(); private set => Set(value); }

        public TimeLapse TimeLapse { get; }

        public bool IsBusy { get => Get(false); private set => Set(value); }
        public bool IsError { get => Get(false); private set => Set(value); }

        public Rect Rect { get => Get(Rect.Empty); private set => Set(value); }
        public System.Windows.Point[] Corners { get => Get<System.Windows.Point[]>(); private set => Set(value); }
        public System.Drawing.Size OriginalSize { get => Get<System.Drawing.Size>(); private set => Set(value); }

        public BitmapImage ThumbnailImage { get => Get<BitmapImage>(); private set => Set(value); }
        public DateTime? PictureDateTime { get => Get<DateTime?>(); private set => Set(value); }

        private BitmapImage image = null;
        public ImageSource Image
        {
            get
            {
                image?.StreamSource?.Dispose();

                var filePath = ResizedAdjustedFilePath ?? ResizedOriginalFilePath;
                if (filePath != null)
                {
                    return Get(() =>
                    {
                        var memoryStream = new MemoryStream();
                        using (var fileStream = File.OpenRead(filePath))
                        {
                            fileStream.CopyTo(memoryStream);
                            memoryStream.Position = 0;
                        }
                        image = new BitmapImage();
                        image.BeginInit();
                        image.StreamSource = memoryStream;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    });
                }
                return null;
            }
        }

        public void RefreshImage()
        {
            ClearProperty(nameof(Image));
            image?.StreamSource?.Dispose();
            image = null;
        }

        public Bitmap GetPreProcessedBitmap()
        {
            if (PreProcessedFilePath != null && File.Exists(PreProcessedFilePath))
            {
                return System.Drawing.Image.FromFile(PreProcessedFilePath) as Bitmap;
            }
            return null;
        }

        private MatrixH homography = null;


        public Task Init()
        {
            return Task.Run(() =>
            {
                using (Bitmap bitmap = System.Drawing.Image.FromFile(OriginalFilePath) as Bitmap)
                using (var thumbnail = new ResizeBilinear((int)(80d / bitmap.Height * bitmap.Width), 80).Apply(bitmap))
                {
                    OriginalSize = bitmap.Size;

                    var dateTimeProperty = bitmap.PropertyItems.Where(p => p.Id == 0x9003 || p.Id == 0x132).OrderByDescending(p => p.Id).FirstOrDefault();
                    if (dateTimeProperty != null && DateTime.TryParseExact(
                        Encoding.UTF8.GetString(dateTimeProperty.Value).Replace(":", "").Replace(" ", "").Replace("\0", ""),
                        "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var dateTime))
                    {
                        PictureDateTime = dateTime;
                    }

                    if (!Directory.Exists(TimeLapse.ResizedOriginalFilePath))
                        Directory.CreateDirectory(TimeLapse.ResizedOriginalFilePath);

                    string resizedOriginalFilePath = Path.Combine(TimeLapse.ResizedOriginalFilePath, id.ToString() + ".jpg");

                    if (!Directory.Exists(TimeLapse.PreProcessedFilePath))
                        Directory.CreateDirectory(TimeLapse.PreProcessedFilePath);

                    string preProcessedFilePath = Path.Combine(TimeLapse.PreProcessedFilePath, id.ToString() + ".jpg");

                    if (bitmap.Height > 1080)
                    {
                        using (var resized = new ResizeBilinear((int)(1080d / bitmap.Height * bitmap.Width), 1080).Apply(bitmap))
                        {
                            using (var stream = File.Open(resizedOriginalFilePath, FileMode.Create, FileAccess.Write))
                            {
                                resized.Save(stream, ImageFormat.Jpeg);
                                stream.Flush();
                            }
                            using (var preProcessed = new ResizeBicubic((int)(720d / bitmap.Height * bitmap.Width), 720).Apply(bitmap))
                            using (var contrast = new ContrastStretch().Apply(preProcessed))
                            using (var stream = File.Open(preProcessedFilePath, FileMode.Create, FileAccess.Write))
                            {
                                contrast.Save(stream, ImageFormat.Jpeg);
                                stream.Flush();
                            }
                        }
                    }
                    else
                    {
                        using (var stream = File.Open(resizedOriginalFilePath, FileMode.Create, FileAccess.Write))
                        {
                            bitmap.Save(stream, ImageFormat.Jpeg);
                            stream.Flush();
                        }
                        using (var contrast = new ContrastStretch().Apply(bitmap))
                        using (var stream = File.Open(preProcessedFilePath, FileMode.Create, FileAccess.Write))
                        {
                            contrast.Save(stream, ImageFormat.Jpeg);
                            stream.Flush();
                        }
                    }

                    Rect = new Rect(0, 0, bitmap.Width, bitmap.Height);
                    Corners = new System.Windows.Point[]
                    {
                                new System.Windows.Point(0, 0),
                                new System.Windows.Point(0, bitmap.Height),
                                new System.Windows.Point(bitmap.Width, bitmap.Height),
                                new System.Windows.Point(bitmap.Width, 0),
                    };

                    if (TimeLapse.Frames.IndexOf(this) == 0)
                        TimeLapse.TotalRect = Rect;

                    ResizedOriginalFilePath = resizedOriginalFilePath;
                    PreProcessedFilePath = preProcessedFilePath;

                    var memoryStream = new MemoryStream();
                    thumbnail.Save(memoryStream, ImageFormat.Jpeg);
                    memoryStream.Position = 0;

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var thumbnailImage = new BitmapImage
                        {
                            CreateOptions = BitmapCreateOptions.DelayCreation
                        };

                        thumbnailImage.BeginInit();
                        thumbnailImage.StreamSource = memoryStream;
                        thumbnailImage.CacheOption = BitmapCacheOption.OnLoad;
                        thumbnailImage.EndInit();
                        thumbnailImage.Freeze();
                        ThumbnailImage = thumbnailImage;
                    });
                }
                IsBusy = false;
            });
        }

        public async Task Adjust(Bitmap baseBitmap, System.Drawing.Size baseOriginalSize)
        {
            IsError = false;
            IsBusy = true;

            MatrixH homography = null;
            Bitmap blended = null;

            float baseWidth = baseOriginalSize.Width;
            float baseHeight = baseOriginalSize.Height;
            float targetWidth = 0;
            float targetHeight = 0;

            var totalRect = TimeLapse.TotalRect.IsEmpty ? new Rect(0, 0, baseWidth, baseHeight) : TimeLapse.TotalRect;

            using (var targetBitmap = GetPreProcessedBitmap())
            {
                targetWidth = OriginalSize.Width;
                targetHeight = OriginalSize.Height;

                homography = await TimeLapse.TimeLapseFrameProcessor.Adjust(baseBitmap, baseOriginalSize, targetBitmap, OriginalSize);
            }

            if (!Directory.Exists(TimeLapse.AdjustedFilePath))
                Directory.CreateDirectory(TimeLapse.AdjustedFilePath);

            string adjustedFilePath = Path.Combine(TimeLapse.AdjustedFilePath, id.ToString() + ".png");

            if (!Directory.Exists(TimeLapse.ResizedAdjustedFilePath))
                Directory.CreateDirectory(TimeLapse.ResizedAdjustedFilePath);

            string resizedAdjustedFilePath = Path.Combine(TimeLapse.ResizedAdjustedFilePath, id.ToString() + ".png");

            await Task.Run(() =>
            {
                using (var bitmap = System.Drawing.Image.FromFile(OriginalFilePath) as Bitmap)
                {
                    var blend = new Blend(homography, new Bitmap(baseOriginalSize.Width, baseOriginalSize.Height));
                    using (blended = blend.Apply(bitmap))
                    {
                        using (var stream = File.Open(adjustedFilePath, FileMode.Create, FileAccess.Write))
                        {
                            blended.Save(stream, ImageFormat.Png);
                            stream.Flush();
                        }
                        double ratio = 1080d / bitmap.Height;
                        using (var resized = new ResizeBilinear((int)(ratio * blended.Width),(int)(ratio * blended.Height)).Apply(blended))
                        {
                            using (var stream = File.Open(resizedAdjustedFilePath, FileMode.Create, FileAccess.Write))
                            {
                                resized.Save(stream, ImageFormat.Png);
                                stream.Flush();
                            }
                        }
                    }
                }
            });

            var corners = homography.Inverse().TransformPoints(new PointF[]
            {
                new PointF(0, 0),
                new PointF(0, targetHeight),
                new PointF(targetWidth, targetHeight),
                new PointF(targetWidth, 0),
            });

            if (corners[0].X >= corners[2].X || corners[0].X >= corners[3].X
                || corners[1].X >= corners[2].X || corners[1].X >= corners[3].X
                || corners[0].Y >= corners[1].Y || corners[0].Y >= corners[2].Y
                || corners[3].Y >= corners[1].Y || corners[3].Y >= corners[2].Y)
            {
                IsError = true;
                IsBusy = false;
                return;
            }

            float[] px = { corners[0].X, corners[1].X, corners[2].X, corners[3].X };
            float[] py = { corners[0].Y, corners[1].Y, corners[2].Y, corners[3].Y };

            float maxX = Accord.Math.Matrix.Max(px);
            float minX = Accord.Math.Matrix.Min(px);
            float newWidth = Math.Max(maxX, baseWidth) - Math.Min(0, minX);

            float maxY = Accord.Math.Matrix.Max(py);
            float minY = Accord.Math.Matrix.Min(py);
            float newHeight = Math.Max(maxY, baseHeight) - Math.Min(0, minY);

            int offsetX = 0, offsetY = 0;
            if (minX < 0) offsetX = (int)Math.Round(minX);
            if (minY < 0) offsetY = (int)Math.Round(minY);

            var rect = new Rect(offsetX, offsetY, (int)Math.Round(newWidth), (int)Math.Round(newHeight));

            this.homography = homography;

            AdjustedFilePath = adjustedFilePath;
            ResizedAdjustedFilePath = resizedAdjustedFilePath;

            Corners = corners.Select(p => new System.Windows.Point(p.X, p.Y)).ToArray();
            Rect = rect;

            totalRect.Union(rect);
            TimeLapse.TotalRect = totalRect;

            RefreshImage();

            IsBusy = false;
        }
    }
}
