namespace PartyGame.Server.Services;

/// <summary>
/// Tracks bot actions to ensure a bot acts only once per phase key.
/// </summary>
public class BotActionTracker
{
    private readonly Dictionary<Guid, string> _lastPhaseByBot = new();

    public bool ShouldActNow(Guid botId, string phaseKey)
    {
        if (_lastPhaseByBot.TryGetValue(botId, out var lastPhase) && lastPhase == phaseKey)
        {
            return false;
        }

        _lastPhaseByBot[botId] = phaseKey;
        return true;
    }

    public void Reset()
    {
        _lastPhaseByBot.Clear();
    }
}
