using Accord;
using Accord.Imaging;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace LongTermTimeLapse.TimeLapseFrameProcessors
{
    public class TimeLapseProcessorByFREAK : ITimeLapseFrameProcessor
    {
        private readonly FastRetinaKeypointDetector keypointDetector = new FastRetinaKeypointDetector(50);

        public Task<MatrixH> Adjust(Bitmap baseBitmap, Size baseOriginalSize, Bitmap targetBitmap, Size targetOriginalSize)
        {
            return Task.Run(() =>
            {
                var basePoints = keypointDetector.Transform(baseBitmap).Select(p => new IntPoint((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
                var targetPoints = keypointDetector.Transform(targetBitmap).Select(p => new IntPoint((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();

                var matches = new CorrelationMatching(9, 50, baseBitmap, targetBitmap).Match(basePoints, targetPoints);

                var baseMatchPoints = matches[0].Select(p => new IntPoint(p.X * baseOriginalSize.Width / baseBitmap.Width , p.Y * baseOriginalSize.Height / baseBitmap.Height )).ToArray();
                var targetMatchPoints = matches[1].Select(p => new IntPoint(p.X * targetOriginalSize.Width / targetBitmap.Width , p.Y * targetOriginalSize.Height / targetBitmap.Height )).ToArray();

                var tasks = Enumerable.Range(0, 20).AsParallel().Select(a =>
                {
                    RansacHomographyEstimator ransac = new RansacHomographyEstimator(0.001, 0.99);
                    ransac.Ransac.MaxEvaluations = 1000;
                    ransac.Ransac.MaxSamplings = 100;

                    var homography = ransac.Estimate(baseMatchPoints, targetMatchPoints);

                    if (ransac.Inliers.Length > 0)
                    {
                        var directions = 
                            Accord.Math.Matrix.Get(baseMatchPoints, ransac.Inliers).Zip(
                            Accord.Math.Matrix.Get(targetMatchPoints, ransac.Inliers), 
                            (point1, point2) => Math.Atan2(point2.Y - point1.Y, point2.X - point1.X) + Math.PI * 2).ToArray();

                        var avgDirection = directions.Average();
                        var variance = directions.Average(d => Math.Pow(d - avgDirection, 2));

                        return new
                        {
                            homography,
                            ransac.Inliers.Length,
                            variance
                        };
                    }
                    else return null;
                }).Where(h => h != null).OrderByDescending(h => h.Length).ThenBy(h => h.variance).ToArray();

                return tasks.FirstOrDefault()?.homography;
            });
        }
    }
}


