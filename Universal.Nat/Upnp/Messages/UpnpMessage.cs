//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Universal.Nat.Upnp.Messages.Responses;

namespace Universal.Nat.Upnp.Messages
{
    internal abstract class MessageBase
    {
        internal static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        protected UpnpNatDevice Device;

        protected MessageBase(UpnpNatDevice device)
        {
            Device = device;
        }

        protected WebRequest CreateRequest(string upnpMethod, string methodParameters, out byte[] body)
        {
            var ss = "http://" + Device.HostEndPoint + Device.ControlUrl;
            Debug.WriteLine("Initiating request to: {0}", ss);
            var location = new Uri(ss);

            var req = (HttpWebRequest) WebRequest.Create(location);
            //req.KeepAlive = false;
            req.Method = "POST";
            req.ContentType = "text/xml; charset=\"utf-8\"";
            req.Headers["SOAPACTION"] = "\"" + Device.ServiceType + "#" + upnpMethod + "\"";

            var bodyString = "<s:Envelope "
                             + "xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" "
                             + "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">"
                             + "<s:Body>"
                             + "<u:" + upnpMethod + " "
                             + "xmlns:u=\"" + Device.ServiceType + "\">"
                             + methodParameters
                             + "</u:" + upnpMethod + ">"
                             + "</s:Body>"
                             + "</s:Envelope>\r\n\r\n";

            body = Encoding.UTF8.GetBytes(bodyString);
            return req;
        }

        public static MessageBase Decode(UpnpNatDevice device, string message)
        {
            var xdocument = XDocument.Parse(message);
            XNamespace xnamespace1 = "urn:schemas-upnp-org:control-1-0";
            XElement element;
            // Check to see if we have a fault code message.
            if ((element = xdocument.Descendants(xnamespace1 + "UPnPError").FirstOrDefault()) != null)
                return
                    new ErrorMessage(
                        Convert.ToInt32(element.Element(xnamespace1 + "errorCode").Value, CultureInfo.InvariantCulture),
                        element.Element(xnamespace1 + "errorDescription").Value);

            XNamespace xnamespace2 = device.ServiceType;
            var defaultNamespace = xdocument.Root.GetDefaultNamespace();

            if ((element = xdocument.Descendants(xnamespace2 + "AddPortMappingResponse").FirstOrDefault()) != null)
                return new CreatePortMappingResponseMessage();

            if ((element = xdocument.Descendants(xnamespace2 + "DeletePortMappingResponse").FirstOrDefault()) != null)
                return new DeletePortMapResponseMessage();

            if ((element = xdocument.Descendants(xnamespace2 + "GetExternalIPAddressResponse").FirstOrDefault()) != null)
                return
                    new GetExternalIPAddressResponseMessage(
                        element.Element(defaultNamespace + "NewExternalIPAddress").Value);

            if ((element = xdocument.Descendants(xnamespace2 + "GetGenericPortMappingEntryResponse").FirstOrDefault()) !=
                null)
                return new GetGenericPortMappingEntryResponseMessage(defaultNamespace, element, true);

            if ((element = xdocument.Descendants(xnamespace2 + "GetSpecificPortMappingEntryResponse").FirstOrDefault()) !=
                null)
                return new GetGenericPortMappingEntryResponseMessage(defaultNamespace, element, false);

            Debug.WriteLine("Unknown message returned. Please send me back the following XML:");
            Debug.WriteLine(message);
            return null;
        }

        public abstract WebRequest Encode(out byte[] body);

        internal static void WriteFullElement(XmlWriter writer, string element, string value)
        {
            writer.WriteStartElement(element);
            writer.WriteString(value);
            writer.WriteEndElement();
        }

        internal static XmlWriter CreateWriter(StringBuilder sb)
        {
            var settings = new XmlWriterSettings();
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            return XmlWriter.Create(sb, settings);
        }
    }
}