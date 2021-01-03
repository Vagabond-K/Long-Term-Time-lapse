using Accord.Imaging.Filters;
using Accord.Math;
using Accord.Video.FFMPEG;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LongTermTimeLapse.VideoExporters
{
    public class VideoExporterMP4 : NotifyPropertyChangeObject, IVideoExporter
    {
        public bool IsExporting { get => Get(false); private set => Set(value); }
        public double Processed { get => Get(0d); private set => Set(value); }

        public Task Export(TimeLapse timeLapse, string filePath)
        {
            return Task.Run(() =>
            {
                IsExporting = true;
                using (VideoFileWriter videoFileWriter = new VideoFileWriter())
                {
                    Rect validRect = timeLapse.ValidRect.IsEmpty ? timeLapse.Frames.FirstOrDefault().Rect: timeLapse.ValidRect;

                    int width = (int)Math.Floor(validRect.Width);
                    int height = (int)Math.Floor(validRect.Height);

                    var videoWidth = width;
                    var videoHeight = height;

                    if (videoHeight > 1080)
                    {
                        videoWidth = (int)(1080d / height * width);
                        videoHeight = 1080;
                        if (videoWidth % 2 == 1) videoWidth--;
                        if (videoHeight % 2 == 1) videoHeight--;
                    }

                    videoFileWriter.Open(filePath, videoWidth, videoHeight, new Rational(60), VideoCodec.H264, 40000000);

                    int fadeCount = 10;
                    int stopCount = 0;

                    var dateTimeFont = new Font("맑은 고딕", 24, System.Drawing.FontStyle.Bold);
                    var dateTimeBrush = new SolidBrush(Color.Yellow);
                    var dateTimeStringFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far };

                    var videoFrameCount = (timeLapse.Frames.Count) * (fadeCount + stopCount);
                    var videoFrameIndex = 0;

                    using (Bitmap buffer = new Bitmap(width, height))
                    using (var graphics = Graphics.FromImage(buffer))
                    {
                        Bitmap lastFrameBitmap = null;
                        Rect? lastFrameRect = null;

                        Bitmap nowFrameBitmap = null;

                        foreach (var frame in timeLapse.Frames.Concat(new[] { timeLapse.Frames.First() }))
                        {
                            using (var bitmap = Image.FromFile(frame.AdjustedFilePath ?? frame.OriginalFilePath) as Bitmap)
                            {
                                nowFrameBitmap = new Bitmap(width, height);
                                using (var frameGraphics = Graphics.FromImage(nowFrameBitmap))
                                {
                                    frameGraphics.DrawImage(bitmap,
                                        new Rectangle(0, 0, videoFileWriter.Width, videoFileWriter.Height),
                                        (int)(validRect.X - frame.Rect.X),
                                        (int)(validRect.Y - frame.Rect.Y), width, height, GraphicsUnit.Pixel);

                                    if (frame.PictureDateTime != null)
                                        frameGraphics.DrawString(frame.PictureDateTime.Value.ToString("yyyy-MM-dd HH:mm:ss"), 
                                            dateTimeFont, dateTimeBrush, videoFileWriter.Width - 20, videoFileWriter.Height - 20, dateTimeStringFormat);
                                }

                                ContrastStretch contrastStretch = new ContrastStretch();
                                contrastStretch.ApplyInPlace(nowFrameBitmap);
                            }

                            if (lastFrameBitmap == null)
                            {
                                graphics.DrawImage(nowFrameBitmap, 0, 0);
                            }
                            else
                            {
                                for (int i = 0; i < fadeCount; i++)
                                {
                                    graphics.DrawImage(lastFrameBitmap, 0, 0);

                                    if (i != 0)
                                    {
                                        var colorMatrix = new ColorMatrix
                                        {
                                            Matrix33 = (float)i / fadeCount
                                        };
                                        var imageAttributes = new ImageAttributes();
                                        imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                                        graphics.DrawImage(nowFrameBitmap,
                                            new Rectangle(0, 0, videoFileWriter.Width, videoFileWriter.Height), 
                                            0, 0, videoFileWriter.Width, videoFileWriter.Height, GraphicsUnit.Pixel, imageAttributes);
                                    }

                                    videoFileWriter.WriteVideoFrame(buffer);
                                    Processed = (double)++videoFrameIndex / videoFrameCount;
                                }
                            }

                            lastFrameBitmap?.Dispose();

                            lastFrameBitmap = nowFrameBitmap;
                            lastFrameRect = frame.Rect;

                            for (int i = 0; i < stopCount; i++)
                            {
                                videoFileWriter.WriteVideoFrame(buffer);
                                Processed = (double)++videoFrameIndex / videoFrameCount;
                            }
                        }

                        lastFrameBitmap?.Dispose();
                    }
                    videoFileWriter.Flush();
                    videoFileWriter.Close();
                }
                IsExporting = false;
            });
        }
    }
}
