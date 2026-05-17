using VitaMR.Models;

namespace VitaMR.Services;

public interface IVitaMasteryService
{
    VitaMasteryResult Evaluate(string vaultRoot, ChartContext chartContext, PatientIdentityRecord? identity);
}
