using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sentinel.NLogViewer.App.Services;
using Xunit;

namespace Sentinel.NLogViewer.App.Tests.Services;

/// <summary>
/// Verifies single-instance ownership and command-line argument forwarding.
/// </summary>
public sealed class SingleInstanceServiceTests
{
    /// <summary>
    /// Ensures that a secondary coordinator forwards all arguments to the primary coordinator.
    /// </summary>
    [Fact]
    public async Task ForwardInvocationAsync_WithSecondaryInstance_ForwardsArguments()
    {
        var uniqueName = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\Boexler.Sentinel.NLogViewer.Tests.{uniqueName}";
        var pipeName = $"Boexler.Sentinel.NLogViewer.Tests.{uniqueName}";
        using var primary = new SingleInstanceService(mutexName, pipeName);
        using var secondary = new SingleInstanceService(mutexName, pipeName);
        var receivedInvocation = new TaskCompletionSource<IReadOnlyList<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        primary.InvocationReceived += (_, arguments) => receivedInvocation.TrySetResult(arguments);
        primary.StartListening();

        Assert.True(primary.IsPrimaryInstance);
        Assert.False(secondary.IsPrimaryInstance);

        await secondary.ForwardInvocationAsync(["first.log", "second.log"]);
        var receivedArguments = await receivedInvocation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["first.log", "second.log"], receivedArguments);
    }

    /// <summary>
    /// Ensures that an invocation without file arguments still reaches the primary coordinator.
    /// </summary>
    [Fact]
    public async Task ForwardInvocationAsync_WithoutArguments_ForwardsActivationRequest()
    {
        var uniqueName = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\Boexler.Sentinel.NLogViewer.Tests.{uniqueName}";
        var pipeName = $"Boexler.Sentinel.NLogViewer.Tests.{uniqueName}";
        using var primary = new SingleInstanceService(mutexName, pipeName);
        using var secondary = new SingleInstanceService(mutexName, pipeName);
        var receivedInvocation = new TaskCompletionSource<IReadOnlyList<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        primary.InvocationReceived += (_, arguments) => receivedInvocation.TrySetResult(arguments);
        primary.StartListening();

        await secondary.ForwardInvocationAsync(Array.Empty<string>());
        var receivedArguments = await receivedInvocation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(receivedArguments);
    }
}
