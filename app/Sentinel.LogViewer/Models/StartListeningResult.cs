namespace Sentinel.LogViewer.Models;

/// <summary>
/// Result of starting the UDP listener(s).
/// </summary>
public sealed class StartListeningResult
{
	/// <summary>
	/// Whether at least one listener was started successfully.
	/// </summary>
	public bool AnyStarted { get; }

	/// <summary>
	/// Aggregated error message for failed addresses; when port is in use, may include process name and PID.
	/// </summary>
	public string ErrorMessage { get; }

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="anyStarted">True if at least one listener started.</param>
	/// <param name="errorMessage">Error message for failures (e.g. address already in use, process name/PID).</param>
	public StartListeningResult(bool anyStarted, string errorMessage)
	{
		AnyStarted = anyStarted;
		ErrorMessage = errorMessage ?? string.Empty;
	}
}
