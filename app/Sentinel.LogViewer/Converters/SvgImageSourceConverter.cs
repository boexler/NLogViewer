using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace Sentinel.LogViewer.Converters
{
    /// <summary>
    /// Converts SVG file paths (pack URIs) to ImageSource for WPF Image controls
    /// </summary>
    public class SvgImageSourceConverter : IValueConverter
    {
        private static readonly WpfDrawingSettings _drawingSettings = new WpfDrawingSettings();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string svgPath || string.IsNullOrEmpty(svgPath))
                return null;

            try
            {
                // Create a pack URI from the string path
                var uri = new Uri(svgPath, UriKind.RelativeOrAbsolute);
                
                // Load the resource stream from the pack URI
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo == null)
                    return null;

                // Use SharpVectors FileSvgReader to read and convert SVG from stream
                // Note: We need to read the stream into memory first as FileSvgReader may need to seek
                using (var memoryStream = new MemoryStream())
                {
                    streamInfo.Stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    
                    var reader = new FileSvgReader(_drawingSettings);
                    var drawing = reader.Read(memoryStream);
                    
                    if (drawing != null)
                    {
                        // Convert Drawing to ImageSource
                        var drawingImage = new DrawingImage(drawing);
                        drawingImage.Freeze();
                        return drawingImage;
                    }
                }
                
                return null;
            }
            catch
            {
                // Return null if conversion fails
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

