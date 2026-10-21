param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("Windows", "Android", "iOS", "MacCatalyst")]
    [string]$Platform,

    [string]$Configuration = "Release",
    [string]$SimulatorId = $env:ORBIT3D_IOS_SIMULATOR_UDID,
    [string]$DeviceId = $env:ORBIT3D_ANDROID_DEVICE
)

$ErrorActionPreference = "Stop"
$rootDir = Resolve-Path "$PSScriptRoot\.."
$sampleDir = Join-Path $rootDir "games\3D\SilkTriangleSample"
$binDir = Join-Path $sampleDir "bin\$Configuration"
$appId = "com.companyname.silktrianglesample"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Orbit3D Artifact Validation: $Platform ($Configuration)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

switch ($Platform) {
    "Windows" {
        $targetTfm = "net10.0-windows10.0.19041.0"
        $tfmDir = Join-Path $binDir $targetTfm
        $exeCandidates = @(
            (Join-Path $tfmDir "win-x64\SilkTriangleSample.exe"),
            (Join-Path $tfmDir "SilkTriangleSample.exe"),
            (Join-Path $tfmDir "win-x64\publish\SilkTriangleSample.exe")
        )

        $resolvedExe = $null
        foreach ($candidate in $exeCandidates) {
            if (Test-Path $candidate) {
                $resolvedExe = $candidate
                break
            }
        }

        if (-not $resolvedExe) {
            Write-Error "Windows artifact not found. Expected executable in '$tfmDir'. Build command: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration $Configuration -f $targetTfm"
            exit 1
        }

        $exeDir = Split-Path $resolvedExe -Parent
        Write-Host "Resolved Windows executable: $resolvedExe" -ForegroundColor Green
        Write-Host "Validating distribution layout in: $exeDir" -ForegroundColor Blue

        $requiredAssets = @(
            "SilkTriangleSample.dll",
            "toyota-gazoo-racing-wrt-gr-yaris-1-10.glb",
            "toyota-gazoo-racing-wrt-gr-yaris-1-10.png"
        )

        foreach ($asset in $requiredAssets) {
            $assetPath = Join-Path $exeDir $asset
            if (-not (Test-Path $assetPath)) {
                Write-Error "Required asset missing from distribution: $asset"
                exit 1
            }
            Write-Host "  [OK] $asset found ($( (Get-Item $assetPath).Length ) bytes)" -ForegroundColor Gray
        }

        # Staging validation directory (simulating unpackaged application installation)
        $installDir = Join-Path $rootDir "artifacts\installation\Windows"
        if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null

        Write-Host "Installing application layout into staging: $installDir" -ForegroundColor Blue
        Copy-Item -Path (Join-Path $exeDir "*") -Destination $installDir -Recurse -Force

        $installedExe = Join-Path $installDir "SilkTriangleSample.exe"
        if (-not (Test-Path $installedExe)) {
            Write-Error "Staged installation failed: executable missing at $installedExe"
            exit 1
        }

        # IMPORTANT: Rule 15.4 - Do not launch the installed executable during desktop artifact validation!
        Write-Host "Windows artifact validation SUCCESSFUL (executable verified, NOT launched as per validation policy)." -ForegroundColor Green
    }

    "Android" {
        $targetTfm = "net10.0-android"
        $tfmDir = Join-Path $binDir $targetTfm
        $apkCandidates = @(
            (Join-Path $tfmDir "$appId-Signed.apk"),
            (Join-Path $tfmDir "$appId.apk"),
            (Join-Path $tfmDir "publish\$appId-Signed.apk")
        )

        $resolvedApk = $null
        foreach ($candidate in $apkCandidates) {
            if (Test-Path $candidate) {
                $resolvedApk = $candidate
                break
            }
        }

        if (-not $resolvedApk) {
            Write-Error "Android APK not found in '$tfmDir'. Build command: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration $Configuration -f $targetTfm"
            exit 1
        }

        Write-Host "Resolved Android APK: $resolvedApk ($( (Get-Item $resolvedApk).Length ) bytes)" -ForegroundColor Green

        # Check for adb
        $adb = Get-Command adb -ErrorAction SilentlyContinue
        if (-not $adb) {
            Write-Warning "Android SDK tool 'adb' is not installed or not in PATH. Cannot execute installation check."
            Write-Host "Artifact is built and verified, but installation check is BLOCKED by missing adb." -ForegroundColor Yellow
            exit 0
        }

        $devicesOutput = & adb devices
        Write-Host $devicesOutput
        $attached = ($devicesOutput -split "`r?`n") | Where-Object { $_ -match "\tdevice$" }
        if (-not $attached) {
            Write-Warning "No active Android device or emulator detected. Cannot execute 'adb install'."
            Write-Host "Artifact is built and verified, but installation check is BLOCKED by missing emulator/device." -ForegroundColor Yellow
            exit 0
        }

        Write-Host "Installing APK to device using adb..." -ForegroundColor Blue
        & adb install -r $resolvedApk
        if ($LASTEXITCODE -ne 0) {
            Write-Error "adb install failed with code $LASTEXITCODE"
            exit $LASTEXITCODE
        }

        $packages = & adb shell pm list packages $appId
        if ($packages -notmatch $appId) {
            Write-Error "Verification failed: package $appId not found after installation."
            exit 1
        }

        Write-Host "Android package installation validation SUCCESSFUL." -ForegroundColor Green
    }

    "iOS" {
        $targetTfm = "net10.0-ios"
        $tfmDir = Join-Path $binDir $targetTfm
        $appCandidates = @(
            (Join-Path $tfmDir "iossimulator-x64\SilkTriangleSample.app"),
            (Join-Path $tfmDir "iossimulator-arm64\SilkTriangleSample.app"),
            (Join-Path $tfmDir "SilkTriangleSample.app")
        )

        $resolvedApp = $null
        foreach ($candidate in $appCandidates) {
            if (Test-Path $candidate) {
                $resolvedApp = $candidate
                break
            }
        }

        if (-not $resolvedApp) {
            Write-Error "iOS Simulator .app bundle not found in '$tfmDir'. Build command: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration $Configuration -f $targetTfm"
            exit 1
        }

        Write-Host "Resolved iOS application bundle: $resolvedApp" -ForegroundColor Green

        $simctl = Get-Command xcrun -ErrorAction SilentlyContinue
        if (-not $simctl) {
            Write-Warning "'xcrun' is only available on macOS. Simulator CLI validation cannot run on this host."
            Write-Host "Artifact validation command on macOS: xcrun simctl install <UDID> '$resolvedApp'" -ForegroundColor Yellow
            exit 0
        }

        if (-not $SimulatorId) {
            Write-Error "No iOS Simulator UDID specified. Set ORBIT3D_IOS_SIMULATOR_UDID or pass -SimulatorId."
            exit 1
        }

        Write-Host "Booting simulator $SimulatorId..." -ForegroundColor Blue
        & xcrun simctl boot $SimulatorId 2>$null

        Write-Host "Installing bundle into simulator $SimulatorId..." -ForegroundColor Blue
        & xcrun simctl install $SimulatorId $resolvedApp
        if ($LASTEXITCODE -ne 0) {
            Write-Error "xcrun simctl install failed with code $LASTEXITCODE"
            exit $LASTEXITCODE
        }

        $container = & xcrun simctl get_app_container $SimulatorId $appId
        Write-Host "App container resolved at: $container" -ForegroundColor Green
        Write-Host "iOS simulator installation validation SUCCESSFUL." -ForegroundColor Green
    }

    "MacCatalyst" {
        $targetTfm = "net10.0-maccatalyst"
        $tfmDir = Join-Path $binDir $targetTfm
        $appCandidates = @(
            (Join-Path $tfmDir "maccatalyst-x64\SilkTriangleSample.app"),
            (Join-Path $tfmDir "maccatalyst-arm64\SilkTriangleSample.app"),
            (Join-Path $tfmDir "SilkTriangleSample.app")
        )

        $resolvedApp = $null
        foreach ($candidate in $appCandidates) {
            if (Test-Path $candidate) {
                $resolvedApp = $candidate
                break
            }
        }

        if (-not $resolvedApp) {
            Write-Error "Mac Catalyst .app bundle not found in '$tfmDir'. Build command: dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration $Configuration -f $targetTfm"
            exit 1
        }

        Write-Host "Resolved Mac Catalyst application bundle: $resolvedApp" -ForegroundColor Green
        $plist = Join-Path $resolvedApp "Contents\Info.plist"
        if (-not (Test-Path $plist)) {
            Write-Error "Contents/Info.plist missing from bundle: $resolvedApp"
            exit 1
        }

        # IMPORTANT: Rule 15.3 - Do not launch the installed executable during desktop artifact validation!
        Write-Host "Mac Catalyst bundle validation SUCCESSFUL (verified structure, NOT launched as per validation policy)." -ForegroundColor Green
    }
}

