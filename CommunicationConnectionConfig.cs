namespace industrial_comm_tool;

public sealed record CommunicationConnectionConfig(
    string DeviceModel,
    string IpAddress,
    string Port,
    string DefaultCommand,
    string DisplayMode)
{
    public static CommunicationConnectionConfig Default { get; } = new(
        "温控采集器",
        "127.0.0.1",
        "502",
        "01 03 00 00 00 02",
        "HEX");
}
