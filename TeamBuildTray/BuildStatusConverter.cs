using System;
using System.Globalization;
using System.Windows.Data;
using Clyde.Rbi.TeamBuildTray.TeamBuildService;

namespace Clyde.Rbi.TeamBuildTray
{
    [ValueConversion(typeof (BuildStatus), typeof (Uri))]
    public class BuildStatusConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            var buildStatus = value as BuildStatus?;
            if (buildStatus.HasValue)
            {
                switch (buildStatus.Value)
                {
                    case BuildStatus.Succeeded:
                        return new Uri("pack://application:,,,/Green.ico", UriKind.RelativeOrAbsolute);
                    case BuildStatus.Failed:
                        return new Uri("pack://application:,,,/Red.ico", UriKind.RelativeOrAbsolute);
                    case BuildStatus.InProgress:
                        return new Uri("pack://application:,,,/Amber.ico", UriKind.RelativeOrAbsolute);
                    default:
                        return new Uri("pack://application:,,,/Grey.ico", UriKind.RelativeOrAbsolute);
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            // we don't intend this to ever be called
            return null;
        }

        #endregion
    }
}