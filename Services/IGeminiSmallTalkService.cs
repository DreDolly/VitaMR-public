namespace VitaMR.Services;

public interface IGeminiSmallTalkService
{
    Task<string> AnswerAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string scrubbedConversationContext,
        CancellationToken cancellationToken = default);

    Task<string> AnswerPersonalAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string topicHint,
        string captureType,
        CancellationToken cancellationToken = default);
}
