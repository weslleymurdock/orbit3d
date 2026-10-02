[CmdletBinding()]
param(
    [ValidateSet("ios", "maccatalyst")] [string]$Platform = "ios",
    [string]$MacHost = $env:ORBIT3D_APPLE_SSH_HOST,
    [string]$MacUser = $env:ORBIT3D_APPLE_SSH_USER,
    [int]$MacPort = $(if ($env:ORBIT3D_APPLE_SSH_PORT) { [int]$env:ORBIT3D_APPLE_SSH_PORT } else { 22 }),
    [string]$IdentityFile = $env:ORBIT3D_APPLE_SSH_KEY,
    [string]$SimulatorId = $env:ORBIT3D_IOS_SIMULATOR_UDID,
    [string]$ProjectPath = "games/3D/SilkTriangleSample/SilkTriangleSample.csproj",
    [ValidateSet("Debug", "Release")] [string]$Configuration = "Debug",
    [int]$WaitSeconds = 8,
    [switch]$SkipWindowsBuild,
    [switch]$KeepRemote,
    [switch]$NoScreenshot,
    [switch]$NoColor
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:UseColor = -not $NoColor -and -not $env:NO_COLOR
$Esc = [char]27

function Write-Color([string]$Text, [string]$Color = "White") {
    if (-not $script:UseColor) { Write-Host $Text; return }
    $colors = @{
        White="$Esc[38;2;248;250;252m"; Purple="$Esc[38;2;216;180;254m"
        Blue="$Esc[38;2;147;197;253m"; Green="$Esc[38;2;134;239;172m"
        Red="$Esc[38;2;252;165;165m"; Muted="$Esc[38;2;148;163;184m"
    }
    Write-Host "$($colors[$Color])$Text$Esc[0m"
}

function Progress([int]$Percent, [string]$Label) {
    $width = 34
    $filled = [math]::Floor($width * $Percent / 100)

    if (-not $script:UseColor) {
        Write-Host ("[{0,3}%] {1}" -f $Percent, $Label)
        return
    }

    $bar = ""
    for ($i = 0; $i -lt $width; $i++) {
        if ($i -lt $filled) {
            if ($i -lt ($filled / 2)) {
                $bar += "$Esc[38;2;216;180;254m━"
            }
            else {
                $bar += "$Esc[38;2;165;180;252m━"
            }
        }
        else {
            $bar += "$Esc[38;2;71;85;105m━"
        }
    }

    Write-Host -NoNewline ([char]13)
    Write-Host -NoNewline "$Esc[2K$bar$Esc[0m "
    Write-Host ("{0,3}% {1}" -f $Percent, $Label)
}

function Run([string]$File, [string[]]$Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File exited with code $LASTEXITCODE."
    }
}

function Fail([string]$Message) {
    Write-Color "ERROR: $Message" Red
    exit 1
}

if ([string]::IsNullOrWhiteSpace($MacHost)) {
    Fail "MacHost is required. Set ORBIT3D_APPLE_SSH_HOST or pass -MacHost."
}

if ([string]::IsNullOrWhiteSpace($MacUser)) {
    Fail "MacUser is required. Set ORBIT3D_APPLE_SSH_USER or pass -MacUser."
}

$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()

if (-not (Test-Path (Join-Path $repoRoot $ProjectPath))) {
    Fail "Project was not found: $ProjectPath"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$localRoot = Join-Path $env:TEMP "orbit3d-remote-$runId"
$stageRoot = Join-Path $localRoot "source"
$archive = Join-Path $localRoot "orbit3d-$runId.tar.gz"
$artifactRoot = Join-Path $repoRoot "artifacts/remote-apple/$runId"
$remoteRoot = "/tmp/orbit3d-validation"
$remoteArchive = "$remoteRoot/$runId.tar.gz"
$remoteRun = "$remoteRoot/$runId"

New-Item -ItemType Directory -Force -Path $stageRoot, $artifactRoot | Out-Null

$sshArgs = @("-p", $MacPort)
$scpArgs = @("-P", $MacPort)

if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
    $sshArgs += @("-i", $IdentityFile)
    $scpArgs += @("-i", $IdentityFile)
}

try {
    Write-Color "Orbit3D Apple Remote Validation" Purple
    Write-Color "Platform: $Platform | Configuration: $Configuration | Run: $runId" Muted

    if (-not $SkipWindowsBuild) {
        Progress 10 "Building backend-independent projects on Windows..."
        Run "dotnet" @(
            "build",
            (Join-Path $repoRoot "Orbit3D.slnx"),
            "--configuration",
            $Configuration
        )
    }
    else {
        Progress 10 "Skipping Windows build by request..."
    }

    Progress 22 "Preparing current working tree..."
    & robocopy $repoRoot $stageRoot /E /NFL /NDL /NJH /NJS /NP `
        /XD ".git" "bin" "obj" "artifacts" ".vs" `
        /XF "*.user" "*.suo" "*.userosscache"

    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with code $LASTEXITCODE."
    }

    Progress 34 "Creating source archive..."
    & tar -czf $archive -C $stageRoot .

    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with code $LASTEXITCODE."
    }

    Progress 48 "Testing SSH connection..."
    Run "ssh" ($sshArgs + @("$MacUser@$MacHost", "uname -s"))
    Write-Color "SSH connection established." Green

    Progress 60 "Uploading source archive with SCP..."
    Run "ssh" ($sshArgs + @("$MacUser@$MacHost", "mkdir -p '$remoteRoot'"))
    Run "scp" ($scpArgs + @(
        $archive,
        ("{0}@{1}:{2}" -f $MacUser, $MacHost, $remoteArchive)
    ))

    $remoteScript = "$remoteRun/tools/remote/apple/remote-validate-apple.sh"

    $command = "mkdir -p '$remoteRun' && tar -xzf '$remoteArchive' -C '$remoteRun' && chmod +x '$remoteScript' && '$remoteScript' --platform '$Platform' --run-id '$runId' --project-path '$ProjectPath' --configuration '$Configuration' --wait-seconds '$WaitSeconds'"

    if (-not [string]::IsNullOrWhiteSpace($SimulatorId)) {
        $command += " --simulator-id '$SimulatorId'"
    }

    if ($NoScreenshot) {
        $command += " --no-screenshot"
    }

    Progress 72 "Building and running on macOS..."
    & ssh @sshArgs "$MacUser@$MacHost" $command

    if ($LASTEXITCODE -ne 0) {
        throw "Remote Apple validation failed with code $LASTEXITCODE."
    }

    Progress 88 "Downloading validation artifacts..."
    Run "scp" ($scpArgs + @(
        "-r",
        ("{0}@{1}:{2}/artifacts/." -f $MacUser, $MacHost, $remoteRun),
        $artifactRoot
    ))

    Progress 100 "Apple validation completed."
    Write-Color "Artifacts: $artifactRoot" Green
}
catch {
    Write-Color "Remote Apple validation failed: $($_.Exception.Message)" Red
    exit 1
}
finally {
    if (-not $KeepRemote -and $MacHost -and $MacUser) {
        try {
            & ssh @sshArgs "$MacUser@$MacHost" "rm -rf '$remoteRun' '$remoteArchive'" 2>$null
        }
        catch {
        }
    }

    if (Test-Path $localRoot) {
        Remove-Item -Recurse -Force $localRoot -ErrorAction SilentlyContinue
    }
}
