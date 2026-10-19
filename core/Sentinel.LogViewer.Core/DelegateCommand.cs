using System.Windows.Input;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// A command that runs an action. It needs no user interface framework: it has no <c>CanExecute</c> that changes, so it never raises
/// <see cref="CanExecuteChanged"/>, which makes it fit for commands that are always available.
/// </summary>
public sealed class DelegateCommand : ICommand
{
	private readonly Action _execute;

	public DelegateCommand(Action execute)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
	}

	public event EventHandler? CanExecuteChanged
	{
		add { }
		remove { }
	}

	public bool CanExecute(object? parameter) => true;

	public void Execute(object? parameter) => _execute();
}

/// <summary>
/// A command with a parameter of type <typeparamref name="T"/> that runs an action; see <see cref="DelegateCommand"/>
/// </summary>
public sealed class DelegateCommand<T> : ICommand
{
	private readonly Action<T?> _execute;

	public DelegateCommand(Action<T?> execute)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
	}

	public event EventHandler? CanExecuteChanged
	{
		add { }
		remove { }
	}

	public bool CanExecute(object? parameter) => true;

	public void Execute(object? parameter) => _execute(parameter is T value ? value : default);
}
