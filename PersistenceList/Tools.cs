using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceList
{
    public static class Tools
    {
        public static dynamic ToDynamic(this object obj)
        {
            if (object.ReferenceEquals(obj, null)) return null;
            if (obj is System.Dynamic.ExpandoObject) return obj;
            else if (obj is IDictionary<string, object>)
            {
                var r = (IDictionary<string, object>)(new System.Dynamic.ExpandoObject());
                foreach (var e in (IDictionary<string, object>)obj) r.Add(e.Key, e.Value);
                return r;
            }
            return obj;
        }

        public static TimeSpan TimeProfiler(Action execute)
        {
            if (execute == null) throw new ArgumentNullException();
            var t = System.Diagnostics.Stopwatch.StartNew();
            execute(); t.Stop();
            return t.Elapsed;
        }

        public static Tuple<TimeSpan, T> TimeProfiler<T>(Func<T> execute)
        {
            if (execute == null) throw new ArgumentNullException();
            var t = System.Diagnostics.Stopwatch.StartNew();
            var value = execute(); t.Stop();
            return Tuple.Create(t.Elapsed, value);
        }

        private readonly static Random random = new Random();
        //https://www.codeproject.com/articles/16583/generate-an-image-with-a-random-number
        public static Bitmap RandomImage(ref int? number)
        {
            // Create a Bitmap image
            Bitmap imageSrc = new Bitmap(155, 85);

            // Fill it randomly with white pixels
            for (int iX = 0; iX <= imageSrc.Width - 1; iX++)
            {
                for (int iY = 0; iY <= imageSrc.Height - 1; iY++)
                {
                    if (random.Next(10) > 5)
                    {
                        imageSrc.SetPixel(iX, iY, Color.White);
                    }
                }
            }

            // Create an ImageGraphics Graphics object from bitmap Image
            using (Graphics imageGraphics = Graphics.FromImage(imageSrc))
            {
                // Generate random code. 
                if (!number.HasValue) number = random.Next(10000000);
                string hiddenCode = number.Value.ToString();

                // Draw random code within Image
                using (Font drawFont = new Font("Arial", 20, FontStyle.Italic))
                {
                    using (SolidBrush drawBrush = new SolidBrush(Color.Black))
                    {
                        float x = (float)(5.0 + (random.NextDouble() * (imageSrc.Width - 120)));
                        float y = (float)(5.0 + (random.NextDouble() * (imageSrc.Height - 30)));
                        StringFormat drawFormat = new StringFormat();
                        imageGraphics.DrawString(hiddenCode, drawFont, drawBrush, x, y, drawFormat);
                    }
                }
            }
            return imageSrc;
        }

        public static byte[] RandomImage(ref int? number, System.Drawing.Imaging.ImageFormat format)
        {
            using (var ms = new MemoryStream())
            {
                using (var imageSrc = RandomImage(ref number))
                {
                    imageSrc.Save(ms, format);
                }
                return ms.ToArray();
            }
        }

        #region SOAP/XML Serialization

        public static string SerializeException(this Exception ex)
        {
            return SerializeSOAP(ex) ?? string.Empty;
        }

        public static string SerializeSOAP(this object obj)
        {
            if (obj == null) return null;
            var formatter = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter(null,
                new StreamingContext(StreamingContextStates.Persistence));
            using (var sm = new System.IO.MemoryStream())
            {
                formatter.Serialize(sm, obj);
                sm.Position = 0;
                using (var sr = new System.IO.StreamReader(sm))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static object DeserializeSOAP(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return null;
            var formatter = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter(null,
                new StreamingContext(StreamingContextStates.Persistence));
            using (var sm = new System.IO.MemoryStream())
            {
                using (var sw = new System.IO.StreamWriter(sm))
                {
                    sw.Write(obj);
                    sw.Flush();
                    sm.Position = 0;
                    return formatter.Deserialize(sm);
                }
            }
        }

        public static string SerializeXML(this object obj)
        {
            if (obj == null) return string.Empty;
            var formatter = new System.Xml.Serialization.XmlSerializer(obj.GetType());
            using (var sm = new StringWriter())
            {
                formatter.Serialize(sm, obj);
                sm.Flush();
                return sm.ToString();
            }
        }

        public static T DeserializeXML<T>(this string obj)
        {
            return (T)DeserializeXML(obj, typeof(T));
        }

        //https://stackoverflow.com/questions/325426/programmatic-equivalent-of-defaulttype
        public static object GetDefaultValue(Type t)
        {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                return null;
            }
        }

        public static object DeserializeXML(this string obj, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(obj)) return GetDefaultValue(type);
            var formatter = new System.Xml.Serialization.XmlSerializer(type);
            using (var sm = new StringReader(obj))
            {
                return formatter.Deserialize(sm);
            }
        }

        public static string SerializeWCF<T>(this T obj)
        {
            if (obj == null) return string.Empty;
            var formatter = new System.Runtime.Serialization.DataContractSerializer(obj.GetType());
            using (var sm = new StringWriter())
            {
                using (var sx = new System.Xml.XmlTextWriter(sm))
                {
                    formatter.WriteObject(sx, obj);
                    sx.Flush();
                }
                return sm.ToString();
            }
        }

        public static T DeserializeWCF<T>(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return default(T);
            var formatter = new System.Runtime.Serialization.DataContractSerializer(typeof(T));
            using (var sm = new System.Xml.XmlTextReader(new StringReader(obj)))
            {
                return (T)formatter.ReadObject(sm);
            }
        }

        public static string SerializeJSON<T>(this T obj)
        {
            if (obj == null) return string.Empty;
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.RecursionLimit = serializer.MaxJsonLength = int.MaxValue;
            return serializer.Serialize(obj);
        }

        public static T DeserializeJSON<T>(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return default(T);
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.RecursionLimit = serializer.MaxJsonLength = int.MaxValue;
            return serializer.Deserialize<T>(obj);
        }

        public static dynamic DeserializeJSON(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return null;
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.RecursionLimit = serializer.MaxJsonLength = int.MaxValue;
            object values = serializer.DeserializeObject(obj);
            return ToDynamic(values);
        }

        public static string SerializePB<T>(this T obj)
        {
            if (obj == null) return string.Empty;
            var formatter = ProtoBuf.Serializer.CreateFormatter<T>();
            using (var sm = new System.IO.MemoryStream())
            {
                formatter.Serialize(sm, obj);
                sm.Position = 0;
                return Convert.ToBase64String(sm.ToArray());
            }
        }

        public static T DeserializePF<T>(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return default(T);
            var formatter = ProtoBuf.Serializer.CreateFormatter<T>();
            using (var sm = new System.IO.MemoryStream(Convert.FromBase64String(obj)))
            {
                sm.Position = 0;
                return (T)formatter.Deserialize(sm);
            }
        }

        #endregion SOAP/XML Serialization

    }
}
