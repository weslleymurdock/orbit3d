[CmdletBinding()]
param(
    [ValidateSet("ios", "maccatalyst")] [string]$Platform = "ios",
    [string]$MacHost = $env:ORBIT3D_APPLE_SSH_HOST,
    [string]$MacUser = $env:ORBIT3D_APPLE_SSH_USER,
    [int]$MacPort = $(if ($env:ORBIT3D_APPLE_SSH_PORT) { [int]$env:ORBIT3D_APPLE_SSH_PORT } else { 22 }),
    [string]$SimulatorId = $env:ORBIT3D_IOS_SIMULATOR_UDID,
    [string]$ProjectPath = "games/3D/SilkTriangleSample/SilkTriangleSample.csproj",
    [ValidateSet("Debug", "Release")] [string]$Configuration = "Debug",
    [int]$WaitSeconds = 8,
    [string]$ArtifactOutputDir = "",
    [switch]$ExerciseLifecycle,
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
                $bar += "$Esc[38;2;216;180;254m?"
            }
            else {
                $bar += "$Esc[38;2;165;180;252m?"
            }
        }
        else {
            $bar += "$Esc[38;2;71;85;105m?"
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

$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$envFile = Join-Path $repoRoot ".env"
$envMap = @{}

if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        $trimmed = $line.Trim()
        if ($trimmed -and -not $trimmed.StartsWith("#") -and $trimmed.Contains("=")) {
            $parts = $trimmed.Split("=", 2)
            $key = $parts[0].Trim()
            $val = $parts[1].Trim().Trim('"', "'")
            $envMap[$key] = $val
        }
    }
}

if ([string]::IsNullOrWhiteSpace($MacHost) -and $envMap.ContainsKey("MAC_ADDR")) {
    $MacHost = $envMap["MAC_ADDR"]
}

if ([string]::IsNullOrWhiteSpace($MacUser) -and $envMap.ContainsKey("MAC_USER")) {
    $MacUser = $envMap["MAC_USER"]
}

$macPassPlain = $null
if ($envMap.ContainsKey("MAC_PASS")) {
    $macPassPlain = $envMap["MAC_PASS"]
}

if ([string]::IsNullOrWhiteSpace($MacHost)) {
    Fail "MAC_ADDR / MacHost is required. Configure .env in repository root or pass -MacHost."
}

try {
    $lookup = if ($MacHost.Contains(".")) { $MacHost } else { "$MacHost.local" }
    $v4 = [System.Net.Dns]::GetHostAddresses($lookup) | Where-Object { $_.AddressFamily -eq 'InterNetwork' }
    if ($v4 -and $v4.Length -gt 0) {
        $MacHost = $v4[0].IPAddressToString
    }
}
catch {
}

if ([string]::IsNullOrWhiteSpace($MacUser)) {
    Fail "MAC_USER / MacUser is required. Configure .env in repository root or pass -MacUser."
}

if ([string]::IsNullOrWhiteSpace($macPassPlain)) {
    Fail "MAC_PASS is required in the repository root .env file."
}

if (-not (Get-Module -ListAvailable -Name Posh-SSH)) {
    Progress 5 "Installing Posh-SSH module for CurrentUser..."
    Install-Module -Name Posh-SSH -Scope CurrentUser -Force -AllowClobber
}
Import-Module Posh-SSH -ErrorAction Stop

$securePassword = ConvertTo-SecureString $macPassPlain -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($MacUser, $securePassword)
$macPassPlain = $null

if (-not (Test-Path (Join-Path $repoRoot $ProjectPath))) {
    Fail "Project was not found: $ProjectPath"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$localRoot = Join-Path $env:TEMP "orbit3d-remote-$runId"
$stageRoot = Join-Path $localRoot "source"
$archive = Join-Path $localRoot "orbit3d-$runId.tar.gz"
$artifactRoot = if ([string]::IsNullOrWhiteSpace($ArtifactOutputDir)) {
    Join-Path $repoRoot "artifacts/remote-apple/$runId"
} else {
    if ([System.IO.Path]::IsPathRooted($ArtifactOutputDir)) { $ArtifactOutputDir } else { Join-Path $repoRoot $ArtifactOutputDir }
}
$remoteRoot = "/private/tmp/orbit3d-validation"
$remoteArchive = "$remoteRoot/$runId.tar.gz"
$remoteRun = "$remoteRoot/$runId"

New-Item -ItemType Directory -Force -Path $stageRoot, $artifactRoot | Out-Null

$sshSession = $null

try {
    Write-Color "Orbit3D Apple Remote Validation" Purple
    Write-Color "Platform: $Platform | Configuration: $Configuration | Run: $runId" Muted

    if (-not $SkipWindowsBuild) {
        Progress 10 "Building backend-independent projects on Windows..."
        Run "dotnet" @(
            "build",
            (Join-Path $repoRoot "engine" "Orbit3D.Engine.slnx"),
            "--configuration",
            $Configuration
        )
    }
    else {
        Progress 10 "Skipping Windows build by request..."
    }

    Progress 22 "Preparing current working tree (excluding secrets and build outputs)..."
    & robocopy $repoRoot $stageRoot /E /NFL /NDL /NJH /NJS /NP `
        /XD ".git" "bin" "obj" "artifacts" ".vs" ".idea" `
        /XF ".env" "*.user" "*.suo" "*.userosscache" "*.DS_Store"

    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with code $LASTEXITCODE."
    }

    if (Test-Path (Join-Path $stageRoot ".env")) {
        Remove-Item -Force (Join-Path $stageRoot ".env")
        throw "Refusing to archive .env file."
    }

    Progress 34 "Creating source archive..."
    Push-Location $localRoot
    try {
        & tar -czf (Split-Path $archive -Leaf) -C $stageRoot .
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with code $LASTEXITCODE."
    }

    Progress 48 "Establishing authenticated Posh-SSH session..."
    Get-SSHSession -ErrorAction SilentlyContinue | Remove-SSHSession -ErrorAction SilentlyContinue
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $sshSession = New-SSHSession -ComputerName $MacHost -Port $MacPort -Credential $credential -AcceptKey -Force -ConnectionTimeout 30
            if ($sshSession) { break }
        }
        catch {
            if ($attempt -eq 3) { throw }
            Start-Sleep -Seconds 2
        }
    }
    if (-not $sshSession) {
        throw "Failed to establish Posh-SSH session to $MacHost."
    }
    $unameResult = Invoke-SSHCommand -SSHSession $sshSession -Command "uname -s" -TimeOut 30
    if ($unameResult.ExitStatus -ne 0) {
        throw "Remote uname check failed."
    }
    Write-Color "SSH connection established ($($unameResult.Output -join ' '))." Green

    Progress 60 "Uploading source archive via SCP..."
    $mkdirResult = Invoke-SSHCommand -SSHSession $sshSession -Command "mkdir -p '$remoteRoot'" -TimeOut 30
    if ($mkdirResult.ExitStatus -ne 0) {
        throw "Failed to create remote directory $remoteRoot."
    }

    Set-SCPItem -ComputerName $MacHost -Port $MacPort -Credential $credential -Path $archive -Destination $remoteRoot -NewName "$runId.tar.gz" -AcceptKey -Force -OperationTimeout 600

    $remoteScript = "$remoteRun/tools/remote/apple/remote-validate-apple.sh"

    $command = "pkill -9 -f SilkTriangleSample 2>/dev/null || true; mkdir -p '$remoteRun' && tar -xzf '$remoteArchive' -C '$remoteRun' && sed -i '' 's/\r$//' '$remoteScript' && chmod +x '$remoteScript' && '$remoteScript' --platform '$Platform' --run-id '$runId' --project-path '$ProjectPath' --configuration '$Configuration' --wait-seconds '$WaitSeconds'"

    if (-not [string]::IsNullOrWhiteSpace($SimulatorId)) {
        $command += " --simulator-id '$SimulatorId'"
    }

    if ($ExerciseLifecycle) {
        $command += " --exercise-lifecycle"
    }

    if ($NoScreenshot) {
        $command += " --no-screenshot"
    }

    Progress 72 "Building and running on macOS..."
    $execResult = Invoke-SSHCommand -SSHSession $sshSession -Command $command -TimeOut 2400
    foreach ($line in $execResult.Output) {
        Write-Host $line
    }
    if ($execResult.ExitStatus -ne 0) {
        if ($execResult.Error) {
            Write-Host ($execResult.Error -join "`n")
        }
        throw "Remote Apple validation failed with code $($execResult.ExitStatus)."
    }

    Progress 88 "Downloading validation artifacts via SCP..."
    $remoteArtifactsArchive = "$remoteRun/artifacts-$runId.tar.gz"
    $packArtifacts = Invoke-SSHCommand -SSHSession $sshSession -Command "tar -czf '$remoteArtifactsArchive' -C '$remoteRun/artifacts' ." -TimeOut 60
    if ($packArtifacts.ExitStatus -ne 0) {
        throw "Failed to package remote artifacts."
    }

    $localArtifactsArchive = Join-Path $localRoot "artifacts-$runId.tar.gz"
    Get-SCPItem -ComputerName $MacHost -Port $MacPort -Credential $credential -Path $remoteArtifactsArchive -PathType File -Destination $localRoot -NewName "artifacts-$runId.tar.gz" -AcceptKey -Force -OperationTimeout 600
    $systemTar = Join-Path $env:SystemRoot "System32\tar.exe"
    if (Test-Path $systemTar) {
        & $systemTar -xzf $localArtifactsArchive -C $artifactRoot
    }
    else {
        $fs = [System.IO.File]::OpenRead($localArtifactsArchive)
        $gz = New-Object System.IO.Compression.GZipStream($fs, [System.IO.Compression.CompressionMode]::Decompress)
        [System.Formats.Tar.TarFile]::ExtractToDirectory($gz, $artifactRoot, $true)
        $gz.Dispose()
        $fs.Dispose()
    }

    Progress 100 "Apple validation completed."
    Write-Color "Artifacts: $artifactRoot" Green
}
catch {
    Write-Color "Remote Apple validation failed: $($_.Exception.Message)" Red
    exit 1
}
finally {
    if ($sshSession) {
        if (-not $KeepRemote) {
            try {
                Invoke-SSHCommand -SSHSession $sshSession -Command "rm -rf '$remoteRun' '$remoteArchive'" -TimeOut 30 | Out-Null
            }
            catch {
            }
        }
        Remove-SSHSession -SSHSession $sshSession | Out-Null
    }

    if (Test-Path $localRoot) {
        Remove-Item -Recurse -Force $localRoot -ErrorAction SilentlyContinue
    }
}
