using System.IO;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class JsonPatientRegistryService : IPatientRegistryService
{
    private readonly string _registryPath;

    public JsonPatientRegistryService()
    {
        var registryFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VitaMR",
            "SealedRegistry");

        _registryPath = Path.Combine(registryFolder, "Patient_Registry.json");
    }

    public PatientIdentityRecord GetOrCreateIdentity(string chartId, string patientDisplayName)
    {
        var normalizedChartId = NormalizeChartId(chartId);
        var registry = LoadRegistry();

        if (registry.TryGetValue(normalizedChartId, out var existing))
        {
            if (!string.IsNullOrWhiteSpace(patientDisplayName) &&
                string.IsNullOrWhiteSpace(existing.PatientDisplayName))
            {
                existing.PatientDisplayName = patientDisplayName.Trim();
                existing.NormalizedName = NormalizeName(patientDisplayName);
                existing.UpdatedAt = DateTime.Now;
                SaveRegistry(registry);
            }

            return existing;
        }

        var record = new PatientIdentityRecord
        {
            ChartId = normalizedChartId,
            PatientDisplayName = string.IsNullOrWhiteSpace(patientDisplayName)
                ? normalizedChartId
                : patientDisplayName.Trim(),
            NormalizedName = NormalizeName(patientDisplayName),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        registry[normalizedChartId] = record;
        SaveRegistry(registry);
        return record;
    }

    public PatientIdentityRecord GetOrCreateIdentityByName(string patientDisplayName)
    {
        var normalizedName = NormalizeName(patientDisplayName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return GetOrCreateIdentity("VITA-000001", "Unknown Patient");
        }

        var registry = LoadRegistry();
        var existing = registry.Values.FirstOrDefault(record =>
            string.Equals(record.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing;
        }

        var chartId = GetNextChartId(registry.Keys);
        var record = new PatientIdentityRecord
        {
            ChartId = chartId,
            PatientDisplayName = patientDisplayName.Trim(),
            NormalizedName = normalizedName,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        registry[chartId] = record;
        SaveRegistry(registry);
        return record;
    }

    public PatientIdentityRecord? TryResolveIdentityByName(string patientDisplayName)
    {
        var normalizedName = NormalizeName(patientDisplayName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var registry = LoadRegistry();

        return registry.Values.FirstOrDefault(record =>
            string.Equals(record.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<PatientIdentityRecord> GetAllIdentities()
    {
        return LoadRegistry()
            .Values
            .OrderBy(record => record.PatientDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string ResolveDisplayName(string chartId)
    {
        var normalizedChartId = NormalizeChartId(chartId);
        var registry = LoadRegistry();

        return registry.TryGetValue(normalizedChartId, out var record) &&
               !string.IsNullOrWhiteSpace(record.PatientDisplayName)
            ? record.PatientDisplayName
            : normalizedChartId;
    }

    public void SaveIdentityRecord(PatientIdentityRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.ChartId))
        {
            return;
        }

        var registry = LoadRegistry();
        record.ChartId = NormalizeChartId(record.ChartId);
        record.PatientDisplayName = string.IsNullOrWhiteSpace(record.PatientDisplayName)
            ? record.ChartId
            : record.PatientDisplayName.Trim();
        record.NormalizedName = NormalizeName(record.PatientDisplayName);
        record.UpdatedAt = DateTime.Now;

        if (registry.TryGetValue(record.ChartId, out var existing))
        {
            record.CreatedAt = existing.CreatedAt;
            record.DateOfBirth = FirstNonEmpty(record.DateOfBirth, existing.DateOfBirth);
            record.Address = FirstNonEmpty(record.Address, existing.Address);
            record.PhoneNumber = FirstNonEmpty(record.PhoneNumber, existing.PhoneNumber);
            record.Email = FirstNonEmpty(record.Email, existing.Email);
            record.SocialSecurityNumber = FirstNonEmpty(record.SocialSecurityNumber, existing.SocialSecurityNumber);
            record.RelationshipNotes = FirstNonEmpty(record.RelationshipNotes, existing.RelationshipNotes);
            record.PhotoPath = FirstNonEmpty(record.PhotoPath, existing.PhotoPath);
            record.MedicalRecordNumber = FirstNonEmpty(record.MedicalRecordNumber, existing.MedicalRecordNumber);
        }

        registry[record.ChartId] = record;
        SaveRegistry(registry);
    }

    public bool RemoveIdentity(string chartId, out PatientIdentityRecord? removedRecord)
    {
        removedRecord = null;
        var normalizedChartId = NormalizeChartId(chartId);
        var registry = LoadRegistry();

        if (!registry.TryGetValue(normalizedChartId, out removedRecord))
        {
            return false;
        }

        registry.Remove(normalizedChartId);
        SaveRegistry(registry);
        return true;
    }

    private Dictionary<string, PatientIdentityRecord> LoadRegistry()
    {
        if (!File.Exists(_registryPath))
        {
            return new Dictionary<string, PatientIdentityRecord>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_registryPath);
            var records = JsonSerializer.Deserialize<List<PatientIdentityRecord>>(json) ?? [];
            return records
                .Where(record => !string.IsNullOrWhiteSpace(record.ChartId))
                .Select(EnsureNormalizedName)
                .GroupBy(record => NormalizeChartId(record.ChartId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, PatientIdentityRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveRegistry(Dictionary<string, PatientIdentityRecord> registry)
    {
        var folder = Path.GetDirectoryName(_registryPath);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var records = registry.Values
            .OrderBy(record => record.ChartId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_registryPath, json);
    }

    private static string NormalizeChartId(string chartId)
    {
        return string.IsNullOrWhiteSpace(chartId)
            ? "VITA-0000"
            : chartId.Trim().ToUpperInvariant();
    }

    private static PatientIdentityRecord EnsureNormalizedName(PatientIdentityRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.NormalizedName))
        {
            record.NormalizedName = NormalizeName(record.PatientDisplayName);
        }

        return record;
    }

    private static string FirstNonEmpty(string preferred, string fallback)
    {
        return string.IsNullOrWhiteSpace(preferred)
            ? fallback
            : preferred.Trim();
    }

    private static string NormalizeName(string? patientDisplayName)
    {
        if (string.IsNullOrWhiteSpace(patientDisplayName))
        {
            return string.Empty;
        }

        var normalized = patientDisplayName
            .Trim()
            .ToLowerInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"['’]s\b", string.Empty);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9\s'-]+", " ");

        var words = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(' ', words);
    }

    private static string GetNextChartId(IEnumerable<string> chartIds)
    {
        var highest = chartIds
            .Select(ReadChartNumber)
            .DefaultIfEmpty(0)
            .Max();

        return $"VITA-{highest + 1:000000}";
    }

    private static int ReadChartNumber(string chartId)
    {
        const string prefix = "VITA-";

        if (!chartId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(chartId[prefix.Length..], out var number)
            ? number
            : 0;
    }
}
