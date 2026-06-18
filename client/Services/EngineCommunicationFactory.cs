namespace client.Services;

public interface IEngineCommunicationFactory
{
    EngineCommunication Create(bool isWhite, int powerCapPercent, string powerCapColor);
}

public class EngineCommunicationFactory : IEngineCommunicationFactory
{
    private readonly PowerLimitState _state;

    public EngineCommunicationFactory(PowerLimitState state)
    {
        _state = state;
    }

    public EngineCommunication Create(bool isWhite, int powerCapPercent, string powerCapColor)
    {
        return new EngineCommunication(isWhite, powerCapPercent, powerCapColor, _state);
    }
}