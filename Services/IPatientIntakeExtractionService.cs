using VitaMR.Models;

namespace VitaMR.Services;

public interface IPatientIntakeExtractionService
{
    Task<PatientIntakeExtractionResult> ExtractAsync(
        string endpoint,
        string inputText,
        CancellationToken cancellationToken = default);
}
