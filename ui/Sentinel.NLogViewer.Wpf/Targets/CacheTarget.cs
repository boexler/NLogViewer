using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Sentinel.NLogViewer.Wpf.Helper;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Sentinel.NLogViewer.Wpf.Targets;

public interface ICacheTarget
{
	IObservable<LogEventInfo> Cache { get; }
}

[Target(nameof(CacheTarget))]
public class CacheTarget : Target, ICacheTarget
{
	/// <summary>
	/// Look in the NLog.config if any target is already defined and returns it, otherwise a new one is registered
	/// </summary>
	/// <param name="defaultMaxCount">The maximum entries which should be buffered. Is only used if no target is defined</param>
	/// <param name="targetName">The name of the target you want to link with</param>
	/// <returns></returns>
	public static CacheTarget GetInstance(int defaultMaxCount = 0, string? targetName = null)
	{
		if(LogManager.Configuration == null)
			LogManager.Configuration = new LoggingConfiguration();

		var predicate = PredicateBuilder.True<Target>().And(t => t is CacheTarget);
		if (!string.IsNullOrEmpty(targetName))
		{
			predicate = predicate.And(t => t.Name.Equals(targetName, StringComparison.CurrentCultureIgnoreCase) ||t.Name.Equals($"{targetName}_wrapped", StringComparison.CurrentCultureIgnoreCase));
		}
                
		var found = LogManager.Configuration.AllTargets.FirstOrDefault(predicate.Compile());
		if (found is not CacheTarget target)
		{
			target = new CacheTarget(defaultMaxCount) { Name = targetName ?? nameof(CacheTarget)};
			LogManager.Configuration.AddTarget(target.Name, target);
			LogManager.Configuration.LoggingRules.Insert(0, new LoggingRule("*", LogLevel.FromString("Trace"), target));
			LogManager.ReconfigExistingLoggers();
		}
		return target;
	}

	// ##############################################################################################################################
	// Properties
	// ##############################################################################################################################

	#region Properties

	// ##########################################################################################
	// Public Properties
	// ##########################################################################################

	public IObservable<LogEventInfo> Cache => _CacheSubject.AsObservable();
	private readonly ReplaySubject<LogEventInfo> _CacheSubject;

	// ##########################################################################################
	// Private Properties
	// ##########################################################################################

	#endregion

	// ##############################################################################################################################
	// Constructor
	// ##############################################################################################################################

	#region Constructor

	/// <summary>
	/// Initializes a new instance of the CacheTarget class with the specified maximum count
	/// </summary>
	/// <param name="maxCount">The maximum amount of entries held in buffer/cache. Defaults to 100 if not specified.</param>
	public CacheTarget(int maxCount = 500)
	{
		if(maxCount == 0)
			maxCount = 500;
		_CacheSubject = new ReplaySubject<LogEventInfo>(maxCount);
	}

	#endregion

	// ##############################################################################################################################
	// override
	// ##############################################################################################################################

	#region override

	protected override void Write(LogEventInfo logEvent)
	{
		_CacheSubject.OnNext(logEvent);
	}

	#endregion
}