$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Assert-SharedEditorFirewall.ps1')
$base=@{enabled='True';direction='Inbound';action='Allow';profiles='Public';protocol='Any';localPort='Any';remotePort='Any';localAddress='Any';remoteAddress='Any';interface='Any';interfaceType='Any';service='Any'}
$count=0
Test-EngineFirewallCoverage -Rules @([pscustomobject]$base) -Profiles @('Public');$count++
foreach($change in @(@{protocol='TCP'},@{enabled='False'},@{profiles='Private'},@{localPort='7777'},@{remoteAddress='LocalSubnet'},@{interface='Other'},@{service='Other'})){
 $candidate=$base.Clone();foreach($key in $change.Keys){$candidate[$key]=$change[$key]};$blocked=$false
 try{Test-EngineFirewallCoverage -Rules @([pscustomobject]$candidate) -Profiles @('Public')}catch{$blocked=$true}
 if(!$blocked){throw 'Incomplete coverage was accepted'};$count++
}
$blocked=$false;try{Test-EngineFirewallCoverage -Rules @([pscustomobject]$base) -Profiles @('Public','Private')}catch{$blocked=$true};if(!$blocked){throw 'Mixed uncovered profiles accepted'};$count++
$blocked=$false;try{Test-EngineFirewallCoverage -Rules @([pscustomobject]$base) -Profiles @()}catch{$blocked=$true};if(!$blocked){throw 'Missing profile accepted'};$count++
@{status='PASS';checks=$count;engineStarted=$false;policyChanged=$false}|ConvertTo-Json|Set-Content artifacts/shared-editor-firewall-policy-tests.json
Write-Output "$count firewall coverage checks PASS"
