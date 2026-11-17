// NieR PS3 북미판(BLUS30481) 한국어 패치 v1.0 — EXE 적용기
// PowerShell 적용기(EasyPatch.ps1 / Apply-Korean-Patch.ps1 / Apply-FolderPatch.ps1)와 같은 절차를
// .NET Framework 4.x 단일 실행파일로 옮긴 것이다. 패치 엔진(NierPatch.cs, NierIso.cs)은 그대로 사용한다.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

[assembly: AssemblyTitle("NieR PS3 USA Korean Patcher")]
[assembly: AssemblyProduct("NieR PS3 USA Korean Patch")]
[assembly: AssemblyDescription("Unofficial Korean fan patch applier for NIER (PS3, BLUS30481)")]
[assembly: AssemblyCopyright("Unofficial fan patch by Yonggary6425")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class Program
{
    const string Version = "v1.0";
    const string DeltaSha256 = "c690f442621528a79bec00d8d019f421b658976e81f6f28421fd99e05a3e6e55";
    const string SdatRel = @"PS3_GAME\USRDIR\MEDIA\STABLE.SDAT";
    const string TrpRel = @"PS3_GAME\TROPDIR\NPWR01125_00\TROPHY.TRP";
    const long GiB = 1024L * 1024 * 1024;

    static string deltaPath;

    sealed class Plan { public string Source, Output, Backup, Parent, Mode; public bool IsIso; }

    static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        string source = "", mode = "", trophy = "";
        bool pause = true;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a.Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) mode = args[++i].ToLowerInvariant();
            else if (a.Equals("--trophy", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) trophy = args[++i].ToLowerInvariant();
            else if (a.Equals("--no-pause", StringComparison.OrdinalIgnoreCase)) pause = false;
            else if (source.Length == 0) source = a;
            else { Console.WriteLine("알 수 없는 인자: " + a); return Finish(2, pause); }
        }
        if (mode != "" && mode != "copy" && mode != "replace") { Console.WriteLine("--mode 는 copy 또는 replace 입니다."); return Finish(2, pause); }
        if (trophy != "" && trophy != "original" && trophy != "korean") { Console.WriteLine("--trophy 는 original 또는 korean 입니다."); return Finish(2, pause); }

        Plan plan = null;
        try
        {
            Color(ConsoleColor.Cyan, "=== NieR PS3 북미판 " + Version + " 한국어 패치 (EXE) ===");
            if (source.Length == 0) { Console.Write("ISO 또는 게임 폴더를 이 창으로 끌어다 놓고 Enter를 누르세요: "); source = (Console.ReadLine() ?? "").Trim(); }
            if (source.Length == 0) throw new Exception("원본을 선택하지 않았습니다.");
            Console.WriteLine();
            if (mode == "")
            {
                Console.WriteLine("1. 새로운 폴더 / ISO 생성 (원본 옆에 자동 저장)");
                Console.WriteLine("2. 선택한 폴더 / ISO 위치에 바로 적용 (원본 자동 백업)");
                while (mode == "") { string a = Ask("적용 방식 [1/2, Enter=1]"); if (a == "" || a == "1") mode = "copy"; else if (a == "2") mode = "replace"; }
            }
            plan = NewPlan(source, mode);
            string self = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');
            if (!plan.IsIso && (self + "\\").StartsWith(plan.Source + "\\", StringComparison.OrdinalIgnoreCase)) throw new Exception("패처를 게임 폴더 밖으로 옮긴 뒤 실행하세요.");
            if (trophy == "")
            {
                Console.WriteLine("1. 원본 트로피 (PS3 실기용 권장)");
                Console.WriteLine("2. 한글 트로피");
                while (trophy == "") { string a = Ask("트로피 [1/2, Enter=1]"); if (a == "" || a == "1") trophy = "original"; else if (a == "2") trophy = "korean"; }
            }
            Console.WriteLine("입력: " + plan.Source);
            if (mode == "copy") Console.WriteLine("자동 저장 위치: " + plan.Output);
            else Console.WriteLine("검증 완료 후 선택한 위치에 적용합니다. 원본은 별도 백업됩니다.");

            deltaPath = ExtractDelta();
            Dictionary<string, object> report;
            string reportPath;
            if (plan.IsIso) { reportPath = plan.Output + ".patch-report.json"; report = IsoPatch(plan.Source, plan.Output, trophy, reportPath); }
            else { reportPath = Path.Combine(plan.Output, "PATCH-VERIFIED.json"); report = FolderPatch(plan.Source, plan.Output, trophy); }

            string want = (string)Manifest()["targetSdatSha256"];
            string got = plan.IsIso ? (string)((Dictionary<string, string>)report["files"])["PS3_GAME/USRDIR/MEDIA/STABLE.SDAT"] : (string)report["sdatSha256"];
            if (got != want || (string)report["trophyMode"] != trophy) throw new Exception("패치 결과 검증 기록이 예상과 다릅니다. 교체하지 않았습니다.");
            if (!plan.IsIso)
            {
                // 게임 폴더 최상위의 추가 파일(PS3_UPDATE 등)도 결과에 보존한다.
                foreach (string f in Directory.GetFileSystemEntries(plan.Source))
                {
                    string name = Path.GetFileName(f);
                    if (Eq(name, "PS3_GAME") || Eq(name, "PS3_DISC.SFB") || Eq(name, "PATCH-VERIFIED.json")) continue;
                    string dest = Path.Combine(plan.Output, name);
                    if (File.Exists(dest) || Directory.Exists(dest)) throw new Exception("추가 파일의 출력 경로 충돌");
                    CopyTree(f, dest);
                }
            }
            report["applyMode"] = mode;
            report["originalBackupPath"] = plan.Backup;
            report["originalLocationReplaced"] = mode == "replace";
            WriteJson(reportPath, report);
            Publish(plan);
            string final;
            if (mode == "replace")
            {
                final = plan.Source;
                if (plan.IsIso)
                {
                    string destReport = final + ".patch-report.json";
                    if (File.Exists(destReport)) destReport = final + ".patch-report-" + Guid.NewGuid().ToString("N") + ".json";
                    File.Move(reportPath, destReport);
                }
            }
            else final = plan.Output;
            Console.WriteLine();
            Color(ConsoleColor.Green, "완료: " + final);
            Cleanup();
            return Finish(0, pause);
        }
        catch (Exception e)
        {
            Color(ConsoleColor.Red, "오류: " + e.Message);
            if (plan != null && plan.Backup != null) Console.WriteLine("백업 예정/보존 위치: " + plan.Backup);
            Cleanup();
            return Finish(1, pause);
        }
    }

    // ---------- 공통 ----------
    static int Finish(int code, bool pause)
    {
        if (pause)
        {
            Console.WriteLine();
            Console.WriteLine(code == 0 ? "모든 작업이 완료되었습니다. 아무 키나 누르면 종료합니다." : "작업이 오류로 중단되었습니다. 위 내용을 확인한 뒤 아무 키나 누르면 종료합니다.");
            try { Console.ReadKey(true); } catch { }
        }
        return code;
    }
    static void Color(ConsoleColor c, string s) { var old = Console.ForegroundColor; Console.ForegroundColor = c; Console.WriteLine(s); Console.ForegroundColor = old; }
    static string Ask(string q) { Console.Write(q + ": "); return (Console.ReadLine() ?? "").Trim(); }
    static bool Eq(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    static bool Exists(string p) { return File.Exists(p) || Directory.Exists(p); }
    static string Full(string p) { return Path.GetFullPath(p.Trim().Trim('"')).TrimEnd('\\'); }

    static string ExtractDelta()
    {
        string p = Path.Combine(Path.GetTempPath(), "nier-ps3-ko-" + Guid.NewGuid().ToString("N") + ".zip");
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ps3-local-delta.zip"))
        {
            if (s == null) throw new Exception("내장 패치 데이터를 찾을 수 없습니다.");
            using (var f = new FileStream(p, FileMode.CreateNew, FileAccess.Write)) s.CopyTo(f);
        }
        if (NierUsa.Hash(p) != DeltaSha256) throw new Exception("내장 패치 데이터가 손상되었습니다. 다시 내려받으세요.");
        return p;
    }
    static void Cleanup() { try { if (deltaPath != null && File.Exists(deltaPath)) File.Delete(deltaPath); } catch { } }

    static Dictionary<string, object> manifest;
    static Dictionary<string, object> Manifest()
    {
        if (manifest != null) return manifest;
        using (var z = System.IO.Compression.ZipFile.OpenRead(deltaPath))
        using (var r = new StreamReader(z.GetEntry("manifest.json").Open(), Encoding.UTF8))
        {
            var j = new JavaScriptSerializer(); j.MaxJsonLength = 64000000;
            manifest = (Dictionary<string, object>)j.DeserializeObject(r.ReadToEnd());
        }
        if ((string)manifest["format"] != "NIER-PS3-USA-KO-LOCAL-1" || (string)manifest["titleId"] != "BLUS30481") throw new Exception("지원하지 않는 패치 데이터입니다.");
        return manifest;
    }
    static void WriteJson(string path, object o)
    {
        var j = new JavaScriptSerializer();
        File.WriteAllText(path, j.Serialize(o), new UTF8Encoding(true));
    }
    static void CopyTree(string from, string to)
    {
        if (Directory.Exists(from))
        {
            Directory.CreateDirectory(to);
            foreach (string e in Directory.GetFileSystemEntries(from)) CopyTree(e, Path.Combine(to, Path.GetFileName(e)));
        }
        else File.Copy(from, to, false);
    }
    static bool HasReparse(string root)
    {
        foreach (string e in Directory.GetFileSystemEntries(root))
        {
            var a = File.GetAttributes(e);
            if ((a & FileAttributes.ReparsePoint) != 0) return true;
            if ((a & FileAttributes.Directory) != 0 && HasReparse(e)) return true;
        }
        return false;
    }
    static void CheckSpace(string path, long need, string what)
    {
        var d = new DriveInfo(Path.GetPathRoot(path));
        if (d.AvailableFreeSpace < need) throw new Exception(what + " 작업 공간 부족. 최소 필요 GiB: " + Math.Ceiling(need / (double)GiB));
    }

    // ---------- 자동 경로 / 교체 (EasyPatch.Common.ps1) ----------
    static string FreePath(string candidate)
    {
        string p = candidate; int i = 2;
        while (Exists(p) || File.Exists(p + ".patch-report.json"))
        {
            if (Eq(Path.GetExtension(candidate), ".iso")) p = Path.Combine(Path.GetDirectoryName(candidate), Path.GetFileNameWithoutExtension(candidate) + " (" + i + ").iso");
            else p = candidate + " (" + i + ")";
            i++;
        }
        return p;
    }
    static Plan NewPlan(string input, string mode)
    {
        string s = Full(input);
        if (!Exists(s)) throw new Exception("원본 경로를 찾을 수 없습니다.");
        if (Directory.Exists(s) && Eq(Path.GetFileName(s), "PS3_GAME")) s = Path.GetDirectoryName(s);
        if ((File.GetAttributes(s) & FileAttributes.ReparsePoint) != 0) throw new Exception("연결된 파일/폴더 대신 실제 원본을 사용하세요.");
        bool isIso = File.Exists(s);
        if (isIso && !Eq(Path.GetExtension(s), ".iso")) throw new Exception("ISO 파일 또는 게임 폴더만 지원합니다.");
        string parent = Path.GetDirectoryName(s);
        if (string.IsNullOrEmpty(parent)) throw new Exception("드라이브 루트는 게임 폴더로 사용할 수 없습니다.");
        if (!isIso)
        {
            if (!File.Exists(Path.Combine(s, SdatRel))) throw new Exception("PS3_GAME이 들어 있는 게임 폴더를 선택하세요.");
            if (HasReparse(s)) throw new Exception("연결/재분석 지점이 있는 게임 폴더는 지원하지 않습니다.");
        }
        var plan = new Plan { Source = s, IsIso = isIso, Mode = mode, Parent = parent };
        string name = Path.GetFileName(s);
        if (mode == "copy")
        {
            name = isIso ? Path.GetFileNameWithoutExtension(s) + "_Korean.iso" : name + "_Korean";
            plan.Output = FreePath(Path.Combine(parent, name));
        }
        else
        {
            plan.Output = Path.Combine(parent, ".nier-ready-" + Guid.NewGuid().ToString("N") + (isIso ? ".iso" : ""));
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string b = isIso ? Path.GetFileNameWithoutExtension(s) + "_OriginalBackup_" + stamp + ".iso" : name + "_OriginalBackup_" + stamp;
            plan.Backup = FreePath(Path.Combine(parent, b));
        }
        return plan;
    }
    static void Move(string from, string to, bool iso) { if (iso) File.Move(from, to); else Directory.Move(from, to); }
    static void Publish(Plan p)
    {
        if (p.Mode != "replace") return;
        string s = Path.GetFullPath(p.Source), o = Path.GetFullPath(p.Output), b = Path.GetFullPath(p.Backup);
        if (s == o || s == b || o == b) throw new Exception("교체 경로가 중복됩니다.");
        foreach (string x in new[] { s, o, b }) if (Path.GetDirectoryName(x) != p.Parent) throw new Exception("교체 경로가 원본 상위 폴더를 벗어났습니다.");
        if (!Exists(s) || !Exists(o) || Exists(b)) throw new Exception("교체 대상 또는 백업 경로가 변경되었습니다.");
        // 두 번의 이름 변경 사이에 중단되어도 복구할 수 있도록 기록을 남긴다.
        string journal = o + ".recovery.json";
        if (File.Exists(journal)) throw new Exception("복구 기록이 이미 있습니다.");
        WriteJson(journal, new Dictionary<string, object> { { "originalPath", s }, { "verifiedResult", o }, { "backupPath", b }, { "status", "prepared" }, { "recovery", "If originalPath is missing, rename backupPath back to originalPath. Keep all files until checked." } });
        Move(s, b, p.IsIso);
        try { Move(o, s, p.IsIso); }
        catch { if (!Exists(s)) Move(b, s, p.IsIso); throw; }
        WriteJson(journal, new Dictionary<string, object> { { "originalPath", s }, { "backupPath", b }, { "status", "completed" }, { "recovery", "Original is preserved at backupPath. The patched game is at originalPath." } });
        Console.WriteLine("원본 백업: " + b);
    }

    // ---------- 폴더 패치 (Apply-FolderPatch.ps1) ----------
    static Dictionary<string, object> FolderPatch(string source, string output, string trophy)
    {
        Console.WriteLine("NieR PS3 USA BLUS30481 Korean Patch " + Version + " - folder");
        source = Full(source); output = Full(output);
        if (Eq(source, output) || output.StartsWith(source + "\\", StringComparison.OrdinalIgnoreCase) || source.StartsWith(output + "\\", StringComparison.OrdinalIgnoreCase)) throw new Exception("원본과 출력 폴더는 서로 분리해야 합니다.");
        if (Exists(output)) throw new Exception("출력 폴더가 이미 존재합니다.");
        string parent = Path.GetDirectoryName(output);
        if (!Directory.Exists(parent)) throw new Exception("출력 폴더의 상위 폴더가 없습니다.");
        string sdat = Path.Combine(source, SdatRel), trp = Path.Combine(source, TrpRel);
        if (!File.Exists(sdat)) throw new Exception("STABLE.SDAT가 없습니다.");
        if (!File.Exists(trp)) throw new Exception("TROPHY.TRP가 없습니다.");
        var m = Manifest(); var mt = (Dictionary<string, object>)m["trophy"];
        Console.WriteLine("원본 해시 확인 중...");
        if (NierUsa.Hash(sdat) != (string)m["sourceSdatSha256"]) throw new Exception("원본 STABLE.SDAT 불일치: 지역/버전 또는 이미 패치된 파일을 확인하세요.");
        if (NierUsa.Hash(trp) != (string)mt["sourceSha256"]) throw new Exception("원본 TROPHY.TRP 불일치");
        string baseDir = Path.Combine(source, "PS3_GAME");
        if (HasReparse(baseDir)) throw new Exception("게임 폴더의 연결/재분석 지점은 지원하지 않습니다.");
        string[] files = Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories);
        string[] dirs = Directory.GetDirectories(baseDir, "*", SearchOption.AllDirectories);
        long total = files.Sum(f => new FileInfo(f).Length);
        long need = Math.Max(18 * GiB, total + 2 * Convert.ToInt64(m["targetCpkSize"]) + GiB);
        CheckSpace(output, need, "폴더");
        string stage = Path.Combine(parent, ".nier-ps3-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            string plain = Path.Combine(stage, "source.cpk"), rebuilt = Path.Combine(stage, "korean.cpk"), game = Path.Combine(stage, "game");
            Console.WriteLine("임시 작업 폴더: " + stage);
            NierUsa.Sdat(sdat, plain, sdat, false);
            NierUsa.Rebuild(plain, rebuilt, deltaPath);
            Directory.CreateDirectory(Path.Combine(game, "PS3_GAME"));
            string dst = Path.Combine(game, SdatRel);
            Console.WriteLine("게임 파일 복사 중...");
            foreach (string d in dirs) Directory.CreateDirectory(Path.Combine(game, "PS3_GAME", d.Substring(baseDir.Length).TrimStart('\\')));
            foreach (string f in files)
            {
                if (Eq(f, sdat)) continue;
                string target = Path.Combine(game, "PS3_GAME", f.Substring(baseDir.Length).TrimStart('\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(f, target, false);
            }
            string disc = Path.Combine(source, "PS3_DISC.SFB");
            if (File.Exists(disc)) File.Copy(disc, Path.Combine(game, "PS3_DISC.SFB"), false);
            NierUsa.Sdat(rebuilt, dst, sdat, true);
            Console.WriteLine("암호화 완료. 최종 게임 파일 검증 중입니다. 잠시 기다려 주세요.");
            if (NierUsa.Hash(dst) != (string)m["targetSdatSha256"]) throw new Exception("최종 SDAT SHA256 불일치");
            string destTrp = Path.Combine(game, TrpRel);
            string trophyHash = (string)mt["sourceSha256"];
            if (trophy == "korean") { File.Delete(destTrp); NierUsa.Trophy(trp, destTrp, deltaPath); trophyHash = (string)mt["targetSha256"]; }
            if (NierUsa.Hash(destTrp) != trophyHash) throw new Exception("트로피 결과 SHA256 불일치");
            var rec = new Dictionary<string, object> { { "titleId", "BLUS30481" }, { "sdatSha256", m["targetSdatSha256"] }, { "trophyMode", trophy }, { "trophySha256", trophyHash }, { "sourcePreserved", true }, { "engine", "NieR-PS3-USA-Korean-Patcher.exe (.NET Framework)" } };
            WriteJson(Path.Combine(game, "PATCH-VERIFIED.json"), rec);
            Directory.Move(game, output);
            File.Delete(plain); File.Delete(rebuilt); Directory.Delete(stage);
            Console.WriteLine("게임 데이터 패치 단계가 끝났습니다. 나머지 작업을 진행합니다.");
            return rec;
        }
        catch
        {
            Console.WriteLine("진단용 작업 파일을 보존했습니다: " + stage);
            throw;
        }
    }

    // ---------- ISO 패치 (Apply-Korean-Patch.ps1) ----------
    static Dictionary<string, object> IsoPatch(string source, string output, string trophy, string reportPath)
    {
        Console.WriteLine("NieR PS3 USA BLUS30481 " + Version + " - ISO");
        source = Full(source); output = Full(output);
        if (Eq(source, output)) throw new Exception("원본과 출력 경로는 분리해야 합니다.");
        if (Exists(output)) throw new Exception("출력이 이미 존재합니다.");
        if (!Eq(Path.GetExtension(source), ".iso") || !Eq(Path.GetExtension(output), ".iso")) throw new Exception("ISO 입력과 출력은 .iso 확장자여야 합니다. 분할 ISO는 지원하지 않습니다.");
        if (File.Exists(reportPath)) throw new Exception("같은 이름의 검증 보고서가 있습니다.");
        string parent = Path.GetDirectoryName(output);
        string stage = null;
        using (var iso = new NierIso(source))
        {
            try
            {
                iso.CheckGame();
                var drive = new DriveInfo(Path.GetPathRoot(output));
                if (drive.DriveFormat == "FAT32") throw new Exception("4GB 이상 ISO를 저장할 수 있는 NTFS/exFAT 드라이브가 필요합니다.");
                CheckSpace(output, Math.Max(32 * GiB, new FileInfo(source).Length * 3 + 10 * GiB), "ISO");
                stage = Path.Combine(parent, ".nier-iso-build-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                string extracted = Path.Combine(stage, "source"), game = Path.Combine(stage, "korean"), tempIso = Path.Combine(stage, "patched.iso");
                Console.WriteLine("작업 폴더: " + stage);
                Console.WriteLine("[1/4] 원본 ISO 추출 및 파일별 SHA256 계산");
                var sourceHashes = iso.Extract(extracted);
                Console.WriteLine("[2/4] 한글패치 적용");
                var rec = FolderPatch(extracted, game, trophy);
                File.Move(Path.Combine(game, "PATCH-VERIFIED.json"), Path.Combine(stage, "PATCH-VERIFIED.json"));
                // PS3_GAME 밖의 디스크 파일(PS3_UPDATE 등)도 보존한다.
                Console.WriteLine("남은 디스크 파일 복사 중입니다. 아직 최종 완료가 아닙니다.");
                foreach (string name in sourceHashes.Keys)
                {
                    string dest = Path.Combine(game, name.Replace('/', '\\'));
                    if (!File.Exists(dest)) { Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(Path.Combine(extracted, name.Replace('/', '\\')), dest, false); }
                }
                Console.WriteLine("[3/4] 새 PS3 ISO 생성 (ISO9660 + Joliet, 4GB 이상 파일 지원)");
                NierIso.Build(game, tempIso);
                Console.WriteLine("[4/4] ISO 내부 전체 파일 및 두 디렉터리 구조 검증");
                Dictionary<string, string> hashes;
                using (var check = new NierIso(tempIso))
                {
                    check.CheckGame(); hashes = check.VerifyFolder(game);
                    if (hashes.Count != sourceHashes.Count) throw new Exception("원본과 출력 ISO의 파일 개수가 다릅니다.");
                    foreach (var kv in sourceHashes)
                    {
                        string expected = kv.Value;
                        if (Eq(kv.Key, "PS3_GAME/USRDIR/MEDIA/STABLE.SDAT")) expected = (string)rec["sdatSha256"];
                        else if (Eq(kv.Key, "PS3_GAME/TROPDIR/NPWR01125_00/TROPHY.TRP")) expected = (string)rec["trophySha256"];
                        string h; if (!hashes.TryGetValue(kv.Key, out h) || h != expected) throw new Exception("ISO 파일 보존/패치 결과 불일치: " + kv.Key);
                    }
                    using (var primary = new NierIso(tempIso, false))
                    {
                        if (primary.Files.Count != check.Files.Count) throw new Exception("ISO9660/Joliet 파일 개수 불일치");
                        foreach (var kv in check.Files)
                        {
                            NierIso.Entry a = kv.Value, b;
                            if (!primary.Files.TryGetValue(kv.Key, out b) || a.Length != b.Length || a.Parts.Count != b.Parts.Count) throw new Exception("ISO9660/Joliet 구조 불일치");
                            for (int i = 0; i < a.Parts.Count; i++) if (a.Parts[i].Offset != b.Parts[i].Offset || a.Parts[i].Length != b.Parts[i].Length) throw new Exception("ISO9660/Joliet extent 불일치");
                        }
                    }
                }
                Console.WriteLine("ISO 전체 해시를 확인하는 중입니다. 파일 크기에 따라 시간이 걸립니다.");
                var report = new Dictionary<string, object> {
                    { "version", "1.0" }, { "engine", "NieR-PS3-USA-Korean-Patcher.exe (.NET ISO)" }, { "inputType", "ISO" }, { "trophyMode", trophy },
                    { "sourcePreserved", true }, { "unchangedFilesVerified", true }, { "fileCount", hashes.Count }, { "files", hashes },
                    { "isoSha256", NierUsa.Hash(tempIso) }, { "isoBytes", new FileInfo(tempIso).Length }, { "runtimeTested", false }, { "hardwareTested", false } };
                File.Move(tempIso, output);
                WriteJson(reportPath, report);
                // 이번에 만든 GUID 작업 폴더만 정리한다.
                string resolved = Path.GetFullPath(stage);
                if (Path.GetDirectoryName(resolved) != parent || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(resolved), "^\\.nier-iso-build-[a-f0-9]{32}$")) throw new Exception("임시 경로 검증 실패: 정리를 생략합니다.");
                Directory.Delete(resolved, true);
                stage = null;
                Console.WriteLine("ISO 생성·검증 단계가 끝났습니다. 최종 저장 처리를 진행합니다.");
                Console.WriteLine("검증 보고서: " + reportPath);
                Console.WriteLine("원본 ISO는 변경하지 않았습니다.");
                return report;
            }
            catch
            {
                if (stage != null) Console.WriteLine("진단용 작업 폴더를 보존했습니다: " + stage);
                throw;
            }
        }
    }
}
