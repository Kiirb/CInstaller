using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class BinVdfWriter
{
    private readonly BinaryWriter writer;

    public BinVdfWriter(Stream stream)
    {
        writer = new BinaryWriter(stream);
    }

    public void WriteCString(string s)
    {
        writer.Write(Encoding.UTF8.GetBytes(s));
        writer.Write((byte)0);
    }

    public void WriteDict(Dictionary<string, object> map)
    {
        foreach (var entry in map)
        {
            string key = entry.Key;
            object value = entry.Value;

            if (value is Dictionary<string, object> dict)
            {
                writer.Write((byte)0x00);
                WriteCString(key);
                WriteDict(dict);
            }
            else if (value is string str)
            {
                writer.Write((byte)0x01);
                WriteCString(key);
                WriteCString(str);
            }
            else if (value is int i)
            {
                writer.Write((byte)0x02);
                WriteCString(key);
                writer.Write(i);
            }
            else if (value is long l)
            {
                writer.Write((byte)0x07);
                WriteCString(key);
                writer.Write(l);
            }
            else
            {
                throw new Exception("Unsupported type " + value);
            }
        }

        writer.Write((byte)0x08);
    }
}