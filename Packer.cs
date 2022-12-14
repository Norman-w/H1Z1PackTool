using System.Text;

namespace H1Z1PackTool;

public class Packer
{
    /// <summary>
    /// 查找根目录下的所有pack文件
    /// </summary>
    /// <param name="rootPath"></param>
    /// <returns></returns>
    public List<string> ListPacks(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine("根目录{0}不存在", rootPath);
            return new List<string>();
        }

        List<string> packFiles = new List<string>();
        var arr = Directory.GetFiles(rootPath, "*.pack");
        packFiles.AddRange(arr);
        return packFiles;
    }

    public PackFile LoadPackFile(string packFilePath)
    {
        if (!File.Exists(packFilePath))
        {
            Console.WriteLine("包件{0}不存在", packFilePath);
            return null;
        }

        PackFile packFile = new PackFile();
        packFile.PackFileFullName = System.IO.Path.GetFullPath(packFilePath);
        FileStream fs = new FileStream(packFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        BinaryReader br = new BinaryReader(fs);
        int nextOffset = 0;
        do
        {
            nextOffset = ReadInt32BE(br);
            int assetsCount = ReadInt32BE(br);
            for (int i = 0; i < assetsCount; i++)
            {
                Asset asset = new Asset();
                var nameLength = ReadUint32BE(br);
                asset.Name = ReadString(br, (int)nameLength);
                asset.Offset = ReadUint32BE(br);
                asset.Length = ReadUint32BE(br);
                asset.CRC32 = ReadUint32BE(br);
                packFile.Assets.Add(asset.Name, asset);
            }

            foreach (var current in packFile.Assets)
            {
                fs.Seek(current.Value.Offset, SeekOrigin.Begin);
                var assetFileContent = new List<byte>();
                for (int i = 0; i < current.Value.Length; i++)
                {
                    assetFileContent.Add((byte)fs.ReadByte());
                }

                current.Value.FileContent = assetFileContent.ToArray();
            }

            fs.Seek(nextOffset, SeekOrigin.Begin);
        } while (nextOffset != 0);

        return packFile;
    }

    /// <summary>
    /// 从文件夹加载所有的资源到包对象中.
    /// </summary>
    /// <param name="assetsDir"></param>
    /// <returns></returns>
    public PackFile LoadAssetsIntoPackFile(string assetsDir)
    {
        if (!Directory.Exists(assetsDir))
        {
            Console.WriteLine("资源文件夹{0}不存在", assetsDir);
            return null;
        }
        var assetsFiles = Directory.GetFiles(assetsDir, "*.*");
        if (assetsFiles.Length<1)
        {
            Console.WriteLine("{0}目录中不存在资源文件", assetsDir);
            return null;
        }

        PackFile packFile = new PackFile();
        for (int i = 0; i < assetsFiles.Length; i++)
        {
            var currentAssetPath = assetsFiles[i];
            Asset asset = new Asset();
            asset.Name = Path.GetFileName(currentAssetPath);
            var currentAssetFileContent = File.ReadAllBytes(currentAssetPath);
            asset.FileContent = currentAssetFileContent;
            asset.Length = Convert.ToUInt32(currentAssetFileContent.Length);
            CRC32Cls crc32Cls = new CRC32Cls();
            asset.CRC32 = Convert.ToUInt32(crc32Cls.GetCRC32Str(currentAssetFileContent));
            //做到这里只有offset没有指定.存储到pack文件时指定offset即可

            packFile.Assets.Add(asset.Name, asset);
        }
        packFile.PackFileFullName = assetsDir + ".pack";
        return packFile;
    }

    /// <summary>
    /// 将包对象存储到磁盘.
    /// </summary>
    /// <param name="packFile"></param>
    public void SavePackFile(PackFile packFile)
    {
        Dictionary<string, int> assetOffsetWritePosDic = new Dictionary<string, int>();
        var headerBytes = BuildPackHeader(packFile,ref assetOffsetWritePosDic);

        //根据header的长度计算一下要把资源放在什么位置.如果不需要偏移对齐,这个数字直接等于header的bytes length
        int howMuchBytesAlign = 8192;
        //实际使用的header长度,使用了对齐的方式.如果不足8192,就向上取整对齐.
        int realUseHeaderLength = (int) Math.Ceiling(headerBytes.Length * 1f / howMuchBytesAlign) * howMuchBytesAlign;
        //如果文件头不够数,加入
        var needAddCount = howMuchBytesAlign - headerBytes.Length;
        var headersBytesList = new List<byte>(headerBytes);
        for (int i = 0; i < needAddCount; i++)
        {
            headersBytesList.Add(0);
        }
        //现在头应该是8192对齐的了.然后开始计算每个文件的偏移

        uint currentAssetFileOffset = Convert.ToUInt32(realUseHeaderLength);
        foreach (var current in packFile.Assets)
        {
            var asset = current.Value;
            asset.Offset = currentAssetFileOffset;
            currentAssetFileOffset += asset.Length;
            //存入到文件头中的原来保存偏移的那里去(不是直接存储到文件中)
            var assetOffsetInfoPosInHeader = assetOffsetWritePosDic[current.Key];
            var assetOffsetBytes = GetUint32BE(asset.Offset);
            for (int i = 0; i < 4; i++)
            {
                headersBytesList[assetOffsetInfoPosInHeader + i] = assetOffsetBytes[i];
            }
        }

        var packFileBytesList = new List<byte>();
        //先加入文件名字的头部
        packFileBytesList.AddRange(headersBytesList);
        packFile.Header = headersBytesList.ToArray();
        //再加入每一个文件,没有做文件对齐.
        foreach (var asset in packFile.Assets)
        {
            packFileBytesList.AddRange(asset.Value.FileContent);
        }

        File.WriteAllBytes(packFile.PackFileFullName, packFileBytesList.ToArray());
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="packFile"></param>
    /// <param name="assetOffsetWritePosDic">资源的名字所对应的偏移位置,此位置开始的4个字节在写入文件之前先计算好偏移,然后写在那个位置.</param>
    /// <returns></returns>
    private byte[] BuildPackHeader(PackFile packFile, ref Dictionary<string, int> assetOffsetWritePosDic)
    {
        List<byte> headerContentByteList = new List<byte>();
        //写入包偏移
        headerContentByteList.AddRange(GetUint32BE(0));
        //写入包内资源总数
        headerContentByteList.AddRange(GetUint32BE(Convert.ToUInt32(packFile.AssetCount)));
        //依次写入文件名信息


        foreach (var current in packFile.Assets)
        {
            var asset = current.Value;
            //文件名的长度
            headerContentByteList.AddRange(GetUint32BE(Convert.ToUInt32(asset.Name.Length)));
            //文件名
            var assetsNameBytes = Encoding.UTF8.GetBytes(asset.Name);
            headerContentByteList.AddRange(assetsNameBytes);
            //偏移的存在位置
            assetOffsetWritePosDic.Add(current.Key, headerContentByteList.Count);
            //文件偏移位置
            headerContentByteList.AddRange(new byte[]{0,0,0,0});
            //文件长度
            headerContentByteList.AddRange(GetUint32BE(asset.Length));
            //文件的crc
            headerContentByteList.AddRange(GetUint32BE(asset.CRC32));
        }

        return headerContentByteList.ToArray();
    }

    /// <summary>
    /// 把包内的所有的资源解压出来
    /// </summary>
    /// <param name="packFile"></param>
    public void UnpackAssetsFromPack(PackFile packFile)
    {
        foreach (var current in packFile.Assets)
        {
            #region 校验CRC32

            // var crc32Checked = CRC32.GetCRC32(current.Value.FileContent);
            CRC32Cls crc32Cls = new CRC32Cls();

            var crc32Checked = crc32Cls.GetCRC32Str(current.Value.FileContent);

            if (current.Value.CRC32 != crc32Checked)
            {
                Console.WriteLine("资源文件{0}的CRC32校验不正确",current.Value.Name);
            }

            #endregion

            var assetFileFullDir = Path.Combine(packFile.PackFileDir, packFile.PackFileName);
            if (!Directory.Exists(assetFileFullDir))
            {
                Directory.CreateDirectory(assetFileFullDir);
            }
            var assetFileFullPath = Path.Combine(assetFileFullDir, current.Value.Name);
            File.WriteAllBytes(assetFileFullPath, current.Value.FileContent);
        }
    }



    private int ReadInt32BE(BinaryReader br)
    {
        var bytes = br.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes,0);
    }

    /// <summary>
    /// 返回4字节长度的uint转换为出的byte数组
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private byte[] GetUint32BE(uint value)
    {
        var ret = new byte[4];
        var str = value.ToString("X").PadLeft(8,'0');
        for (int i = 0; i < 4; i++)
        {
            ret[i] = Convert.ToByte(str.Substring(i * 2, 2),16);
        }
        return ret;
    }
    /// <summary>
    /// 16进制字符串转byte数组
    /// </summary>
    /// <param name="hexString">16进制字符</param>
    /// <returns></returns>
    public static byte[] BytesToHexString(string hexString)
    {
        // 将16进制秘钥转成字节数组
        byte[] bytes = new byte[hexString.Length / 2];
        for (var x = 0; x < bytes.Length; x++)
        {
            var i = Convert.ToInt32(hexString.Substring(x * 2, 2), 16);
            bytes[x] = (byte)i;
        }
        return bytes;
    }
    private uint ReadUint32BE(BinaryReader br)
    {
        var bytes = br.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes,0);
    }

    private string ReadString(BinaryReader br, int length)
    {
        var bytes = br.ReadBytes(length);
        // return Convert.ToString(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 这个获取的结果不正确
    /// </summary>
    public class CRC32
    {
        static UInt32[] crcTable =
        {
          0x00000000, 0x04c11db7, 0x09823b6e, 0x0d4326d9, 0x130476dc, 0x17c56b6b, 0x1a864db2, 0x1e475005,
          0x2608edb8, 0x22c9f00f, 0x2f8ad6d6, 0x2b4bcb61, 0x350c9b64, 0x31cd86d3, 0x3c8ea00a, 0x384fbdbd,
          0x4c11db70, 0x48d0c6c7, 0x4593e01e, 0x4152fda9, 0x5f15adac, 0x5bd4b01b, 0x569796c2, 0x52568b75,
          0x6a1936c8, 0x6ed82b7f, 0x639b0da6, 0x675a1011, 0x791d4014, 0x7ddc5da3, 0x709f7b7a, 0x745e66cd,
          0x9823b6e0, 0x9ce2ab57, 0x91a18d8e, 0x95609039, 0x8b27c03c, 0x8fe6dd8b, 0x82a5fb52, 0x8664e6e5,
          0xbe2b5b58, 0xbaea46ef, 0xb7a96036, 0xb3687d81, 0xad2f2d84, 0xa9ee3033, 0xa4ad16ea, 0xa06c0b5d,
          0xd4326d90, 0xd0f37027, 0xddb056fe, 0xd9714b49, 0xc7361b4c, 0xc3f706fb, 0xceb42022, 0xca753d95,
          0xf23a8028, 0xf6fb9d9f, 0xfbb8bb46, 0xff79a6f1, 0xe13ef6f4, 0xe5ffeb43, 0xe8bccd9a, 0xec7dd02d,
          0x34867077, 0x30476dc0, 0x3d044b19, 0x39c556ae, 0x278206ab, 0x23431b1c, 0x2e003dc5, 0x2ac12072,
          0x128e9dcf, 0x164f8078, 0x1b0ca6a1, 0x1fcdbb16, 0x018aeb13, 0x054bf6a4, 0x0808d07d, 0x0cc9cdca,
          0x7897ab07, 0x7c56b6b0, 0x71159069, 0x75d48dde, 0x6b93dddb, 0x6f52c06c, 0x6211e6b5, 0x66d0fb02,
          0x5e9f46bf, 0x5a5e5b08, 0x571d7dd1, 0x53dc6066, 0x4d9b3063, 0x495a2dd4, 0x44190b0d, 0x40d816ba,
          0xaca5c697, 0xa864db20, 0xa527fdf9, 0xa1e6e04e, 0xbfa1b04b, 0xbb60adfc, 0xb6238b25, 0xb2e29692,
          0x8aad2b2f, 0x8e6c3698, 0x832f1041, 0x87ee0df6, 0x99a95df3, 0x9d684044, 0x902b669d, 0x94ea7b2a,
          0xe0b41de7, 0xe4750050, 0xe9362689, 0xedf73b3e, 0xf3b06b3b, 0xf771768c, 0xfa325055, 0xfef34de2,
          0xc6bcf05f, 0xc27dede8, 0xcf3ecb31, 0xcbffd686, 0xd5b88683, 0xd1799b34, 0xdc3abded, 0xd8fba05a,
          0x690ce0ee, 0x6dcdfd59, 0x608edb80, 0x644fc637, 0x7a089632, 0x7ec98b85, 0x738aad5c, 0x774bb0eb,
          0x4f040d56, 0x4bc510e1, 0x46863638, 0x42472b8f, 0x5c007b8a, 0x58c1663d, 0x558240e4, 0x51435d53,
          0x251d3b9e, 0x21dc2629, 0x2c9f00f0, 0x285e1d47, 0x36194d42, 0x32d850f5, 0x3f9b762c, 0x3b5a6b9b,
          0x0315d626, 0x07d4cb91, 0x0a97ed48, 0x0e56f0ff, 0x1011a0fa, 0x14d0bd4d, 0x19939b94, 0x1d528623,
          0xf12f560e, 0xf5ee4bb9, 0xf8ad6d60, 0xfc6c70d7, 0xe22b20d2, 0xe6ea3d65, 0xeba91bbc, 0xef68060b,
          0xd727bbb6, 0xd3e6a601, 0xdea580d8, 0xda649d6f, 0xc423cd6a, 0xc0e2d0dd, 0xcda1f604, 0xc960ebb3,
          0xbd3e8d7e, 0xb9ff90c9, 0xb4bcb610, 0xb07daba7, 0xae3afba2, 0xaafbe615, 0xa7b8c0cc, 0xa379dd7b,
          0x9b3660c6, 0x9ff77d71, 0x92b45ba8, 0x9675461f, 0x8832161a, 0x8cf30bad, 0x81b02d74, 0x857130c3,
          0x5d8a9099, 0x594b8d2e, 0x5408abf7, 0x50c9b640, 0x4e8ee645, 0x4a4ffbf2, 0x470cdd2b, 0x43cdc09c,
          0x7b827d21, 0x7f436096, 0x7200464f, 0x76c15bf8, 0x68860bfd, 0x6c47164a, 0x61043093, 0x65c52d24,
          0x119b4be9, 0x155a565e, 0x18197087, 0x1cd86d30, 0x029f3d35, 0x065e2082, 0x0b1d065b, 0x0fdc1bec,
          0x3793a651, 0x3352bbe6, 0x3e119d3f, 0x3ad08088, 0x2497d08d, 0x2056cd3a, 0x2d15ebe3, 0x29d4f654,
          0xc5a92679, 0xc1683bce, 0xcc2b1d17, 0xc8ea00a0, 0xd6ad50a5, 0xd26c4d12, 0xdf2f6bcb, 0xdbee767c,
          0xe3a1cbc1, 0xe760d676, 0xea23f0af, 0xeee2ed18, 0xf0a5bd1d, 0xf464a0aa, 0xf9278673, 0xfde69bc4,
          0x89b8fd09, 0x8d79e0be, 0x803ac667, 0x84fbdbd0, 0x9abc8bd5, 0x9e7d9662, 0x933eb0bb, 0x97ffad0c,
          0xafb010b1, 0xab710d06, 0xa6322bdf, 0xa2f33668, 0xbcb4666d, 0xb8757bda, 0xb5365d03, 0xb1f740b4
        };

        public static uint GetCRC32(byte[] bytes)
        {
            uint iCount = (uint)bytes.Length;
            uint crc = 0xFFFFFFFF;

            for (uint i = 0; i < iCount; i++)
            {
                crc = (crc << 8) ^ crcTable[(crc >> 24) ^ bytes[i]];
            }

            return crc;
        }
    }
    /// <summary>
    /// 这个可以正常使用.但是要先new出来对象
    /// </summary>
    class CRC32Cls
    {
        protected ulong[] Crc32Table;
        //生成CRC32码表
        public void GetCRC32Table()
        {
            ulong Crc;
            Crc32Table = new ulong[256];
            int i,j;
            for(i = 0;i < 256; i++)
            {
                Crc = (ulong)i;
                for (j = 8; j > 0; j--)
                {
                    if ((Crc & 1) == 1)
                        Crc = (Crc >> 1) ^ 0xEDB88320;
                    else
                        Crc >>= 1;
                }
                Crc32Table[i] = Crc;
            }
        }

        //获取字符串的CRC32校验值
        public ulong GetCRC32Str(string sInputString)
        {
            //生成码表
            GetCRC32Table();
            byte[] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes(sInputString);
            ulong value = 0xffffffff;
            int len = buffer.Length;
            for (int i = 0; i < len; i++)
            {
                value = (value >> 8) ^ Crc32Table[(value & 0xFF)^ buffer[i]];
            }
            return value ^ 0xffffffff;
        }
        public ulong GetCRC32Str(byte[] bytes)
        {
            //生成码表
            GetCRC32Table();
            byte[] buffer = bytes;
            ulong value = 0xffffffff;
            int len = buffer.Length;
            for (int i = 0; i < len; i++)
            {
                value = (value >> 8) ^ Crc32Table[(value & 0xFF)^ buffer[i]];
            }
            return value ^ 0xffffffff;
        }
    }
}
