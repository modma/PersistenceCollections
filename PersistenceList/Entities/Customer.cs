using Bond;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PersistenceList
{
    [Serializable][ProtoContract][DataContract][Schema]
    public class Customer
    {
        [ProtoMember(1), DataMember, Id(1)]public Guid Id { get; set; }
        [ProtoMember(2), DataMember, Id(2)] public string FirstName { get; set; }
        [ProtoMember(3), DataMember, Id(3)] public string LastName { get; set; }
        [ProtoMember(4), DataMember, Id(4)] public string Company { get; set; }
        [ProtoMember(5), DataMember, Id(5)] public string Address { get; set; }
        [ProtoMember(6), DataMember, Id(6)] public string City { get; set; }
        [ProtoMember(7), DataMember, Id(7)] public string State { get; set; }
        [ProtoMember(8), DataMember, Id(8)] public string ZipCode { get; set; }
        [ProtoMember(9), DataMember, Id(9)] public string PhoneNumber { get; set; }
        [ProtoMember(10), DataMember, Id(10)] public string EmailAddress { get; set; }
        [ProtoMember(11), DataMember, Id(11)] public DateTime Date { get; set; }
        [ProtoMember(12), DataMember, Id(12)] public string UserFileName { get; set; }
        [ProtoMember(13), DataMember, Id(13)] public long Position { get; set; }
        [ProtoMember(14), DataMember, Id(14)] public int Random { get; set; }
        [ProtoMember(15), DataMember, Id(15)] public byte[] Image { get; set; }
    }
}