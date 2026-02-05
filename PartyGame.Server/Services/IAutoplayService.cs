namespace PartyGame.Server.Services;

public interface IAutoplayService
{
    Task StartAsync(string roomCode);
    Task StopAsync(string roomCode);
    bool IsRunning(string roomCode);
    IReadOnlyCollection<Guid> GetBotIds(string roomCode);
}
