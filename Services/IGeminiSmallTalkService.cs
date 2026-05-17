namespace VitaMR.Services;

public interface IGeminiSmallTalkService
{
    Task<string> AnswerAsync(
        bool isEnabled,
        string modelName,
        string scrubbedUserText,
        string scrubbedConversationContext,
        CancellationToken cancellationToken = default);
}

