using System.Windows;
using System.Windows.Media;

namespace Sentinel.LogViewer.Helpers
{
    /// <summary>
    /// The colours of a tab, handed from the style of the tab item to its template: the stripe over the whole tab, the tint behind it and the
    /// stronger tint of the selected tab. A tab without a colour has none of them.
    /// </summary>
    public static class TabAppearance
    {
        public static readonly DependencyProperty StripeProperty =
            DependencyProperty.RegisterAttached("Stripe", typeof(Brush), typeof(TabAppearance), new PropertyMetadata(null));

        public static readonly DependencyProperty TintProperty =
            DependencyProperty.RegisterAttached("Tint", typeof(Brush), typeof(TabAppearance), new PropertyMetadata(null));

        public static readonly DependencyProperty TintSelectedProperty =
            DependencyProperty.RegisterAttached("TintSelected", typeof(Brush), typeof(TabAppearance), new PropertyMetadata(null));

        /// <summary>Whether the tab is tinted: its text is dark then, because the tint is light in every theme</summary>
        public static readonly DependencyProperty IsTintedProperty =
            DependencyProperty.RegisterAttached("IsTinted", typeof(bool), typeof(TabAppearance), new PropertyMetadata(false));

        public static bool GetIsTinted(DependencyObject element) => (bool)element.GetValue(IsTintedProperty);

        public static void SetIsTinted(DependencyObject element, bool value) => element.SetValue(IsTintedProperty, value);

        public static Brush? GetStripe(DependencyObject element) => (Brush?)element.GetValue(StripeProperty);

        public static void SetStripe(DependencyObject element, Brush? value) => element.SetValue(StripeProperty, value);

        public static Brush? GetTint(DependencyObject element) => (Brush?)element.GetValue(TintProperty);

        public static void SetTint(DependencyObject element, Brush? value) => element.SetValue(TintProperty, value);

        public static Brush? GetTintSelected(DependencyObject element) => (Brush?)element.GetValue(TintSelectedProperty);

        public static void SetTintSelected(DependencyObject element, Brush? value) => element.SetValue(TintSelectedProperty, value);
    }
}
