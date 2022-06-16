﻿Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Build-TFSProcessor() {
    Write-Host "Building TFSProcessor"
    Invoke-MSBuild "SonarScanner.MSBuild.TFS.sln" "/t:Restore"
    Invoke-MSBuild "SonarScanner.MSBuild.TFS.sln" "/t:Rebuild" "/p:Configuration=Release"
    Write-Host "TFSProcessor build has completed."
}

function Build-Scanner() {
    Write-Host "Building SonarScanner for MSBuild"
    Invoke-MSBuild "SonarScanner.MSBuild.sln" "/t:Restore"
    Invoke-MSBuild "SonarScanner.MSBuild.sln" "/t:Rebuild" "/p:Configuration=Release"
    Write-Host "Build for SonarScanner has completed."
}

function CleanAndRecreate-BuildDirectories([string]$tfm) {
    if (Test-Path("$fullBuildOutputDir\sonarscanner-msbuild-$tfm")) {
        Remove-Item "$fullBuildOutputDir\sonarscanner-msbuild-$tfm\*" -Recurse -Force
    }
}

try {
    Write-Host $PSScriptRoot
    
    . (Join-Path $PSScriptRoot "build-utils.ps1")
    . (Join-Path $PSScriptRoot "package-artifacts.ps1")
    . (Join-Path $PSScriptRoot "variables.ps1")

    CleanAndRecreate-BuildDirectories "net462"
    CleanAndRecreate-BuildDirectories "netcoreapp2.0"
    CleanAndRecreate-BuildDirectories "netcoreapp3.0"
    CleanAndRecreate-BuildDirectories "net5.0"
    Download-ScannerCli

    Build-TFSProcessor
    Build-Scanner

    Package-Net46Scanner
    Package-NetScanner "netcoreapp3.1" "netcoreapp3.0"
    Package-NetScanner "netcoreapp2.1" "netcoreapp2.0"
    Package-NetScanner "net5.0" "net5.0"
    
    Write-Host -ForegroundColor Green "SUCCESS: CI job was successful!"
    exit 0
}
catch {
    Write-Host -ForegroundColor Red $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}