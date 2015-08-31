using System;
using System.Net;
using Universal.Nat.EventArgs;

namespace Universal.Nat
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