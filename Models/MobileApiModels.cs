namespace VitaMR.Models;

public sealed class MobileTrustedDevice
{
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset PairedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.Now;
}

public sealed class MobileTrustedDeviceStore
{
    public List<MobileTrustedDevice> Devices { get; set; } = [];
}

public sealed class MobilePairStartResponse
{
    public string PairingCode { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class MobilePairCompleteRequest
{
    public string PairingCode { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;
}

public sealed class MobilePairCompleteResponse
{
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}

public sealed class MobileChatRequest
{
    public string Message { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public bool WasVoiceInput { get; set; }
}

public sealed class MobileChatResponse
{
    public string RequestId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Reply { get; set; } = string.Empty;

    public List<string> Lines { get; set; } = [];

    public string Route { get; set; } = string.Empty;

    public string ActiveChartId { get; set; } = string.Empty;

    public string ActivePatientDisplayName { get; set; } = string.Empty;

    public MobileChartPacketResponse? Packet { get; set; }
}

public sealed class MobileChartPacketRequest
{
    public string ChartId { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public string Mode { get; set; } = "current";
}

public sealed class MobileChartPacketResponse
{
    public string Status { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public string PacketType { get; set; } = string.Empty;

    public string Mode { get; set; } = "current";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class MobileVitaMasteryRequest
{
    public string ChartId { get; set; } = string.Empty;
}

public sealed class MobileDataHunterQuestRequest
{
    public string ChartId { get; set; } = string.Empty;
}

public sealed class MobileVitaMasteryQuestResponse
{
    public string QuestId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string EvidenceHint { get; set; } = string.Empty;

    public int Points { get; set; }
}

public sealed class MobileVitaMasteryResponse
{
    public string Status { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public int PercentComplete { get; set; }

    public string CurrentStage { get; set; } = string.Empty;

    public int EarnedPoints { get; set; }

    public int PossiblePoints { get; set; }

    public string SummaryLine { get; set; } = string.Empty;

    public List<MobileVitaMasteryQuestResponse> ActiveMicroQuests { get; set; } = [];

    public string DataHunterTitle { get; set; } = string.Empty;

    public string DataHunterStage { get; set; } = string.Empty;

    public int DataHunterPercent { get; set; }

    public string DataHunterQuestion { get; set; } = string.Empty;

    public string DataHunterQuestCategory { get; set; } = string.Empty;

    public string DataHunterQuestStatus { get; set; } = string.Empty;

    public int DataHunterQuestXp { get; set; }

    public int DataHunterXpTotal { get; set; }

    public string DataHunterMasterTarget { get; set; } = string.Empty;

    public string DataHunterMasterTargetDetail { get; set; } = string.Empty;

    public int DataHunterMasterAcceptedCount { get; set; }

    public int DataHunterMasterPendingCount { get; set; }

    public string DataHunterQuestControlPrompt { get; set; } = string.Empty;

    public string DataHunterPrompt { get; set; } = string.Empty;
}

public sealed class MobilePacketRequest
{
    public string Request { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;
}

public sealed class MobilePacketResponse
{
    public string PacketId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class MobileCaptureRequest
{
    public string ChartId { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string CaptureType { get; set; } = "photo";

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public string Base64Data { get; set; } = string.Empty;
}

public sealed class MobileCaptureResponse
{
    public string CaptureId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class MobileChartPhotoRequest
{
    public string ChartId { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Base64Data { get; set; } = string.Empty;
}

public sealed class MobileChartPhotoResponse
{
    public string Status { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public bool HasPhoto { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public string Base64Data { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class MobileOfflineItemRequest
{
    public string LocalId { get; set; } = string.Empty;

    public string CreatedAt { get; set; } = string.Empty;

    public string ChartId { get; set; } = string.Empty;

    public string PatientDisplayName { get; set; } = string.Empty;

    public string Kind { get; set; } = "text";

    public string Note { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Base64Data { get; set; } = string.Empty;
}

public sealed class MobileOfflineItemResponse
{
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool NeedsContext { get; set; }

    public string ContextQuestion { get; set; } = string.Empty;
}
