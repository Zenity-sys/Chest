using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Chest
{
    internal static class DetectionLog
    {
        static readonly object gate = new object();
        static readonly Queue<string> lines = new Queue<string>();

        public static void Write(string line)
        {
            string row = DateTime.Now.ToString("HH:mm:ss.fff ") + line;
            lock (gate)
            {
                lines.Enqueue(row);
                while (lines.Count > 1000) lines.Dequeue();
            }
        }

        public static string Tail()
        {
            lock (gate)
            {
                StringBuilder text = new StringBuilder();
                foreach (string line in lines) text.AppendLine(line);
                return text.ToString();
            }
        }
    }

    internal static class StoredZip
    {
        public static void Create(string path, List<string> names, List<byte[]> files)
        {
            using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                List<byte[]> headers = new List<byte[]>();
                List<uint> offsets = new List<uint>();
                for (int i = 0; i < names.Count; i++)
                {
                    offsets.Add((uint)output.Position);
                    byte[] name = Encoding.UTF8.GetBytes(names[i].Replace('\\', '/'));
                    byte[] data = files[i] ?? new byte[0];
                    uint crc = Crc(data);
                    byte[] local = new byte[30 + name.Length];
                    local[0] = 0x50; local[1] = 0x4b; local[2] = 0x03; local[3] = 0x04;
                    local[4] = 20;
                    Write(local, 14, crc);
                    Write(local, 18, (uint)data.Length);
                    Write(local, 22, (uint)data.Length);
                    local[26] = (byte)name.Length; local[27] = (byte)(name.Length >> 8);
                    Buffer.BlockCopy(name, 0, local, 30, name.Length);
                    output.Write(local, 0, local.Length);
                    output.Write(data, 0, data.Length);
                    byte[] central = new byte[46 + name.Length];
                    central[0] = 0x50; central[1] = 0x4b; central[2] = 0x01; central[3] = 0x02;
                    central[4] = 20; central[6] = 20;
                    Write(central, 16, crc);
                    Write(central, 20, (uint)data.Length);
                    Write(central, 24, (uint)data.Length);
                    central[28] = (byte)name.Length; central[29] = (byte)(name.Length >> 8);
                    Write(central, 42, offsets[i]);
                    Buffer.BlockCopy(name, 0, central, 46, name.Length);
                    headers.Add(central);
                }
                uint start = (uint)output.Position;
                uint size = 0;
                foreach (byte[] header in headers)
                {
                    output.Write(header, 0, header.Length);
                    size += (uint)header.Length;
                }
                byte[] end = new byte[22];
                end[0] = 0x50; end[1] = 0x4b; end[2] = 0x05; end[3] = 0x06;
                end[8] = (byte)names.Count; end[9] = (byte)(names.Count >> 8);
                end[10] = end[8]; end[11] = end[9];
                Write(end, 12, size);
                Write(end, 16, start);
                output.Write(end, 0, end.Length);
            }
        }

        static void Write(byte[] buffer, int at, uint value)
        {
            buffer[at] = (byte)value;
            buffer[at + 1] = (byte)(value >> 8);
            buffer[at + 2] = (byte)(value >> 16);
            buffer[at + 3] = (byte)(value >> 24);
        }

        static uint Crc(byte[] data)
        {
            uint crc = 0xffffffff;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            }
            return ~crc;
        }
    }
}
