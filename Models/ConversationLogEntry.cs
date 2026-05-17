namespace VitaMR.Models;

public sealed class ConversationLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string SessionId { get; set; } = string.Empty;

    public int Turn { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public IReadOnlyList<string> Paragraphs { get; set; } = [];

    public IReadOnlyList<ConversationAttachmentLog> Attachments { get; set; } = [];

    public string LinkedChartId { get; set; } = string.Empty;

    public string Mode { get; set; } = "conversation";

    public bool IsTestData { get; set; } = true;
}
