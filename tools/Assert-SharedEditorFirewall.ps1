function Test-EngineFirewallCoverage {
 param([object[]]$Rules,[string[]]$Profiles)
 if(!$Profiles.Count){throw 'No active network profile could be verified'}
 foreach($profile in $Profiles){foreach($protocol in @('TCP','UDP')){
  $matched=@($Rules|Where-Object {$_.enabled-eq'True'-and$_.direction-eq'Inbound'-and$_.action-in@('Allow','Block')-and($_.profiles-eq'Any'-or($_.profiles -split ',\s*')-contains$profile)-and$_.protocol-in@('Any',$protocol)-and$_.localPort-eq'Any'-and$_.remotePort-eq'Any'-and$_.localAddress-eq'Any'-and$_.remoteAddress-eq'Any'-and$_.interface-eq'Any'-and$_.interfaceType-eq'Any'-and$_.service-eq'Any'})
  if(!$matched.Count){throw ('No complete existing firewall coverage for '+$profile+'/'+$protocol)}
 }}
}
function Assert-SharedEditorFirewall {
 param([Parameter(Mandatory=$true)][string]$Executable)
 $expected=[IO.Path]::GetFullPath($Executable)
 $allowed=@('C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe','C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe')
 if($expected-notin$allowed){throw 'Only the audited installed Unity or Unreal editor path is allowed'}
 $profiles=@(Get-NetConnectionProfile -ErrorAction Stop|ForEach-Object {switch([string]$_.NetworkCategory){Public{'Public'} Private{'Private'} DomainAuthenticated{'Domain'} default{throw 'Unknown network profile'}}}|Sort-Object -Unique)
 $rows=@(Get-NetFirewallApplicationFilter -PolicyStore ActiveStore -ErrorAction Stop|Where-Object {$_.Program-eq$expected}|ForEach-Object {
  $rule=Get-NetFirewallRule -AssociatedNetFirewallApplicationFilter $_ -ErrorAction Stop
  $port=$rule|Get-NetFirewallPortFilter;$address=$rule|Get-NetFirewallAddressFilter;$interface=$rule|Get-NetFirewallInterfaceFilter;$interfaceType=$rule|Get-NetFirewallInterfaceTypeFilter;$service=$rule|Get-NetFirewallServiceFilter
  [pscustomobject]@{name=$rule.Name;enabled=[string]$rule.Enabled;direction=[string]$rule.Direction;action=[string]$rule.Action;profiles=[string]$rule.Profile;protocol=[string]$port.Protocol;localPort=[string]$port.LocalPort;remotePort=[string]$port.RemotePort;localAddress=[string]$address.LocalAddress;remoteAddress=[string]$address.RemoteAddress;interface=[string]$interface.InterfaceAlias;interfaceType=[string]$interfaceType.InterfaceType;service=[string]$service.Service}
 })
 Test-EngineFirewallCoverage -Rules $rows -Profiles $profiles
 return [pscustomobject]@{executable=$expected;profiles=$profiles;rules=$rows;timestamp=(Get-Date).ToString('o');policyChanged=$false}
}
