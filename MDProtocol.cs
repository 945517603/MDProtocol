using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace MDProtocol
{
    public enum SerType
    {
        None = 0,
        Single = 1,
        Array = 2,
        Map = 3,
        Object = 4
    }
    public abstract class MDType
    {
        public static MDType Decoder(string format, byte[] datas)
        {
            return _Decoder(format, datas);
        }
        public static byte[] Encoder(string format, IList args)
        {
            return _Encoder(format, args).ToArray();
        }
        //递归编码
        private static List<byte> _Encoder(string format, IList args)
        {
            List<byte> bts = new List<byte>();
            int argStep = 0;
            int fStep = 0;
            char cur;
            object arg;
            SerType type;
            bool canNull;
            while (true)
            {
                if (fStep >= format.Length)
                    break;
                cur = format[fStep++];

                canNull = cur == '_';
                if (cur == '_')
                    cur = format[fStep++];
                if (cur == '[')
                    type = SerType.Array;
                else if (cur == '<')
                    type = SerType.Map;
                else if (cur == '{')
                    type = SerType.Object;
                else
                    type = SerType.Single;
                arg = args[argStep++];
                if (type == SerType.Array || type == SerType.Object || type == SerType.Map)
                {
                    bts.AddRange(BitConverter.GetBytes(arg != null));
                    if (arg == null)
                    {
                        if (type == SerType.Array)
                        {
                            int end = FindEndIndex(format, fStep - 1, SerType.Array);
                            if (end == format.Length - 1)
                                return bts;
                            fStep = end + 1;
                        }
                        else if (type == SerType.Object)
                        {
                            int end = FindEndIndex(format, fStep - 1, SerType.Object);
                            if (end == format.Length - 1)
                                return bts;
                            fStep = end + 1;
                        }
                        continue;
                    }
                }
                if (type == SerType.Single)
                {
                    bts.AddRange(MDSingle.EncoderSingle(cur, arg, canNull));
                }
                else if (type == SerType.Array)
                {
                    //数组长度
                    var t = (ICollection)arg;
                    bts.AddRange(BitConverter.GetBytes(t.Count));
                    int end = FindEndIndex(format, fStep - 1, type);
                    var f = format.Substring(fStep, end - fStep);
                    bool cannull = f[0] == '_';
                    if (cannull)
                        f = f.Substring(1);
                    foreach (var i in t)
                    {
                        if (IsArray(f) || IsMap(f) || IsObject(f))
                        {
                            var d = _Encoder(f, new ArrayList { i });
                            bts.AddRange(BitConverter.GetBytes(d.Count));
                            bts.AddRange(d);
                        }
                        else
                        {
                            if (cannull)
                                bts.AddRange(BitConverter.GetBytes(i != null));
                            if (!cannull || i != null)
                            {
                                var d = _Encoder(f, new ArrayList { i });
                                if (MDSingle.GetFixedLength(f[0]) < 0)
                                    bts.AddRange(BitConverter.GetBytes(d.Count));
                                bts.AddRange(d);
                            }
                        }
                    }
                    fStep = end + 1;
                }
                else if (type == SerType.Object)
                {
                    int end = FindEndIndex(format, fStep - 1, type);
                    var d = _Encoder(format.Substring(fStep, end - fStep), (IList)arg);
                    //数据长度
                    bts.AddRange(BitConverter.GetBytes(d.Count));
                    //数据
                    bts.AddRange(d);
                    fStep = end + 1;
                }
                else if (type == SerType.Map)
                {
                    var t = (IDictionary)arg;
                    bts.AddRange(BitConverter.GetBytes(t.Count));
                    int end = FindEndIndex(format, fStep - 1, type);
                    var f = format.Substring(fStep, end - fStep);
                    var k = f.Substring(0, 1);
                    var v = f.Substring(1);
                    var kLen = MDSingle.GetFixedLength(k[0]);
                    var vLen = -1;
                    var vCanNull = false;
                    //字典的value也是single
                    if (!IsArray(v) && !IsMap(v) && !IsObject(v))
                    {
                        if (v[0] == '_')
                        {
                            vCanNull = true;
                            v = v.Substring(1);
                        }
                        vLen = MDSingle.GetFixedLength(v[0]);
                    }
                    else
                    {
                        vCanNull = true;
                    }
                    foreach (var p in t.Keys)
                    {
                        //key
                        var dk = _Encoder(k, new ArrayList { p });
                        if (kLen < 0)
                            bts.AddRange(BitConverter.GetBytes(dk.Count));
                        bts.AddRange(dk);
                        //value
                        if (vCanNull)
                            bts.AddRange(BitConverter.GetBytes(t[p] != null));
                        if (!vCanNull || t[p] != null)
                        {
                            var dv = _Encoder(v, new ArrayList { t[p] });
                            if (vLen < 0)
                                bts.AddRange(BitConverter.GetBytes(dv.Count));
                            bts.AddRange(dv);
                        }
                    }
                    fStep = end + 1;
                }
            }
            return bts;
        }
        //递归解码
        private static MDType _Decoder(string format, byte[] datas, MDType parent = null, int dPivot = 0)
        {
            int fStep = 0;
            char cur;
            SerType type;
            bool canNull;
            while (true)
            {
                if (fStep >= format.Length)
                    break;
                cur = format[fStep++];
                canNull = cur == '_';
                if (cur == '_')
                {
                    cur = format[fStep++];
                }
                if (cur == '[')
                    type = SerType.Array;
                else if (cur == '<')
                    type = SerType.Map;
                else if (cur == '{')
                    type = SerType.Object;
                else
                    type = SerType.Single;
                bool valid = true;
                if (type == SerType.Array || type == SerType.Map || type == SerType.Object || canNull)
                {
                    valid = BitConverter.ToBoolean(datas, dPivot++);
                }
                MDType eItem = null;
                int end;
                int len;
                if (!valid)
                {
                    if (parent == null)
                    {
                        if (IsArray(format) || IsMap(format) || IsObject(format))
                            return null;
                        parent = new MDObject();
                    }
                    if (parent.Type == SerType.Array)
                        parent.AsArray.AddItem(null);
                    else if (parent.Type == SerType.Object)
                        parent.AsObject.AddItem(null);
                    else if (parent.Type == SerType.Map)
                        parent.AsMap.Add(null);
                    if (type == SerType.Array || type == SerType.Object || type == SerType.Map)
                        fStep = FindEndIndex(format, fStep - 1, type) + 1;
                }
                else
                {
                    int size;
                    string f;
                    switch (type)
                    {
                        case SerType.Array:
                            //数组长度
                            eItem = new MDArray();
                            size = BitConverter.ToInt32(datas, dPivot);
                            dPivot += 4;
                            end = FindEndIndex(format, fStep - 1, type);
                            f = format.Substring(fStep, end - fStep);
                            bool cannull = f[0] == '_';
                            if (cannull)
                                f = f.Substring(1);
                            //做定长数据优化处理(不加定长数据的长度(4个byte))
                            for (int i = 0; i < size; i++)
                            {
                                if (IsArray(f) || IsMap(f) || IsObject(f))
                                {
                                    len = BitConverter.ToInt32(datas, dPivot);
                                    dPivot += 4;
                                    _Decoder(f, datas, eItem, dPivot);
                                    dPivot += len;
                                }
                                else
                                {
                                    bool vld = true;
                                    if (cannull)
                                    {
                                        vld = BitConverter.ToBoolean(datas, dPivot);
                                        dPivot++;
                                    }
                                    if (!cannull || vld)
                                    {
                                        len = MDSingle.GetFixedLength(f[0]);
                                        if (len < 0)
                                        {
                                            len = BitConverter.ToInt32(datas, dPivot);
                                            dPivot += 4;
                                        }
                                        _Decoder(f, datas, eItem, dPivot);
                                        dPivot += len;
                                    }
                                    else
                                    {
                                        eItem.AsArray.AddItem(null);
                                    }
                                }
                            }
                            fStep = end + 1;
                            break;
                        case SerType.Map:
                            //key和value做了定长优化处理
                            eItem = new MDMap();
                            size = BitConverter.ToInt32(datas, dPivot);
                            dPivot += 4;
                            end = FindEndIndex(format, fStep - 1, type);
                            f = format.Substring(fStep, end - fStep);
                            var k = f.Substring(0, 1);
                            var v = f.Substring(1);
                            var kLen = MDSingle.GetFixedLength(k[0]);
                            var vLen = -1;
                            var vCanNull = false;
                            //字典的value也是single
                            if (!IsArray(v) && !IsMap(v) && !IsObject(v))
                            {
                                if (v[0] == '_')
                                {
                                    vCanNull = true;
                                    v = v.Substring(1);
                                }
                                vLen = MDSingle.GetFixedLength(v[0]);
                            }
                            else
                            {
                                vCanNull = true;
                            }
                            var kFixedLen = kLen > 0;
                            var vFixedLen = vLen > 0;
                            for (int i = 0; i < size; i++)
                            {
                                //key
                                if (!kFixedLen)
                                {
                                    kLen = BitConverter.ToInt32(datas, dPivot);
                                    dPivot += 4;
                                }
                                _Decoder(k, datas, eItem, dPivot);
                                dPivot += kLen;

                                //value
                                bool vld = true;
                                if (vCanNull)
                                {
                                    vld = BitConverter.ToBoolean(datas, dPivot);
                                    dPivot++;
                                }
                                if (vld)
                                {
                                    if (!vFixedLen)
                                    {
                                        vLen = BitConverter.ToInt32(datas, dPivot);
                                        dPivot += 4;
                                    }
                                    _Decoder(v, datas, eItem, dPivot);
                                    dPivot += vLen;
                                }
                                else
                                    eItem.AsMap.Add(null);
                            }
                            fStep = end + 1;
                            break;
                        case SerType.Object:
                            eItem = new MDObject();
                            end = FindEndIndex(format, fStep - 1, type);
                            //对象数据
                            len = BitConverter.ToInt32(datas, dPivot);
                            dPivot += 4;
                            _Decoder(format.Substring(fStep, end - fStep), datas, eItem, dPivot);
                            dPivot += len;
                            fStep = end + 1;
                            break;
                        case SerType.Single:
                            eItem = MDSingle.DecoderSingle(cur, datas, dPivot);
                            dPivot += eItem.AsSingle.ByteCount;
                            break;
                    }
                    if (parent == null)
                    {
                        if (IsArray(format) || IsObject(format) || IsMap(format))
                            parent = eItem;
                        else
                            parent = new MDObject().AddItem(eItem);
                    }
                    else
                    {
                        if (parent.Type == SerType.Array)
                            parent.AsArray.AddItem(eItem);
                        else if (parent.Type == SerType.Object)
                            parent.AsObject.AddItem(eItem);
                        else if (parent.Type == SerType.Map)
                            parent.AsMap.Add(eItem);
                    }
                }
            }
            return parent;
        }
        //private static bool FormatChecker(string format,SerType parentType = SerType.None)
        //{
        //    if (format == null)
        //        throw new Exception("format cant be null");
        //    if (format.Equals(""))
        //        throw new Exception("format cant be empty string");
        //    char cur;
        //    int fStep = 0;
        //    bool canNull = false;
        //    while (true)
        //    {
        //        if (fStep >= format.Length)
        //            break;
        //        cur = format[fStep];
        //        if(cur == '_')
        //    }
        //    return false;
        //}
        private static int FindEndIndex(string format, int startIdx, SerType type)
        {
            //查找与第一个字符匹配的一组字符的下标
            char start = '\0', end = '\0';
            if (type == SerType.Array)
            {
                start = '[';
                end = ']';
            }
            else if (type == SerType.Object)
            {
                start = '{';
                end = '}';
            }
            else if (type == SerType.Map)
            {
                start = '<';
                end = '>';
            }
            if (start != '\0')
            {
                int firstEnd = format.IndexOf(end, startIdx);
                int count = 0;
                for (int i = startIdx; i < firstEnd; i++)
                {
                    if (format[i] == start)
                        count++;
                }
                for (int i = firstEnd; i < format.Length; i++)
                {
                    if (format[i] == end)
                        count--;
                    if (count == 0)
                        return i;
                }
            }
            return 0;
        }
        private static bool IsArray(string format)
        {
            return format[0] == '[' && FindEndIndex(format, 0, SerType.Array) == format.Length - 1;
        }
        private static bool IsMap(string format)
        {
            return format[0] == '<' && FindEndIndex(format, 0, SerType.Map) == format.Length - 1;
        }
        private static bool IsObject(string format)
        {
            return format[0] == '{' && FindEndIndex(format, 0, SerType.Object) == format.Length - 1;
        }

        public SerType Type { get; protected set; }
        public MDSingle AsSingle => this as MDSingle;
        public MDObject AsObject => this as MDObject;
        public MDArray AsArray => this as MDArray;
        public MDMap AsMap => this as MDMap;
        public override string ToString()
        {
            return _tostring(this);
        }
        private string _tostring(MDType cur)
        {
            StringBuilder builder = new StringBuilder();
            switch (cur.Type)
            {
                case SerType.Array:
                    builder.Append("[");
                    for(int i = 0; i < cur.AsArray.Count; i++)
                    {
                        if (cur.AsArray[i] == null)
                            builder.Append("null");
                        else
                            builder.Append(cur.AsArray[i]);
                        if (i < cur.AsArray.Count - 1)
                            builder.Append(",");
                    }
                    builder.Append("]");
                    break;
                case SerType.Object:
                    builder.Append("{");
                    for(int i = 0; i < cur.AsObject.Count; i++)
                    {
                        if (cur.AsObject[i] == null)
                            builder.Append("null");
                        else
                            builder.Append(cur.AsObject[i]);
                        if (i < cur.AsObject.Count - 1)
                            builder.Append(",");
                    }
                    builder.Append("}");
                    break;
                case SerType.Map:
                    builder.Append("<");
                    int iMap = 0;
                    foreach(var k in cur.AsMap)
                    {
                        builder.Append(k);
                        builder.Append(":");
                        if (cur.AsMap[k] == null)
                            builder.Append("null");
                        else
                            builder.Append(cur.AsMap[k]);
                        if (iMap < cur.AsMap.Count - 1)
                            builder.Append(",");
                        iMap++;
                    }
                    builder.Append(">");
                    break;
                case SerType.Single:
                    builder.Append(cur.AsSingle.Value);
                    break;
            }
            return builder.ToString();
        }
    }
    public class MDSingle : MDType
    {
        public static List<Func<char, byte[], int, MDSingle>> Decoders { get; private set; } = new List<Func<char, byte[], int, MDSingle>>() { DefaultDecoderSingle };
        public static List<Func<char, object, IList<byte>>> Encoders { get; private set; } = new List<Func<char, object, IList<byte>>>() { DefaultEncoderSingle };
        /// <summary> 符号数据定长</summary>
        public static Dictionary<char, int> FixedLength { get; private set; } = new Dictionary<char, int>();
        public static int GetFixedLength(char stype)
        {
            if (FixedLength.ContainsKey(stype))
                return FixedLength[stype];
            return -1;
        }
        public static MDSingle DecoderSingle(char stype, byte[] datas, int pivot)
        {
            for (int i = 0; i < Decoders.Count; i++)
            {
                var single = Decoders[i]?.Invoke(stype, datas, pivot);
                if (single != null)
                    return single;
            }
            throw new Exception("未找到符号:" + stype + "的解码方法!");
        }
        public static IList<byte> EncoderSingle(char stype, object value, bool canNull)
        {
            List<byte> bts = new List<byte>();
            if (canNull)
            {
                bts.AddRange(BitConverter.GetBytes(value != null));
                if (value == null)
                    return bts;
            }
            for (int i = 0; i < Encoders.Count; i++)
            {
                var dt = Encoders[i]?.Invoke(stype, value);
                if (dt != null)
                {
                    bts.AddRange(dt);
                    return bts;
                }
            }
            throw new Exception("未找到符号:" + stype + "的编码方法!");
        }






        public object Value { get; private set; }
        public Type ValuType { get; private set; }
        public int ByteCount { get; private set; }

        public MDSingle(object value, Type valType, int btCount)
        {
            Type = SerType.Single;
            Value = value;
            ValuType = valType;
            ByteCount = btCount;
        }
        /// <summary> 默认解码方法</summary>
        private static MDSingle DefaultDecoderSingle(char stype, byte[] datas, int pivot)
        {
            MDSingle single = null;
            if (stype == 's')
            {
                int len = BitConverter.ToInt32(datas, pivot);
                single = new MDSingle(Encoding.Default.GetString(datas, pivot + 4, len), typeof(string), 4 + len);
            }
            else if (stype == 'i')
            {
                single = new MDSingle(BitConverter.ToInt32(datas, pivot), typeof(int), 4);
            }
            return single;
        }
        /// <summary> 默认编码方法</summary>
        private static IList<byte> DefaultEncoderSingle(char stype, object value)
        {
            if (stype == 'i')
            {
                return BitConverter.GetBytes((int)value);
            }
            else if (stype == 's')
            {
                List<byte> list = new List<byte>(BitConverter.GetBytes(((string)value).Length));
                list.AddRange(Encoding.Default.GetBytes((string)value));
                return list;
            }
            return null;
        }

        public T Cast<T>()
        {
            if (typeof(T) == ValuType)
                return (T)Value;
            return default;
        }
    }
    public class MDArray : MDType
    {
        public MDType this[int idx]
        {
            get
            {
                if (idx >= Count)
                    throw new Exception("index out of range");
                return Values[idx];
            }
        }
        private List<MDType> Values = new List<MDType>();
        public int Count => Values.Count;
        public MDArray()
        {
            Type = SerType.Array;
        }
        public MDArray AddItem(MDType item)
        {
            Values.Add(item);
            return this;
        }
    }
    public class MDObject : MDType
    {
        public MDType this[int idx]
        {
            get
            {
                if (idx >= Count)
                    throw new Exception("index out of range");
                return Values[idx];
            }
        }
        private List<MDType> Values = new List<MDType>();
        public int Count => Values.Count;
        public MDObject()
        {
            Type = SerType.Object;
        }
        public MDObject AddItem(MDType item)
        {
            Values.Add(item);
            return this;
        }
    }
    public class MDMap : MDType ,IEnumerable
    {
        public MDType this[object key]
        {
            get
            {
                for(int i = 0; i < Keys.Count; i++)
                {
                    if(MDKeys[i].Value.Equals(key))
                    {
                        return MDValues[i];
                    }
                }
                return null;
            }
        }
        private HashSet<object> Keys = new HashSet<object>();
        private List<MDSingle> MDKeys = new List<MDSingle>();
        private List<MDType> MDValues = new List<MDType>();
        public int Count => MDKeys.Count;
        private int Pivot = 0;
        public MDMap()
        {
            Type = SerType.Map;
        }
        public MDMap Add(MDType korv)
        {
            if (Pivot % 2 == 0)
            {
                MDKeys.Add(korv.AsSingle);
                Keys.Add(korv.AsSingle.Value);
            }
            else
                MDValues.Add(korv);
            Pivot++;
            return this;
        }
        public MDMap AddItem(MDSingle key,MDType value)
        {
            MDKeys.Add(key);
            MDValues.Add(value);
            Keys.Add(key.Value);
            Pivot += 2;
            return this;
        }
        public Dictionary<TKey,TValue> Cast<TKey, TValue>()
        {
            Dictionary<TKey, TValue> dic = new Dictionary<TKey, TValue>();
            var tp = typeof(TValue);
            foreach(object k in this)
            {
                if (this[k] is TValue)
                    dic.Add((TKey)k, (TValue)(object)this[k]);
                else
                    dic.Add((TKey)k, (this[k] as MDSingle).Cast<TValue>());
            }
            return dic;
        }
        public IEnumerator GetEnumerator()
        {
            return Keys.GetEnumerator();
        }
    }
}