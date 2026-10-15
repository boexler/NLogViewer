using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for <see cref="MessageDialogWindow"/>: the buttons that are shown and the answer each way of closing gives
/// </summary>
[Collection(WpfCollection.Name)]
public class MessageDialogWindowTests
{
	private readonly WpfStaContextFixture _context;

	public MessageDialogWindowTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	[Theory]
	[InlineData(MessageBoxButton.OK, new[] { "OkButton" })]
	[InlineData(MessageBoxButton.OKCancel, new[] { "OkButton", "CancelButton" })]
	[InlineData(MessageBoxButton.YesNo, new[] { "YesButton", "NoButton" })]
	[InlineData(MessageBoxButton.YesNoCancel, new[] { "YesButton", "NoButton", "CancelButton" })]
	public void ShowsOnlyTheButtonsOfTheKind(MessageBoxButton buttons, string[] expected)
	{
		_context.RunOnSta(() =>
		{
			var window = new MessageDialogWindow("Message", "Caption", buttons, MessageBoxImage.Question);
			try
			{
				var visible = new[] { "YesButton", "NoButton", "OkButton", "CancelButton" }
					.Where(name => ((Button)window.FindName(name)).Visibility == Visibility.Visible);

				Assert.Equal(expected.OrderBy(name => name), visible.OrderBy(name => name));
				Assert.Equal("Caption", window.Title);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Theory]
	[InlineData(MessageBoxButton.OK, null, MessageBoxResult.OK)]
	[InlineData(MessageBoxButton.YesNo, null, MessageBoxResult.No)]
	[InlineData(MessageBoxButton.YesNoCancel, null, MessageBoxResult.Cancel)]
	[InlineData(MessageBoxButton.YesNo, "YesButton", MessageBoxResult.Yes)]
	[InlineData(MessageBoxButton.YesNo, "NoButton", MessageBoxResult.No)]
	[InlineData(MessageBoxButton.OKCancel, "OkButton", MessageBoxResult.OK)]
	[InlineData(MessageBoxButton.OKCancel, "CancelButton", MessageBoxResult.Cancel)]
	public void Result_IsTheButtonThatWasClicked_AndOtherwiseTheAnswerThatDoesNothing(MessageBoxButton buttons, string? click, MessageBoxResult expected)
	{
		_context.RunOnSta(() =>
		{
			var window = new MessageDialogWindow("Message", "Caption", buttons, MessageBoxImage.Warning);
			window.Left = -10000;
			window.Top = -10000;
			window.ShowActivated = false;
			window.Show();

			if (click != null)
				((Button)window.FindName(click)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
			else
				window.Close();

			Assert.Equal(expected, window.Result);
		});
	}

	[Fact]
	public void TheDefaultButtonIsTheAffirmativeOne_AndEscapeChoosesTheNegativeOne()
	{
		_context.RunOnSta(() =>
		{
			var window = new MessageDialogWindow("Message", "Caption", MessageBoxButton.YesNo, MessageBoxImage.Question);
			try
			{
				Assert.True(((Button)window.FindName("YesButton")).IsDefault);
				Assert.True(((Button)window.FindName("NoButton")).IsCancel);
			}
			finally
			{
				window.Close();
			}
		});
	}
}
