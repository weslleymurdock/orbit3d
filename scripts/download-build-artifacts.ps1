<#
.SYNOPSIS
Downloads and validates SilkTriangleSample multiplatform build artifacts from GitHub Actions.

.DESCRIPTION
Uses the authenticated GitHub CLI (gh) to download artifacts produced by the dedicated
'.github/workflows/silk-triangle-sample-build.yml' workflow, verifies package presence,
non-zero file size, SHA-256 checksums, and inspects the iOS .ipa archive structure and
CFBundleIdentifier without exposing sensitive signing contents.
#>
param (
    [Parameter(Mandatory = $false)]
    [string]$RunId = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet("All", "Android", "iOS", "MacCatalyst", "Windows")]
    [string]$Target = "All",

    [Parameter(Mandatory = $false)]
    [switch]$IosOnly,

    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = "",

    [Parameter(Mandatory = $false)]
    [string]$Repository = "weslleymurdock/orbit3d",

    [Parameter(Mandatory = $false)]
    [string]$WorkflowName = "silk-triangle-sample-build.yml"
)

$ErrorActionPreference = "Stop"
$rootDir = (Resolve-Path "$PSScriptRoot\..").Path
$expectedBundleId = "com.remotecodehub.silktrianglesample"

if ($IosOnly) {
    $Target = "iOS"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $rootDir "artifacts\ci-builds"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Orbit3D CI Build Artifact Downloader & Validator" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    Write-Error "GitHub CLI ('gh') is not installed or not in PATH. Install GitHub CLI and authenticate via 'gh auth login'."
    exit 1
}

& gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "GitHub CLI is not authenticated. Run 'gh auth login' before downloading workflow artifacts."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    Write-Host "Querying latest run for workflow '$WorkflowName' in '$Repository'..." -ForegroundColor Blue
    $runsJson = & gh run list -R $Repository --workflow $WorkflowName --limit 1 --json databaseId,status,conclusion,headSha,url 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($runsJson) -or $runsJson -eq "[]") {
        Write-Error "No workflow runs found for '$WorkflowName' in '$Repository'. Trigger the workflow first via: gh workflow run $WorkflowName -R $Repository"
        exit 1
    }

    $latestRun = ($runsJson | ConvertFrom-Json)[0]
    $RunId = "$($latestRun.databaseId)"
    Write-Host "Selected workflow run #$RunId (status=$($latestRun.status), conclusion=$($latestRun.conclusion), sha=$($latestRun.headSha))" -ForegroundColor Green
}

$artifactMap = [ordered]@{
    "Android"     = [ordered]@{ Name = "silk-triangle-sample-android";     Extension = ".apk" }
    "iOS"         = [ordered]@{ Name = "silk-triangle-sample-ios";         Extension = ".ipa" }
    "MacCatalyst" = [ordered]@{ Name = "silk-triangle-sample-maccatalyst"; Extension = ".zip" }
    "Windows"     = [ordered]@{ Name = "silk-triangle-sample-windows";     Extension = ".exe" }
}

$selectedPlatforms = if ($Target -eq "All") { @("Android", "iOS", "MacCatalyst", "Windows") } else { @($Target) }

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

function Inspect-IosIpaPackage {
    param (
        [string]$IpaPath,
        [string]$RequiredBundleId
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($IpaPath)
    try {
        $plistEntry = $archive.Entries | Where-Object { $_.FullName -replace '\\', '/' -match '^Payload/[^/]+\.app/Info\.plist$' } | Select-Object -First 1
        if (-not $plistEntry) {
            throw "IPA '$IpaPath' does not contain Payload/<App>.app/Info.plist."
        }

        $parts = ($plistEntry.FullName -replace '\\', '/').Split('/')
        $appBundleName = $parts[1]
        $bundlePrefix = "Payload/$appBundleName/"

        $stream = $plistEntry.Open()
        $ms = New-Object System.IO.MemoryStream
        $stream.CopyTo($ms)
        $bytes = $ms.ToArray()
        $ms.Dispose()
        $stream.Dispose()

        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        if ($text -notmatch '<plist') {
            $text = [System.Text.Encoding]::GetEncoding("ISO-8859-1").GetString($bytes)
        }

        $bundleId = $null
        if ($text -match '<key>\s*CFBundleIdentifier\s*</key>\s*<string>\s*([^<]+?)\s*</string>') {
            $bundleId = $Matches[1].Trim()
        } elseif ($text -match '(com\.[a-zA-Z0-9_\-]+(?:\.[a-zA-Z0-9_\-]+)+)') {
            $bundleId = $Matches[1].Trim()
        }

        if (-not $bundleId) {
            throw "Could not extract CFBundleIdentifier from Info.plist in '$IpaPath'."
        }

        if ($bundleId -ne $RequiredBundleId) {
            throw "IPA CFBundleIdentifier '$bundleId' does not match expected '$RequiredBundleId'."
        }

        $hasProvisioning = ($archive.Entries | Where-Object { $_.FullName -replace '\\', '/' -eq "${bundlePrefix}embedded.mobileprovision" }).Count -gt 0

        return [ordered]@{
            appBundleName           = $appBundleName
            bundleIdentifier        = $bundleId
            hasProvisioningProfile  = $hasProvisioning
        }
    }
    finally {
        $archive.Dispose()
    }
}

$results = @()

foreach ($platform in $selectedPlatforms) {
    $entry = $artifactMap[$platform]
    $artifactName = $entry.Name
    $expectedExt = $entry.Extension
    $targetDir = Join-Path $OutputDirectory $artifactName

    if (Test-Path $targetDir) {
        Remove-Item $targetDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    Write-Host "`n[$platform] Downloading artifact '$artifactName' from run $RunId..." -ForegroundColor Blue
    & gh run download $RunId -R $Repository -n $artifactName -D $targetDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "[$platform] Failed to download artifact '$artifactName' from run $RunId."
        exit 1
    }

    $packageFile = Get-ChildItem -Path $targetDir -Recurse -File | Where-Object { $_.Extension -ieq $expectedExt } | Select-Object -First 1
    if (-not $packageFile) {
        Write-Error "[$platform] Expected package with extension '$expectedExt' was not found in '$targetDir'."
        exit 1
    }

    if ($packageFile.Length -le 0) {
        Write-Error "[$platform] Downloaded package '$($packageFile.FullName)' is empty (0 bytes)."
        exit 1
    }

    $sha256 = (Get-FileHash -Path $packageFile.FullName -Algorithm SHA256).Hash
    Write-Host "  [OK] Package: $($packageFile.FullName)" -ForegroundColor Green
    Write-Host "  [OK] Size:    $($packageFile.Length) bytes" -ForegroundColor Gray
    Write-Host "  [OK] SHA-256: $sha256" -ForegroundColor Gray

    $metadataFile = Join-Path $targetDir "artifact-metadata.json"
    if (Test-Path $metadataFile) {
        $ciMeta = Get-Content $metadataFile -Raw | ConvertFrom-Json
        if ($ciMeta.sha256 -and ($ciMeta.sha256 -ne $sha256)) {
            Write-Error "[$platform] SHA-256 mismatch! Downloaded '$sha256' vs CI metadata '$($ciMeta.sha256)'."
            exit 1
        }
        Write-Host "  [OK] Verified SHA-256 matches CI artifact-metadata.json" -ForegroundColor Green
    }

    $ipaDetails = $null
    if ($platform -eq "iOS") {
        $ipaDetails = Inspect-IosIpaPackage -IpaPath $packageFile.FullName -RequiredBundleId $expectedBundleId
        Write-Host "  [OK] Verified iOS .ipa Bundle ID: $($ipaDetails.bundleIdentifier)" -ForegroundColor Green
        Write-Host "  [OK] Provisioning Profile Present: $($ipaDetails.hasProvisioningProfile)" -ForegroundColor Gray
    }

    $results += [ordered]@{
        platform      = $platform
        artifactName  = $artifactName
        packagePath   = $packageFile.FullName
        fileSizeBytes = $packageFile.Length
        sha256        = $sha256
        ipaDetails    = $ipaDetails
    }
}

$manifestPath = Join-Path $OutputDirectory "download-manifest.json"
[ordered]@{
    repository    = $Repository
    runId         = $RunId
    downloadedAt  = (Get-Date).ToUniversalTime().ToString("o")
    artifacts     = $results
} | ConvertTo-Json -Depth 5 | Out-File -FilePath $manifestPath -Encoding utf8

Write-Host "`nAll requested artifacts downloaded and validated successfully." -ForegroundColor Green
Write-Host "Manifest written to: $manifestPath" -ForegroundColor Cyan
