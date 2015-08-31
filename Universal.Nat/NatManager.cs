using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System.Threading;
using Torrent.Uwp.Nat.Enums;
using Torrent.Uwp.Nat.EventArgs;
using Torrent.Uwp.Nat.Exceptions;

namespace Torrent.Uwp.Nat
{
    public class NatManager
    {
        private static readonly TimeSpan CheckPeriod = TimeSpan.FromMinutes(10.0);
        private readonly List<INatDevice> _devices = new List<INatDevice>();
        private ThreadPoolTimer _checkMappingsTimer;
        private Task _currentTask;
        private Mapping _portMapping;

        public NatManager(int port)
        {
            NatUtility.DeviceFound += DeviceFound;
            NatUtility.DeviceLost += DeviceLost;
            Port = port;
        }

        public bool IsStarted => _checkMappingsTimer != null;

        public int Port
        {
            get { return _portMapping.PublicPort; }
            set
            {
                if (_portMapping != null && _portMapping.PublicPort == value)
                    return;
                var oldMapping = _portMapping;
                _portMapping = new Mapping(Protocol.Tcp, value, value);
                if (!IsStarted)
                    return;
                ScheduleTask(async () =>
                {
                    if (oldMapping != null)
                        await RemoveMappingAsync(oldMapping);
                    await AddMappingAsync();
                });
            }
        }

        public void Start()
        {
            if (IsStarted)
                return;
            try
            {
                NatUtility.StartDiscovery();
                ScheduleTask(() => AddMappingAsync().Wait());
                _checkMappingsTimer = ThreadPoolTimer.CreatePeriodicTimer(t => CheckMappings(), CheckPeriod);
            }
            catch
            {
                // ignored
            }
        }

        public void Stop()
        {
            try
            {
                ScheduleTask(() => RemoveMappingAsync(_portMapping).Wait());
                NatUtility.StopDiscovery();
                _checkMappingsTimer.Cancel();
                _checkMappingsTimer = null;
            }
            catch
            {
                // ignored
            }
        }

        private void ScheduleTask(Action action)
        {
            if (_currentTask == null || _currentTask.IsCompleted)
                _currentTask = Task.Run(action);
            else
                _currentTask.ContinueWith(t => action());
        }

        private static IEnumerable<Mapping> GetAllMappings(INatDevice device)
        {
            try
            {
                return device.GetAllMappings();
            }
            catch
            {
                // ignored
            }
            return new Mapping[0];
        }

        private void CheckMappings()
        {
            ScheduleTask(() => ForEachDevice(device =>
            {
                if (GetAllMappings(device).FirstOrDefault(m => m.PublicPort == _portMapping.PublicPort) != null)
                    return;
                CreatePortMapAsync(device).Wait();
            }).Wait());
        }

        private async Task ForEachDevice(Action<INatDevice> action)
        {
            await Task.Run(() =>
            {
                lock (_devices)
                {
                    Parallel.ForEach(_devices, action);
                }
            });
        }

        private async Task AddMappingAsync()
        {
            await ForEachDevice(d => CreatePortMapAsync(d).Wait());
        }

        private async Task RemoveMappingAsync(Mapping mapping)
        {
            await ForEachDevice(d => RemovePortMapAsync(d, mapping).Wait());
        }

        private async Task CreatePortMapAsync(INatDevice device)
        {
            var mapping = new Mapping(_portMapping.Protocol, _portMapping.PrivatePort, _portMapping.PublicPort)
            {
                Description = "Universal.Torrent: " + device.LocalAddress
            };
            await Task.Run(() =>
            {
                try
                {
                    device.CreatePortMap(mapping);
                }
                catch (MappingException)
                {
                }
                catch
                {
                    // ignored
                }
            });
        }

        private async Task RemovePortMapAsync(INatDevice device, Mapping mapping)
        {
            await Task.Run(() =>
            {
                try
                {
                    device.DeletePortMap(mapping);
                }
                catch (MappingException)
                {
                }
                catch
                {
                    // ignored
                }
            });
        }

        private void DeviceFound(object sender, DeviceEventArgs args)
        {
            lock (_devices)
            {
                _devices.Add(args.Device);
            }
            ScheduleTask(async () =>
            {
                var mappings = GetAllMappings(args.Device);
                var oldMappings =
                    mappings.Where(m => m.Description.Equals("Universal.Torrent: " + args.Device.LocalAddress));
                foreach (var mapping in oldMappings)
                    await RemovePortMapAsync(args.Device, mapping);
                await CreatePortMapAsync(args.Device);
            });
        }

        private void DeviceLost(object sender, DeviceEventArgs args)
        {
            lock (_devices)
            {
                _devices.Remove(args.Device);
            }
        }
    }
}