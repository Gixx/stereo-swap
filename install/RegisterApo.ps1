<#
.SYNOPSIS
  StereoSwap APO COM registration + per-device FxProperties bind/unbind.

.PARAMETER Action
  install | register | unregister | bind | unbind | enable | disable

  install  = copy DLL to Program Files + register COM
  enable   = install (if needed) + bind + restart audio service
  disable  = unbind (+ optional unregister with -Unregister)

.PARAMETER DeviceId
  MMDevice ID, e.g. {0.0.0.00000000}.{guid}

.PARAMETER DllPath
  Path to StereoSwapApo.dll (install/register/enable)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('install', 'register', 'unregister', 'bind', 'unbind', 'enable', 'disable')]
    [string] $Action,

    [string] $DeviceId,

    [string] $DllPath,

    [switch] $RestartAudio = $true,

    [switch] $Unregister
)

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# Force UTF-8 for console + log file (Hungarian OS error text otherwise mojibakes in the tray).
try { chcp 65001 | Out-Null } catch { }
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding $false

$Clsid = '{B3E8C1A0-7D4F-4E2A-9C1B-5F6A8D0E2B11}'
$InstallDir = Join-Path $env:ProgramFiles 'StereoSwap'
$InstalledDll = Join-Path $InstallDir 'StereoSwapApo.dll'
$LogDir = Join-Path $env:ProgramData 'StereoSwap'
$LogFile = Join-Path $LogDir 'last-install.log'
$LogFileUser = Join-Path $env:LOCALAPPDATA 'StereoSwap\last-install.log'

function Write-ApoLog([string] $Message) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    foreach ($path in @($LogFile, $LogFileUser)) {
        try {
            $dir = Split-Path $path -Parent
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            if (-not (Test-Path -LiteralPath $path)) {
                [System.IO.File]::WriteAllText($path, $line + [Environment]::NewLine, $script:Utf8NoBom)
            }
            else {
                [System.IO.File]::AppendAllText($path, $line + [Environment]::NewLine, $script:Utf8NoBom)
            }
        }
        catch {
            # never fail the installer because of logging
        }
    }
    try { Write-Host $Message } catch { }
}

# When elevated, ensure the ProgramData log is writable by the interactive user too.
try {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    if (Test-Path -LiteralPath $LogFile) {
        & icacls.exe $LogFile /grant '*S-1-5-32-545:(M)' /grant '*S-1-5-32-544:(F)' | Out-Null
    }
}
catch { }

# EqAPO-compatible FX property keys. Types must match existing Windows values.
$FxValues = @(
    @{ Name = '{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5'; Type = 'REG_SZ' }       # LFX / PreMix
    @{ Name = '{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6'; Type = 'REG_SZ' }       # GFX / PostMix
    @{ Name = '{d3993a3f-99c2-4402-b5ec-a92a0367664b},5'; Type = 'REG_MULTI_SZ' } # SFX
    @{ Name = '{d3993a3f-99c2-4402-b5ec-a92a0367664b},6'; Type = 'REG_MULTI_SZ' } # MFX
)
$FxKeys = @($FxValues | ForEach-Object { $_.Name })

function Normalize-DeviceId([string] $Id) {
    if ([string]::IsNullOrWhiteSpace($Id)) { return $Id }
    return $Id.Trim().Trim("'").Trim('"').Trim()
}

function Get-EndpointFxKey {
    param([string] $Id)
    $Id = Normalize-DeviceId $Id
    # MMDevice id: {0.0.0.00000000}.{endpoint-guid}  → registry folder is endpoint-guid only
    if ($Id -match '\.(\{[0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}\})$') {
        $guid = $Matches[1]
    }
    elseif ($Id -match '^(\{[0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}\})$') {
        $guid = $Matches[1]
    }
    else {
        throw "Cannot parse endpoint GUID from DeviceId: $Id"
    }

    $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$guid"
    if (-not (Test-Path -LiteralPath $key)) {
        throw "Render endpoint key not found: $key"
    }
    return "$key\FxProperties"
}

function Register-ApoDll {
    param([string] $Path)
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) {
        throw "DllPath required and must exist. Got: $DllPath"
    }
    $full = (Resolve-Path -LiteralPath $Path).Path
    $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$Clsid"
    New-Item -Path $clsidKey -Force | Out-Null
    Set-ItemProperty -Path $clsidKey -Name '(default)' -Value 'StereoSwap APO'
    $inproc = Join-Path $clsidKey 'InprocServer32'
    New-Item -Path $inproc -Force | Out-Null
    Set-ItemProperty -Path $inproc -Name '(default)' -Value $full
    Set-ItemProperty -Path $inproc -Name 'ThreadingModel' -Value 'Both'

    $apoKey = "HKLM:\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\$Clsid"
    New-Item -Path $apoKey -Force | Out-Null
    Set-ItemProperty -Path $apoKey -Name 'FriendlyName' -Value 'StereoSwap APO'
    Set-ItemProperty -Path $apoKey -Name 'Copyright' -Value 'Copyright StereoSwap'
    New-ItemProperty -Path $apoKey -Name 'MajorVersion' -PropertyType DWord -Value 1 -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'MinorVersion' -PropertyType DWord -Value 0 -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'Flags' -PropertyType DWord -Value 0x0d -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'MinInputConnections' -PropertyType DWord -Value 1 -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'MaxInputConnections' -PropertyType DWord -Value 1 -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'MinOutputConnections' -PropertyType DWord -Value 1 -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'MaxOutputConnections' -PropertyType DWord -Value 1 -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'MaxInstances' -PropertyType DWord -Value 0xffffffff -Force | Out-Null
    New-ItemProperty -Path $apoKey -Name 'NumAPOInterfaces' -PropertyType DWord -Value 1 -Force | Out-Null
    Set-ItemProperty -Path $apoKey -Name 'APOInterface0' -Value '{FD7F2B29-24D0-4B5C-B177-592C39F9CA10}'
    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio' -Force | Out-Null
    New-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio' -Name 'DisableProtectedAudioDG' -PropertyType DWord -Value 1 -Force | Out-Null
    Write-ApoLog "Registered CLSID $Clsid -> $full (+ AudioProcessingObjects, DisableProtectedAudioDG=1)"
}

function Unregister-ApoDll {
    $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$Clsid"
    if (Test-Path $clsidKey) {
        Remove-Item -Path $clsidKey -Recurse -Force
        Write-ApoLog "Unregistered CLSID $Clsid"
    }
}

function Install-Apo {
    $src = $DllPath
    if (-not $src) {
        $candidates = @(
            (Join-Path $PSScriptRoot 'StereoSwapApo.dll'),
            (Join-Path $PSScriptRoot '..\StereoSwapApo.dll'),
            (Join-Path $PSScriptRoot '..\..\apo\build\bin\StereoSwapApo.dll'),
            (Join-Path $PSScriptRoot '..\..\apo\build\bin\Release\StereoSwapApo.dll')
        )
        $src = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $src -or -not (Test-Path -LiteralPath $src)) {
        throw "StereoSwapApo.dll not found. Build apo\Build-Apo.bat first, or pass -DllPath."
    }

    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Copy-Item -LiteralPath $src -Destination $InstalledDll -Force
    Register-ApoDll -Path $InstalledDll
    Write-ApoLog "Installed to $InstalledDll"
}

function Resolve-DeviceId {
    param([string] $Id)
    if (-not [string]::IsNullOrWhiteSpace($Id)) {
        return (Normalize-DeviceId $Id)
    }
    if ($env:STEREOSWAP_DEVICE_ID) {
        return (Normalize-DeviceId $env:STEREOSWAP_DEVICE_ID)
    }
    $pending = Join-Path $LogDir 'pending-device-id.txt'
    if (Test-Path -LiteralPath $pending) {
        return (Normalize-DeviceId (Get-Content -LiteralPath $pending -Raw))
    }
    return $null
}

function Enable-SeTakeOwnershipPrivilege {
    $def = @"
using System;
using System.Runtime.InteropServices;
public class StereoSwapTokenPriv {
  [DllImport("advapi32.dll", ExactSpelling=true, SetLastError=true)]
  internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall,
    ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr rel);
  [DllImport("kernel32.dll", ExactSpelling=true)]
  internal static extern IntPtr GetCurrentProcess();
  [DllImport("advapi32.dll", ExactSpelling=true, SetLastError=true)]
  internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);
  [DllImport("advapi32.dll", SetLastError=true)]
  internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);
  [StructLayout(LayoutKind.Sequential, Pack=1)]
  internal struct TokPriv1Luid { public int Count; public long Luid; public int Attr; }
  internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
  internal const int TOKEN_QUERY = 0x00000008;
  internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
  public static bool Enable(string privilege) {
    TokPriv1Luid tp; IntPtr htok = IntPtr.Zero;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok)) return false;
    tp.Count = 1; tp.Luid = 0; tp.Attr = SE_PRIVILEGE_ENABLED;
    if (!LookupPrivilegeValue(null, privilege, ref tp.Luid)) return false;
    return AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
  }
}
"@
    if (-not ([System.Management.Automation.PSTypeName]'StereoSwapTokenPriv').Type) {
        Add-Type $def -ErrorAction Stop
    }
    [void][StereoSwapTokenPriv]::Enable('SeTakeOwnershipPrivilege')
    [void][StereoSwapTokenPriv]::Enable('SeRestorePrivilege')
}

function ConvertTo-RegExePath([string] $PsPath) {
    return ($PsPath -replace '^HKLM:\\', 'HKLM\' -replace '^HKEY_LOCAL_MACHINE\\', 'HKLM\')
}

function Unlock-RegistryKeyForAdmin {
    param([Parameter(Mandatory = $true)][string] $RelativeHkLmPath)
    Enable-SeTakeOwnershipPrivilege

    # Current elevated user SID — avoids localized "Administrators" / IdentityNotMappedException.
    $ownerSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    if ($null -eq $ownerSid) {
        $ownerSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
    }

    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey(
        $RelativeHkLmPath,
        [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree,
        [System.Security.AccessControl.RegistryRights]::TakeOwnership)
    if ($null -eq $key) { throw "Cannot open registry key for TakeOwnership: $RelativeHkLmPath" }
    try {
        $acl = $key.GetAccessControl([System.Security.AccessControl.AccessControlSections]::None)
        $acl.SetOwner($ownerSid)
        $key.SetAccessControl($acl)
    }
    finally { $key.Close() }

    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey(
        $RelativeHkLmPath,
        [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree,
        [System.Security.AccessControl.RegistryRights]::ChangePermissions)
    if ($null -eq $key) { throw "Cannot open registry key for ChangePermissions: $RelativeHkLmPath" }
    try {
        $acl = $key.GetAccessControl()
        $rule = New-Object System.Security.AccessControl.RegistryAccessRule(
            $ownerSid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.ResetAccessRule($rule)
        $key.SetAccessControl($acl)
    }
    finally { $key.Close() }
}

function Ensure-FxPropertiesWritable {
    param([Parameter(Mandatory = $true)][string] $FxPsPath)
    $rel = $FxPsPath -replace '^HKLM:\\', '' -replace '^HKEY_LOCAL_MACHINE\\', ''
    $parentRel = Split-Path $rel -Parent
    Write-ApoLog "Unlocking registry ACL (SID S-1-5-32-544)..."
    Unlock-RegistryKeyForAdmin -RelativeHkLmPath $parentRel
    $regPath = ConvertTo-RegExePath $FxPsPath
    & reg.exe add $regPath /f | Out-Null
    Unlock-RegistryKeyForAdmin -RelativeHkLmPath $rel
}

function Set-FxClsidValues {
    param([Parameter(Mandatory = $true)][string] $FxPsPath)
    $regPath = ConvertTo-RegExePath $FxPsPath
    Write-ApoLog "Writing FxProperties via reg.exe: $regPath"
    & reg.exe add $regPath /f | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "reg.exe could not open/create key $regPath (exit $LASTEXITCODE)"
    }
    foreach ($item in $FxValues) {
        & reg.exe add $regPath /v $item.Name /t $item.Type /d $Clsid /f | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "reg.exe add failed for '$($item.Name)' as $($item.Type) (exit $LASTEXITCODE)"
        }
    }
}

function Clear-FxClsidValues {
    param([Parameter(Mandatory = $true)][string] $FxPsPath)
    $regPath = ConvertTo-RegExePath $FxPsPath
    if (-not (Test-Path -LiteralPath $FxPsPath)) {
        Write-ApoLog "FxProperties missing — nothing to unbind."
        return
    }
    Write-ApoLog "Clearing FxProperties via reg.exe: $regPath"
    foreach ($name in $FxKeys) {
        & reg.exe delete $regPath /v $name /f 2>$null | Out-Null
    }
}

function Bind-Device {
    $id = Resolve-DeviceId -Id $DeviceId
    if ([string]::IsNullOrWhiteSpace($id)) {
        throw 'DeviceId required for bind/unbind/enable/disable'
    }
    Write-ApoLog "Binding device: $id"
    $fx = Get-EndpointFxKey -Id $id

    try {
        Set-FxClsidValues -FxPsPath $fx
    }
    catch {
        Write-ApoLog ("reg.exe write failed, trying ACL unlock then retry. Detail: " + $_.Exception.Message)
        Ensure-FxPropertiesWritable -FxPsPath $fx
        Set-FxClsidValues -FxPsPath $fx
    }

    Write-ApoLog "Bound StereoSwap APO on $fx"
}

function Unbind-Device {
    $id = Resolve-DeviceId -Id $DeviceId
    if ([string]::IsNullOrWhiteSpace($id)) {
        throw 'DeviceId required for bind/unbind/enable/disable'
    }
    Write-ApoLog "Unbinding device: $id"
    $fx = Get-EndpointFxKey -Id $id

    try {
        Clear-FxClsidValues -FxPsPath $fx
    }
    catch {
        Write-ApoLog ("reg.exe delete failed, trying ACL unlock then retry. Detail: " + $_.Exception.Message)
        if (Test-Path -LiteralPath $fx) {
            Ensure-FxPropertiesWritable -FxPsPath $fx
            Clear-FxClsidValues -FxPsPath $fx
        }
    }

    Write-ApoLog "Unbound StereoSwap APO on $fx"
}

function Restart-AudioService {
    if (-not $RestartAudio) { return }
    Write-ApoLog "Restarting Windows Audio service (brief silence)..."
    Restart-Service -Name Audiosrv -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

try {
    Write-ApoLog "Action=$Action"
    switch ($Action) {
        'install' {
            Install-Apo
        }
        'register' {
            if (-not $DllPath) { $DllPath = $InstalledDll }
            Register-ApoDll -Path $DllPath
        }
        'unregister' {
            Unregister-ApoDll
        }
        'bind' {
            Bind-Device
            Restart-AudioService
        }
        'unbind' {
            Unbind-Device
            Restart-AudioService
        }
        'enable' {
            if (-not (Test-Path $InstalledDll)) {
                Install-Apo
            }
            elseif (-not (Test-Path "HKLM:\SOFTWARE\Classes\CLSID\$Clsid\InprocServer32")) {
                Register-ApoDll -Path $InstalledDll
            }
            Bind-Device
            Restart-AudioService
            Write-ApoLog "Swap ENABLED on device (shared mode)."
        }
        'disable' {
            Unbind-Device
            if ($Unregister) {
                Unregister-ApoDll
            }
            Restart-AudioService
            Write-ApoLog "Swap DISABLED on device."
        }
    }
    exit 0
}
catch {
    $msg = $_.Exception.Message
    # Prefer English summary for localized UnauthorizedAccessException text.
    if ($msg -match 'hozz.f.r|enged.lyezett|access is not allowed|Access is denied|UnauthorizedAccess|IdentityNotMapped|azonos.t.si hivatkoz') {
        $msg = "Registry ACL/ownership failed (localized account name). Using SID-based unlock. Original: $msg"
    }
    Write-ApoLog ("ERROR: " + $msg)
    exit 1
}
