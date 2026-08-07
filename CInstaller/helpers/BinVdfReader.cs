using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class BinVdfReader
{
    private readonly BinaryReader reader;

    public BinVdfReader(Stream stream)
    {
        reader = new BinaryReader(stream);
    }

    private int ReadInt()
    {
        return reader.ReadInt32();
    }

    private long ReadLong()
    {
        return reader.ReadInt64();
    }

    private string ReadCString()
    {
        List<byte> bytes = new();

        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public Dictionary<string, object> ReadDict()
    {
        var map = new Dictionary<string, object>();

        while (true)
        {
            int dtype = reader.ReadByte();

            if (dtype == 0x08)
                break;

            string name = ReadCString();
            object value;

            switch (dtype)
            {
                case 0x00:
                    value = ReadDict();
                    break;

                case 0x01:
                    value = ReadCString();
                    break;

                case 0x02:
                    value = ReadInt();
                    break;

                case 0x07:
                    value = ReadLong();
                    break;

                default:
                    throw new Exception("Unknown dtype " + dtype);
            }

            map[name] = value;
        }

        return map;
    }
}