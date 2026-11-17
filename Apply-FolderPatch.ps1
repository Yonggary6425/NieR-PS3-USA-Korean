param([string]$Source='', [string]$Output='', [ValidateSet('','original','korean')][string]$Trophy='')
$ErrorActionPreference='Stop'
try {
    Write-Host 'NieR PS3 USA BLUS30481 Korean Patch v1.0 - PowerShell'
    if (-not $Source) { $Source=Read-Host '원본 게임 폴더 (PS3_GAME이 들어 있는 폴더)' }
    if (-not $Output) { $Output=Read-Host '새 출력 폴더 (존재하지 않는 경로)' }
    if (-not $Source -or -not $Output) { throw '경로가 비어 있습니다.' }
    $Source=[IO.Path]::GetFullPath($Source.Trim('"')).TrimEnd('\')
    $Output=[IO.Path]::GetFullPath($Output.Trim('"')).TrimEnd('\')
    if ($Source -eq $Output -or $Output.StartsWith($Source+'\',[StringComparison]::OrdinalIgnoreCase) -or $Source.StartsWith($Output+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '원본과 출력 폴더는 서로 분리해야 합니다.' }
    if (Test-Path -LiteralPath $Output) { throw '출력 폴더가 이미 존재합니다. 새 경로를 선택하세요.' }
    $parent=Split-Path -Parent $Output
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw '출력 폴더의 상위 폴더가 없습니다.' }
    $sdat=Join-Path $Source 'PS3_GAME\USRDIR\MEDIA\STABLE.SDAT'
    $trp=Join-Path $Source 'PS3_GAME\TROPDIR\NPWR01125_00\TROPHY.TRP'
    $patch=Join-Path $PSScriptRoot 'ps3-local-delta.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.Web.Extensions
    Add-Type -Path (Join-Path $PSScriptRoot 'NierPatch.cs') -ReferencedAssemblies System.IO.Compression,System.IO.Compression.FileSystem,System.Web.Extensions
    $z=[IO.Compression.ZipFile]::OpenRead($patch)
    try { $r=New-Object IO.StreamReader($z.GetEntry('manifest.json').Open()); try { $m=$r.ReadToEnd() | ConvertFrom-Json } finally {$r.Dispose()} } finally {$z.Dispose()}
    if ($m.format -ne 'NIER-PS3-USA-KO-LOCAL-1' -or $m.titleId -ne 'BLUS30481') { throw '지원하지 않는 패치 데이터입니다.' }
    Write-Host '원본 해시 확인 중...'
    if ([NierUsa]::Hash($sdat) -ne $m.sourceSdatSha256) { throw '원본 STABLE.SDAT 불일치: 지역/버전 또는 이미 패치된 파일을 확인하세요.' }
    if ([NierUsa]::Hash($trp) -ne $m.trophy.sourceSha256) { throw '원본 TROPHY.TRP 불일치' }
    if (-not $Trophy) {
        Write-Host '1: 원본 트로피 유지 (PS3 실기용 권장)'
        Write-Host '2: 한글 트로피 (RPCS3용)'
        while (-not $Trophy) { $a=Read-Host '선택 [1/2, Enter=1]'; if ($a -eq '1' -or $a -eq '') {$Trophy='original'} elseif ($a -eq '2') {$Trophy='korean'} }
    }
    $files=@(Get-ChildItem -LiteralPath (Join-Path $Source 'PS3_GAME') -Recurse -Force)
    if (@($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw '게임 폴더의 연결/재분석 지점은 지원하지 않습니다.' }
    $total=($files | Where-Object {-not $_.PSIsContainer} | Measure-Object Length -Sum).Sum
    $need=[Math]::Max(18GB, $total+2*$m.targetCpkSize+1GB)
    $drive=New-Object IO.DriveInfo([IO.Path]::GetPathRoot($Output))
    if ($drive.AvailableFreeSpace -lt $need) {throw ('여유 공간 부족. 필요 바이트: '+$need)}
    $stage=Join-Path $parent ('.nier-ps3-build-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    $plain=Join-Path $stage 'source.cpk'; $rebuilt=Join-Path $stage 'korean.cpk'; $game=Join-Path $stage 'game'
    Write-Host "임시 작업 폴더: $stage"
    [NierUsa]::Sdat($sdat,$plain,$sdat,$false)
    [NierUsa]::Rebuild($plain,$rebuilt,$patch)
    [IO.Directory]::CreateDirectory((Join-Path $game 'PS3_GAME')) | Out-Null
    $dst=Join-Path $game 'PS3_GAME\USRDIR\MEDIA\STABLE.SDAT'
    Write-Host '게임 파일 복사 중...'
    $base=Join-Path $Source 'PS3_GAME'
    foreach ($f in $files) {
        $relative=$f.FullName.Substring($base.Length).TrimStart('\');$target=Join-Path (Join-Path $game 'PS3_GAME') $relative
        if($f.PSIsContainer){[IO.Directory]::CreateDirectory($target)|Out-Null}
        elseif($f.FullName -ne $sdat){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;[IO.File]::Copy($f.FullName,$target,$false)}
    }
    $disc=Join-Path $Source 'PS3_DISC.SFB'
    if(Test-Path -LiteralPath $disc){[IO.File]::Copy($disc,(Join-Path $game 'PS3_DISC.SFB'),$false)}
    [NierUsa]::Sdat($rebuilt,$dst,$sdat,$true)
    Write-Host '암호화 완료. 최종 게임 파일 검증 중입니다. 잠시 기다려 주세요.'
    if([NierUsa]::Hash($dst) -ne $m.targetSdatSha256){throw '최종 SDAT SHA256 불일치'}
    $destTrp=Join-Path $game 'PS3_GAME\TROPDIR\NPWR01125_00\TROPHY.TRP'
    $trophyHash=$m.trophy.sourceSha256
    if($Trophy -eq 'korean'){[NierUsa]::Trophy($trp,$destTrp,$patch);$trophyHash=$m.trophy.targetSha256}
    if([NierUsa]::Hash($destTrp) -ne $trophyHash){throw '트로피 결과 SHA256 불일치'}
    @{titleId='BLUS30481';sdatSha256=$m.targetSdatSha256;trophyMode=$Trophy;trophySha256=$trophyHash;sourcePreserved=$true;engine='Windows PowerShell 5.1 / .NET'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $game 'PATCH-VERIFIED.json') -Encoding UTF8
    [IO.Directory]::Move($game,$Output)
    [IO.File]::Delete($plain);[IO.File]::Delete($rebuilt);[IO.Directory]::Delete($stage)
    Write-Host '게임 데이터 패치 단계가 끝났습니다. 나머지 작업을 진행합니다.'

} catch {
    Write-Host ('ERROR: '+$_.Exception.Message) -ForegroundColor Red
    if($stage){Write-Host "진단용 작업 파일을 보존했습니다: $stage"}
    exit 1
}
