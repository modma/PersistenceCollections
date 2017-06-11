using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;

namespace PersistenceList
{
    public class FakeCustomerDataRepository
    {
        private readonly static long minD = DateTime.MinValue.Ticks;
        private readonly static long maxD = DateTime.MaxValue.Ticks;
        private readonly static Random random = new Random();

        public static IEnumerable<Customer> GetCustomers(int count, bool createBitmap = false)
        {
            //no use ordered if not enable compression
            return Enumerable.Range(0, count).AsParallel().AsOrdered().WithMergeOptions(ParallelMergeOptions.NotBuffered).Select(e => New(e, createBitmap));
        }

        public static Customer New(long pos, bool createBitmap = false)
        {
            var num = (int?)pos;
            return new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = Faker.Name.First(),
                LastName = Faker.Name.Last(),
                Company = Faker.Company.Name(),
                Address = Faker.Address.StreetAddress(),
                City = Faker.Address.City(),
                State = Faker.Address.UsStateAbbr(),
                ZipCode = Faker.Address.ZipCode(),
                PhoneNumber = Faker.Phone.Number(),
                EmailAddress = Faker.Internet.Email(),
                Date = new DateTime(LongRandom(random, minD, maxD)),
                UserFileName = System.IO.Path.GetRandomFileName(),
                Position = pos,
                Random = random.Next(),
                Image = createBitmap ? Tools.RandomImage(ref num, System.Drawing.Imaging.ImageFormat.Bmp) : null
        };
        }

        private static long LongRandom(Random rand)
        {
            var buffer = new byte[sizeof(Int64)];
            rand.NextBytes(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }

        private static long LongRandom(Random rand, long min, long max)
        {
            long longRand = LongRandom(rand);
            return (Math.Abs(longRand % (max - min)) + min);
        }


    }
}