using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Torrent.Uwp.Nat.Enums;
using Torrent.Uwp.Nat.Exceptions;
using Torrent.Uwp.Nat.Upnp.AsyncResults;
using Torrent.Uwp.Nat.Upnp.Messages;
using Torrent.Uwp.Nat.Upnp.Messages.Requests;
using Torrent.Uwp.Nat.Upnp.Messages.Responses;

namespace Torrent.Uwp.Nat.Upnp
{
    public sealed class UpnpNatDevice : AbstractNatDevice, IEquatable<UpnpNatDevice>
    {
        /// <summary>
        ///     The callback to invoke when we are finished setting up the device
        /// </summary>
        private NatDeviceCallback _callback;

        internal UpnpNatDevice(IPAddress localAddress, string deviceDetails, string serviceType)
        {
            LastSeen = DateTime.Now;
            InternalLocalAddress = localAddress;

            // Split the string at the "location" section so i can extract the ipaddress and service description url
            var locationDetails =
                deviceDetails.Substring(deviceDetails.IndexOf("Location", StringComparison.OrdinalIgnoreCase) + 9)
                    .Split('\r')[0];
            ServiceType = serviceType;

            // Make sure we have no excess whitespace
            locationDetails = locationDetails.Trim();

            // FIXME: Is this reliable enough. What if we get a hostname as opposed to a proper http address
            // Are we going to get addresses with the "http://" attached?
            if (locationDetails.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("Found device at: {0}", locationDetails);
                // This bit strings out the "http://" from the string
                locationDetails = locationDetails.Substring(7);

                // We then split off the end of the string to get something like: 192.168.0.3:241 in our string
                var hostAddressAndPort = locationDetails.Remove(locationDetails.IndexOf('/'));

                // From this we parse out the IP address and Port
                if (hostAddressAndPort.IndexOf(':') > 0)
                {
                    HostEndPoint =
                        new IPEndPoint(IPAddress.Parse(hostAddressAndPort.Remove(hostAddressAndPort.IndexOf(':'))),
                            Convert.ToUInt16(hostAddressAndPort.Substring(hostAddressAndPort.IndexOf(':') + 1),
                                CultureInfo.InvariantCulture));
                }
                else
                {
                    // there is no port specified, use default port (80)
                    HostEndPoint = new IPEndPoint(IPAddress.Parse(hostAddressAndPort), 80);
                }

                Debug.WriteLine("Parsed device as: {0}", HostEndPoint.ToString());

                // The service description URL is the remainder of the "locationDetails" string. The bit that was originally after the ip
                // and port information
                ServiceDescriptionUrl = locationDetails.Substring(locationDetails.IndexOf('/'));
            }
            else
            {
                Debug.WriteLine("Couldn't decode address. Please send following string to the developer: ");
                Debug.WriteLine(deviceDetails);
            }
        }

        /// <summary>
        ///     The EndPoint that the device is at
        /// </summary>
        internal EndPoint HostEndPoint { get; private set; }

        /// <summary>
        ///     The relative url of the xml file that describes the list of services is at
        /// </summary>
        internal string ServiceDescriptionUrl { get; }

        /// <summary>
        ///     The relative url that we can use to control the port forwarding
        /// </summary>
        internal string ControlUrl { get; private set; }

        /// <summary>
        ///     The service type we're using on the device
        /// </summary>
        internal string ServiceType { get; }


        public bool Equals(UpnpNatDevice other)
        {
            return (other != null) && (HostEndPoint.Equals(other.HostEndPoint)
                //&& this.controlUrl == other.controlUrl
                                       && ServiceDescriptionUrl == other.ServiceDescriptionUrl);
        }

        /// <summary>
        ///     Begins an async call to get the external ip address of the router
        /// </summary>
        public override IAsyncResult BeginGetExternalIP(AsyncCallback callback, object asyncState)
        {
            // Create the port map message
            var message = new GetExternalIPAddressMessage(this);
            return BeginMessageInternal(message, callback, asyncState, EndGetExternalIPInternal);
        }

        /// <summary>
        ///     Maps the specified port to this computer
        /// </summary>
        public override IAsyncResult BeginCreatePortMap(Mapping mapping, AsyncCallback callback, object asyncState)
        {
            var message = new CreatePortMappingMessage(mapping, InternalLocalAddress, this);
            return BeginMessageInternal(message, callback, mapping, EndCreatePortMapInternal);
        }

        /// <summary>
        ///     Removes a port mapping from this computer
        /// </summary>
        public override IAsyncResult BeginDeletePortMap(Mapping mapping, AsyncCallback callback, object asyncState)
        {
            var message = new DeletePortMappingMessage(mapping, this);
            return BeginMessageInternal(message, callback, asyncState, EndDeletePortMapInternal);
        }


        public override IAsyncResult BeginGetAllMappings(AsyncCallback callback, object asyncState)
        {
            var message = new GetGenericPortMappingEntry(0, this);
            return BeginMessageInternal(message, callback, asyncState, EndGetAllMappingsInternal);
        }


        public override IAsyncResult BeginGetSpecificMapping(Protocol protocol, int port, AsyncCallback callback,
            object asyncState)
        {
            var message = new GetSpecificPortMappingEntryMessage(protocol, port, this);
            return BeginMessageInternal(message, callback, asyncState, EndGetSpecificMappingInternal);
        }

        /// <summary>
        /// </summary>
        /// <param name="result"></param>
        public override void EndCreatePortMap(IAsyncResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var mappingResult = result as PortMapAsyncResult;
            if (mappingResult == null)
                throw new ArgumentException("Invalid AsyncResult", nameof(result));

            // Check if we need to wait for the operation to finish
            if (!result.IsCompleted)
                result.AsyncWaitHandle.WaitOne();

            // If we have a saved exception, it means something went wrong during the mapping
            // so we just rethrow the exception and let the user figure out what they should do.
            if (mappingResult.SavedMessage is ErrorMessage)
            {
                var msg = mappingResult.SavedMessage as ErrorMessage;
                throw new MappingException(msg.ErrorCode, msg.Description);
            }

            //return result.AsyncState as Mapping;
        }


        /// <summary>
        /// </summary>
        /// <param name="result"></param>
        public override void EndDeletePortMap(IAsyncResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var mappingResult = result as PortMapAsyncResult;
            if (mappingResult == null)
                throw new ArgumentException("Invalid AsyncResult", nameof(result));

            // Check if we need to wait for the operation to finish
            if (!mappingResult.IsCompleted)
                mappingResult.AsyncWaitHandle.WaitOne();

            // If we have a saved exception, it means something went wrong during the mapping
            // so we just rethrow the exception and let the user figure out what they should do.
            if (mappingResult.SavedMessage is ErrorMessage)
            {
                var msg = mappingResult.SavedMessage as ErrorMessage;
                throw new MappingException(msg.ErrorCode, msg.Description);
            }

            // If all goes well, we just return
            //return true;
        }


        public override Mapping[] EndGetAllMappings(IAsyncResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var mappingResult = result as GetAllMappingsAsyncResult;
            if (mappingResult == null)
                throw new ArgumentException("Invalid AsyncResult", nameof(result));

            if (!mappingResult.IsCompleted)
                mappingResult.AsyncWaitHandle.WaitOne();

            if (mappingResult.SavedMessage is ErrorMessage)
            {
                var msg = mappingResult.SavedMessage as ErrorMessage;
                if (msg.ErrorCode != 713)
                    throw new MappingException(msg.ErrorCode, msg.Description);
            }

            return mappingResult.Mappings.ToArray();
        }


        /// <summary>
        ///     Ends an async request to get the external ip address of the router
        /// </summary>
        public override IPAddress EndGetExternalIP(IAsyncResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var mappingResult = result as PortMapAsyncResult;
            if (mappingResult == null)
                throw new ArgumentException("Invalid AsyncResult", nameof(result));

            if (!result.IsCompleted)
                result.AsyncWaitHandle.WaitOne();

            if (mappingResult.SavedMessage is ErrorMessage)
            {
                var msg = mappingResult.SavedMessage as ErrorMessage;
                throw new MappingException(msg.ErrorCode, msg.Description);
            }

            return ((GetExternalIPAddressResponseMessage) mappingResult.SavedMessage).ExternalIPAddress;
        }


        public override Mapping EndGetSpecificMapping(IAsyncResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var mappingResult = result as GetAllMappingsAsyncResult;
            if (mappingResult == null)
                throw new ArgumentException("Invalid AsyncResult", nameof(result));

            if (!mappingResult.IsCompleted)
                mappingResult.AsyncWaitHandle.WaitOne();

            if (mappingResult.SavedMessage is ErrorMessage)
            {
                var message = mappingResult.SavedMessage as ErrorMessage;
                if (message.ErrorCode != 0x2ca)
                {
                    throw new MappingException(message.ErrorCode, message.Description);
                }
            }
            if (mappingResult.Mappings.Count == 0)
                return new Mapping(Protocol.Tcp, -1, -1);

            return mappingResult.Mappings[0];
        }


        public override bool Equals(object obj)
        {
            var device = obj as UpnpNatDevice;
            return (device != null) && Equals((device));
        }

        public override int GetHashCode()
        {
            return (HostEndPoint.GetHashCode() ^ ControlUrl.GetHashCode() ^ ServiceDescriptionUrl.GetHashCode());
        }

        private IAsyncResult BeginMessageInternal(MessageBase message, AsyncCallback storedCallback, object asyncState,
            AsyncCallback callback)
        {
            byte[] body;
            var request = message.Encode(out body);
            var mappingResult = PortMapAsyncResult.Create(message, request, storedCallback, asyncState);

            if (body.Length > 0)
            {
                request.BeginGetRequestStream(delegate(IAsyncResult result)
                {
                    try
                    {
                        var s = request.EndGetRequestStream(result);
                        s.Write(body, 0, body.Length);
                        request.BeginGetResponse(callback, mappingResult);
                    }
                    catch (Exception ex)
                    {
                        mappingResult.Complete(ex);
                    }
                }, null);
            }
            else
            {
                request.BeginGetResponse(callback, mappingResult);
            }
            return mappingResult;
        }

        private void CompleteMessage(IAsyncResult result)
        {
            var mappingResult = result.AsyncState as PortMapAsyncResult;
            if (mappingResult != null)
            {
                mappingResult.CompletedSynchronously = result.CompletedSynchronously;
                mappingResult.Complete();
            }
        }

        private MessageBase DecodeMessageFromResponse(Stream s, long length)
        {
            var data = new StringBuilder();
            int bytesRead;
            var totalBytesRead = 0;
            var buffer = new byte[10240];

            // Read out the content of the message, hopefully picking everything up in the case where we have no contentlength
            if (length != -1)
            {
                while (totalBytesRead < length)
                {
                    bytesRead = s.Read(buffer, 0, buffer.Length);
                    data.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    totalBytesRead += bytesRead;
                }
            }
            else
            {
                while ((bytesRead = s.Read(buffer, 0, buffer.Length)) != 0)
                    data.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }

            // Once we have our content, we need to see what kind of message it is. It'll either a an error
            // or a response based on the action we performed.
            return MessageBase.Decode(this, data.ToString());
        }

        private void EndCreatePortMapInternal(IAsyncResult result)
        {
            EndMessageInternal(result);
            CompleteMessage(result);
        }

        private void EndMessageInternal(IAsyncResult result)
        {
            HttpWebResponse response = null;
            var mappingResult = (PortMapAsyncResult) result.AsyncState;

            try
            {
                try
                {
                    response = (HttpWebResponse) mappingResult.Request.EndGetResponse(result);
                }
                catch (WebException ex)
                {
                    // Even if the request "failed" i want to continue on to read out the response from the router
                    response = ex.Response as HttpWebResponse;
                    if (response == null)
                        mappingResult.SavedMessage = new ErrorMessage((int) ex.Status, ex.Message);
                }
                if (response != null)
                    mappingResult.SavedMessage = DecodeMessageFromResponse(response.GetResponseStream(),
                        response.ContentLength);
            }

            finally
            {
                response?.Dispose();
            }
        }

        private void EndDeletePortMapInternal(IAsyncResult result)
        {
            EndMessageInternal(result);
            CompleteMessage(result);
        }

        private void EndGetAllMappingsInternal(IAsyncResult result)
        {
            EndMessageInternal(result);

            var mappingResult = (GetAllMappingsAsyncResult) result.AsyncState;
            var message = mappingResult.SavedMessage as GetGenericPortMappingEntryResponseMessage;
            if (message != null)
            {
                var mapping = new Mapping(message.Protocol, message.InternalPort, message.ExternalPort,
                    message.LeaseDuration) {Description = message.PortMappingDescription};
                mappingResult.Mappings.Add(mapping);
                var next = new GetGenericPortMappingEntry(mappingResult.Mappings.Count, this);

                // It's ok to do this synchronously because we should already be on anther thread
                // and this won't block the user.
                byte[] body;
                var request = next.Encode(out body);
                if (body.Length > 0)
                {
                    request.GetRequestStreamAsync().Result.Write(body, 0, body.Length);
                }
                mappingResult.Request = request;
                request.BeginGetResponse(EndGetAllMappingsInternal, mappingResult);
                return;
            }

            CompleteMessage(result);
        }

        private void EndGetExternalIPInternal(IAsyncResult result)
        {
            EndMessageInternal(result);
            CompleteMessage(result);
        }

        private void EndGetSpecificMappingInternal(IAsyncResult result)
        {
            EndMessageInternal(result);

            var mappingResult = (GetAllMappingsAsyncResult) result.AsyncState;
            var message = mappingResult.SavedMessage as GetGenericPortMappingEntryResponseMessage;
            if (message != null)
            {
                var mapping = new Mapping(mappingResult.SpecificMapping.Protocol, message.InternalPort,
                    mappingResult.SpecificMapping.PublicPort, message.LeaseDuration)
                {
                    Description = mappingResult.SpecificMapping.Description
                };
                mappingResult.Mappings.Add(mapping);
            }

            CompleteMessage(result);
        }

        internal void GetServicesList(NatDeviceCallback callback)
        {
            // Save the callback so i can use it again later when i've finished parsing the services available
            _callback = callback;

            // Create a HTTPWebRequest to download the list of services the device offers
            byte[] body;
            var request = new GetServicesMessage(ServiceDescriptionUrl, HostEndPoint).Encode(out body);
            if (body.Length > 0)
                Debug.WriteLine("Error: Services Message contained a body");
            request.BeginGetResponse(ServicesReceived, request);
        }

        private void ServicesReceived(IAsyncResult result)
        {
            HttpWebResponse response = null;
            try
            {
                var abortCount = 0;
                var buffer = new byte[10240];
                var servicesXml = new StringBuilder();
                XDocument xmldoc;
                var request = (HttpWebRequest) result.AsyncState;
                response = (HttpWebResponse) request.EndGetResponse(result);
                var s = response.GetResponseStream();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Debug.WriteLine("{0}: Couldn't get services list: {1}", HostEndPoint, response.StatusCode);
                    return; // FIXME: This the best thing to do??
                }

                while (true)
                {
                    var bytesRead = s.Read(buffer, 0, buffer.Length);
                    servicesXml.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    try
                    {
                        xmldoc = XDocument.Parse(servicesXml.ToString());
                        response.Dispose();
                        break;
                    }
                    catch (XmlException)
                    {
                        // If we can't receive the entire XML within 500ms, then drop the connection
                        // Unfortunately not all routers supply a valid ContentLength (mine doesn't)
                        // so this hack is needed to keep testing our recieved data until it gets successfully
                        // parsed by the xmldoc. Without this, the code will never pick up my router.
                        if (abortCount++ > 50)
                        {
                            response.Dispose();
                            return;
                        }
                        Debug.WriteLine("{0}: Couldn't parse services list", HostEndPoint);
                        Task.Delay(10).Wait();
                    }
                }

                Debug.WriteLine("{0}: Parsed services list", HostEndPoint);

                var ns = (XNamespace)"urn:schemas-upnp-org:device-1-0";

                foreach (var node in xmldoc.Descendants(ns + "serviceList"))
                {
                    //Go through each service there
                    foreach (var service in node.Elements())
                    {
                        //If the service is a WANIPConnection, then we have what we want
                        var type = service.Element(ns + "serviceType").Value;
                        Debug.WriteLine("{0}: Found service: {1}", HostEndPoint, type);
                        var c = StringComparison.OrdinalIgnoreCase;
                        if (type.Equals(ServiceType, c))
                        {
                            ControlUrl = service.Element(ns + "controlURL").Value;
                            Debug.WriteLine("{0}: Found upnp service at: {1}", HostEndPoint, ControlUrl);
                            try
                            {
                                var u = new Uri(ControlUrl, UriKind.RelativeOrAbsolute);
                                if (u.IsAbsoluteUri)
                                {
                                    var old = HostEndPoint;
                                    HostEndPoint = new IPEndPoint(IPAddress.Parse(u.Host), u.Port);
                                    Debug.WriteLine("{0}: Absolute URI detected. Host address is now: {1}", old,
                                        HostEndPoint);
                                    ControlUrl = u.PathAndQuery;
                                    Debug.WriteLine("{0}: New control url: {1}", HostEndPoint, ControlUrl);
                                }
                            }
                            catch
                            {
                                Debug.WriteLine("{0}: Assuming control Uri is relative: {1}", HostEndPoint, ControlUrl);
                            }
                            Debug.WriteLine("{0}: Handshake Complete", HostEndPoint);
                            _callback(this);
                            return;
                        }
                    }
                }

                //If we get here, it means that we didn't get WANIPConnection service, which means no uPnP forwarding
                //So we don't invoke the callback, so this device is never added to our lists
            }
            catch (WebException ex)
            {
                // Just drop the connection, FIXME: Should i retry?
                Debug.WriteLine("{0}: Device denied the connection attempt: {1}", HostEndPoint, ex);
            }
            finally
            {
                response?.Dispose();
            }
        }

        /// <summary>
        ///     Overridden.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            //GetExternalIP is blocking and can throw exceptions, can't use it here.
            return
                $"UpnpNatDevice - EndPoint: {HostEndPoint}, External IP: {"Manually Check"}, Control Url: {ControlUrl}, Service Description Url: {ServiceDescriptionUrl}, Service Type: {ServiceType}, Last Seen: {LastSeen}";
        }
    }
}