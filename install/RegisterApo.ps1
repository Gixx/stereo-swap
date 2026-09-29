<#
.SYNOPSIS
  StereoSwap APO COM registration + per-device FxProperties bind/unbind (MVP).

.DESCRIPTION
  Equalizer APO–style approach:
  - Register StereoSwapApo.dll as InprocServer32 (CLSID)
  - Set FxProperties on a render endpoint so audiodg loads the APO (shared mode only)

.PARAMETER Action
  register | unregister | bind | unbind

.PARAMETER DeviceId
  MMDevice ID, e.g. {0.0.0.00000000}.{guid}

.PARAMETER DllPath
  Full path to StereoSwapApo.dll (required for register)

.EXAMPLE
  .\RegisterApo.ps1 -Action register -DllPath D:\builds\StereoSwapApo.dll
  .\RegisterApo.ps1 -Action bind -DeviceId '{0.0.0.00000000}.{...}'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('register', 'unregister', 'bind', 'unbind')]
    [string] $Action,

    [string] $DeviceId,

    [string] $DllPath
)

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# Must match apo\include\StereoSwapApo.h
$Clsid = '{B3E8C1A0-7D4F-4E2A-9C1B-5F6A8D0E2B11}'

# PKEY_FX_PreMixEffectClsid / PostMix — same property store keys EqAPO uses
$FxPreMix  = '{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5'
$FxPostMix = '{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6'

function Get-EndpointFxKey {
    param([string] $Id)
    # DeviceId looks like: {0.0.0.00000000}.{DEADBEEF-...}
    if ($Id -match '\.(\{[0-9A-Fa-f\-]+\})$') {
        $guid = $Matches[1]
    }
    else {
        $guid = $Id
    }
    return "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$guid\FxProperties"
}

function Register-Apo {
    if (-not $DllPath -or -not (Test-Path -LiteralPath $DllPath)) {
        throw "DllPath required and must exist for register. Got: $DllPath"
    }
    $full = (Resolve-Path -LiteralPath $DllPath).Path
    $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$Clsid"
    New-Item -Path $clsidKey -Force | Out-Null
    Set-ItemProperty -Path $clsidKey -Name '(default)' -Value 'StereoSwap APO'
    $inproc = Join-Path $clsidKey 'InprocServer32'
    New-Item -Path $inproc -Force | Out-Null
    Set-ItemProperty -Path $inproc -Name '(default)' -Value $full
    Set-ItemProperty -Path $inproc -Name 'ThreadingModel' -Value 'Both'
    Write-Host "Registered CLSID $Clsid -> $full"
}

function Unregister-Apo {
    $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$Clsid"
    if (Test-Path $clsidKey) {
        Remove-Item -Path $clsidKey -Recurse -Force
        Write-Host "Unregistered CLSID $Clsid"
    }
}

function Bind-Device {
    if ([string]::IsNullOrWhiteSpace($DeviceId)) {
        throw 'DeviceId required for bind/unbind'
    }
    $fx = Get-EndpointFxKey -Id $DeviceId
    if (-not (Test-Path $fx)) {
        New-Item -Path $fx -Force | Out-Null
    }
    # Store as REG_SZ CLSID string (EqAPO-compatible MVP)
    New-ItemProperty -Path $fx -Name $FxPreMix  -PropertyType String -Value $Clsid -Force | Out-Null
    New-ItemProperty -Path $fx -Name $FxPostMix -PropertyType String -Value $Clsid -Force | Out-Null
    Write-Host "Bound StereoSwap APO on $fx"
    Write-Host "Tip: toggle the device or restart audio service if effect does not apply immediately."
}

function Unbind-Device {
    if ([string]::IsNullOrWhiteSpace($DeviceId)) {
        throw 'DeviceId required for bind/unbind'
    }
    $fx = Get-EndpointFxKey -Id $DeviceId
    if (Test-Path $fx) {
        Remove-ItemProperty -Path $fx -Name $FxPreMix  -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $fx -Name $FxPostMix -ErrorAction SilentlyContinue
        Write-Host "Unbound StereoSwap APO on $fx"
    }
}

switch ($Action) {
    'register'   { Register-Apo }
    'unregister' { Unregister-Apo }
    'bind'       { Bind-Device }
    'unbind'     { Unbind-Device }
}
