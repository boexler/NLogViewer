using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests.Models;

/// <summary>
/// Tests for <see cref="LoggerTree"/>: structure, counters, check states and search
/// </summary>
public class LoggerTreeTests
{
	private static LogEntry Entry(string logger, LogEntryLevel? level = null) => new(level ?? LogEntryLevel.Info, logger, "message");

	private static LoggerTree CreateTree(params (string Logger, LogEntryLevel Level)[] entries)
	{
		var tree = new LoggerTree();
		foreach (var (logger, level) in entries)
			tree.Add(Entry(logger, level));

		return tree;
	}

	private static LoggerNode Node(LoggerTree tree, string fullName)
	{
		var parts = fullName.Split('.');
		var node = tree.Roots.Single(root => root.Name == parts[0]);
		foreach (var part in parts.Skip(1))
			node = node.Children.Single(child => child.Name == part);

		return node;
	}

	[Fact]
	public void Loggers_AreSplitAtTheDots_AndSortedByName()
	{
		var tree = CreateTree(("Company.Product.Service", LogEntryLevel.Info), ("company.product.alpha", LogEntryLevel.Info),
			("Company.Beta", LogEntryLevel.Info), ("Other", LogEntryLevel.Info));

		Assert.Equal(new[] { "Company", "company", "Other" }, tree.Roots.Select(root => root.Name));
		var product = Node(tree, "Company.Product");
		Assert.Equal("Company.Product", product.FullName);
		Assert.Same(Node(tree, "Company"), product.Parent);
		Assert.Equal(new[] { "Service" }, product.Children.Select(child => child.Name));
		Assert.Equal(new[] { "Beta", "Product" }, Node(tree, "Company").Children.Select(child => child.Name));
	}

	[Fact]
	public void Counters_CountOwnEntriesAndEverythingBelow_WithWarningsAndErrorsSeparately()
	{
		var tree = CreateTree(
			("A", LogEntryLevel.Info),
			("A.B", LogEntryLevel.Warn), ("A.B", LogEntryLevel.Warn),
			("A.B.C", LogEntryLevel.Error), ("A.B.C", LogEntryLevel.Fatal), ("A.B.C", LogEntryLevel.Debug),
			("A.D", LogEntryLevel.Trace));

		var a = Node(tree, "A");
		var b = Node(tree, "A.B");
		var c = Node(tree, "A.B.C");
		Assert.Equal((7, 2, 2), (a.TotalCount, a.WarnCount, a.ErrorCount));
		Assert.Equal((5, 2, 2), (b.TotalCount, b.WarnCount, b.ErrorCount));
		Assert.Equal((3, 0, 2), (c.TotalCount, c.WarnCount, c.ErrorCount));
		Assert.Equal((1, 3), (a.OwnCount, c.OwnCount));
	}

	[Fact]
	public void Remove_TakesTheEntryOutOfTheCounters()
	{
		var tree = new LoggerTree();
		var warning = Entry("A.B", LogEntryLevel.Warn);
		tree.Add(warning);
		tree.Add(Entry("A.B", LogEntryLevel.Info));

		tree.Remove(warning);

		Assert.Equal((1, 0), (Node(tree, "A").TotalCount, Node(tree, "A").WarnCount));
		Assert.Equal(1, Node(tree, "A.B").OwnCount);
	}

	[Fact]
	public void Rebuild_ReplacesTheTreeWithTheLoggersOfTheEntries()
	{
		var tree = CreateTree(("Old.Logger", LogEntryLevel.Info));

		tree.Rebuild(new[] { Entry("New.Logger"), Entry("New.Logger"), Entry("New.Other") });

		Assert.Equal(new[] { "New" }, tree.Roots.Select(root => root.Name));
		Assert.Equal(3, Node(tree, "New").TotalCount);

		tree.Rebuild(new List<LogEntry>());
		Assert.Empty(tree.Roots);
	}

	[Fact]
	public void EntriesWithoutALoggerName_AreNotListed_AndAreShownUnlessEverythingIsHidden()
	{
		var tree = new LoggerTree();
		tree.Add(Entry(string.Empty));
		tree.Add(Entry(null!));

		Assert.Empty(tree.Roots);
		Assert.True(tree.IsVisible(null));

		tree.Rules.ShowOnly("A");
		Assert.False(tree.IsVisible(string.Empty));
	}

	[Fact]
	public void CounterChanges_AreAnnouncedWhenTheScheduledUpdateRuns()
	{
		var scheduled = new List<System.Action>();
		var tree = new LoggerTree(scheduled.Add);
		tree.Add(Entry("A.B"));
		var changed = new List<string?>();
		Node(tree, "A").PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		tree.Add(Entry("A.B"));
		tree.Add(Entry("A.B"));
		Assert.Single(scheduled);
		Assert.Empty(changed);

		scheduled[0]();

		Assert.Contains(nameof(LoggerNode.TotalCount), changed);
		Assert.Equal(3, Node(tree, "A").TotalCount);
	}

	[Fact]
	public void CheckState_IsTrueForAll_FalseForNone_AndNullForSome()
	{
		var tree = CreateTree(("Company.Product.Service", LogEntryLevel.Info), ("Company.Product.Other", LogEntryLevel.Info), ("Company.Elsewhere", LogEntryLevel.Info));
		var company = Node(tree, "Company");
		var product = Node(tree, "Company.Product");
		Assert.True(company.IsChecked);

		Node(tree, "Company.Product.Service").IsChecked = false;

		Assert.Null(product.IsChecked);
		Assert.Null(company.IsChecked);
		Assert.False(Node(tree, "Company.Product.Service").IsChecked);
		Assert.True(Node(tree, "Company.Product.Other").IsChecked);

		product.IsChecked = false;
		Assert.False(product.IsChecked);
		Assert.Null(company.IsChecked);

		company.IsChecked = true;
		Assert.True(company.IsChecked);
		Assert.False(tree.Rules.HasRules);
	}

	[Fact]
	public void UncheckingAParent_AlsoHidesLoggersThatAppearBelowItLater()
	{
		var tree = CreateTree(("Company.Product", LogEntryLevel.Info));

		Node(tree, "Company").IsChecked = false;
		tree.Add(Entry("Company.Product.New"));

		Assert.False(tree.IsVisible("Company.Product.New"));
		Assert.False(Node(tree, "Company.Product.New").IsChecked);
	}

	[Fact]
	public void ACheckStateChange_IsAnnouncedForAllLoggers_AndRulesChangedIsRaised()
	{
		var tree = CreateTree(("A.B", LogEntryLevel.Info));
		var announced = new List<string>();
		foreach (var name in new[] { "A", "A.B" })
		{
			var node = Node(tree, name);
			node.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(LoggerNode.IsChecked))
					announced.Add(node.FullName);
			};
		}

		var raised = 0;
		tree.RulesChanged += (_, _) => raised++;
		Node(tree, "A.B").IsChecked = false;

		Assert.Equal(new[] { "A", "A.B" }, announced.OrderBy(name => name));
		Assert.Equal(1, raised);
	}

	[Fact]
	public void ShowOnlyCommand_HidesAllOtherLoggers_AndShowAllCommandShowsThemAgain()
	{
		var tree = CreateTree(("A.B", LogEntryLevel.Info), ("A.C", LogEntryLevel.Info), ("D", LogEntryLevel.Info));

		tree.ShowOnlyCommand.Execute(Node(tree, "A.B"));

		Assert.True(tree.IsVisible("A.B"));
		Assert.False(tree.IsVisible("A.C"));
		Assert.False(tree.IsVisible("D"));
		Assert.Null(Node(tree, "A").IsChecked);

		tree.ShowAllCommand.Execute(null);

		Assert.True(tree.IsVisible("A.C"));
		Assert.True(tree.IsVisible("D"));
	}

	[Fact]
	public void ExpandAllAndCollapseAll_ChangeEveryLogger()
	{
		var tree = CreateTree(("A.B.C", LogEntryLevel.Info), ("D.E", LogEntryLevel.Info));
		var all = new[] { "A", "A.B", "A.B.C", "D", "D.E" }.Select(name => Node(tree, name)).ToList();
		Assert.All(all, node => Assert.True(node.IsExpanded));

		tree.CollapseAllCommand.Execute(null);
		Assert.All(all, node => Assert.False(node.IsExpanded));

		tree.ExpandAllCommand.Execute(null);
		Assert.All(all, node => Assert.True(node.IsExpanded));
	}

	[Fact]
	public void Search_ListsMatchingLoggersAndTheOnesAboveThem_AndOpensThem()
	{
		var tree = CreateTree(("Company.Product.Service", LogEntryLevel.Info), ("Company.Product.Other", LogEntryLevel.Info), ("Brand.Thing", LogEntryLevel.Info));
		tree.CollapseAllCommand.Execute(null);

		tree.SearchText = "serv";

		Assert.True(Node(tree, "Company").IsListed);
		Assert.True(Node(tree, "Company.Product").IsListed);
		Assert.True(Node(tree, "Company.Product.Service").IsListed);
		Assert.False(Node(tree, "Company.Product.Other").IsListed);
		Assert.False(Node(tree, "Brand").IsListed);
		Assert.True(Node(tree, "Company").IsExpanded);
		Assert.True(Node(tree, "Company.Product").IsExpanded);

		tree.SearchText = string.Empty;
		Assert.All(new[] { "Company", "Company.Product.Other", "Brand", "Brand.Thing" }, name => Assert.True(Node(tree, name).IsListed));
	}

	[Fact]
	public void Search_AlsoAppliesToLoggersThatAppearWhileSearching()
	{
		var tree = CreateTree(("Company.Product", LogEntryLevel.Info));
		tree.SearchText = "service";
		Assert.False(Node(tree, "Company").IsListed);

		tree.Add(Entry("Company.Product.Service"));
		tree.Add(Entry("Company.Product.Other"));

		Assert.True(Node(tree, "Company").IsListed);
		Assert.True(Node(tree, "Company.Product.Service").IsListed);
		Assert.False(Node(tree, "Company.Product.Other").IsListed);
	}
}
