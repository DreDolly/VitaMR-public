using VitaMR.Models;

namespace VitaMR.Services;

public interface IVaultContextService
{
    VaultContextPacket BuildSterileContext(string vaultRoot, ChartContext chartContext, string userQuestion = "");

    VaultContextPacket BuildFamilyVaultRosterContext(
        string vaultRoot,
        IReadOnlyList<PatientIdentityRecord> knownPatients);
}
