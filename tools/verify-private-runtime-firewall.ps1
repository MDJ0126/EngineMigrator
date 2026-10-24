$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Assert-PrivateRuntimeFirewall.ps1')
$testExe=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/firewall-policy-test/RuntimeWorld.exe'
$script:rule=$null
$script:program=$testExe
$script:protocol='Any'
function Get-NetFirewallRule { param($PolicyStore,$Name) $script:rule }
function Get-NetFirewallApplicationFilter { param([Parameter(ValueFromPipeline=$true)]$InputObject) process { [pscustomobject]@{Program=$script:program} } }
function Get-NetFirewallPortFilter { param([Parameter(ValueFromPipeline=$true)]$InputObject) process { [pscustomobject]@{Protocol=$script:protocol;LocalPort='Any';RemotePort='Any'} } }
function Get-NetFirewallAddressFilter { param([Parameter(ValueFromPipeline=$true)]$InputObject) process { [pscustomobject]@{LocalAddress='Any';RemoteAddress='Any'} } }
function New-NetFirewallRule { throw 'TEST FAILURE: firewall mutation attempted' }
$checks=0
function MustReject([string]$Executable,[string]$Expected){
    try { [void](Assert-PrivateRuntimeFirewall -Executable $Executable);throw 'TEST FAILURE: policy accepted an unsafe configuration' }
    catch { if($_.Exception.Message -notlike $Expected){throw};$script:checks++ }
}
MustReject $testExe '*rule is missing*'
MustReject 'C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe' '*Only this workspace*'
MustReject (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/OtherGame.exe') '*Only this workspace*'
$script:rule=[pscustomobject]@{Enabled='True';Action='Block';Direction='Inbound';Profile='Any'}
[void](Assert-PrivateRuntimeFirewall -Executable $testExe);$checks++
foreach($change in @(@{field='Profile';bad='Public';good='Any'},@{field='Action';bad='Allow';good='Block'},@{field='Enabled';bad='False';good='True'},@{field='Direction';bad='Outbound';good='Inbound'})){
    $script:rule.($change.field)=$change.bad
    MustReject $testExe '*not the required*'
    $script:rule.($change.field)=$change.good
}
$script:program='C:/OtherProject/RuntimeWorld.exe';MustReject $testExe '*not the required*';$script:program=$testExe
$script:protocol='TCP';MustReject $testExe '*not the required*'
Write-Output "PRIVATE_RUNTIME_FIREWALL_POLICY_PASS: $checks checks, mocked rules only; no firewall changes or engines started"
