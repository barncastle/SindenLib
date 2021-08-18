namespace SindenLib.Static
{
    public enum ConnectionState : byte
    {
        Success,
        AlreadyConnected,
        DeviceNotResponding,
        InvalidAuthentication
    }
}
