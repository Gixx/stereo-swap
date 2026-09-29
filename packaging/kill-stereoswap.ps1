$ErrorActionPreference = 'SilentlyContinue'
Get-Process -Name 'StereoSwap' -ErrorAction SilentlyContinue | Stop-Process -Force
Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'dotnet.exe' -and
        $_.CommandLine -and
        ($_.CommandLine -like '*StereoSwap.dll*' -or $_.CommandLine -like '*StereoSwap.Tray*')
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Milliseconds 500
