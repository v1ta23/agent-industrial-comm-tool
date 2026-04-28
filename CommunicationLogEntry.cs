namespace industrial_comm_tool;

public sealed record CommunicationLogEntry(
    string Id,
    string Time,
    string Protocol,
    string Direction,
    string Content,
    string Status,
    int DurationMs,
    string ErrorType);
