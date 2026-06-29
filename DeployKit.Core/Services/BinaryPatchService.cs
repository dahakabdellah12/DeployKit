using System.Buffers.Binary;

namespace DeployKit.Core.Services;

public class BinaryPatchService
{
    private const int BlockSize = 32;
    private const int MinMatch = 16;

    public async Task CreatePatchAsync(string oldFile, string newFile, string patchFile)
    {
        var oldData = await File.ReadAllBytesAsync(oldFile);
        var newData = await File.ReadAllBytesAsync(newFile);

        await using var fs = File.Create(patchFile);
        await using var writer = new BinaryWriter(fs);

        writer.Write("DKBP"u8);
        writer.Write(BinaryPrimitives.ReverseEndianness(1));
        writer.Write(BinaryPrimitives.ReverseEndianness(oldData.Length));
        writer.Write(BinaryPrimitives.ReverseEndianness(newData.Length));

        var commands = new List<byte[]>();
        var oldIndex = BuildIndex(oldData);
        int newPos = 0;

        while (newPos < newData.Length)
        {
            var match = FindLongestMatch(oldData, newData, oldIndex, newPos);
            if (match.Length >= MinMatch && match.OldPos + match.Length <= oldData.Length)
            {
                commands.Add(EncodeCopy(match.OldPos, match.Length));
                newPos += match.Length;
            }
            else
            {
                int insertLen = Math.Min(256, newData.Length - newPos);
                commands.Add(EncodeInsert(newData.AsSpan(newPos, insertLen)));
                newPos += insertLen;
            }
        }

        writer.Write(BinaryPrimitives.ReverseEndianness(commands.Count));
        foreach (var cmd in commands)
            await fs.WriteAsync(cmd);

        var patchSize = fs.Length;
        if (patchSize < newData.Length)
        {
            fs.SetLength(patchSize);
        }
        else
        {
            await fs.DisposeAsync();
            File.Copy(newFile, patchFile, overwrite: true);
        }
    }

    public async Task ApplyPatchAsync(string oldFile, string patchFile, string newFile)
    {
        await using var fs = File.OpenRead(patchFile);
        using var reader = new BinaryReader(fs);

        var magic = reader.ReadBytes(4);
        if (magic[0] != 'D' || magic[1] != 'K' || magic[2] != 'B' || magic[3] != 'P')
        {
            File.Copy(patchFile, newFile, overwrite: true);
            return;
        }

        reader.ReadInt32();
        var oldSize = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
        var newSize = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());

        var oldData = await File.ReadAllBytesAsync(oldFile);
        byte[] newData;

        if (oldData.Length != oldSize)
        {
            File.Copy(patchFile, newFile, overwrite: true);
            return;
        }

        using var ms = new MemoryStream(newSize);
        var cmdCount = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());

        for (int i = 0; i < cmdCount; i++)
        {
            var cmdType = reader.ReadByte();
            var length = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());

            if (cmdType == 0)
            {
                var offset = BinaryPrimitives.ReverseEndianness(reader.ReadInt64());
                ms.Write(oldData, (int)offset, length);
            }
            else
            {
                var buffer = reader.ReadBytes(length);
                ms.Write(buffer);
            }
        }

        newData = ms.ToArray();
        if (newData.Length != newSize)
            throw new InvalidOperationException("Patch output size mismatch");

        var dir = Path.GetDirectoryName(newFile)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(newFile, newData);
    }

    private static Dictionary<int, List<int>> BuildIndex(byte[] data)
    {
        var index = new Dictionary<int, List<int>>();
        for (int i = 0; i <= data.Length - BlockSize; i++)
        {
            var hash = FastHash(data, i);
            if (!index.TryGetValue(hash, out var list))
                index[hash] = list = [];
            list.Add(i);
        }
        return index;
    }

    private static Match FindLongestMatch(byte[] oldData, byte[] newData,
        Dictionary<int, List<int>> index, int newPos)
    {
        if (newPos > newData.Length - BlockSize)
            return default;

        var hash = FastHash(newData, newPos);
        if (!index.TryGetValue(hash, out var positions))
            return default;

        int bestLen = 0;
        int bestOldPos = 0;

        foreach (var oldPos in positions)
        {
            int len = 0;
            while (newPos + len < newData.Length &&
                   oldPos + len < oldData.Length &&
                   newData[newPos + len] == oldData[oldPos + len])
            {
                len++;
            }
            if (len > bestLen)
            {
                bestLen = len;
                bestOldPos = oldPos;
            }
        }

        return new Match { OldPos = bestOldPos, Length = bestLen };
    }

    private static int FastHash(byte[] data, int offset)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < BlockSize; i++)
            {
                hash ^= data[offset + i];
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    private static byte[] EncodeCopy(long offset, int length)
    {
        var data = new byte[13];
        data[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(1), length);
        BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(5), offset);
        return data;
    }

    private static byte[] EncodeInsert(ReadOnlySpan<byte> bytes)
    {
        var data = new byte[5 + bytes.Length];
        data[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(1), bytes.Length);
        bytes.CopyTo(data.AsSpan(5));
        return data;
    }

    private struct Match
    {
        public int OldPos;
        public int Length;
    }
}
