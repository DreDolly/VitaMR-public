using VitaMR.Models;

namespace VitaMR.Services;

public interface IPatternLinkerService
{
    PatternLinkerResult ScanAllCharts(string vaultRoot, IReadOnlyList<PatientIdentityRecord> activePatients);
}
