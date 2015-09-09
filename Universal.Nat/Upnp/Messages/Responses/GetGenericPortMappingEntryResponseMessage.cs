//
// Authors:
//   Alan McGovern  alan.mcgovern@gmail.com
//   Lucas Ontivero lucas.ontivero@gmail.com
//
// Copyright (C) 2006 Alan McGovern
// Copyright (C) 2014 Lucas Ontivero
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
using System.Xml.Linq;
using Universal.Nat.Enums;

namespace Universal.Nat.Upnp.Messages.Responses
{
    internal class GetPortMappingEntryResponseMessage : ResponseMessageBase
    {
        internal GetPortMappingEntryResponseMessage(XDocument response, string serviceType, bool genericMapping)
            : base(response, serviceType, genericMapping ? "GetGenericPortMappingEntryResponseMessage" : "GetSpecificPortMappingEntryResponseMessage")
        {
            var data = GetNode();

            RemoteHost = (genericMapping) ? data.Element("NewRemoteHost").Value : string.Empty;
            ExternalPort = (genericMapping) ? Convert.ToInt32(data.Element("NewExternalPort").Value) : ushort.MaxValue;
            if (genericMapping)
                Protocol = data.Element("NewProtocol").Value.Equals("TCP", StringComparison.OrdinalIgnoreCase)
                               ? Protocol.Tcp
                               : Protocol.Udp;
            else
                Protocol = Protocol.Udp;

            InternalPort = Convert.ToInt32(data.Element("NewInternalPort").Value);
            InternalClient = data.Element("NewInternalClient").Value;
            Enabled = data.Element("NewEnabled").Value == "1";
            PortMappingDescription = data.Element("NewPortMappingDescription").Value;
            LeaseDuration = Convert.ToInt32(data.Element("NewLeaseDuration").Value);
        }

        public string RemoteHost { get; private set; }
        public int ExternalPort { get; private set; }
        public Protocol Protocol { get; private set; }
        public int InternalPort { get; private set; }
        public string InternalClient { get; private set; }
        public bool Enabled { get; private set; }
        public string PortMappingDescription { get; private set; }
        public int LeaseDuration { get; private set; }
    }
}