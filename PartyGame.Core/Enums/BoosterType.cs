namespace PartyGame.Core.Enums;

/// <summary>
/// All available booster types in the game.
/// Each player receives one random booster at game start.
/// </summary>
public enum BoosterType
{
    /// <summary>
    /// Doubles points earned for the first question of the round.
    /// Activate during CategorySelection phase.
    /// </summary>
    DoublePoints = 0,

    /// <summary>
    /// Removes 2 incorrect options from the current question.
    /// Activate during Answering phase (CategoryQuiz/Dictionary only).
    /// </summary>
    FiftyFifty = 1,

    /// <summary>
    /// Resets target player's round points to 0 for current round.
    /// Activate during Reveal or Scoreboard phase. Requires target.
    /// </summary>
    BackToZero = 2,

    /// <summary>
    /// Prevents target player from answering the next question.
    /// Activate during Question phase. Requires target.
    /// </summary>
    Nope = 3,

    /// <summary>
    /// Swaps your points with target player's points for current question.
    /// Only works if you were wrong and target was correct.
    /// Activate during Reveal phase. Requires target.
    /// </summary>
    PositionSwitch = 4,

    /// <summary>
    /// Grants +5 extra seconds to submit answer.
    /// Activate during Answering phase.
    /// </summary>
    LateLock = 5,

    /// <summary>
    /// Copies target player's answer when they submit.
    /// Activate during Answering phase. Requires target.
    /// </summary>
    Mirror = 6,

    /// <summary>
    /// Shuffles answer order on all other players' phones.
    /// Activate during Question phase (before Answering).
    /// </summary>
    ChaosMode = 8,

    /// <summary>
    /// Passive: blocks one negative booster (BackToZero/Nope/PositionSwitch).
    /// Automatically consumed when targeted.
    /// </summary>
    Shield = 9
}
