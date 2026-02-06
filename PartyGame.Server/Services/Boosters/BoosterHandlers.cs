using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Boosters;
using PartyGame.Core.Models.Quiz;

namespace PartyGame.Server.Services.Boosters;

/// <summary>
/// Base class for booster handlers with common functionality.
/// </summary>
public abstract class BoosterHandlerBase : IBoosterHandler
{
    public abstract BoosterType Type { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual bool RequiresTarget => false;
    public virtual bool IsPassive => false;
    public virtual bool IsNegative => false;
    public abstract QuizPhase[] ValidPhases { get; }

    public virtual string? Validate(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        // Check phase
        if (!ValidPhases.Contains(state.Phase))
            return $"Cannot use {Name} in current phase.";

        // Check target if required
        if (RequiresTarget && !targetId.HasValue)
            return $"{Name} requires a target player.";

        if (RequiresTarget && targetId.HasValue)
        {
            // Can't target self (except for some boosters)
            if (targetId.Value == activatorId)
                return "Cannot target yourself.";

            // Target must exist
            if (!state.Scoreboard.Any(p => p.PlayerId == targetId.Value))
                return "Target player not found.";
        }

        return null;
    }

    public virtual ActiveBoosterEffect Apply(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        return new ActiveBoosterEffect
        {
            BoosterType = Type,
            ActivatorPlayerId = activatorId,
            TargetPlayerId = targetId,
            QuestionNumber = state.QuestionNumber,
            RoundNumber = state.RoundNumber
        };
    }

    protected static PlayerScoreState? GetPlayer(QuizGameState state, Guid playerId)
    {
        return state.Scoreboard.FirstOrDefault(p => p.PlayerId == playerId);
    }
}

/// <summary>
/// DoublePoints: Doubles points earned for the first question of the round.
/// </summary>
public class DoublePointsHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.DoublePoints;
    public override string Name => "Double Points";
    public override string Description => "Doubles your points for the first question of this round.";
    public override QuizPhase[] ValidPhases => [QuizPhase.CategorySelection];

    public override string? Validate(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var baseError = base.Validate(state, activatorId, targetId);
        if (baseError != null) return baseError;

        // Can only activate at start of round (before any questions)
        if (state.CurrentRound?.CurrentQuestionIndex > 0)
            return "Can only activate Double Points before the round's first question.";

        return null;
    }
}

/// <summary>
/// FiftyFifty: Removes 2 incorrect options.
/// </summary>
public class FiftyFiftyHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.FiftyFifty;
    public override string Name => "50/50";
    public override string Description => "Removes 2 wrong answers, leaving only 2 options.";
    public override QuizPhase[] ValidPhases => [QuizPhase.Answering, QuizPhase.DictionaryAnswering];

    public override ActiveBoosterEffect Apply(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var effect = base.Apply(state, activatorId, targetId);

        // Determine which options to remove (keep correct + 1 random incorrect)
        var correctKey = state.CorrectOptionKey;
        var incorrectOptions = state.Options
            .Where(o => !o.Key.Equals(correctKey, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.Key)
            .ToList();

        // Remove 2 random incorrect options
        var random = new Random();
        var optionsToRemove = incorrectOptions
            .OrderBy(_ => random.Next())
            .Take(2)
            .ToList();

        effect.Data["RemovedOptions"] = optionsToRemove;
        return effect;
    }
}

/// <summary>
/// BackToZero: Resets target's round points to 0.
/// </summary>
public class BackToZeroHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.BackToZero;
    public override string Name => "Back to Zero";
    public override string Description => "Resets a player's points earned this round to zero.";
    public override bool RequiresTarget => true;
    public override bool IsNegative => true;
    public override QuizPhase[] ValidPhases => [QuizPhase.Reveal, QuizPhase.Scoreboard];
}

/// <summary>
/// Nope: Prevents target from answering next question.
/// </summary>
public class NopeHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.Nope;
    public override string Name => "NOPE";
    public override string Description => "Prevents a player from answering the current question.";
    public override bool RequiresTarget => true;
    public override bool IsNegative => true;
    public override QuizPhase[] ValidPhases => [QuizPhase.Question];
}

/// <summary>
/// PositionSwitch: Swaps points with target for current question.
/// </summary>
public class PositionSwitchHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.PositionSwitch;
    public override string Name => "Position Switch";
    public override string Description => "Swaps your points with another player's points for this question. Only works if you were wrong and they were correct.";
    public override bool RequiresTarget => true;
    public override bool IsNegative => true;
    public override QuizPhase[] ValidPhases => [QuizPhase.Reveal];

    public override string? Validate(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var baseError = base.Validate(state, activatorId, targetId);
        if (baseError != null) return baseError;

        var activator = GetPlayer(state, activatorId);
        var target = targetId.HasValue ? GetPlayer(state, targetId.Value) : null;

        // Activator must have answered incorrectly
        if (activator?.AnsweredCorrectly == true)
            return "You can only use Position Switch if you answered incorrectly.";

        // Target must have answered correctly
        if (target?.AnsweredCorrectly != true)
            return "Target must have answered correctly.";

        return null;
    }
}

/// <summary>
/// LateLock: Grants +5 extra seconds to submit.
/// </summary>
public class LateLockHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.LateLock;
    public override string Name => "Late Lock";
    public override string Description => "Grants you 5 extra seconds to submit your answer.";
    public override QuizPhase[] ValidPhases => [QuizPhase.Answering, QuizPhase.DictionaryAnswering];

    public override ActiveBoosterEffect Apply(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var effect = base.Apply(state, activatorId, targetId);
        effect.Data["ExtendedDeadline"] = state.PhaseEndsUtc.AddSeconds(5);
        return effect;
    }
}

/// <summary>
/// Mirror: Copies target's answer when they submit.
/// </summary>
public class MirrorHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.Mirror;
    public override string Name => "Mirror";
    public override string Description => "Copies another player's answer when they submit.";
    public override bool RequiresTarget => true;
    public override QuizPhase[] ValidPhases => [QuizPhase.Answering, QuizPhase.DictionaryAnswering];
}

/// <summary>
/// JuryDuty: Awards +20 bonus to a player among correct answerers.
/// </summary>
public class JuryDutyHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.JuryDuty;
    public override string Name => "Jury Duty";
    public override string Description => "Awards +20 bonus points to a player of your choice among correct answerers.";
    public override bool RequiresTarget => true;
    public override QuizPhase[] ValidPhases => [QuizPhase.Reveal];

    public override string? Validate(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var baseError = base.Validate(state, activatorId, targetId);
        if (baseError != null) return baseError;

        // Need at least 2 correct answerers
        var correctCount = state.Scoreboard.Count(p => p.AnsweredCorrectly == true);
        if (correctCount < 2)
            return "Jury Duty requires at least 2 correct answers.";

        // Target must have answered correctly
        var target = targetId.HasValue ? GetPlayer(state, targetId.Value) : null;
        if (target?.AnsweredCorrectly != true)
            return "Target must have answered correctly.";

        return null;
    }
}

/// <summary>
/// ChaosMode: Shuffles answer order for everyone except activator.
/// </summary>
public class ChaosModeHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.ChaosMode;
    public override string Name => "Chaos Mode";
    public override string Description => "Shuffles the answer order on everyone else's phones.";
    public override QuizPhase[] ValidPhases => [QuizPhase.Question];

    public override ActiveBoosterEffect Apply(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var effect = base.Apply(state, activatorId, targetId);

        // Generate shuffled order for each player (except activator)
        var shuffledOrders = new Dictionary<Guid, List<string>>();
        var originalOrder = state.Options.Select(o => o.Key).ToList();
        var random = new Random();

        foreach (var player in state.Scoreboard)
        {
            if (player.PlayerId != activatorId)
            {
                var shuffled = originalOrder.OrderBy(_ => random.Next()).ToList();
                shuffledOrders[player.PlayerId] = shuffled;
            }
        }

        effect.Data["ShuffledOrders"] = shuffledOrders;
        return effect;
    }
}

/// <summary>
/// Shield: Passive protection against negative boosters.
/// </summary>
public class ShieldHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.Shield;
    public override string Name => "Shield";
    public override string Description => "Automatically blocks one negative booster targeting you.";
    public override bool IsPassive => true;
    public override QuizPhase[] ValidPhases => []; // Cannot be manually activated

    public override string? Validate(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        return "Shield is a passive booster and cannot be manually activated.";
    }
}

/// <summary>
/// Wildcard: Allows changing answer once after submitting.
/// </summary>
public class WildcardHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.Wildcard;
    public override string Name => "Wildcard";
    public override string Description => "Allows you to change your answer once after submitting.";
    public override QuizPhase[] ValidPhases => [QuizPhase.Answering, QuizPhase.DictionaryAnswering];

    public override string? Validate(QuizGameState state, Guid activatorId, Guid? targetId)
    {
        var baseError = base.Validate(state, activatorId, targetId);
        if (baseError != null) return baseError;

        // Must have already submitted an answer
        var hasAnswered = state.CurrentRound?.Type == RoundType.DictionaryGame
            ? state.DictionaryAnswers.TryGetValue(activatorId, out var da) && da != null
            : state.Answers.TryGetValue(activatorId, out var a) && a != null;

        if (!hasAnswered)
            return "Must submit an answer before using Wildcard.";

        return null;
    }
}

/// <summary>
/// Spotlight: Your answer is revealed first with dramatic effect.
/// </summary>
public class SpotlightHandler : BoosterHandlerBase
{
    public override BoosterType Type => BoosterType.Spotlight;
    public override string Name => "Spotlight";
    public override string Description => "Your answer is revealed first on the TV with dramatic effect.";
    public override QuizPhase[] ValidPhases => [QuizPhase.Reveal];
}
