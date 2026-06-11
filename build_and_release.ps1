# Script tu dong Build ung dung, tao Bo cai dat va Upload len GitHub Release
$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "1. BAT DAU BUILD UNG DUNG..." -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Dong bang cac tien trinh dang chay de tranh loi File in Use
Stop-Process -Name "DesktopVideoWallpaper" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "msedgewebview2" -Force -ErrorAction SilentlyContinue

# 2. Chay dotnet publish ban Self-Contained Release
& "C:\Program Files\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true

# 3. Dong bo hoa presets.json va background sang thu muc publish
Copy-Item -Path "bin\Debug\net10.0-windows\presets.json" -Destination "bin\Release\net10.0-windows\win-x64\publish\presets.json" -Force -ErrorAction SilentlyContinue
Copy-Item -Path "bin\Debug\net10.0-windows\backgroud\*" -Destination "bin\Release\net10.0-windows\win-x64\publish\backgroud\" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "=========================================" -ForegroundColor Green
Write-Host "2. BIEN DICH BO CAI DAT (INNO SETUP)..." -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

# 4. Bien dich installer.iss bang Inno Setup Compiler
& "C:\Users\Vuthao\AppData\Local\Programs\Inno Setup 6\ISCC.exe" "installer.iss"

Write-Host "=========================================" -ForegroundColor Yellow
Write-Host "3. TU DONG DAY LEN GITHUB RELEASES..." -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Yellow

# Doc phien ban hien tai tu MainWindow.xaml.cs
$versionLine = Get-Content -Path "MainWindow.xaml.cs" | Select-String -Pattern 'private const string AppVersion = "(v[^"]+)"'
if ($versionLine -and $versionLine.Matches.Count -gt 0) {
    $version = $versionLine.Matches[0].Groups[1].Value
} else {
    $version = "v1.0.0"
}
Write-Host "Phien ban hien tai: $version" -ForegroundColor DarkCyan

$installerPath = "installer_output\DesktopVideoWallpaper_Setup.exe"

# Kiem tra su ton tai cua tep cai dat
if (!(Test-Path $installerPath)) {
    Write-Error "Khong tim thay tep cai dat tai $installerPath!"
}

# Kiem tra trang thai dang nhap cua gh CLI
$ghPath = "C:\Program Files\GitHub CLI\gh.exe"
$isLoggedIn = $false

try {
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $authCheck = & $ghPath auth status 2>&1
    $ErrorActionPreference = $oldPreference
    
    foreach ($line in $authCheck) {
        if ($line.ToString() -like "*Logged in to github.com*") {
            $isLoggedIn = $true
            break;
        }
    }
} catch {
    # Ignored
}

if ($isLoggedIn) {
    Write-Host "Da dang nhap GitHub CLI thanh cong. Tien hanh tao/cap nhat Release..." -ForegroundColor Green
    
    # Tao hoac cap nhat Release tren GitHub
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $createResult = & $ghPath release create $version $installerPath --title "$version - Ban cai dat chinh thuc" --notes "Ban phat hanh tu dong dong goi tu ma nguon moi nhat." 2>&1
    
    # Neu release da ton tai thi no se fail, ta thuc hien upload de de len file cu bang --clobber
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Release da ton tai. Dang cap nhat/ghi de asset bang upload --clobber..." -ForegroundColor Yellow
        & $ghPath release upload $version $installerPath --clobber
    }
    $ErrorActionPreference = $oldPreference
    
    Write-Host "Da tai len GitHub Release thanh cong!" -ForegroundColor Green
} else {
    Write-Host "--------------------------------------------------------" -ForegroundColor Red
    Write-Host "CANH BAO: BAN CHUA DANG NHAP GITHUB CLI!" -ForegroundColor Red
    Write-Host "Vui long mo Terminal (PowerShell) va chay lenh sau de ket noi tai khoan GitHub:" -ForegroundColor Yellow
    Write-Host "  gh auth login" -ForegroundColor Cyan
    Write-Host "Sau khi dang nhap xong, hay chay lai script nay de tu dong tai Release len GitHub." -ForegroundColor Yellow
    Write-Host "--------------------------------------------------------" -ForegroundColor Red
}

Write-Host "Hoan tat!" -ForegroundColor Green
