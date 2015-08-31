namespace Torrent.Uwp.Nat.EventArgs
{
    public class DeviceEventArgs : System.EventArgs
    {
        public DeviceEventArgs(INatDevice device)
        {
            Device = device;
        }

        public INatDevice Device { get; }
    }
}