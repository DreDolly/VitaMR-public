using VitaMR.Models;

namespace VitaMR.Services;

public interface IPatientRegistryService
{
    PatientIdentityRecord GetOrCreateIdentity(string chartId, string patientDisplayName);

    PatientIdentityRecord GetOrCreateIdentityByName(string patientDisplayName);

    PatientIdentityRecord? TryResolveIdentityByName(string patientDisplayName);

    IReadOnlyList<PatientIdentityRecord> GetAllIdentities();

    string ResolveDisplayName(string chartId);

    void SaveIdentityRecord(PatientIdentityRecord record);

    bool RemoveIdentity(string chartId, out PatientIdentityRecord? removedRecord);
}
