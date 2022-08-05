namespace H1Z1PackTool;

public class PackFile
{
    private string _packFileFullName = "";
    public string PackFileFullName
    {
        get { return _packFileFullName;}
        set { _packFileFullName = value; }
    }

    public string PackFileName
    {
        get
        {
            return System.IO.Path.GetFileNameWithoutExtension(this._packFileFullName);
        }
    }

    public string PackFileDir
    {
        get
        {
            return System.IO.Path.GetDirectoryName(this._packFileFullName);
        } }
    private Dictionary<string, Asset> _assets = new Dictionary<string, Asset>();
    public int AssetCount
    {
        get { return this._assets.Count; }
    }
    public Dictionary<string,Asset> Assets
    {
        get { return _assets;}
    }

    public long ByteCount { get; set; }

    public int AssetGroupCount { get; set; }
}
