# Offline test games must have an explicit, path-specific inbound block before launch.
# This is not used for shared engine executables or user projects.
function Assert-PrivateRuntimeFirewall {
    param([Parameter(Mandatory=$true)][string]$Executable,[switch]$Install)
    $root=[IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    $path=[IO.Path]::GetFullPath($Executable)
    if(!$path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($path) -ne 'RuntimeWorld.exe'){
        throw 'Only this workspace private RuntimeWorld.exe test artifacts may be configured.'
    }
    for($candidate=$path; $candidate; $candidate=Split-Path $candidate -Parent){
        if(Test-Path -LiteralPath $candidate){
            if((Get-Item -LiteralPath $candidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){
                throw 'Reparse paths are not allowed for private firewall rules.'
            }
        }
    }
    $sha=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($path.ToLowerInvariant()))
    $name='EngineMigrator-OfflineTest-'+[Convert]::ToHexString($sha).Substring(0,24)
    $rule=Get-NetFirewallRule -PolicyStore ActiveStore -Name $name -ErrorAction SilentlyContinue
    if(!$rule -and $Install){
        $principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
        if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
            throw 'Administrator privileges are required to install the private test rule. No UAC prompt or engine will be started.'
        }
        New-NetFirewallRule -Name $name -DisplayName $name -Group 'EngineMigrator offline tests' -Description 'Blocks inbound network traffic only for this private offline test executable; prevents first-listen firewall prompts.' -Direction Inbound -Action Block -Enabled True -Profile Any -Protocol Any -Program $path -ErrorAction Stop | Out-Null
        $rule=Get-NetFirewallRule -PolicyStore ActiveStore -Name $name -ErrorAction Stop
    }
    if(!$rule){throw 'Private test firewall rule is missing. Execution must remain blocked; do not launch or auto-elevate.'}
    $app=@($rule|Get-NetFirewallApplicationFilter -ErrorAction Stop)
    $port=@($rule|Get-NetFirewallPortFilter -ErrorAction Stop)
    $address=@($rule|Get-NetFirewallAddressFilter -ErrorAction Stop)
    if(@($rule).Count-ne 1 -or [string]$rule.Enabled-ne'True' -or [string]$rule.Action-ne'Block' -or [string]$rule.Direction-ne'Inbound' -or [string]$rule.Profile-ne'Any' -or $app.Count-ne 1 -or $app[0].Program-ne$path -or $port.Count-ne 1 -or [string]$port[0].Protocol-ne'Any' -or [string]$port[0].LocalPort-ne'Any' -or [string]$port[0].RemotePort-ne'Any' -or $address.Count-ne 1 -or [string]$address[0].LocalAddress-ne'Any' -or [string]$address[0].RemoteAddress-ne'Any'){
        throw 'Private offline firewall rule is not the required exact-path all-profile inbound block. Execution must remain blocked.'
    }
    return $name
}
