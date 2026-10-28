using System;
using System.Windows;
using Xunit;
using ViewerControl = Sentinel.LogViewer.Wpf.LogViewer;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that the controls of the library get their template from the styles of the library
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerStyleTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerStyleTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	[Fact]
	public void TheLogViewerThatListensToNLog_GetsTheTemplateOfTheViewer()
	{
		_context.RunOnSta(() =>
		{
			var viewer = new ViewerControl { MaxCount = 100 };
			var window = WpfTestHelper.PrepareForTest(new Window { Width = 600, Height = 400, Content = viewer });
			window.Resources.MergedDictionaries.Add(new ResourceDictionary
			{
				Source = new Uri("pack://application:,,,/Sentinel.LogViewer.Wpf;component/Themes/Generic.xaml")
			});
			try
			{
				window.Show();
				WpfTestHelper.Settle();

				Assert.NotNull(viewer.Template);
				Assert.NotNull(viewer.Template.FindName("PART_ListView", viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}
}
