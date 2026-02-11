param([string]$InputPath, [string]$OutputPath)

Add-Type -AssemblyName System.Drawing

$img = [System.Drawing.Bitmap]::FromFile($InputPath)
$iconHandle = $img.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)

$fileStream = New-Object System.IO.FileStream($OutputPath, [System.IO.FileMode]::Create)
$icon.Save($fileStream)

$fileStream.Close()
$icon.Dispose()
$img.Dispose()
[System.Runtime.InteropServices.Marshal]::DestroyIcon($iconHandle)

Write-Host "Converted $InputPath to $OutputPath"
