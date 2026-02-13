using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Boosters;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.Services.Boosters;
using Xunit;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for the booster service and handlers.
/// </summary>
public class BoosterServiceTests
{
    private readonly IBoosterService _sut;

    public BoosterServiceTests()
    {
        // Create all handlers
        var handlers = new List<IBoosterHandler>
        {
            new DoublePointsHandler(),
            new FiftyFiftyHandler(),
            new BackToZeroHandler(),
            new NopeHandler(),
            new PositionSwitchHandler(),
            new LateLockHandler(),
            new MirrorHandler(),
            new ChaosModeHandler(),
            new ShieldHandler()
        };

        _sut = new BoosterService(handlers, NullLogger<BoosterService>.Instance);
    }

    private static QuizGameState CreateTestState(int playerCount = 3)
    {
        var state = new QuizGameState
        {
            RoomCode = "TEST",
            Phase = QuizPhase.CategorySelection,
            RoundNumber = 1,
            QuestionNumber = 0,
            CurrentRound = GameRound.Create(Guid.NewGuid())
        };

        for (int i = 0; i < playerCount; i++)
        {
            var playerId = Guid.NewGuid();
            state.Scoreboard.Add(new PlayerScoreState
            {
                PlayerId = playerId,
                DisplayName = $"Player{i + 1}",
                Score = (playerCount - i) * 100,
                Position = i + 1
            });
            state.Answers[playerId] = null;
        }

        return state;
    }

    #region Booster Assignment Tests

    [Fact]
    public void AssignBoostersAtGameStart_AssignsOneBoosterPerPlayer()
    {
        // Arrange
        var state = CreateTestState(4);

        // Act
        _sut.AssignBoostersAtGameStart(state);

        // Assert
        state.PlayerBoosters.Should().HaveCount(4);
        state.PlayerBoosters.Values.Should().AllSatisfy(b => b.IsUsed.Should().BeFalse());
    }

    [Fact]
    public void AssignBooster_AssignsSpecificBoosterType()
    {
        // Arrange
        var state = CreateTestState(1);
        var playerId = state.Scoreboard[0].PlayerId;

        // Act
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);

        // Assert
        state.PlayerBoosters[playerId].Type.Should().Be(BoosterType.DoublePoints);
        state.PlayerBoosters[playerId].IsUsed.Should().BeFalse();
    }

    #endregion

    #region CanActivateBooster Tests

    [Fact]
    public void CanActivateBooster_NoBooster_ReturnsFalse()
    {
        // Arrange
        var state = CreateTestState(1);
        var playerId = state.Scoreboard[0].PlayerId;
        // Don't assign a booster

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_NOT_OWNED");
    }

    [Fact]
    public void CanActivateBooster_WrongBoosterType_ReturnsFalse()
    {
        // Arrange
        var state = CreateTestState(1);
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.FiftyFifty);

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_NOT_OWNED");
    }

    [Fact]
    public void CanActivateBooster_AlreadyUsed_ReturnsFalse()
    {
        // Arrange
        var state = CreateTestState(1);
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);
        state.PlayerBoosters[playerId].IsUsed = true;

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_ALREADY_USED");
    }

    [Fact]
    public void CanActivateBooster_WrongPhase_ReturnsFalse()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.Reveal; // DoublePoints requires CategorySelection
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_INVALID_PHASE");
    }

    [Fact]
    public void CanActivateBooster_ValidConditions_ReturnsTrue()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.CategorySelection;
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        canActivate.Should().BeTrue();
        errorCode.Should().BeNull();
    }

    #endregion

    #region ActivateBooster Tests

    [Fact]
    public void ActivateBooster_ValidActivation_MarksAsUsed()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.CategorySelection;
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);

        // Act
        var result = _sut.ActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        result.Success.Should().BeTrue();
        state.PlayerBoosters[playerId].IsUsed.Should().BeTrue();
        state.PlayerBoosters[playerId].ActivatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ActivateBooster_CreatesActiveEffect()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.CategorySelection;
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);

        // Act
        var result = _sut.ActivateBooster(state, playerId, BoosterType.DoublePoints, null);

        // Assert
        result.Effect.Should().NotBeNull();
        result.Effect!.BoosterType.Should().Be(BoosterType.DoublePoints);
        result.Effect.ActivatorPlayerId.Should().Be(playerId);
        state.ActiveEffects.Should().Contain(result.Effect);
    }

    #endregion

    #region Shield Blocking Tests

    [Fact]
    public void ActivateBooster_TargetHasShield_BlocksNegativeBooster()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Question;
        var activatorId = state.Scoreboard[0].PlayerId;
        var targetId = state.Scoreboard[1].PlayerId;
        
        _sut.AssignBooster(state, activatorId, BoosterType.Nope);
        _sut.AssignBooster(state, targetId, BoosterType.Shield);

        // Act
        var result = _sut.ActivateBooster(state, activatorId, BoosterType.Nope, targetId);

        // Assert
        result.Success.Should().BeFalse();
        result.WasBlockedByShield.Should().BeTrue();
        result.ShieldBlockerPlayerId.Should().Be(targetId);
        
        // Shield should be consumed
        state.PlayerBoosters[targetId].IsUsed.Should().BeTrue();
        
        // Activator's booster should also be marked as used
        state.PlayerBoosters[activatorId].IsUsed.Should().BeTrue();
    }

    [Fact]
    public void ActivateBooster_TargetHasUsedShield_DoesNotBlock()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Question;
        var activatorId = state.Scoreboard[0].PlayerId;
        var targetId = state.Scoreboard[1].PlayerId;
        
        _sut.AssignBooster(state, activatorId, BoosterType.Nope);
        _sut.AssignBooster(state, targetId, BoosterType.Shield);
        state.PlayerBoosters[targetId].IsUsed = true; // Shield already used

        // Act
        var result = _sut.ActivateBooster(state, activatorId, BoosterType.Nope, targetId);

        // Assert
        result.Success.Should().BeTrue();
        result.WasBlockedByShield.Should().BeFalse();
    }

    #endregion

    #region Specific Booster Tests

    [Fact]
    public void FiftyFifty_StoresRemovedOptions()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.Answering;
        state.CorrectOptionKey = "A";
        state.Options = new List<QuizOptionState>
        {
            new() { Key = "A", Text = "Correct" },
            new() { Key = "B", Text = "Wrong1" },
            new() { Key = "C", Text = "Wrong2" },
            new() { Key = "D", Text = "Wrong3" }
        };
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.FiftyFifty);

        // Act
        var result = _sut.ActivateBooster(state, playerId, BoosterType.FiftyFifty, null);

        // Assert
        result.Success.Should().BeTrue();
        result.Effect!.Data.Should().ContainKey("RemovedOptions");
        var removed = result.Effect.Data["RemovedOptions"] as List<string>;
        removed.Should().HaveCount(2);
        removed.Should().NotContain("A"); // Correct answer should not be removed
    }

    [Fact]
    public void Nope_RequiresTarget()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Question;
        var activatorId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, activatorId, BoosterType.Nope);

        // Act - No target
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, activatorId, BoosterType.Nope, null);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_INVALID_TARGET");
    }

    [Fact]
    public void Nope_CannotTargetSelf()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Question;
        var activatorId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, activatorId, BoosterType.Nope);

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, activatorId, BoosterType.Nope, activatorId);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_INVALID_TARGET");
    }

    [Fact]
    public void PositionSwitch_RequiresActivatorToBeWrong()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Reveal;
        var activatorId = state.Scoreboard[0].PlayerId;
        var targetId = state.Scoreboard[1].PlayerId;
        
        // Activator answered correctly - should NOT be able to use PositionSwitch
        state.Scoreboard[0].AnsweredCorrectly = true;
        state.Scoreboard[1].AnsweredCorrectly = true;
        
        _sut.AssignBooster(state, activatorId, BoosterType.PositionSwitch);

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, activatorId, BoosterType.PositionSwitch, targetId);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_INVALID");
    }

    [Fact]
    public void PositionSwitch_RequiresTargetToBeCorrect()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Reveal;
        var activatorId = state.Scoreboard[0].PlayerId;
        var targetId = state.Scoreboard[1].PlayerId;
        
        state.Scoreboard[0].AnsweredCorrectly = false;
        state.Scoreboard[1].AnsweredCorrectly = false; // Target also wrong
        
        _sut.AssignBooster(state, activatorId, BoosterType.PositionSwitch);

        // Act
        var (canActivate, errorCode, _) = _sut.CanActivateBooster(state, activatorId, BoosterType.PositionSwitch, targetId);

        // Assert
        canActivate.Should().BeFalse();
        errorCode.Should().Be("BOOSTER_INVALID"); // Error code is BOOSTER_INVALID for validation errors
    }

    [Fact]
    public void Shield_CannotBeManuallyActivated()
    {
        // Arrange
        var state = CreateTestState(1);
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.Shield);

        // Act
        var (canActivate, _, errorMessage) = _sut.CanActivateBooster(state, playerId, BoosterType.Shield, null);

        // Assert
        canActivate.Should().BeFalse();
        errorMessage.Should().Contain("passive");
    }

    #endregion

    #region GetAnsweringEffects Tests

    [Fact]
    public void GetAnsweringEffects_NopedPlayer_HasIsNopedTrue()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Question;
        state.QuestionNumber = 1;
        var activatorId = state.Scoreboard[0].PlayerId;
        var targetId = state.Scoreboard[1].PlayerId;
        
        _sut.AssignBooster(state, activatorId, BoosterType.Nope);
        _sut.ActivateBooster(state, activatorId, BoosterType.Nope, targetId);

        // Act
        var effects = _sut.GetAnsweringEffects(state);

        // Assert
        effects[targetId].IsNoped.Should().BeTrue();
        effects[activatorId].IsNoped.Should().BeFalse();
    }

    [Fact]
    public void GetAnsweringEffects_FiftyFiftyPlayer_HasRemovedOptions()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.Answering;
        state.QuestionNumber = 1;
        state.CorrectOptionKey = "A";
        state.Options = new List<QuizOptionState>
        {
            new() { Key = "A", Text = "Correct" },
            new() { Key = "B", Text = "Wrong1" },
            new() { Key = "C", Text = "Wrong2" },
            new() { Key = "D", Text = "Wrong3" }
        };
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.FiftyFifty);
        _sut.ActivateBooster(state, playerId, BoosterType.FiftyFifty, null);

        // Act
        var effects = _sut.GetAnsweringEffects(state);

        // Assert
        effects[playerId].RemovedOptions.Should().HaveCount(2);
    }

    [Fact]
    public void GetAnsweringEffects_LateLockPlayer_HasExtendedDeadline()
    {
        // Arrange
        var state = CreateTestState(1);
        state.Phase = QuizPhase.Answering;
        state.QuestionNumber = 1;
        state.PhaseEndsUtc = DateTime.UtcNow.AddSeconds(10);
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.LateLock);
        _sut.ActivateBooster(state, playerId, BoosterType.LateLock, null);

        // Act
        var effects = _sut.GetAnsweringEffects(state);

        // Assert
        effects[playerId].ExtendedDeadline.Should().NotBeNull();
        effects[playerId].ExtendedDeadline.Should().BeCloseTo(
            state.PhaseEndsUtc.AddSeconds(5), 
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetAnsweringEffects_MirrorPlayer_HasMirrorTargetId()
    {
        // Arrange
        var state = CreateTestState(2);
        state.Phase = QuizPhase.Answering;
        state.QuestionNumber = 1;
        var activatorId = state.Scoreboard[0].PlayerId;
        var targetId = state.Scoreboard[1].PlayerId;
        
        _sut.AssignBooster(state, activatorId, BoosterType.Mirror);
        _sut.ActivateBooster(state, activatorId, BoosterType.Mirror, targetId);

        // Act
        var effects = _sut.GetAnsweringEffects(state);

        // Assert
        effects[activatorId].MirrorTargetId.Should().Be(targetId);
        effects[targetId].MirrorTargetId.Should().BeNull();
    }

    [Fact]
    public void GetAnsweringEffects_ChaosModePlayer_OtherPlayersHaveShuffledOrder()
    {
        // Arrange
        var state = CreateTestState(3);
        state.Phase = QuizPhase.Question;
        state.QuestionNumber = 1;
        state.Options = new List<QuizOptionState>
        {
            new() { Key = "A", Text = "Option A" },
            new() { Key = "B", Text = "Option B" },
            new() { Key = "C", Text = "Option C" },
            new() { Key = "D", Text = "Option D" }
        };
        var activatorId = state.Scoreboard[0].PlayerId;
        var otherPlayerId1 = state.Scoreboard[1].PlayerId;
        var otherPlayerId2 = state.Scoreboard[2].PlayerId;
        
        _sut.AssignBooster(state, activatorId, BoosterType.ChaosMode);
        _sut.ActivateBooster(state, activatorId, BoosterType.ChaosMode, null);

        // Act
        var effects = _sut.GetAnsweringEffects(state);

        // Assert
        // Activator should NOT have shuffled order
        effects[activatorId].ShuffledOptionOrder.Should().BeNull();
        
        // Other players SHOULD have shuffled order
        effects[otherPlayerId1].ShuffledOptionOrder.Should().NotBeNull();
        effects[otherPlayerId1].ShuffledOptionOrder.Should().HaveCount(4);
        effects[otherPlayerId2].ShuffledOptionOrder.Should().NotBeNull();
        effects[otherPlayerId2].ShuffledOptionOrder.Should().HaveCount(4);
    }

    [Fact]
    public void GetAnsweringEffects_MultipleEffects_AllAppliedCorrectly()
    {
        // Arrange: Player1 uses Nope on Player2, Player3 uses 50/50 on self
        var state = CreateTestState(3);
        state.Phase = QuizPhase.Question;
        state.QuestionNumber = 1;
        state.CorrectOptionKey = "A";
        state.Options = new List<QuizOptionState>
        {
            new() { Key = "A", Text = "Correct" },
            new() { Key = "B", Text = "Wrong1" },
            new() { Key = "C", Text = "Wrong2" },
            new() { Key = "D", Text = "Wrong3" }
        };
        
        var player1Id = state.Scoreboard[0].PlayerId;
        var player2Id = state.Scoreboard[1].PlayerId;
        var player3Id = state.Scoreboard[2].PlayerId;
        
        // Player1 uses Nope on Player2
        _sut.AssignBooster(state, player1Id, BoosterType.Nope);
        _sut.ActivateBooster(state, player1Id, BoosterType.Nope, player2Id);
        
        // Player3 uses 50/50 (need to change phase to Answering for this)
        state.Phase = QuizPhase.Answering;
        _sut.AssignBooster(state, player3Id, BoosterType.FiftyFifty);
        _sut.ActivateBooster(state, player3Id, BoosterType.FiftyFifty, null);

        // Act
        var effects = _sut.GetAnsweringEffects(state);

        // Assert
        effects[player1Id].IsNoped.Should().BeFalse();
        effects[player1Id].RemovedOptions.Should().BeEmpty();
        
        effects[player2Id].IsNoped.Should().BeTrue();
        effects[player2Id].RemovedOptions.Should().BeEmpty();
        
        effects[player3Id].IsNoped.Should().BeFalse();
        effects[player3Id].RemovedOptions.Should().HaveCount(2);
    }

    #endregion

    #region GetBoosterInfo Tests

    [Fact]
    public void GetBoosterInfo_ReturnsCorrectInfo()
    {
        // Arrange
        var state = CreateTestState(1);
        var playerId = state.Scoreboard[0].PlayerId;
        _sut.AssignBooster(state, playerId, BoosterType.DoublePoints);

        // Act
        var info = _sut.GetBoosterInfo(state, playerId);

        // Assert
        info.Should().NotBeNull();
        info!.Type.Should().Be(BoosterType.DoublePoints);
        info.Name.Should().Be("Double Points");
        info.IsUsed.Should().BeFalse();
        info.RequiresTarget.Should().BeFalse();
        info.ValidPhases.Should().Contain("CategorySelection");
    }

    #endregion
}
