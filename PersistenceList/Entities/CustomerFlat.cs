using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersistenceList
{
    public class CustomerFlat
    {
        public virtual Guid Id { get; set; }
        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }
        public virtual string Company { get; set; }
        public virtual string Address { get; set; }
        public virtual string City { get; set; }
        public virtual string State { get; set; }
        public virtual string ZipCode { get; set; }
        public virtual string PhoneNumber { get; set; }
        public virtual string EmailAddress { get; set; }
        public virtual DateTime Date { get; set; }
        public virtual string UserFileName { get; set; }
        public virtual long Position { get; set; }
        public virtual int Random { get; set; }
        public virtual byte[] Image { get; set; }
    }
}