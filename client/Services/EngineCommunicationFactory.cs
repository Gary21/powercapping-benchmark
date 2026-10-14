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
    private readonly PowerLimitEnforcer _enforcer;

    public EngineCommunicationFactory(PowerLimitState state, EnergyMeasurementService energyMeasurementService, PowerLimitEnforcer enforcer)
    {
        _state = state;
        _energyMeasurementService = energyMeasurementService;
        _enforcer = enforcer;
    }

    public EngineCommunication Create(bool isWhite, NewGameModel game)
    {
        return new EngineCommunication(isWhite, game, _state, _energyMeasurementService, _enforcer);
    }
}
