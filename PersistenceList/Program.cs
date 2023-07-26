using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceList
{
    class Program
    {
        const int TotalDataToGenerate = 100000;

        static void Main(string[] args)
        {
            var byteFill = new byte[4096];
            byteFill.Memset(255);
            byteFill.Memzero();
            byteFill.Memset(16, 127, 256);
            byteFill.Memzero(32, 128);

            var testInstance = ProxyGeneratorAttributePropertyTest.CreateClassDecoratedWithAttribute<CustomerFlat>();
            var serialiced = FakeCustomerDataRepository.New(0).SerializeXML().Replace("Customer", testInstance.GetType().Name);
            testInstance = (CustomerFlat)serialiced.DeserializeXML(testInstance.GetType());//to use proxy type
            var asd1 = testInstance.SerializePB();
            var asd2 = testInstance.SerializeXML();
            var asd3 = testInstance.SerializeJSON();
            //var asd4 = testInstance.SerializeWCF();
            var testMetatype = PersistenceListProtoBuffer<CustomerFlat>.MetaDecorator();
            var testMetatype2 = PersistenceListProtoBuffer<Activator>.MetaDecorator();
            var testMetatype3 = PersistenceListProtoBuffer<Tuple<int, string>>.MetaDecorator();

            using (var test = new PersistenceDictionary<Guid, Customer>(new PersistenceListProtoBuffer<Customer>(compression: CompressionMode.LZ4Fast)))
            {
                const int testIndex = 333;
                var time1 = Tools.TimeProfiler(() => test.AddRange(FakeCustomerDataRepository.GetCustomers(TotalDataToGenerate, true), e => e.Id, e => e, false));
                var replacer = Tools.TimeProfiler(() => test[test.GetKeyByIndex(testIndex)] = FakeCustomerDataRepository.New(-1)); //this update destroy correlation to 1
                var time2 = Tools.TimeProfiler(() => test.All(e => true));
                //var taker = Tools.TimeProfiler(() => test.OrderBy(e => e.Value.Position).Take(100).ToArray());
                var verify1 = Tools.TimeProfiler(() => test.IndexedItems().Where(e => e.Item1 != e.Item3.Position).Count());
                var verify2 = Tools.TimeProfiler(() => test.Where(e => e.Key != e.Value.Id).Count());
                var retrieve = Tools.TimeProfiler(() => test.GetValueByIndex(testIndex));
                var retrieveNum = Tools.TimeProfiler(() => test.GetIndexByKey(retrieve.Item2.Id));
                var time3 = Tools.TimeProfiler(() => test.Remove(retrieve.Item2.Id));
                var retrieve2 = Tools.TimeProfiler(() => test.GetItemByIndex(testIndex));
                var time4 = Tools.TimeProfiler(() => test.RemoveAt(testIndex));
                var nEntry = FakeCustomerDataRepository.New(TotalDataToGenerate);
                var insert = Tools.TimeProfiler(() => test.Add(nEntry.Id, nEntry));
                var verify3 = Tools.TimeProfiler(() => test.Where(e => e.Key != e.Value.Id).Count());
                var verify4 = Tools.TimeProfiler(() => test.Values.Average(e => e.Random));
                var verify5 = Tools.TimeProfiler(() => test.Average(e => e.Value.Random));
            }
            GC.Collect();

            var t0 = Stopwatch.StartNew();
            var dataBuffer = FakeCustomerDataRepository.GetCustomers(TotalDataToGenerate).ToList();
            t0.Stop();
            Console.WriteLine("data: " + t0.ElapsedMilliseconds);

            Stopwatch tX = null;
            var t1 = Stopwatch.StartNew();
            using (var test = new PersistenceListProtoBuffer<Customer>())
            {
                using (var buffer = test.MakeBufferedAdd())
                {
                    foreach (var e in dataBuffer) buffer.Add(e);
                }
                var insert = Tools.TimeProfiler(() => test.Insert(15, FakeCustomerDataRepository.New(TotalDataToGenerate)));
                tX = Stopwatch.StartNew();
                var renderList = test.ToList();
                tX.Stop();
            }
            t1.Stop();
            Console.WriteLine("buffered: " + (t1.ElapsedMilliseconds - tX.ElapsedMilliseconds));
            Console.WriteLine("renderList: " + tX.ElapsedMilliseconds);

            var t2 = Stopwatch.StartNew();
            using (var test = new PersistenceListProtoBuffer<Customer>())
            {
                test.AddRange(dataBuffer, false);
                test.Add(null);
                //var result = test.ToList();
                //Console.WriteLine(result.Count);
                int num1 = test.IndexOf(test[3]);
                int num2 = test.IndexOf(null);

                bool test1 = test.Contains(test[3]);
                bool test2 = test.Contains(null);

                var test3 = test.Where(e => e.FirstName.StartsWith("A")).Skip(1000).Take(1000).ToList();
            }
            t2.Stop();
            Console.WriteLine("chunk: " + t2.ElapsedMilliseconds);

            var t3 = Stopwatch.StartNew();
            using (var test = new PersistenceListProtoBuffer<Customer>())
            {
                test.AddRange(dataBuffer, true);
                test.Add(null);
                //var result = test.ToList();
                //Console.WriteLine(result.Count);
                int num1 = test.IndexOf(test[3]);
                int num2 = test.IndexOf(null);
                
                bool test1 = test.Contains(test[3]);
                bool test2 = test.Contains(null);

                var test3 = test.Where(e=>e.FirstName.StartsWith("A")).Skip(1000).Take(1000).ToList();                
            }
            t3.Stop();
            Console.WriteLine("size: " + t3.ElapsedMilliseconds);

            var t4 = Stopwatch.StartNew();
            using (var test = new PersistenceListProtoBuffer<Customer>())
            {
                test.AddRangeAsync(dataBuffer, false).Wait();
                test.Add(null);
                //var result = test.ToList();
                //Console.WriteLine(result.Count);
                int num1 = test.IndexOf(test[3]);
                int num2 = test.IndexOf(null);

                bool test1 = test.Contains(test[3]);
                bool test2 = test.Contains(null);

                var test3 = test.Where(e => e.FirstName.StartsWith("A")).Skip(1000).Take(1000).ToList();
            }
            t4.Stop();
            Console.WriteLine("chuck async: " + t4.ElapsedMilliseconds);

            var t5 = Stopwatch.StartNew();
            using (var test = new PersistenceListProtoBuffer<Customer>())
            {
                test.AddRangeAsync(dataBuffer, true).Wait();
                test.Add(null);
                //var result = test.ToList();
                //Console.WriteLine(result.Count);
                int num1 = test.IndexOf(test[3]);
                int num2 = test.IndexOf(null);

                bool test1 = test.Contains(test[3]);
                bool test2 = test.Contains(null);

                var test3 = test.Where(e => e.FirstName.StartsWith("A")).Skip(1000).Take(1000).ToList();
            }
            t5.Stop();
            Console.WriteLine("size async: " + t5.ElapsedMilliseconds);

            var t6 = Stopwatch.StartNew();
            using (var test = new PersistenceListBSON<Customer>())
            {
                test.AddRange(dataBuffer);
                test.Add(null);
                //var result = test.ToList();
                //Console.WriteLine(result.Count);
                int num1 = test.IndexOf(test[3]);
                int num2 = test.IndexOf(null);

                bool test1 = test.Contains(test[3]);
                bool test2 = test.Contains(null);

                var test3 = test.Where(e => e.FirstName.StartsWith("A")).Skip(1000).Take(1000).ToList();
            }
            t6.Stop();
            Console.WriteLine("BSON: " + t6.ElapsedMilliseconds);

            Console.WriteLine("terminado pulse tecla");
            Console.ReadKey();
        }

    }

}
