namespace BranchApi.Services;

public record NotificationMessage(
    DateTime ReceivedAt,
    string Channel,
    string Payload
);
