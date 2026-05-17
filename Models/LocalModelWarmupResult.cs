namespace VitaMR.Models;

public sealed class LocalModelWarmupResult
{
    public bool IsAvailable { get; set; }

    public string Status { get; set; } = "LOCAL_MODEL_WARMUP_NOT_RUN";

    public string Message { get; set; } = string.Empty;

    public TimeSpan Elapsed { get; set; }
}
