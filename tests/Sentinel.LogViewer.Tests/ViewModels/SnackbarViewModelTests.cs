using System;
using System.Threading;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.ViewModels;

/// <summary>
/// Tests for the card that tells the user something for a moment
/// </summary>
[Collection(WpfCollection.Name)]
public class SnackbarViewModelTests
{
	private readonly WpfStaContextFixture _context;

	public SnackbarViewModelTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static void Wait(TimeSpan time)
	{
		var end = DateTime.UtcNow + time;
		while (DateTime.UtcNow < end)
		{
			Thread.Sleep(20);
			WpfTestHelper.Settle();
		}
	}

	[Fact]
	public void AHint_Opens_AndGoesAwayByItself()
	{
		_context.RunOnSta(() =>
		{
			var snackbar = new SnackbarViewModel { Duration = TimeSpan.FromMilliseconds(100) };
			Assert.False(snackbar.IsOpen);

			snackbar.Show("Exported to a.log");

			Assert.True(snackbar.IsOpen);
			Assert.Equal(("Exported to a.log", SnackbarKind.Info, false), (snackbar.Message, snackbar.Kind, snackbar.IsError));
			Wait(TimeSpan.FromMilliseconds(500));
			Assert.False(snackbar.IsOpen);
		});
	}

	[Fact]
	public void AnError_Stays_UntilItIsClosed()
	{
		_context.RunOnSta(() =>
		{
			var snackbar = new SnackbarViewModel { Duration = TimeSpan.FromMilliseconds(50) };

			snackbar.ShowError("Port in use");
			Wait(TimeSpan.FromMilliseconds(300));

			Assert.True(snackbar.IsOpen);
			Assert.True(snackbar.IsError);

			snackbar.CloseCommand.Execute(null);
			Assert.False(snackbar.IsOpen);
		});
	}

	[Fact]
	public void ANewMessage_TakesThePlaceOfTheOldOne_AndStartsTheTimeAgain()
	{
		_context.RunOnSta(() =>
		{
			var snackbar = new SnackbarViewModel { Duration = TimeSpan.FromMilliseconds(400) };
			snackbar.Show("first");
			Wait(TimeSpan.FromMilliseconds(250));

			snackbar.Show("second");
			Wait(TimeSpan.FromMilliseconds(250));

			Assert.True(snackbar.IsOpen);
			Assert.Equal("second", snackbar.Message);
			snackbar.ShowError("third");
			Assert.Equal((SnackbarKind.Error, "third"), (snackbar.Kind, snackbar.Message));
			Wait(TimeSpan.FromMilliseconds(600));
			Assert.True(snackbar.IsOpen);
		});
	}

	[Fact]
	public void TheUndoButton_TakesBackWhatWasDone_AndClosesTheCard()
	{
		_context.RunOnSta(() =>
		{
			var snackbar = new SnackbarViewModel();
			var undone = 0;

			snackbar.Show("Preset removed", "Undo", () => undone++);
			Assert.True(snackbar.HasUndo);
			Assert.Equal("Undo", snackbar.UndoText);

			snackbar.UndoCommand.Execute(null);

			Assert.Equal(1, undone);
			Assert.False(snackbar.IsOpen);

			snackbar.Show("Hint");
			Assert.False(snackbar.HasUndo);
		});
	}
}
