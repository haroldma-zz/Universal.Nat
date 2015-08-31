using System;
using System.Collections.Generic;
using Torrent.Uwp.Nat.EventArgs;

namespace Torrent.Uwp.Nat
{
    public static class NatUtility
    {
        private static readonly List<ISearcher> Controllers = new List<ISearcher>();

        static NatUtility()
        {
            Controllers.Add(UpnpSearcher.Instance);
            foreach (var searcher in Controllers)
            {
                searcher.DeviceFound += (sender, e) =>
               {
                   if (DeviceFound == null)
                       return;
                   DeviceFound(sender, e);
               };
                searcher.DeviceLost += (sender, e) =>
               {
                   if (DeviceLost == null)
                       return;
                   DeviceLost(sender, e);
               };
            }
        }

        public static event EventHandler<DeviceEventArgs> DeviceFound;

        public static event EventHandler<DeviceEventArgs> DeviceLost;

        public static void StartDiscovery()
        {
            foreach (var searcher in Controllers)
                searcher.Start();
        }

        public static void StopDiscovery()
        {
            foreach (var searcher in Controllers)
                searcher.Stop();
        }
    }
}