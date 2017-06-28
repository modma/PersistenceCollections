using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class DynamicTuple : DynamicObject, ISerializable, IDisposable
    {
        public static dynamic Do(IList properties) => new DynamicTuple(properties);

        const string prefix = "Item";
        IList _dataList;

        private DynamicTuple()
        {
            _dataList = new ArrayList();
        }

        public DynamicTuple(IList data)
        {
            if (data == null) throw new ArgumentNullException();
            _dataList = data;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            if (_dataList == null) throw new ObjectDisposedException(nameof(_dataList));
            return Enumerable.Range(1, _dataList.Count + 1).Select(i => prefix + i);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) => TryGetMember(binder.Name, out result);

        private bool TryGetMember(string name, out object result)
        {
            if (_dataList == null) throw new ObjectDisposedException(nameof(_dataList));
            int pos;
            if (name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out pos) && pos > 0 && pos <= _dataList.Count)
            {
                result = _dataList[--pos];
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public override bool TrySetMember(SetMemberBinder binder, object value) => TrySetMember(binder.Name, value);

        private bool TrySetMember(string name, object value)
        {
            if (_dataList == null) throw new ObjectDisposedException(nameof(_dataList));
            int pos;
            if (name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out pos) && pos > 0 && pos <= _dataList.Count)
            {
                _dataList[--pos] = value;
                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (_dataList == null) throw new ObjectDisposedException(nameof(_dataList));
            if (indexes.Length != 1 || object.ReferenceEquals(indexes[0], null)) return false;
            int index;
            if (TryCastInt(indexes[0], out index))
            {
                if (index < 0 || index >= _dataList.Count) return false;
                _dataList[index] = value;
                return true;
            }
            return TrySetMember(indexes[0].ToString(), value);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (_dataList == null) throw new ObjectDisposedException(nameof(_dataList));
            if (indexes.Length != 1 || object.ReferenceEquals(indexes[0], null)) { result = null; return false; }
            int index;
            if (TryCastInt(indexes[0], out index))
            {
                if (index < 0 || index >= _dataList.Count) { result = null; return false; }
                result = _dataList[index];
                return true;
            }
            return TryGetMember(indexes[0].ToString(), out result);
        }

        private bool TryCastInt(object obj, out int result)
        {
            try
            {
                result = Convert.ToInt32(obj);
                return true;
            }
            catch
            {
                result = default(int);
                return false;
            }
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            //new BinaryFormatter(); //new SoapFormatter(); //Newtonsoft.Json
            if (_dataList == null) throw new ObjectDisposedException(nameof(_dataList));
            for (int i = 0; i < _dataList.Count; i++) info.AddValue(prefix + (i + 1), _dataList[i]);
        }

        void IDisposable.Dispose()
        {
            _dataList = null;
        }
    }
}
