using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LongTermTimeLapse.Converters
{
    class FramePathDataConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2
                && values[0] is Rect rect)
            {
                if (values[1] is Point[] points)
                {
                    var path = new PathGeometry();
                    path.Figures.Add(new PathFigure(
                        new Point(points[0].X - rect.X, points[0].Y - rect.Y),
                        points.Skip(1).Select(point =>
                        new LineSegment(new Point(point.X - rect.X, point.Y - rect.Y), true)
                    ), true));
                    return path;
                }
                else
                {
                    var path = new PathGeometry();
                    path.Figures.Add(new PathFigure(
                        new Point(rect.Left, rect.Top),
                        new LineSegment[]
                        {
                            new LineSegment(new Point(rect.Left, rect.Bottom), true),
                            new LineSegment(new Point(rect.Right, rect.Bottom), true),
                            new LineSegment(new Point(rect.Right, rect.Top), true)
                        }
                    , true));
                    return path;
                }
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
