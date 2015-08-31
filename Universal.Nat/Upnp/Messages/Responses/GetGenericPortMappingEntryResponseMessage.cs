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
using System.Net;
using System.Xml.Linq;
using Universal.Nat.Enums;

namespace Universal.Nat.Upnp.Messages.Responses
{
    internal class GetGenericPortMappingEntryResponseMessage : MessageBase
    {
        public GetGenericPortMappingEntryResponseMessage(XNamespace ns, XContainer container, bool genericMapping)
            : base(null)
        {
            RemoteHost = (genericMapping) ? container.Element(ns + "NewRemoteHost").Value : string.Empty;
            ExternalPort = (genericMapping)
                ? Convert.ToInt32(container.Element(ns + "NewExternalPort").Value)
                : -1;
            if (genericMapping)
                Protocol = container.Element(ns + "NewProtocol")
                    .Value.Equals("TCP", StringComparison.OrdinalIgnoreCase)
                    ? Protocol.Tcp
                    : Protocol.Udp;
            else
                Protocol = Protocol.Udp;

            InternalPort = Convert.ToInt32(container.Element(ns + "NewInternalPort").Value);
            InternalClient = container.Element(ns + "NewInternalClient").Value;
            Enabled = container.Element(ns + "NewEnabled").Value == "1";
            PortMappingDescription = container.Element(ns + "NewPortMappingDescription").Value;
            LeaseDuration = Convert.ToInt32(container.Element(ns + "NewLeaseDuration").Value);
        }

        public string RemoteHost { get; }

        public int ExternalPort { get; }

        public Protocol Protocol { get; }

        public int InternalPort { get; }

        public string InternalClient { get; }

        public bool Enabled { get; }

        public string PortMappingDescription { get; }

        public int LeaseDuration { get; }

        public override WebRequest Encode(out byte[] body)
        {
            throw new NotImplementedException();
        }
    }
}