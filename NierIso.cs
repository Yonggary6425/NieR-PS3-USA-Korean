// Native ISO9660 level-3 / Joliet reader and writer for the BLUS30481 patcher.
// All game data is supplied locally by the user. No external ISO utility is used.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

public sealed class NierIso : IDisposable {
    const int Sector=2048;
    const long Chunk=0x3ffff800;
    FileStream input;
    public sealed class Extent { public long Offset,Length; }
    public sealed class Entry { public string Name; public long Length; public List<Extent> Parts=new List<Extent>(); }
    public Dictionary<string,Entry> Files=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
    HashSet<long> visited=new HashSet<long>();
    Encoding names;
    static uint LE(byte[] b,int o) {return BitConverter.ToUInt32(b,o);}
    static uint BE(byte[] b,int o) {return ((uint)b[o]<<24)|((uint)b[o+1]<<16)|((uint)b[o+2]<<8)|b[o+3];}
    static void Put(byte[] b,int o,uint n,bool big) {for(int i=0;i<4;i++)b[o+i]=(byte)(n>>(big?(3-i)*8:i*8));}
    static void Both(byte[] b,int o,uint n) {Put(b,o,n,false);Put(b,o+4,n,true);}
    static void Both16(byte[] b,int o,int n) {b[o]=(byte)n;b[o+1]=(byte)(n>>8);b[o+2]=(byte)(n>>8);b[o+3]=(byte)n;}
    static long Round(long n) {return checked((n+Sector-1)/Sector*Sector);}
    static byte[] Read(FileStream f,long off,int n) {if(off<0 || n<0 || off>f.Length-n)throw new IOException("ISO extent outside file");byte[] b=new byte[n];f.Position=off;int at=0;while(at<n){int r=f.Read(b,at,n-at);if(r==0)throw new EndOfStreamException();at+=r;}return b;}
    static void NameCheck(string n) {
        if(n.Length==0 || n=="." || n==".." || n.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || n.EndsWith(".") || n.EndsWith(" "))throw new IOException("Unsafe ISO filename: "+n);
        string stem=n.Split('.')[0].ToUpperInvariant();if(stem=="CON"||stem=="PRN"||stem=="AUX"||stem=="NUL"||System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(COM|LPT)[0-9]$"))throw new IOException("Reserved filename: "+n);
    }
    public NierIso(string path) : this(path,true) {}
    public NierIso(string path,bool preferJoliet) {
        input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try {
            if(input.Length%Sector!=0)throw new IOException("ISO size is not sector aligned");
            byte[] primary=null,joliet=null;bool ended=false;
            for(int i=16;i<64;i++){byte[] v=Read(input,(long)i*Sector,Sector);if(Encoding.ASCII.GetString(v,1,5)!="CD001" || v[6]!=1)throw new IOException("Unsupported ISO descriptor");if(v[0]==1)primary=v;if(v[0]==2&&v[88]==37&&v[89]==47&&(v[90]==64||v[90]==67||v[90]==69))joliet=v;if(v[0]==255){ended=true;break;}}
            if(primary==null||!ended)throw new IOException("ISO9660 descriptor missing");
            byte[] d=preferJoliet&&joliet!=null?joliet:primary;names=d==joliet?Encoding.BigEndianUnicode:Encoding.ASCII;
            if(d[128]!=0||d[129]!=8||LE(d,80)!=BE(d,84)||LE(d,80)*2048L>input.Length)throw new IOException("Unsupported ISO volume size");
            Walk("",LE(d,158),LE(d,166),0);
        } catch {input.Dispose();throw;}
    }
    void Walk(string parent,long lba,long size,int depth) {
        if(depth>32||size>64*1024*1024||!visited.Add(lba))throw new IOException("Invalid ISO directory tree");
        byte[] b=Read(input,checked(lba*Sector),checked((int)size));int pos=0;string continuation=null;
        while(pos<b.Length){int n=b[pos];if(n==0){pos=(pos/Sector+1)*Sector;continue;}if(n<34||pos+n>b.Length||pos%Sector+n>Sector)throw new IOException("Damaged ISO directory record");
            int p=pos;pos+=n;int nl=b[p+32];if(nl==0||33+nl>n||b[p+1]!=0||b[p+26]!=0||b[p+27]!=0)throw new IOException("Unsupported ISO record");
            if(nl==1&&(b[p+33]==0||b[p+33]==1))continue;
            string name=names.GetString(b,p+33,nl);if(name.EndsWith(";1"))name=name.Substring(0,name.Length-2);NameCheck(name);string rel=parent.Length==0?name:parent+"/"+name;
            uint off=LE(b,p+2),len=LE(b,p+10);if(off!=BE(b,p+6)||len!=BE(b,p+14)||(long)off*Sector>input.Length-len)throw new IOException("Invalid extent");
            if((b[p+25]&2)!=0){if(continuation!=null)throw new IOException("Broken multi-extent file");Walk(rel,off,len,depth+1);}
            else {Entry e;if(Files.TryGetValue(rel,out e)){if(continuation!=rel)throw new IOException("Duplicate ISO file");}else{if(continuation!=null)throw new IOException("Missing continuation");e=new Entry{Name=rel};Files.Add(rel,e);}e.Parts.Add(new Extent{Offset=(long)off*Sector,Length=len});e.Length+=len;continuation=(b[p+25]&128)!=0?rel:null;}
            if(Files.Count>100000)throw new IOException("Too many ISO files");
        }
        if(continuation!=null)throw new IOException("Truncated multi-extent file");
    }
    public byte[] SmallFile(string name) {Entry e=Files[name];if(e.Length>8*1024*1024)throw new IOException("Not a small file");using(var m=new MemoryStream()){Copy(e,m,null);return m.ToArray();}}
    void Copy(Entry e,Stream output,HashAlgorithm hash) {byte[] b=new byte[4*1024*1024];foreach(var x in e.Parts){input.Position=x.Offset;long left=x.Length;while(left>0){int n=input.Read(b,0,(int)Math.Min(left,b.Length));if(n==0)throw new EndOfStreamException();if(output!=null)output.Write(b,0,n);if(hash!=null)hash.TransformBlock(b,0,n,b,0);left-=n;}}if(hash!=null)hash.TransformFinalBlock(new byte[0],0,0);}
    public string FileHash(string name) {using(var h=SHA256.Create()){Copy(Files[name],null,h);return BitConverter.ToString(h.Hash).Replace("-","").ToLowerInvariant();}}
    public void CheckGame() {
        if(!Files.ContainsKey("PS3_GAME/PARAM.SFO")||!Files.ContainsKey("PS3_GAME/USRDIR/EBOOT.BIN")||!Files.ContainsKey("PS3_DISC.SFB"))throw new IOException("PS3 game files missing");
        byte[] s=SmallFile("PS3_GAME/PARAM.SFO");if(s.Length<20||s[0]!=0||Encoding.ASCII.GetString(s,1,3)!="PSF")throw new IOException("Encrypted or invalid ISO: decrypted ISO required");
        uint keys=LE(s,8),data=LE(s,12),count=LE(s,16);bool matched=false;
        for(int i=0;i<count;i++){int p=checked(20+i*16);if(p+16>s.Length)throw new IOException("Invalid SFO");int k=checked((int)keys+BitConverter.ToUInt16(s,p));int end=k;while(end<s.Length&&s[end]!=0)end++;if(k>=s.Length||end==s.Length)throw new IOException("Invalid SFO key");if(Encoding.ASCII.GetString(s,k,end-k)=="TITLE_ID"){int at=checked((int)(data+LE(s,p+12)));int len=checked((int)LE(s,p+4));if(at<0||at>s.Length-len)throw new IOException("Invalid SFO value");matched=Encoding.ASCII.GetString(s,at,len).TrimEnd('\0')=="BLUS30481";}}
        if(!matched)throw new IOException("Only BLUS30481 is supported");
        var e=Files["PS3_GAME/USRDIR/EBOOT.BIN"];byte[] magic=Read(input,e.Parts[0].Offset,4);if(Encoding.ASCII.GetString(magic)!="SCE\0")throw new IOException("Encrypted or invalid EBOOT: decrypted ISO required");
        e=Files["PS3_GAME/USRDIR/MEDIA/STABLE.SDAT"];magic=Read(input,e.Parts[0].Offset,4);if(Encoding.ASCII.GetString(magic)!="NPD\0")throw new IOException("Encrypted or invalid SDAT: decrypted ISO required");
    }
    public Dictionary<string,string> Extract(string dest) {
        if(Directory.Exists(dest)||File.Exists(dest))throw new IOException("Extraction destination exists");Directory.CreateDirectory(dest);string root=Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var result=new Dictionary<string,string>();int i=0;foreach(var e in Files.Values){string p=Path.GetFullPath(Path.Combine(root,e.Name.Replace('/',Path.DirectorySeparatorChar)));if(!p.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe extraction path");Directory.CreateDirectory(Path.GetDirectoryName(p));using(var f=new FileStream(p,FileMode.CreateNew,FileAccess.Write))using(var hash=SHA256.Create()){Copy(e,f,hash);result.Add(e.Name,BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant());}Console.WriteLine("ISO extract: "+(++i)+" / "+Files.Count);}return result;
    }
    public Dictionary<string,string> VerifyFolder(string root) {
        var result=new Dictionary<string,string>();var local=Directory.GetFiles(root,"*",SearchOption.AllDirectories);if(local.Length!=Files.Count)throw new IOException("ISO file count mismatch");
        foreach(string path in local){string name=path.Substring(root.TrimEnd('\\').Length).TrimStart('\\').Replace('\\','/');Entry e;if(!Files.TryGetValue(name,out e)||e.Length!=new FileInfo(path).Length)throw new IOException("ISO file size mismatch: "+name);string h;using(var f=File.OpenRead(path))using(var s=SHA256.Create())h=BitConverter.ToString(s.ComputeHash(f)).Replace("-","").ToLowerInvariant();if(FileHash(name)!=h)throw new IOException("ISO file hash mismatch: "+name);result.Add(name,h);Console.WriteLine("ISO verified: "+name); }return result;
    }
    public void Dispose(){input.Dispose();}

    sealed class Node {public string Name,Path;public Node Parent;public bool Dir;public long Length,Data;public int Id;public long[] Lba=new long[2],Size=new long[2];public List<Node> Children=new List<Node>();}
    static Node Scan(string path,Node parent) {var a=File.GetAttributes(path);if((a&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse points not supported");var n=new Node{Path=path,Name=System.IO.Path.GetFileName(path),Parent=parent,Dir=(a&FileAttributes.Directory)!=0};if(parent!=null){NameCheck(n.Name);foreach(char c in n.Name)if(c>127)throw new IOException("This game ISO writer requires ASCII game filenames");if(n.Name.Length>64)throw new IOException("Filename too long");}if(n.Dir){foreach(string p in Directory.GetFileSystemEntries(path))n.Children.Add(Scan(p,n));n.Children.Sort((a1,b)=>StringComparer.Ordinal.Compare(a1.Name,b.Name));}else n.Length=new FileInfo(path).Length;return n;}
    static byte[] Id(Node n,int view){return (view==0?Encoding.ASCII:Encoding.BigEndianUnicode).GetBytes(n.Name+(n.Dir?"":";1"));}
    static byte[] Record(byte[] name,long lba,long size,byte flags) {int len=33+name.Length+(name.Length%2==0?1:0);if(len>255)throw new IOException("ISO identifier too long");byte[] b=new byte[len];b[0]=(byte)len;Both(b,2,checked((uint)lba));Both(b,10,checked((uint)size));b[18]=126;b[19]=1;b[20]=1;b[25]=flags;Both16(b,28,1);b[32]=(byte)name.Length;name.CopyTo(b,33);return b;}
    static void AppendRecord(MemoryStream m,byte[] r){if(m.Position%Sector+r.Length>Sector)m.SetLength(Round(m.Position));m.Position=m.Length;m.Write(r,0,r.Length);}
    static byte[] DirData(Node d,int view) {using(var m=new MemoryStream()){AppendRecord(m,Record(new byte[]{0},d.Lba[view],d.Size[view],2));Node p=d.Parent??d;AppendRecord(m,Record(new byte[]{1},p.Lba[view],p.Size[view],2));foreach(Node c in d.Children){if(c.Dir)AppendRecord(m,Record(Id(c,view),c.Lba[view],c.Size[view],2));else{long done=0;do{long len=Math.Min(Chunk,c.Length-done);AppendRecord(m,Record(Id(c,view),c.Data+done/Sector,len,(byte)(done+len<c.Length?128:0)));done+=len;}while(done<c.Length);}}m.SetLength(Round(m.Length));return m.ToArray();}}
    static byte[] PathTable(List<Node> dirs,int view,bool big){using(var m=new MemoryStream()){foreach(var d in dirs){byte[] name=d.Parent==null?new byte[]{0}:Id(d,view);byte[] b=new byte[8+name.Length+name.Length%2];b[0]=(byte)name.Length;Put(b,2,(uint)d.Lba[view],big);int id=d.Parent==null?1:d.Parent.Id;b[6]=(byte)(big?id>>8:id);b[7]=(byte)(big?id:id>>8);name.CopyTo(b,8);m.Write(b,0,b.Length);}return m.ToArray();}}
    static void WriteAt(FileStream f,long off,byte[] b){f.Position=off;f.Write(b,0,b.Length);}
    static byte[] Volume(Node root,int view,long sectors,int pathSize,long lp,long mp) {
        byte[] v=new byte[Sector];v[0]=(byte)(view+1);Encoding.ASCII.GetBytes("CD001").CopyTo(v,1);v[6]=1;
        Encoding enc=view==0?Encoding.ASCII:Encoding.BigEndianUnicode;
        for(int i=8;i<72;i++)v[i]=(byte)(view==0?32:0);enc.GetBytes("PS3VOLUME").CopyTo(v,40);
        if(view==1){v[88]=37;v[89]=47;v[90]=69;}
        Both(v,80,checked((uint)sectors));Both16(v,120,1);Both16(v,124,1);Both16(v,128,Sector);Both(v,132,(uint)pathSize);Put(v,140,(uint)lp,false);Put(v,148,(uint)mp,true);
        Record(new byte[]{0},root.Lba[view],root.Size[view],2).CopyTo(v,156);
        if(view==0)for(int i=190;i<813;i++)v[i]=32;
        enc.GetBytes("PS3VOLUME").CopyTo(v,190);foreach(int off in new int[]{813,830,847,864})Encoding.ASCII.GetBytes("0000000000000000").CopyTo(v,off);
        Encoding.ASCII.GetBytes("2026010100000000").CopyTo(v,813);v[881]=1;return v;
    }
    public static void Build(string folder,string output) {
        if(File.Exists(output)||Directory.Exists(output))throw new IOException("Output exists");
        Node root=Scan(folder,null);var dirs=new List<Node>{root};var files=new List<Node>();for(int i=0;i<dirs.Count;i++){Node n=dirs[i];n.Id=i+1;foreach(var c in n.Children)if(c.Dir)dirs.Add(c);else files.Add(c);}if(dirs.Count>65535)throw new IOException("Too many directories");
        long next=32;long[,] tables=new long[2,2];int[] sizes=new int[2];for(int v=0;v<2;v++){sizes[v]=PathTable(dirs,v,false).Length;for(int b=0;b<2;b++){tables[v,b]=next;next+=Round(sizes[v])/Sector;}}
        for(int v=0;v<2;v++)foreach(Node d in dirs){d.Size[v]=DirData(d,v).Length;d.Lba[v]=next;next+=d.Size[v]/Sector;}
        next=(next+31)/32*32;foreach(Node f in files){f.Data=next;next+=Round(f.Length)/Sector;}long sectors=(next+31)/32*32;
        using(var o=new FileStream(output,FileMode.CreateNew,FileAccess.Write)){o.SetLength(sectors*Sector);byte[] h=new byte[32768];Put(h,0,1,true);Put(h,8,0,true);Put(h,12,(uint)(sectors-1),true);Encoding.ASCII.GetBytes("PlayStation3").CopyTo(h,0x800);Encoding.ASCII.GetBytes("BLUS-30481".PadRight(32)).CopyTo(h,0x810);WriteAt(o,0,h);
            for(int v=0;v<2;v++){WriteAt(o,(16+v)*Sector,Volume(root,v,sectors,sizes[v],tables[v,0],tables[v,1]));for(int b=0;b<2;b++)WriteAt(o,tables[v,b]*Sector,PathTable(dirs,v,b==1));foreach(Node d in dirs)WriteAt(o,d.Lba[v]*Sector,DirData(d,v));}
            byte[] end=new byte[Sector];end[0]=255;Encoding.ASCII.GetBytes("CD001").CopyTo(end,1);end[6]=1;WriteAt(o,18*Sector,end);
            byte[] buf=new byte[4*1024*1024];foreach(Node f in files){o.Position=f.Data*Sector;using(var src=File.OpenRead(f.Path)){if(src.Length!=f.Length)throw new IOException("Game file changed during ISO creation");int n;while((n=src.Read(buf,0,buf.Length))>0)o.Write(buf,0,n);}Console.WriteLine("ISO write: "+f.Name);}
        }
    }
}
