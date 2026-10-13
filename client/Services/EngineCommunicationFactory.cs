using server.Models;

namespace client.Services;

public interface IEngineCommunicationFactory
{
    EngineCommunication Create(bool isWhite, NewGameModel game);
}

public class EngineCommunicationFactory : IEngineCommunicationFactory
{
    private readonly PowerLimitState _state;
    private readonly EnergyMeasurementService _energyMeasurementService;

    public EngineCommunicationFactory(PowerLimitState state, EnergyMeasurementService energyMeasurementService)
    {
        _state = state;
        _energyMeasurementService = energyMeasurementService;
    }

    public EngineCommunication Create(bool isWhite, NewGameModel game)
    {
        return new EngineCommunication(isWhite, game, _state, _energyMeasurementService);
    }
}
