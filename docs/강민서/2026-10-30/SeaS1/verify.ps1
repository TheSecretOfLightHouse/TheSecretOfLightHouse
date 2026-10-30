# 저장소 루트에서 실행한다. Unity를 실행하지 않고 저장된 S1 결과를 재검수한다.
$ErrorActionPreference='Stop'
$s1Dir=Join-Path $PSScriptRoot '.'
$first=(Get-Content (Join-Path $s1Dir 'measurement.json') -Raw | ConvertFrom-Json)
$second=(Get-Content (Join-Path $s1Dir 'measurement-verified.json') -Raw | ConvertFrom-Json)
if(!$first.success -or !$second.success -or $second.result.Count -ne 6){throw '측정 결과 누락'}
$dockCount=0
$maxError=0.0
foreach($a in $second.result){
 $b=$first.result | Where-Object id -eq $a.id
 if((ConvertTo-Json $a.groups -Depth 10 -Compress) -ne (ConvertTo-Json $b.groups -Depth 10 -Compress)){throw "반복 불일치: $($a.name)"}
 foreach($v in $a.rotationAudit){$maxError=[math]::Max($maxError,[math]::Abs($v.radius-$a.groups.placement.rootRadius))}
 if(@($a.records | Where-Object { $_.method -eq 'meshBoundsFallback' -or $_.method -eq 'none' }).Count){throw '측정 fallback 발생'}
 if(@($a.docks | Where-Object { !$_.active -or @($_.landingSurfaces | Where-Object { !$_.Length }).Count }).Count){throw '부두 참조 누락'}
 $dockCount += $a.docks.Count
}
if($maxError -gt 0.0001 -or $dockCount -ne 7){throw '회전 또는 부두 수 검수 실패'}
$before=Get-Content (Join-Path $s1Dir 'inputs-before.json') -Raw | ConvertFrom-Json
$changed=@($before | Where-Object { (Get-FileHash -LiteralPath $_.path -Algorithm SHA256).Hash -ne $_.sha256 })
if($changed.Count){throw '의존 입력 변경: 실측을 다시 확인할 것'}
$docs=@('docs/강민서/2026-10-30/해역축소-S1-Final섬실측.md','docs/handover/강민서/2026-10-30/해역축소-S1-인수인계.md','docs/design/sea-size-and-map-data-plan.md','docs/강민서/POINTER.md')
$links=0
foreach($doc in $docs){$body=Get-Content -LiteralPath $doc -Raw;foreach($m in [regex]::Matches($body,'\]\(([^)]+)\)')){$target=$m.Groups[1].Value.Split('#')[0];if(!$target -or $target -match '^https?://'){continue};$links++;if(!(Test-Path -LiteralPath (Join-Path (Split-Path $doc) $target))){throw "링크 누락: $doc → $target"}}}
[pscustomobject]@{variants=$second.result.Count;docks=$dockCount;maxYawRadiusError=$maxError;inputFiles=$before.Count;changedInputs=$changed.Count;checkedLocalLinks=$links} | ConvertTo-Json
