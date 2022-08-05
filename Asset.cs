namespace H1Z1PackTool;

public class Asset
{
    public string Name { get; set; }
    public uint Offset { get; set; }
    public uint Length { get; set; }

    public uint CRC32 { get; set; }
    public byte[] FileContent { get; set; }
}
