# Get the current directory path
$exeDirectory = Get-Location

# Retrieve the current system PATH environment variable
$currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Machine)

# Check if the current directory is already in the PATH
if (-not $currentPath.Contains($exeDirectory)) {
    # Add the current directory to the PATH
    $newPath = $currentPath + ";" + $exeDirectory
    [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::Machine)
    Write-Output "Directory added to PATH: $exeDirectory"
} else {
    Write-Output "Directory already in PATH: $exeDirectory"
}
