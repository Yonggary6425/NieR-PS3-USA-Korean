using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

public static class NierUsa
{
    static readonly byte[] SDAT_KEY = Hex("0D655EF8E674A98AB8505CFA7D012933");
    static readonly byte[] EDAT_KEY_0 = Hex("BE959CA8308DEFA2E5E180C63712A9AE");
    static readonly byte[] EDAT_KEY_1 = Hex("4CA9C14B01C95309969BEC68AA0BC081");
    const uint F_ENCKEY = 0x8, F_10 = 0x10, F_20 = 0x20, F_COMP = 0x1, F_02 = 0x2;

    static byte[] Hex(string s)
    {
        byte[] b = new byte[s.Length / 2];
        for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        return b;
    }
    static uint BE32(byte[] b, int o) { return ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3]; }
    static ushort BE16(byte[] b, int o) { return (ushort)((b[o] << 8) | b[o + 1]); }
    static ulong BE64(byte[] b, int o) { return ((ulong)BE32(b, o) << 32) | BE32(b, o + 4); }
    static void PutBE32(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
    static void PutBE64(byte[] b, int o, ulong v) { PutBE32(b, o, (uint)(v >> 32)); PutBE32(b, o + 4, (uint)v); }

    static byte[] AesX(byte[] key, byte[] iv, byte[] data, bool encrypt, CipherMode mode)
    {
        using (Aes a = System.Security.Cryptography.Aes.Create())
        {
            a.Mode = mode; a.Padding = PaddingMode.None; a.Key = key;
            if (mode == CipherMode.CBC) a.IV = iv;
            using (ICryptoTransform t = encrypt ? a.CreateEncryptor() : a.CreateDecryptor())
                return t.TransformFinalBlock(data, 0, data.Length);
        }
    }
    static byte[] EcbEnc(byte[] k, byte[] d) { return AesX(k, null, d, true, CipherMode.ECB); }

    static byte[] Dbl(byte[] l)
    {
        byte[] r = new byte[16]; int carry = 0;
        for (int i = 15; i >= 0; i--) { int v = (l[i] << 1) | carry; r[i] = (byte)v; carry = (l[i] >> 7) & 1; }
        if ((l[0] & 0x80) != 0) r[15] ^= 0x87;
        return r;
    }
    public static byte[] Cmac(byte[] key, byte[] data)
    {
        byte[] k1 = Dbl(EcbEnc(key, new byte[16])); byte[] k2 = Dbl(k1);
        int n = (data.Length + 15) / 16; bool full = data.Length > 0 && data.Length % 16 == 0;
        if (n == 0) n = 1;
        byte[] x = new byte[16];
        if (n > 1)
        {
            byte[] prefix = new byte[(n - 1) * 16];
            Buffer.BlockCopy(data, 0, prefix, 0, prefix.Length);
            byte[] c = AesX(key, new byte[16], prefix, true, CipherMode.CBC);
            Buffer.BlockCopy(c, c.Length - 16, x, 0, 16);
        }
        byte[] last = new byte[16]; int rem = data.Length - (n - 1) * 16;
        Buffer.BlockCopy(data, (n - 1) * 16, last, 0, rem);
        if (full) { for (int i = 0; i < 16; i++) last[i] ^= k1[i]; }
        else { last[rem] = 0x80; for (int i = 0; i < 16; i++) last[i] ^= k2[i]; }
        for (int i = 0; i < 16; i++) last[i] ^= x[i];
        return EcbEnc(key, last);
    }
    static bool Eq(byte[] a, int ao, byte[] b, int bo, int n) { for (int i = 0; i < n; i++) if (a[ao + i] != b[bo + i]) return false; return true; }
    static byte[] Sha1(byte[] d, int n) { using (SHA1 s = SHA1.Create()) return s.ComputeHash(d, 0, n); }
    static byte[] ReadAt(FileStream f, long off, int n)
    {
        byte[] b = new byte[n]; f.Seek(off, SeekOrigin.Begin); int got = 0;
        while (got < n) { int r = f.Read(b, got, n - got); if (r <= 0) throw new Exception("파일 끝을 넘어 읽음"); got += r; }
        return b;
    }
    public static string Hash(string path) { using(var f=File.OpenRead(path)) using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }
    static string HashBytes(byte[] b) { using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
    static long Num(Dictionary<string,object> d,string k) { return Convert.ToInt64(d[k]); }
    static Dictionary<string,object> Dict(object x) { return (Dictionary<string,object>)x; }
    static byte[] ZipRead(System.IO.Compression.ZipArchive z,string name) { var e=z.GetEntry(name); if(e==null) throw new Exception("Missing payload: "+name); using(var f=e.Open()) using(var m=new MemoryStream()) { f.CopyTo(m); return m.ToArray(); } }
    static byte[] Delta(System.IO.Compression.ZipArchive z,Dictionary<string,object> r,byte[] old) {
        if(old.Length!=Num(r,"sourceSize") || HashBytes(old)!=(string)r["sourceSha256"]) throw new Exception("Source asset mismatch: "+r["payload"]);
        byte[] b=ZipRead(z,(string)r["payload"]);
        if(b.Length!=Num(r,"targetSize") || old.Length==0) throw new Exception("Invalid delta length");
        for(int i=0;i<b.Length;i++) b[i]^=old[i%old.Length];
        if(HashBytes(b)!=(string)r["targetSha256"]) throw new Exception("Patched asset mismatch"); return b;
    }
    class Bits { byte[] b; int off,bits,pool; public Bits(byte[] b,int off) {this.b=b;this.off=off;} public int Get(int n) { int v=0; while(n>0) { if(bits==0) { if(off<16) throw new Exception("Invalid CRILAYLA stream"); pool=b[off--]; bits=8; } int t=Math.Min(bits,n); v=(v<<t)|((pool>>(bits-t))&((1<<t)-1));bits-=t;n-=t; } return v; } }
    static byte[] Unpack(byte[] b) {
        if(b.Length<16 || Encoding.ASCII.GetString(b,0,8)!="CRILAYLA") return b;
        int size=BitConverter.ToInt32(b,8),hoff=BitConverter.ToInt32(b,12); byte[] o=new byte[checked(size+256)]; Buffer.BlockCopy(b,checked(hoff+16),o,0,256);
        var r=new Bits(b,hoff+15); int w=o.Length-1;
        while(w>=256) { if(r.Get(1)==0) o[w--]=(byte)r.Get(8); else { int back=r.Get(13)+3,len=3; bool more=true; foreach(int n in new int[]{2,3,5,8}) {int x=r.Get(n);len+=x;if(x!=(1<<n)-1){more=false;break;}} if(more) {int x; do{x=r.Get(8);len+=x;}while(x==255);} for(int i=0;i<len && w>=256;i++,w--) o[w]=o[checked(w+back)]; } } return o;
    }
    static void CopyRegion(FileStream src,FileStream dst,long off,long dest,long count) { if(off<0 || count<0 || off>src.Length-count || dest<0) throw new Exception("Invalid region"); src.Position=off;dst.Position=dest;byte[] b=new byte[1048576];while(count>0){int n=src.Read(b,0,(int)Math.Min(count,b.Length));if(n==0)throw new EndOfStreamException();dst.Write(b,0,n);count-=n;} }
    public static void Rebuild(string source,string output,string patch) {
        using(var z=System.IO.Compression.ZipFile.OpenRead(patch)) {
            var j=new System.Web.Script.Serialization.JavaScriptSerializer();j.MaxJsonLength=64000000;
            var m=Dict(j.DeserializeObject(Encoding.UTF8.GetString(ZipRead(z,"manifest.json"))));
            if((string)m["format"]!="NIER-PS3-USA-KO-LOCAL-1") throw new Exception("Unsupported manifest");
            using(var f=File.OpenRead(source)) using(var o=new FileStream(output,FileMode.CreateNew,FileAccess.Write)) {
                var h=Dict(m["header"]);byte[] b=Delta(z,h,ReadAt(f,0,(int)Num(h,"sourceSize")));o.Write(b,0,b.Length);long end=b.Length;
                var entries=new List<Dictionary<string,object>>();foreach(var r in (object[])m["entries"])entries.Add(Dict(r));entries.Sort((a,c)=>Num(a,"targetOffset").CompareTo(Num(c,"targetOffset")));
                int idx=0;foreach(var r in entries) {long off=Num(r,"targetOffset"),sz=Num(r,"targetStoredSize");if(off<end)throw new Exception("Overlapping regions");
                    if(r.ContainsKey("change")){b=Delta(z,Dict(r["change"]),Unpack(ReadAt(f,Num(r,"sourceOffset"),checked((int)Num(r,"sourceStoredSize")))));if(b.Length!=sz)throw new Exception("Target size mismatch");o.Position=off;o.Write(b,0,b.Length);}
                    else {if(sz!=Num(r,"sourceStoredSize"))throw new Exception("Copy size mismatch");CopyRegion(f,o,Num(r,"sourceOffset"),off,sz);}end=checked(off+sz);if(++idx%500==0)Console.WriteLine("Archive: "+idx+" / "+entries.Count);
                }
                var tail=Dict(m["etoc"]);if(Num(tail,"targetOffset")<end || Num(tail,"targetOffset")+Num(tail,"size")!=Num(m,"targetCpkSize"))throw new Exception("Invalid tail");CopyRegion(f,o,Num(tail,"sourceOffset"),Num(tail,"targetOffset"),Num(tail,"size"));o.SetLength(Num(m,"targetCpkSize"));
            }
            if(Hash(output)!=(string)m["targetCpkSha256"])throw new Exception("CPK SHA256 mismatch");
        }
    }
    public static void Trophy(string source,string output,string patch) {using(var z=System.IO.Compression.ZipFile.OpenRead(patch)){var j=new System.Web.Script.Serialization.JavaScriptSerializer();j.MaxJsonLength=64000000;var m=Dict(j.DeserializeObject(Encoding.UTF8.GetString(ZipRead(z,"manifest.json"))));File.WriteAllBytes(output,Delta(z,Dict(m["trophy"]),File.ReadAllBytes(source)));}}
    public static void Sdat(string source,string output,string template,bool encrypt) {
        using(var t=File.OpenRead(template)) using(var f=File.OpenRead(source)) using(var o=new FileStream(output,FileMode.CreateNew,FileAccess.Write)) {
            byte[] h=ReadAt(t,0,256);int ver=(int)BE32(h,4);uint flags=BE32(h,128);int bs=checked((int)BE32(h,132));
            if(Encoding.ASCII.GetString(h,0,4)!="NPD\0" || flags!=0x0100000C || bs<=0)throw new Exception("Unsupported SDAT header");
            long size=encrypt?f.Length:checked((long)BE64(h,136));int nb=checked((int)((size+bs-1)/bs));byte[] meta=encrypt?new byte[checked(nb*16)]:ReadAt(f,256,checked(nb*16));
            byte[] key=new byte[16],iv=new byte[16],dev=new byte[16];Buffer.BlockCopy(h,96,dev,0,16);if(ver>1)Buffer.BlockCopy(h,64,iv,0,16);for(int i=0;i<16;i++)key[i]=(byte)(dev[i]^SDAT_KEY[i]);byte[] ek=ver==4?EDAT_KEY_1:EDAT_KEY_0,hk=AesX(ek,new byte[16],key,false,CipherMode.CBC);
            if(!encrypt && !Eq(Cmac(hk,ReadAt(t,0,160)),0,h,160,16))throw new Exception("SDAT header CMAC mismatch");
            long dataBase=256L+nb*16L;if(encrypt)o.Position=dataBase;
            for(int i=0;i<nb;i++) {int ln=(int)Math.Min(bs,size-(long)i*bs),aln=(ln+15)&~15;byte[] bk=new byte[16];if(ver>1)Buffer.BlockCopy(dev,0,bk,0,12);PutBE32(bk,12,(uint)i);byte[] k=AesX(ek,new byte[16],EcbEnc(key,bk),false,CipherMode.CBC);
                byte[] b=ReadAt(f,encrypt?(long)i*bs:dataBase+(long)i*bs,encrypt?ln:aln);if(encrypt){Array.Resize(ref b,aln);b=AesX(k,iv,b,true,CipherMode.CBC);Buffer.BlockCopy(Cmac(k,b),0,meta,i*16,16);o.Write(b,0,b.Length);}else{if(!Eq(Cmac(k,b),0,meta,i*16,16))throw new Exception("SDAT block CMAC mismatch: "+i);b=AesX(k,iv,b,false,CipherMode.CBC);o.Write(b,0,ln);}if(i%32768==0)Console.WriteLine((encrypt?"Encrypt: ":"Decrypt: ")+i+" / "+nb);
            }
            if(encrypt){byte[] hdr=new byte[256];Buffer.BlockCopy(h,0,hdr,0,144);PutBE64(hdr,136,(ulong)size);Buffer.BlockCopy(Cmac(hk,meta),0,hdr,144,16);byte[] prefix=new byte[160];Buffer.BlockCopy(hdr,0,prefix,0,160);Buffer.BlockCopy(Cmac(hk,prefix),0,hdr,160,16);o.Position=0;o.Write(hdr,0,256);o.Write(meta,0,meta.Length);}
        }
    }
}
