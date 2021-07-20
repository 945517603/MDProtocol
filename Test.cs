using MDProtocol;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class Test : MonoBehaviour
{

    void Start()
    {
        
        MDSingle.Encoders.Add((stype, value) =>
        {
            if(stype == 'f')
            {
                return BitConverter.GetBytes((float)value);
            }
            if(stype == 'v')
            {
                List<byte> list = new List<byte>();
                Vector3 v3 = (Vector3)value;
                list.AddRange(BitConverter.GetBytes(v3.x));
                list.AddRange(BitConverter.GetBytes(v3.y));
                list.AddRange(BitConverter.GetBytes(v3.z));
                return list;
            }
            if(stype == 'b')
            {
                return BitConverter.GetBytes((bool)value);
            }
            return null;
        });
        MDSingle.Decoders.Add((stype, datas, pivot) =>
        {
            if (stype == 'f')
            {
                return new MDSingle(BitConverter.ToSingle(datas, pivot), typeof(float), 4);
            }
            if(stype == 'v')
            {
                Vector3 v3 = Vector3.zero;
                v3.x = BitConverter.ToSingle(datas, pivot);
                v3.y = BitConverter.ToSingle(datas, pivot + 4);
                v3.z = BitConverter.ToSingle(datas, pivot + 8);
                return new MDSingle(v3, typeof(Vector3), 12);
            }
            if(stype == 'b')
            {
                return new MDSingle(BitConverter.ToBoolean(datas, pivot), typeof(bool), 1);
            }
            return null;
        });
        MDSingle.FixedLength.Add('b', 1);
        MDSingle.FixedLength.Add('i', 4);
        MDSingle.FixedLength.Add('v', 12);


        //string format = "<i{[i][v]}>";
        //ArrayList param = new ArrayList();
        //param.Add(new Dictionary<int, ArrayList>
        //{
        //    {
        //        1,new ArrayList{
        //            new ArrayList{1,2,3,4,5},
        //            new ArrayList{Vector3.one*0.3f,Vector3.one*558f}
        //        }
        //    },
        //    {
        //        2,new ArrayList{
        //            new ArrayList{1,2,3,4,5},
        //            new ArrayList{Vector3.one*0.3f,Vector3.one*558f}
        //        }
        //    },
        //    {
        //        3,new ArrayList
        //        {
        //            new ArrayList{8889453,15351,81321},
        //            new ArrayList{Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,Vector3.down}
        //        }
        //    },
        //});
        string format = "i_is";
        ArrayList param = new ArrayList();
        param.Add(1888);
        param.Add(null);
        param.Add("hello world");
        var se = MDType.Encoder(format, param);
        print("序列化之后数据长度：" + se.Length);
        var p = MDType.Decoder(format, se);
        print("反序列化之后的数据信息：");
        print(p);
        var m = p.AsMap.Cast<int,MDObject>();
    }
}

