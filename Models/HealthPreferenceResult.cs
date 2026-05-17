namespace VitaMR.Models;

public sealed class HealthPreferenceResult
{
    public bool WasRun { get; set; }

    public bool WasCaptured { get; set; }

    public string Status { get; set; } = "HEALTH_PREFERENCE_NOT_RUN";

    public string UserName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> UpdatedFiles { get; } = [];
}
