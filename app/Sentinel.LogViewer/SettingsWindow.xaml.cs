using System;
using System.Windows;
using Sentinel.LogViewer.Wpf;
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
            InitializeComponent();
            DataContext = _viewModel;

            _viewModel.CancelCommand = new RelayCommand(() => DialogResult = false);
            _viewModel.SaveCommand = new RelayCommand(() =>
            {
                _viewModel.Save();
                DialogResult = true;
                Close();
            });
        }

        public bool? ShowDialog(Window owner)
        {
            Owner = owner;
            return ShowDialog();
        }
    }
}

