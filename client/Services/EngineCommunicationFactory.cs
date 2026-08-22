namespace client.Services;

public interface IEngineCommunicationFactory
{
    EngineCommunication Create(bool isWhite, int powerCapPercent, string powerCapColor, bool isGpu);
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

    public EngineCommunication Create(bool isWhite, int powerCapPercent, string powerCapColor, bool isGpu)
    {
        return new EngineCommunication(isWhite, powerCapPercent, powerCapColor, _state, isGpu, _energyMeasurementService);
    }
}