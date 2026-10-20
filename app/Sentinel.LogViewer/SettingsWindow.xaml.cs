using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sentinel.LogViewer.Helpers;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.Dialogs = new WpfSettingsDialogs(this);
            InitializeComponent();
            DataContext = _viewModel;
        }

        /// <summary>
        /// Whether any setting was changed while the window was open
        /// </summary>
        public bool HasChanges => _viewModel.HasChanges;

        /// <summary>
        /// Opens the page of a category, e.g. <see cref="SettingsViewModel.FilterPresetsId"/>
        /// </summary>
        internal void ShowCategory(string categoryId) =>
            _viewModel.SelectedCategory = _viewModel.Categories.FirstOrDefault(category => category.Id == categoryId) ?? _viewModel.SelectedCategory;

        public bool? ShowDialog(Window owner)
        {
            Owner = owner;
            return ShowDialog();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && e.OriginalSource is TextBox { IsReadOnly: false } textBox)
            {
                // A text box saves when it loses focus; Enter saves right away.
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Controls that use Escape themselves (an open drop-down) have handled it by now.
            if (e.Key != Key.Escape || e.Handled)
                return;

            // The first Escape clears the search, the next one closes the window.
            if (_viewModel.IsSearching)
                _viewModel.SearchText = string.Empty;
            else
                Close();

            e.Handled = true;
        }
    }
}
