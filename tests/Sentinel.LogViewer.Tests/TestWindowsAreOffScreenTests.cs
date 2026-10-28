using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// The windows of the tests must not show on the screen of the user: a test that shows a window prepares it with
/// <see cref="WpfTestHelper.PrepareForTest{T}"/> first, which is the only place that says how
/// </summary>
public class TestWindowsAreOffScreenTests
{
	private static readonly string TestsFolder = Path.Combine(RepositoryRoot(), "tests", "Sentinel.LogViewer.Tests");

	private static string RepositoryRoot()
	{
		var folder = new DirectoryInfo(AppContext.BaseDirectory);
		while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Sentinel.LogViewer.CI.slnf")))
			folder = folder.Parent;

		return folder?.FullName ?? throw new InvalidOperationException("The folder of the repository was not found.");
	}

	private static System.Collections.Generic.IEnumerable<(string Name, string Text)> TestFiles() =>
		Directory.GetFiles(TestsFolder, "*.cs", SearchOption.AllDirectories)
			.Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
			.Select(file => (Path.GetFileName(file), File.ReadAllText(file)));

	[Fact]
	public void ATestThatShowsAWindow_PreparesItWithTheHelper()
	{
		var shows = new Regex(@"\b(window|dialog)\.Show(Dialog)?\(");

		var unprepared = TestFiles()
			.Where(file => file.Name != nameof(TestWindowsAreOffScreenTests) + ".cs" && shows.IsMatch(file.Text) && !file.Text.Contains("PrepareForTest"))
			.Select(file => file.Name);

		Assert.Empty(unprepared);
	}

	[Fact]
	public void OnlyTheHelper_SaysWhereTheWindowsOfTheTestsGo()
	{
		var copies = TestFiles()
			.Where(file => file.Name is not ("WpfTestHelper.cs" or nameof(TestWindowsAreOffScreenTests) + ".cs") && Regex.IsMatch(file.Text, @"Left = -10000|ShowInTaskbar = false"))
			.Select(file => file.Name);

		Assert.Empty(copies);
	}
}