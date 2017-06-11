using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PersistenceList
{
    [Serializable][ProtoContract][DataContract]
    public class Customer
    {
        [ProtoMember(1), DataMember]public Guid Id { get; set; }
        [ProtoMember(2), DataMember]public string FirstName { get; set; }
        [ProtoMember(3), DataMember]public string LastName { get; set; }
        [ProtoMember(4), DataMember]public string Company { get; set; }
        [ProtoMember(5), DataMember]public string Address { get; set; }
        [ProtoMember(6), DataMember]public string City { get; set; }
        [ProtoMember(7), DataMember]public string State { get; set; }
        [ProtoMember(8), DataMember]public string ZipCode { get; set; }
        [ProtoMember(9), DataMember]public string PhoneNumber { get; set; }
        [ProtoMember(10), DataMember]public string EmailAddress { get; set; }
        [ProtoMember(11), DataMember]public DateTime Date { get; set; }
        [ProtoMember(12), DataMember]public string UserFileName { get; set; }
        [ProtoMember(13), DataMember]public long Position { get; set; }
        [ProtoMember(14), DataMember]public int Random { get; set; }
        [ProtoMember(15), DataMember]public byte[] Image { get; set; }
    }
}