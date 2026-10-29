namespace Proyecto_Final.Models.Chat;

public sealed record LegacyPersonalArchiveViewModel(IReadOnlyList<int> Conversations,
    int? SelectedConversation, int Page, IReadOnlyList<ChatMessageViewModel> Messages);
