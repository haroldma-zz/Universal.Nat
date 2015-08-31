using System;
using Torrent.Uwp.Nat.Enums;

namespace Torrent.Uwp.Nat
{
    public class Mapping
    {
        public Mapping(Protocol protocol, int privatePort, int publicPort)
            : this(protocol, privatePort, publicPort, 0)
        {
        }

        public Mapping(Protocol protocol, int privatePort, int publicPort, int lifetime)
        {
            Protocol = protocol;
            PrivatePort = privatePort;
            PublicPort = publicPort;
            Lifetime = lifetime;

            switch (lifetime)
            {
                case int.MaxValue:
                    Expiration = DateTime.MaxValue;
                    break;
                case 0:
                    Expiration = DateTime.Now;
                    break;
                default:
                    Expiration = DateTime.Now.AddSeconds(lifetime);
                    break;
            }
        }

        public string Description { get; set; }

        public Protocol Protocol { get; internal set; }

        public int PrivatePort { get; internal set; }

        public int PublicPort { get; internal set; }

        public int Lifetime { get; internal set; }

        public DateTime Expiration { get; internal set; }

        public bool IsExpired()
        {
            return Expiration < DateTime.Now;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Mapping;
            return other != null && (Protocol == other.Protocol &&
                                     PrivatePort == other.PrivatePort && PublicPort == other.PublicPort);
        }

        public override int GetHashCode()
        {
            return Protocol.GetHashCode() ^ PrivatePort.GetHashCode() ^ PublicPort.GetHashCode();
        }

        public override string ToString()
        {
            return
                $"Protocol: {Protocol}, Public Port: {PublicPort}, Private Port: {PrivatePort}, Description: {Description}, Expiration: {Expiration}, Lifetime: {Lifetime}";
        }
    }
}