namespace VitaMR.Models;

public sealed class WikiPatchApplyResult
{
    public bool WasApplied { get; set; }

    public List<string> UpdatedFiles { get; set; } = [];

    public List<string> Messages { get; set; } = [];
}
