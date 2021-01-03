using System;
using System.Globalization;
using System.Windows.Data;

namespace LongTermTimeLapse.Converters
{
    class StrokeThicknessInViewBoxConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 4)
            {
                double viewboxWidth = values[0].To<double>();
                double viewboxHeight = values[1].To<double>();
                double viewportWidth = values[2].To<double>();
                double viewportHeight = values[3].To<double>();

                if (viewboxWidth > 0 && viewboxHeight > 0 && viewportWidth > 0 && viewportHeight > 0)
                {
                    if (viewboxWidth / viewboxHeight > viewportWidth / viewportHeight)
                    {
                        return viewportHeight / viewboxHeight;
                    }
                    else
                    {
                        return viewportWidth / viewboxWidth;
                    }
                }
            }

            return 0d;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
