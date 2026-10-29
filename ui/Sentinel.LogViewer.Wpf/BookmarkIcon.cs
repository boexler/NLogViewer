using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The picture of a bookmark: a ribbon that is flat at the top and has a notch at the bottom. The mark in the rows, the button of the list and
    /// the menus use this one shape, so a host can show the same picture in its own menus.
    /// </summary>
    public static class BookmarkIcon
    {
        /// <summary>The shape in a box of 24 by 24; it fills x from 5 to 19 and y from 2 to 22</summary>
        public static Geometry Ribbon { get; } = Create();

        /// <summary>The ribbon with two lines cut out of it; the mark of a bookmark that has a comment</summary>
        public static Geometry RibbonWithNote { get; } = Freeze(Geometry.Parse(
            "F0 M7,2 L17,2 C18.1,2 19,2.9 19,4 L19,22 L12,18 L5,22 L5,4 C5,2.9 5.9,2 7,2 Z M8.5,6 L15.5,6 L15.5,7.6 L8.5,7.6 Z M8.5,9.8 L15.5,9.8 L15.5,11.4 L8.5,11.4 Z"));

        /// <summary>The colour of the ribbon in the rows and the menus</summary>
        public static Brush Gold { get; } = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0xA8, 0x00)));

        /// <summary>
        /// The ribbon as an element for the icon of a menu entry
        /// </summary>
        public static Path ForMenu() => new()
        {
            Data = Ribbon,
            Fill = Gold,
            Stretch = Stretch.Uniform,
            Width = 10,
            Height = 14
        };

        private static Geometry Create() => Freeze(Geometry.Parse("M7,2 L17,2 C18.1,2 19,2.9 19,4 L19,22 L12,18 L5,22 L5,4 C5,2.9 5.9,2 7,2 Z"));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
