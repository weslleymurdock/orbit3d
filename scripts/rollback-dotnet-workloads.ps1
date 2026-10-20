param (
    [Parameter(Mandatory=$true, HelpMessage="Mac SSH Target (e.g., user@192.168.1.10 or alias like 'mini')")]
    [string]$SshTarget
)

$targetVersion = "26.5.10318"
$rollbackJson = "{`"microsoft.net.sdk.ios`":`"$targetVersion`", `"microsoft.net.sdk.maccatalyst`":`"$targetVersion`"}"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Executing Rollback on WINDOWS (Local)    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$rollbackJson | Out-File -FilePath "workload-rollback.json" -Encoding utf8
Write-Host "File workload-rollback.json generated locally." -ForegroundColor Blue

Write-Host "Starting dotnet workload update on Windows..." -ForegroundColor Yellow
dotnet workload update --from-rollback-file workload-rollback.json

if ($LASTEXITCODE -eq 0) {
    Write-Host "Windows update completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Windows update failed! Ensure your active .NET SDK band is 10.0.1xx, not 10.0.400." -ForegroundColor Red
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Executing Rollback on MACOS (Remote)     " -ForegroundColor Cyan
Write-Host " Target: $SshTarget                       " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Using an explicit array for arguments prevents PowerShell from mangling the 'mini' hostname with the 'echo' command.
$macCommand = "echo '$rollbackJson' > workload-rollback.json && sudo dotnet workload update --from-rollback-file workload-rollback.json"
$sshArgs = @("-t", $SshTarget, "bash", "-c", $macCommand)

Write-Host "Establishing interactive SSH connection (Your macOS password will be requested below)..." -ForegroundColor Yellow
& ssh $sshArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "macOS update completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Remote execution failed (Exit Code $LASTEXITCODE)." -ForegroundColor Red
}