using VitaMR.Models;

namespace VitaMR.Services;

public interface IResponsePersonalizationService
{
    string Personalize(
        string backendAnswer,
        ChartContext? chartContext,
        IReadOnlyList<PatientIdentityRecord> knownPatients);
}
