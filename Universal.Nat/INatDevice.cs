using System;
using System.Net;
using Universal.Nat.Enums;

namespace Universal.Nat
{
    public interface INatDevice
    {
        DateTime LastSeen { get; set; }

        IPAddress LocalAddress { get; }

        void CreatePortMap(Mapping mapping);

        void DeletePortMap(Mapping mapping);

        Mapping[] GetAllMappings();

        IPAddress GetExternalIP();

        Mapping GetSpecificMapping(Protocol protocol, int port);

        IAsyncResult BeginCreatePortMap(Mapping mapping, AsyncCallback callback, object asyncState);

        IAsyncResult BeginDeletePortMap(Mapping mapping, AsyncCallback callback, object asyncState);

        IAsyncResult BeginGetAllMappings(AsyncCallback callback, object asyncState);

        IAsyncResult BeginGetExternalIP(AsyncCallback callback, object asyncState);

        IAsyncResult BeginGetSpecificMapping(Protocol protocol, int externalPort, AsyncCallback callback,
            object asyncState);

        void EndCreatePortMap(IAsyncResult result);

        void EndDeletePortMap(IAsyncResult result);

        Mapping[] EndGetAllMappings(IAsyncResult result);

        IPAddress EndGetExternalIP(IAsyncResult result);

        Mapping EndGetSpecificMapping(IAsyncResult result);
    }
}