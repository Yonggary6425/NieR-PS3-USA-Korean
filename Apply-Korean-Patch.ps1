param([string]$Source='', [string]$Output='', [ValidateSet('','original','korean')][string]$Trophy='')
$ErrorActionPreference='Stop'
$iso=$null;$stage=$null
try {
    Write-Host 'NieR PS3 USA BLUS30481 v1.0 - ISO / Folder - No Python'
    if(-not $Source){$Source=Read-Host '원본 ISO 파일 또는 게임 폴더 경로'}
    if(-not $Source){throw '원본 경로가 비어 있습니다.'}
    $Source=[IO.Path]::GetFullPath($Source.Trim('"')).TrimEnd('\')
    if(-not (Test-Path -LiteralPath $Source)){throw '원본을 찾을 수 없습니다.'}
    $isIso=Test-Path -LiteralPath $Source -PathType Leaf
    if(-not $Output){if($isIso){$Output=Read-Host '새 출력 ISO 경로 (.iso)'}else{$Output=Read-Host '새 출력 게임 폴더 경로'}}
    if(-not $Output){throw '출력 경로가 비어 있습니다.'}
    $Output=[IO.Path]::GetFullPath($Output.Trim('"')).TrimEnd('\')
    if($Source -eq $Output -or $Output.StartsWith($Source+'\',[StringComparison]::OrdinalIgnoreCase) -or $Source.StartsWith($Output+'\',[StringComparison]::OrdinalIgnoreCase)){throw '원본과 출력 경로는 분리해야 합니다.'}
    if(Test-Path -LiteralPath $Output){throw '출력이 이미 존재합니다. 새 이름을 사용하세요.'}
    $parent=Split-Path -Parent $Output
    if(-not(Test-Path -LiteralPath $parent -PathType Container)){throw '출력 상위 폴더가 없습니다.'}
    if(-not $isIso){
        if([IO.Path]::GetExtension($Output) -eq '.iso'){throw '폴더 입력은 새 폴더로 출력합니다. ISO 출력은 원본 ISO를 입력하세요.'}
        $folderArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Apply-FolderPatch.ps1'),'-Source',$Source,'-Output',$Output)
        if($Trophy){$folderArgs+=@('-Trophy',$Trophy)}
        & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @folderArgs
        exit $LASTEXITCODE
    }
    if([IO.Path]::GetExtension($Source) -ne '.iso' -or [IO.Path]::GetExtension($Output) -ne '.iso'){throw 'ISO 입력과 출력은 .iso 확장자여야 합니다. 분할 ISO는 지원하지 않습니다.'}
    $reportPath=$Output+'.patch-report.json'
    if(Test-Path -LiteralPath $reportPath){throw '같은 이름의 검증 보고서가 있습니다. 새 출력 이름을 사용하세요.'}
    Add-Type -Path (Join-Path $PSScriptRoot 'NierIso.cs')
    $iso=New-Object NierIso($Source)
    $iso.CheckGame()
    $drive=New-Object IO.DriveInfo([IO.Path]::GetPathRoot($Output))
    $need=[Math]::Max(32GB,(Get-Item -LiteralPath $Source).Length*3+10GB)
    if($drive.DriveFormat -eq 'FAT32'){throw '4GB 이상 ISO를 저장할 수 있는 NTFS/exFAT 드라이브가 필요합니다.'}
    if($drive.AvailableFreeSpace -lt $need){throw ('ISO 작업 공간 부족. 최소 필요 GiB: '+[Math]::Ceiling($need/1GB))}
    if(-not $Trophy){
        Write-Host '1: 원본 트로피 유지 (PS3 실기용 권장, 기본값)'
        Write-Host '2: 한글 트로피 적용 (RPCS3용)'
        while(-not $Trophy){$a=Read-Host '선택 [1/2, Enter=1]';if($a -eq '' -or $a -eq '1'){$Trophy='original'}elseif($a -eq '2'){$Trophy='korean'}}
    }
    $stage=Join-Path $parent ('.nier-iso-build-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stage)|Out-Null
    $extracted=Join-Path $stage 'source';$game=Join-Path $stage 'korean';$temporaryIso=Join-Path $stage 'patched.iso'
    Write-Host "작업 폴더: $stage"
    Write-Host '[1/4] 원본 ISO 추출 및 파일별 SHA256 계산'
    $sourceHashes=$iso.Extract($extracted)
    Write-Host '[2/4] 한글패치 적용'
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Apply-FolderPatch.ps1') -Source $extracted -Output $game -Trophy $Trophy
    if($LASTEXITCODE -ne 0){throw '내부 한글패치에 실패했습니다. 원본 ISO는 보존됩니다.'}
    $patchRecord=Get-Content -LiteralPath (Join-Path $game 'PATCH-VERIFIED.json') -Raw | ConvertFrom-Json
    [IO.File]::Move((Join-Path $game 'PATCH-VERIFIED.json'),(Join-Path $stage 'PATCH-VERIFIED.json'))
    # Preserve disc files outside PS3_GAME too (including PS3_UPDATE).
    Write-Host '남은 디스크 파일 복사 중입니다. 아직 최종 완료가 아닙니다.'
    foreach($name in $sourceHashes.Keys){$dest=Join-Path $game $name.Replace('/','\');if(-not(Test-Path -LiteralPath $dest)){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest))|Out-Null;[IO.File]::Copy((Join-Path $extracted $name.Replace('/','\')),$dest,$false)}}
    Write-Host '[3/4] 새 PS3 ISO 생성 (ISO9660 + Joliet, 4GB 이상 파일 지원)'
    [NierIso]::Build($game,$temporaryIso)
    Write-Host '[4/4] ISO 내부 전체 파일 및 두 디렉터리 구조 검증'
    $check=New-Object NierIso($temporaryIso)
    try {
        $check.CheckGame();$hashes=$check.VerifyFolder($game)
        if($hashes.Count -ne $sourceHashes.Count){throw '원본과 출력 ISO의 파일 개수가 다릅니다.'}
        foreach($name in $sourceHashes.Keys){
            $expected=$sourceHashes[$name]
            if($name -ieq 'PS3_GAME/USRDIR/MEDIA/STABLE.SDAT'){$expected=$patchRecord.sdatSha256}
            elseif($name -ieq 'PS3_GAME/TROPDIR/NPWR01125_00/TROPHY.TRP'){$expected=$patchRecord.trophySha256}
            if($hashes[$name] -ne $expected){throw "ISO 파일 보존/패치 결과 불일치: $name"}
        }
        $primary=New-Object NierIso($temporaryIso,$false)
        try {
            if($primary.Files.Count -ne $check.Files.Count){throw 'ISO9660/Joliet 파일 개수 불일치'}
            foreach($name in $check.Files.Keys){$a=$check.Files[$name];$b=$primary.Files[$name];if($null -eq $b -or $a.Length -ne $b.Length -or $a.Parts.Count -ne $b.Parts.Count){throw 'ISO9660/Joliet 구조 불일치'};for($i=0;$i -lt $a.Parts.Count;$i++){if($a.Parts[$i].Offset -ne $b.Parts[$i].Offset -or $a.Parts[$i].Length -ne $b.Parts[$i].Length){throw 'ISO9660/Joliet extent 불일치'}}}
        }finally{$primary.Dispose()}
    }finally{$check.Dispose()}
    Write-Host 'ISO 전체 해시를 확인하는 중입니다. 파일 크기에 따라 시간이 걸립니다.'
    $report=@{version='1.0';engine='Windows PowerShell 5.1 / .NET ISO';inputType='ISO';trophyMode=$Trophy;sourcePreserved=$true;unchangedFilesVerified=$true;fileCount=$hashes.Count;files=$hashes;isoSha256=(Get-FileHash -LiteralPath $temporaryIso -Algorithm SHA256).Hash.ToLowerInvariant();isoBytes=(Get-Item -LiteralPath $temporaryIso).Length;runtimeTested=$false;hardwareTested=$false}
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'report.json') -Encoding UTF8
    [IO.File]::Move($temporaryIso,$Output)
    [IO.File]::Move((Join-Path $stage 'report.json'),$reportPath)
    $iso.Dispose();$iso=$null
    # Only the freshly generated GUID staging directory is eligible for cleanup.
    $resolved=[IO.Path]::GetFullPath($stage)
    if([IO.Path]::GetDirectoryName($resolved) -ne $parent -or [IO.Path]::GetFileName($resolved) -notmatch '^\.nier-iso-build-[a-f0-9]{32}$'){throw '임시 경로 검증 실패: 정리를 생략합니다.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
    Write-Host 'ISO 생성·검증 단계가 끝났습니다. 최종 저장 처리를 진행합니다.'
    Write-Host "검증 보고서: $reportPath"
    Write-Host '원본 ISO는 변경하지 않았습니다.'
}catch{
    Write-Host ('ERROR: '+$_.Exception.Message) -ForegroundColor Red
    if($stage){Write-Host "진단용 작업 폴더를 보존했습니다: $stage"}
    exit 1
}finally{if($null -ne $iso){$iso.Dispose()}}
