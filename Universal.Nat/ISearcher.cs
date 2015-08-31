using System;
using System.Net;
using Torrent.Uwp.Nat.EventArgs;

namespace Torrent.Uwp.Nat
{
    public delegate void NatDeviceCallback(INatDevice device);

    internal interface ISearcher
    {
        event EventHandler<DeviceEventArgs> DeviceFound;
        event EventHandler<DeviceEventArgs> DeviceLost;

        void Start();
        void Stop();
        void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint);
    }
}