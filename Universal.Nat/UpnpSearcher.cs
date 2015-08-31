using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Universal.Nat.EventArgs;
using Universal.Nat.Upnp;

namespace Universal.Nat
{
    internal class UpnpSearcher : ISearcher
    {
        internal const string WanIpUrn = "urn:schemas-upnp-org:service:WANIPConnection:1";
        private const string Port = "1900";

        private static readonly HostName Address1 = new HostName("239.255.255.250");
        private static readonly HostName Address2 = new HostName("ff02::c");
        private static readonly HostName Address3 = new HostName("ff05::c");
        private static readonly HostName Address4 = new HostName("ff08::c");

        private static readonly TimeSpan SearchPeriod = TimeSpan.FromMinutes(5.0);

        private readonly List<INatDevice> _devices;
        private readonly Dictionary<IPAddress, DateTime> _lastFetched;
        private ThreadPoolTimer _threadPoolTimer;

        private UpnpSearcher()
        {
            _devices = new List<INatDevice>();
            _lastFetched = new Dictionary<IPAddress, DateTime>();
            Init();
            NetworkChange.NetworkAddressChanged += NetworkChangeOnNetworkAddressChanged;
        }

        public static UpnpSearcher Instance { get; } = new UpnpSearcher();

        public DatagramSocket Socket { get; private set; }


        public event EventHandler<DeviceEventArgs> DeviceFound;
        public event EventHandler<DeviceEventArgs> DeviceLost;

        public void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint)
        {
            // Convert it to a string for easy parsing
            string dataString = null;

            // No matter what, this method should never throw an exception. If something goes wrong
            // we should still be in a position to handle the next reply correctly.
            try
            {
                dataString = Encoding.UTF8.GetString(response);


                Debug.WriteLine("UPnP Response: {0}", dataString);
                // If this device does not have a WANIPConnection service, then ignore it
                // Technically i should be checking for WANIPConnection:1 and InternetGatewayDevice:1
                // but there are some routers missing the '1'.
                var log = "UPnP Response: Router advertised a '{0}' service";
                var c = StringComparison.OrdinalIgnoreCase;
                if (dataString.IndexOf("urn:schemas-upnp-org:service:WANIPConnection:", c) != -1)
                    Debug.WriteLine(log, "urn:schemas-upnp-org:service:WANIPConnection:");
                else if (dataString.IndexOf("urn:schemas-upnp-org:device:InternetGatewayDevice:", c) != -1)
                    Debug.WriteLine(log, "urn:schemas-upnp-org:device:InternetGatewayDevice:");
                else if (dataString.IndexOf("urn:schemas-upnp-org:service:WANPPPConnection:", c) != -1)
                    Debug.WriteLine(log, "urn:schemas-upnp-org:service:WANPPPConnection:");
                else
                    return;

                // We have an internet gateway device now
                var d = new UpnpNatDevice(localAddress, dataString, WanIpUrn);

                if (_devices.Contains(d))
                {
                    // We already have found this device, so we just refresh it to let people know it's
                    // Still alive. If a device doesn't respond to a search, we dump it.
                    _devices[_devices.IndexOf(d)].LastSeen = DateTime.Now;
                }
                else
                {
                    // If we send 3 requests at a time, ensure we only fetch the services list once
                    // even if three responses are received
                    if (_lastFetched.ContainsKey(endpoint.Address))
                    {
                        var last = _lastFetched[endpoint.Address];
                        if ((DateTime.Now - last) < TimeSpan.FromSeconds(20))
                            return;
                    }
                    _lastFetched[endpoint.Address] = DateTime.Now;

                    // Once we've parsed the information we need, we tell the device to retrieve it's service list
                    // Once we successfully receive the service list, the callback provided will be invoked.
                    Debug.WriteLine("Fetching service list: {0}", d.HostEndPoint);
                    d.GetServicesList(DeviceSetupComplete);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Unhandled exception when trying to decode a device's response Send me the following data: ");
                Debug.WriteLine("ErrorMessage:");
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("Data string:");
                Debug.WriteLine(dataString);
            }
        }

        public void Start()
        {
            if (_threadPoolTimer != null)
                return;
            SearchAll();
            _threadPoolTimer = ThreadPoolTimer.CreatePeriodicTimer(timer => Search(), SearchPeriod);
        }

        public void Stop()
        {
            if (_threadPoolTimer == null)
                return;
            _threadPoolTimer.Cancel();
            _threadPoolTimer = null;
        }

        private void Init()
        {
            Socket = new DatagramSocket();
            Socket.MessageReceived += SocketOnMessageReceived;
            Task.Run(async () => await Socket.BindServiceNameAsync(""));
        }

        private void SocketOnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var dataReader = args.GetDataReader();
            var numArray = new byte[dataReader.UnconsumedBufferLength];
            dataReader.ReadBytes(numArray);
            var localAddress = args.LocalAddress;
            var obj = new IPEndPoint(IPAddress.Parse(args.RemoteAddress.RawName), int.Parse(args.RemotePort));
            Handle(IPAddress.Parse(localAddress.RawName), numArray, obj);
        }

        private void NetworkChangeOnNetworkAddressChanged(object sender, System.EventArgs eventArgs)
        {
            if (_threadPoolTimer == null)
                return;
            _threadPoolTimer.Cancel();
            _threadPoolTimer = null;
            Start();
        }

        public void Search()
        {
            lock (_devices)
            {
                _devices.RemoveAll(p => DateTime.Now - p.LastSeen > TimeSpan.FromMinutes(10.0));

                foreach (var device in _devices)
                {
                    OnDeviceFound(new DeviceEventArgs(device));
                }
                try
                {
                    SearchAll();
                }
                catch
                {
                    // ignored
                }
            }
        }


        private void SearchAll()
        {
            Search(Address1);
            Search(Address2);
            Search(Address3);
            Search(Address4);
        }

        private void Search(HostName hostName)
        {
            Task.Run(async () =>
            {
                var str =
                    "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp: discover\"\r\nMX: 3\r\nST: ssdp:all\r\n\r\n";
                var numArray = Encoding.UTF8.GetBytes(str);
                var dataWriter = new DataWriter(await Socket.GetOutputStreamAsync(hostName, Port));
                try
                {
                    dataWriter.WriteBytes(numArray);
                    await dataWriter.StoreAsync();
                }
                finally
                {
                    dataWriter.DetachStream();
                }
            });
        }

        private void DeviceSetupComplete(INatDevice device)
        {
            lock (_devices)
            {
                // We don't want the same device in there twice
                if (_devices.Contains(device))
                    return;

                _devices.Add(device);
            }

            OnDeviceFound(new DeviceEventArgs(device));
        }

        private void OnDeviceFound(DeviceEventArgs args)
        {
            DeviceFound?.Invoke(this, args);
        }
    }
}