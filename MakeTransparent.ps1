Add-Type -AssemblyName System.Drawing

$inputPath = "c:\Users\Uri\Desktop\NewPCSetupWPF\Images\app_icon.png"
$outputPath = "c:\Users\Uri\Desktop\NewPCSetupWPF\Images\app_icon.png"

# Load image
$img = [System.Drawing.Bitmap]::FromFile($inputPath)
$newImg = New-Object System.Drawing.Bitmap($img.Width, $img.Height)
$newImg.SetResolution($img.HorizontalResolution, $img.VerticalResolution)

$g = [System.Drawing.Graphics]::FromImage($newImg)
$g.DrawImage($img, 0, 0)
$g.Dispose()
$img.Dispose() # Release file lock

# Make transparent
# Assuming 0,0 is the background color (black)
$backColor = $newImg.GetPixel(0, 0)
$newImg.MakeTransparent($backColor)

# Save
$newImg.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$newImg.Dispose()

Write-Host "Transparency applied."
