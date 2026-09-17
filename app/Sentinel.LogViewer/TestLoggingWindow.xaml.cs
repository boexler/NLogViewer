using System.Windows;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer;

/// <summary>
/// Debug-only window for configuring and controlling the test log generator.
/// </summary>
public partial class TestLoggingWindow : Window
{
    public TestLoggingWindow(TestLoggingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }
}
