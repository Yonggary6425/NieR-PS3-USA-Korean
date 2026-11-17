param([string]$Source='', [ValidateSet('','copy','replace')][string]$Mode='', [ValidateSet('','original','korean')][string]$Trophy='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EasyPatch.Common.ps1')
try{
    Write-Host '=== NieR PS3 북미판 v1.0 간편 패처 ===' -ForegroundColor Cyan
    if(-not $Source){$Source=Read-Host 'ISO 또는 게임 폴더를 이 창으로 끌어다 놓고 Enter를 누르세요'}
    if(-not $Source){throw '원본을 선택하지 않았습니다.'}
    Write-Host ''
    if(-not $Mode){
        Write-Host '1. 새로운 폴더 / ISO 생성 (원본 옆에 자동 저장)'
        Write-Host '2. 선택한 폴더 / ISO 위치에 바로 적용 (원본 자동 백업)'
        while(-not $Mode){$a=Read-Host '적용 방식 [1/2, Enter=1]';if($a -eq '' -or $a -eq '1'){$Mode='copy'}elseif($a -eq '2'){$Mode='replace'}}
    }
    $plan=New-PatchPlan $Source $Mode
    # A launcher inside the game tree would be moved during replacement.
    if(-not $plan.IsIso -and $PSScriptRoot.StartsWith($plan.Source+'\',[StringComparison]::OrdinalIgnoreCase)){throw '패처 폴더를 게임 폴더 밖으로 옮긴 뒤 실행하세요.'}
    if(-not $Trophy){
        Write-Host '1. 원본 트로피 (PS3 실기용 권장)'
        Write-Host '2. 한글 트로피'
        while(-not $Trophy){$a=Read-Host '트로피 [1/2, Enter=1]';if($a -eq '' -or $a -eq '1'){$Trophy='original'}elseif($a -eq '2'){$Trophy='korean'}}
    }
    Write-Host "입력: $($plan.Source)"
    if($Mode -eq 'copy'){Write-Host "자동 저장 위치: $($plan.Output)"}else{Write-Host '검증 완료 후 선택한 위치에 적용합니다. 원본은 별도 백업됩니다.'}
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Apply-Korean-Patch.ps1') -Source $plan.Source -Output $plan.Output -Trophy $Trophy
    if($LASTEXITCODE -ne 0){throw '패치에 실패하여 교체하지 않았습니다. 원본은 그대로입니다.'}
    if($plan.IsIso){$report=$plan.Output+'.patch-report.json'}else{$report=Join-Path $plan.Output 'PATCH-VERIFIED.json'}
    $verified=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $want='77f12a3b1ad333bfe432bccb1ac3a10818b1d5db2276c194177dcf0e70b96902'
    if($plan.IsIso){$got=$verified.files.'PS3_GAME/USRDIR/MEDIA/STABLE.SDAT'}else{$got=$verified.sdatSha256}
    if($got -ne $want -or $verified.trophyMode -ne $Trophy){throw '패치 결과 검증 기록이 예상과 다릅니다. 교체하지 않았습니다.'}
    if(-not $plan.IsIso){
        # Preserve root-level extras which the historical folder engine omits.
        foreach($f in Get-ChildItem -LiteralPath $plan.Source -Force){
            if($f.Name -in @('PS3_GAME','PS3_DISC.SFB','PATCH-VERIFIED.json')){continue}
            $dest=Join-Path $plan.Output $f.Name
            if(Test-Path -LiteralPath $dest){throw '추가 파일의 출력 경로 충돌'}
            Copy-Item -LiteralPath $f.FullName -Destination $dest -Recurse -Force
        }
    }
    $verified | Add-Member -NotePropertyName applyMode -NotePropertyValue $Mode -Force
    $verified | Add-Member -NotePropertyName originalBackupPath -NotePropertyValue $plan.Backup -Force
    $verified | Add-Member -NotePropertyName originalLocationReplaced -NotePropertyValue ($Mode -eq 'replace') -Force
    $verified | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $report -Encoding UTF8
    Publish-PatchResult $plan
    if($Mode -eq 'replace'){
        $final=$plan.Source
        if($plan.IsIso){
            $destReport=$final+'.patch-report.json'
            if(Test-Path -LiteralPath $destReport){$destReport=$final+'.patch-report-'+[Guid]::NewGuid().ToString('N')+'.json'}
            [IO.File]::Move($report,$destReport)
        }
    }else{$final=$plan.Output}
    Write-Host ''
    Write-Host "완료: $final" -ForegroundColor Green
}catch{
    Write-Host ('오류: '+$_.Exception.Message) -ForegroundColor Red
    if($plan -and $plan.Backup){Write-Host "백업 예정/보존 위치: $($plan.Backup)"}
    exit 1
}
