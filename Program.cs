// See https://aka.ms/new-console-template for more information




using H1Z1PackTool;

void Test()
{
    Packer packer = new Packer();
// string srcPackFilePath = "/Volumes/BOOTCAMP/h1z1/Assets/Assets_000.pack";
    string srcPackFilePath = "/Users/coolking/Desktop/Assets_256.pack";
    //读取包文件到对象
// var pack = packer.LoadPackFile(srcPackFilePath);
//把包内的资源都解压出来.
// packer.UnpackAssetsFromPack(pack);
// return;
//从资源文件夹中加载出的包信息,不包含每一个资源的offset信息,也不包含pack文件头信息
    var fromAssetsDirLoadedPackInfo = packer.LoadAssetsIntoPackFile(srcPackFilePath.Substring(0,srcPackFilePath.Length-5));
    packer.SavePackFile(fromAssetsDirLoadedPackInfo);

    #region Finish

    Console.WriteLine("\r\n^_^解包完成^_^\r\n\r\n");
    string norman = "This tool is made by [Norman]";

    foreach (var c in norman)
    {
        Console.Write(c);
        if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
        {
            Console.Beep((int)c * 50, 100);
        }
        else
        {
            Thread.Sleep(100);
        }
    }
    if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
    {
        Console.Beep(3000, 1000);
    }
    Console.ResetColor();

    Console.ReadLine();

    #endregion
}

//把所有的包都解开并且重新包一遍
void Repack(string packSrcRootDir, string packDestRootDir)
{
    Packer packer = new Packer();
    var packFilesPathList = packer.ListPacks(packSrcRootDir);
    if (!Directory.Exists(packDestRootDir))
    {
        Directory.CreateDirectory(packDestRootDir);
    }

    var pos = 1;
    foreach (var packFilePath in packFilesPathList)
    {
        var packFile = packer.LoadPackFile(packFilePath);
        packFile.PackFileFullName = Path.Combine(packDestRootDir, packFile.PackFileName) + ".pack";
        packer.SavePackFile(packFile);
        Console.WriteLine("正在重存第{0}个资源包", pos++);
    }
}

Repack("/Volumes/BOOTCAMP/h1z1/201612/Resources/Assets", "/Volumes/BOOTCAMP/h1z1/Assets_New/");
