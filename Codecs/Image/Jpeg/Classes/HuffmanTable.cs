using System.Collections.Generic;

namespace Codec.Jpeg.Classes;

internal class HuffmanTable
{
    public byte Id;

    public int[] MinCode { get; set; }
    public int[] MaxCode { get; set; }
    public int[] ValPtr { get; set; }
    public byte[] Values { get; set; }
    public Dictionary<int, (int code, int length)> CodeTable { get; set; }

    public HuffmanTable()
    {
        MinCode = new int[16];
        MaxCode = new int[16];
        ValPtr = new int[16];
        Values = new byte[256];
        CodeTable = new Dictionary<int, (int code, int length)>();
    }

    public (int code, int length) GetCode(int value)
    {
        if (CodeTable.TryGetValue(value, out var codeInfo))
        {
            return codeInfo;
        }
        return (0, 0);
    }

    public int GetCodeLength(int value)
    {
        if (CodeTable.TryGetValue(value, out var codeInfo))
        {
            return codeInfo.length;
        }
        return 0;
    }
}
