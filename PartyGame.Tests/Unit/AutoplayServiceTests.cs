using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Server.Options;
using PartyGame.Server.Services;

namespace PartyGame.Tests.Unit;

public class AutoplayServiceTests
{
    [Fact]
    public async Task StartStopLifecycle_TogglesRunningState()
    {
        // Arrange
        var roomStore = Substitute.For<IRoomStore>();
        var orchestrator = Substitute.For<IQuizGameOrchestrator>();
        var logger = Substitute.For<ILogger<AutoplayService>>();
        var options = Options.Create(new AutoplayOptions { PollIntervalMs = 1, MinActionDelayMs = 1, MaxActionDelayMs = 2 });

        var room = new Room { Code = "TEST", Status = RoomStatus.Lobby };
        roomStore.TryGetRoom("TEST", out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        var sut = new AutoplayService(roomStore, orchestrator, options, logger);

        // Act
        await sut.StartAsync("TEST");
        var runningAfterStart = sut.IsRunning("TEST");
        await sut.StopAsync("TEST");
        var runningAfterStop = sut.IsRunning("TEST");

        // Assert
        runningAfterStart.Should().BeTrue();
        runningAfterStop.Should().BeFalse();
    }

    [Fact]
    public void BotActionTracker_PreventsDuplicateActionsInSamePhase()
    {
        // Arrange
        var tracker = new BotActionTracker();
        var botId = Guid.NewGuid();

        // Act
        var first = tracker.ShouldActNow(botId, "phase-1");
        var second = tracker.ShouldActNow(botId, "phase-1");
        var third = tracker.ShouldActNow(botId, "phase-2");

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        third.Should().BeTrue();
    }
}
