param([switch]$Install,[switch]$Plan)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Assert-PrivateRuntimeFirewall.ps1')
$workspace=Split-Path $PSScriptRoot -Parent
$executable=Join-Path $workspace 'artifacts/offline-runtime-runner/Windows/RuntimeWorld/Binaries/Win64/RuntimeWorld.exe'
$result=[ordered]@{
    timestamp=(Get-Date).ToString('o')
    program=[IO.Path]::GetFullPath($executable)
    purpose='One stable executable path for private offline packaged tests'
    action='Block';direction='Inbound';profiles='Any';protocol='Any'
    globalFirewallSettingsChanged=$false
    sharedEngineRulesChanged=$false
    engineStarted=$false
    autoElevation=$false
    status='PLAN_ONLY'
}
if(!$Plan){
    try{
        $result.rule=Assert-PrivateRuntimeFirewall -Executable $executable -Install:$Install
        $result.status='VERIFIED'
    }catch{
        $result.status=if($_.Exception.Message -like '*Administrator privileges are required*'){'ADMIN_REQUIRED'}else{'RULE_MISSING_OR_INVALID'}
        $result.error=$_.Exception.Message
    }
}
$result|ConvertTo-Json -Depth 4|Set-Content (Join-Path $workspace 'artifacts/offline-runtime-firewall-installation.json')
$result|ConvertTo-Json -Depth 4
if($result.status -notin @('PLAN_ONLY','VERIFIED')){exit 2}
