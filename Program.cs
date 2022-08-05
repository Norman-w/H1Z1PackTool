// See https://aka.ms/new-console-template for more information

using H1Z1PackTool;

Packer packer = new Packer();
var pack = packer.LoadPackFile("/Users/coolking/Desktop/Assets_256.pack");
packer.UnpackAssetsFromPack(pack);

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
