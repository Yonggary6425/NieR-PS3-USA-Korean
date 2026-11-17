function Get-FreePatchPath([string]$Candidate) {
    $p=$Candidate;$i=2
    while((Test-Path -LiteralPath $p) -or (Test-Path -LiteralPath ($p+'.patch-report.json'))){
        if([IO.Path]::GetExtension($Candidate) -eq '.iso'){$p=[IO.Path]::Combine([IO.Path]::GetDirectoryName($Candidate),([IO.Path]::GetFileNameWithoutExtension($Candidate)+' ('+$i+').iso'))}
        else{$p=$Candidate+' ('+$i+')'}
        $i++
    }
    return $p
}
function New-PatchPlan([string]$InputPath,[string]$Mode) {
    $s=[IO.Path]::GetFullPath($InputPath.Trim('"')).TrimEnd('\')
    if(-not(Test-Path -LiteralPath $s)){throw '원본 경로를 찾을 수 없습니다.'}
    $item=Get-Item -LiteralPath $s
    if($item.PSIsContainer -and $item.Name -ieq 'PS3_GAME'){$s=Split-Path -Parent $s;$item=Get-Item -LiteralPath $s}
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw '연결된 파일/폴더 대신 실제 원본을 사용하세요.'}
    $isIso=-not $item.PSIsContainer
    if($isIso -and $item.Extension -ine '.iso'){throw 'ISO 파일 또는 게임 폴더만 지원합니다.'}
    $parent=[IO.Path]::GetDirectoryName($s)
    if(-not $parent){throw '드라이브 루트는 게임 폴더로 사용할 수 없습니다.'}
    if(-not $isIso){
        if(-not(Test-Path -LiteralPath (Join-Path $s 'PS3_GAME\USRDIR\MEDIA\STABLE.SDAT'))){throw 'PS3_GAME이 들어 있는 게임 폴더를 선택하세요.'}
        if(@(Get-ChildItem -LiteralPath $s -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw '연결/재분석 지점이 있는 게임 폴더는 지원하지 않습니다.'}
    }
    if($Mode -notin @('copy','replace')){throw '잘못된 적용 방식입니다.'}
    $name=[IO.Path]::GetFileName($s)
    if($Mode -eq 'copy'){
        if($isIso){$name=[IO.Path]::GetFileNameWithoutExtension($s)+'_Korean.iso'}else{$name+='_Korean'}
        $out=Get-FreePatchPath (Join-Path $parent $name);$backup=$null
    }else{
        $id=[Guid]::NewGuid().ToString('N')
        $out=Join-Path $parent ('.nier-ready-'+$id+$(if($isIso){'.iso'}else{''}))
        $stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
        if($isIso){$backupName=[IO.Path]::GetFileNameWithoutExtension($s)+'_OriginalBackup_'+$stamp+'.iso'}else{$backupName=$name+'_OriginalBackup_'+$stamp}
        $backup=Get-FreePatchPath (Join-Path $parent $backupName)
    }
    return [pscustomobject]@{Source=$s;Output=$out;Backup=$backup;IsIso=$isIso;Mode=$Mode;Parent=$parent}
}
function Move-PatchPath([string]$From,[string]$To,[bool]$IsIso) {
    if($IsIso){[IO.File]::Move($From,$To)}else{[IO.Directory]::Move($From,$To)}
}
function Publish-PatchResult($Plan) {
    if($Plan.Mode -ne 'replace'){return}
    $s=[IO.Path]::GetFullPath($Plan.Source);$o=[IO.Path]::GetFullPath($Plan.Output);$b=[IO.Path]::GetFullPath($Plan.Backup)
    if($s -eq $o -or $s -eq $b -or $o -eq $b){throw '교체 경로가 중복됩니다.'}
    foreach($p in @($s,$o,$b)){if([IO.Path]::GetDirectoryName($p) -ne $Plan.Parent){throw '교체 경로가 원본 상위 폴더를 벗어났습니다.'}}
    if(-not(Test-Path -LiteralPath $s) -or -not(Test-Path -LiteralPath $o) -or (Test-Path -LiteralPath $b)){throw '교체 대상 또는 백업 경로가 변경되었습니다.'}
    # The journal survives process interruption between the two renames.
    $journal=$o+'.recovery.json'
    if(Test-Path -LiteralPath $journal){throw '복구 기록이 이미 있습니다.'}
    @{originalPath=$s;verifiedResult=$o;backupPath=$b;status='prepared';recovery='If originalPath is missing, rename backupPath back to originalPath. Keep all files until checked.'} | ConvertTo-Json | Set-Content -LiteralPath $journal -Encoding UTF8
    Move-PatchPath $s $b $Plan.IsIso
    try{
        Move-PatchPath $o $s $Plan.IsIso
    }catch{
        if(-not(Test-Path -LiteralPath $s)){
            Move-PatchPath $b $s $Plan.IsIso
        }
        throw
    }
    @{originalPath=$s;backupPath=$b;status='completed';recovery='Original is preserved at backupPath. The patched game is at originalPath.'} | ConvertTo-Json | Set-Content -LiteralPath $journal -Encoding UTF8
    Write-Host "원본 백업: $b"
}
