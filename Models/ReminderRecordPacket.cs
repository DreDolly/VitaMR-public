namespace VitaMR.Models;

public sealed class ReminderRecordPacket
{
    public string ReminderText { get; set; } = string.Empty;

    public string DueDate { get; set; } = string.Empty;

    public string RelatedPatientDisplayName { get; set; } = string.Empty;

    public string Priority { get; set; } = "routine";

    public string Notes { get; set; } = string.Empty;
}
