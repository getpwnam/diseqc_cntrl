param(
    [string]$Configuration = "Release",
    [string]$Project,
    [string]$Solution,
    [string]$NanoPsPath,
    [string]$Image,
    [switch]$DeployOnly,
    [switch]$Deploy,
    [string]$SerialPort,
    [string]$Address,
    [int]$Baud = 115200,
    [switch]$Reset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-MsBuildCommand {
    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) {
        return [PSCustomObject]@{
            Exe = $cmd.Source
            PrefixArgs = @()
            DisplayName = $cmd.Source
        }
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\\**\\Bin\\MSBuild.exe" | Select-Object -First 1
        if ($msbuildPath -and (Test-Path $msbuildPath)) {
            return [PSCustomObject]@{
                Exe = $msbuildPath
                PrefixArgs = @()
                DisplayName = $msbuildPath
            }
        }
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        return [PSCustomObject]@{
            Exe = $dotnet.Source
            PrefixArgs = @("msbuild")
            DisplayName = "$($dotnet.Source) msbuild"
        }
    }

    throw "Could not find MSBuild. Install Visual Studio Build Tools (MSBuild), or install .NET SDK for 'dotnet msbuild'."
}

function Invoke-MsBuild {
    param(
        [PSCustomObject]$MsBuildCommand,
        [string[]]$Arguments
    )

    $allArgs = @()
    if ($MsBuildCommand.PrefixArgs) {
        $allArgs += $MsBuildCommand.PrefixArgs
    }
    $allArgs += $Arguments

    & $MsBuildCommand.Exe @allArgs
}

function Get-WslPathInfo {
    param([string]$Path)

    if (-not $Path) {
        return $null
    }

    $normalizedPath = $Path

    if ($normalizedPath.StartsWith('Microsoft.PowerShell.Core\\FileSystem::')) {
        $normalizedPath = $normalizedPath.Substring('Microsoft.PowerShell.Core\\FileSystem::'.Length)
    }

    if ($normalizedPath.StartsWith('Microsoft.PowerShell.Core\FileSystem::')) {
        $normalizedPath = $normalizedPath.Substring('Microsoft.PowerShell.Core\FileSystem::'.Length)
    }

    if ($normalizedPath -match '^\\\\wsl\.localhost\\([^\\]+)\\(.+)$') {
        $distro = $matches[1]
        $linuxPath = "/" + $matches[2].Replace('\', '/')
        return [PSCustomObject]@{
            Distro = $distro
            LinuxPath = $linuxPath
        }
    }

    return $null
}

function Resolve-ProviderPath {
    param([string]$Path)

    if (-not $Path) {
        return $Path
    }

    if ($Path.StartsWith('Microsoft.PowerShell.Core\\FileSystem::')) {
        return $Path.Substring('Microsoft.PowerShell.Core\\FileSystem::'.Length)
    }

    if ($Path.StartsWith('Microsoft.PowerShell.Core\FileSystem::')) {
        return $Path.Substring('Microsoft.PowerShell.Core\FileSystem::'.Length)
    }

    return $Path
}

function Resolve-InputPath {
    param(
        [string]$Path,
        [string]$BaseDir,
        [switch]$MustExist
    )

    if (-not $Path) {
        return $Path
    }

    $normalized = Resolve-ProviderPath -Path $Path

    if ([System.IO.Path]::IsPathRooted($normalized)) {
        return $normalized
    }

    $candidates = @()

    try {
        $cwd = (Get-Location).ProviderPath
        if ($cwd) {
            $candidates += (Join-Path $cwd $normalized)
        }
    }
    catch {
        # Ignore location lookup failures and continue with BaseDir fallback.
    }

    if ($BaseDir) {
        $candidates += (Join-Path $BaseDir $normalized)
    }

    $candidates += $normalized
    $candidates = $candidates | Select-Object -Unique

    if (-not $MustExist) {
        return $candidates[0]
    }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $candidates[-1]
}

function Invoke-WslManagedBuildFallback {
    param(
        [string]$ScriptPath,
        [string]$ProjectPath,
        [string]$SolutionPath,
        [string]$NanoProjectSystemPath,
        [string]$BuildConfiguration,
        [string]$ImagePath
    )

    $scriptInfo = Get-WslPathInfo -Path $ScriptPath
    $projectInfo = Get-WslPathInfo -Path $ProjectPath
    $solutionInfo = Get-WslPathInfo -Path $SolutionPath
    $nanoInfo = Get-WslPathInfo -Path $NanoProjectSystemPath

    if (-not $scriptInfo -or -not $projectInfo -or -not $solutionInfo -or -not $nanoInfo) {
        Write-Host "WSL fallback unavailable: one or more paths are not under \\\\wsl.localhost." -ForegroundColor Yellow
        return $false
    }

    if ($scriptInfo.Distro -ne $projectInfo.Distro -or $scriptInfo.Distro -ne $solutionInfo.Distro -or $scriptInfo.Distro -ne $nanoInfo.Distro) {
        Write-Host "WSL fallback unavailable: paths span multiple WSL distros." -ForegroundColor Yellow
        return $false
    }

    $wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
    if (-not $wsl) {
        Write-Host "WSL fallback unavailable: wsl.exe not found." -ForegroundColor Yellow
        return $false
    }

    $linuxBuildScript = $null

    $scriptSlashIndex = $scriptInfo.LinuxPath.LastIndexOf('/')
    if ($scriptSlashIndex -ge 1) {
        $linuxToolchainDir = $scriptInfo.LinuxPath.Substring(0, $scriptSlashIndex)
        $candidateFromScriptPath = "$linuxToolchainDir/build-managed-cli.sh"
        if (Test-Path "\\wsl.localhost\$($scriptInfo.Distro)$($candidateFromScriptPath.Replace('/', '\'))") {
            $linuxBuildScript = $candidateFromScriptPath
        }
    }

    if (-not $linuxBuildScript) {
        $projectSlashIndex = $projectInfo.LinuxPath.LastIndexOf('/')
        if ($projectSlashIndex -lt 1) {
            Write-Host "WSL fallback unavailable: could not resolve Linux project directory." -ForegroundColor Yellow
            return $false
        }

        $projectDir = $projectInfo.LinuxPath.Substring(0, $projectSlashIndex)
        $candidateFromProjectPath = "$projectDir/../toolchain/build-managed-cli.sh"
        if (Test-Path "\\wsl.localhost\$($scriptInfo.Distro)$($candidateFromProjectPath.Replace('/', '\'))") {
            $linuxBuildScript = $candidateFromProjectPath
        }
    }

    if (-not $linuxBuildScript) {
        Write-Host "WSL fallback unavailable: could not locate build-managed-cli.sh in WSL workspace." -ForegroundColor Yellow
        return $false
    }

    $nanoPsLinuxPath = $nanoInfo.LinuxPath
    if (-not $nanoPsLinuxPath.EndsWith('/')) {
        $nanoPsLinuxPath += '/'
    }

    $wslArgs = @(
        "-d", $scriptInfo.Distro,
        "--",
        "bash", $linuxBuildScript,
        "--configuration", $BuildConfiguration,
        "--project", $projectInfo.LinuxPath,
        "--solution", $solutionInfo.LinuxPath,
        "--nano-ps-path", $nanoPsLinuxPath
    )

    if ($ImagePath) {
        $imageInfo = Get-WslPathInfo -Path $ImagePath
        if ($imageInfo -and $imageInfo.Distro -eq $scriptInfo.Distro) {
            $wslArgs += @("--image", $imageInfo.LinuxPath)
        }
    }

    Write-Host "Attempting WSL managed build fallback on distro '$($scriptInfo.Distro)'..." -ForegroundColor Yellow
    $wslOutput = & $wsl.Source @wslArgs 2>&1
    $wslExitCode = $LASTEXITCODE
    $wslOutput | ForEach-Object { Write-Host $_ }

    if ($wslExitCode -ne 0) {
        Write-Host "Primary WSL fallback (build-managed-cli.sh) failed; trying build-chain.sh..." -ForegroundColor Yellow

        $projectRoot = $projectInfo.LinuxPath.Substring(0, $projectInfo.LinuxPath.LastIndexOf('/'))
        $rootDir = $projectRoot
        if ($rootDir.EndsWith('/DiSEqC_Control')) {
            $rootDir = $rootDir.Substring(0, $rootDir.Length - '/DiSEqC_Control'.Length)
        }

        $linuxBuildChainScript = "$rootDir/toolchain/build-chain.sh"
        $buildChainUnc = "\\wsl.localhost\$($scriptInfo.Distro)$($linuxBuildChainScript.Replace('/', '\'))"
        if (-not (Test-Path $buildChainUnc)) {
            return $false
        }

        Write-Host "Retrying WSL fallback with build-chain.sh (includes metadata-processor workaround)..." -ForegroundColor Yellow
        $wslBuildChainArgs = @(
            "-d", $scriptInfo.Distro,
            "--",
            "bash", "-lc",
            "CONFIGURATION='$BuildConfiguration' NANO_PS_PATH='$nanoPsLinuxPath' '$linuxBuildChainScript'"
        )
        $wslBuildChainOutput = & $wsl.Source @wslBuildChainArgs 2>&1
        $wslBuildChainExitCode = $LASTEXITCODE
        $wslBuildChainOutput | ForEach-Object { Write-Host $_ }

        if ($wslBuildChainExitCode -ne 0) {
            Write-Host "Secondary WSL fallback (build-chain.sh) failed." -ForegroundColor Yellow
            return $false
        }

        Write-Host "Secondary WSL fallback (build-chain.sh) succeeded." -ForegroundColor Green
        return $true
    }

    Write-Host "Primary WSL fallback (build-managed-cli.sh) succeeded." -ForegroundColor Green
    return $true
}

function Resolve-NanoProjectSystemPath {
    param(
        [string]$OverridePath,
        [string]$ScriptPath
    )

    if ($OverridePath) {
        return $OverridePath
    }

    $searchRoots = @(
        (Join-Path $env:USERPROFILE ".vscode\extensions"),
        (Join-Path $env:USERPROFILE ".vscode-insiders\extensions"),
        (Join-Path $env:USERPROFILE ".vscode-oss\extensions")
    )

    # If running from a WSL UNC path (\\wsl.localhost\<distro>\...),
    # also search that distro's .vscode-server extensions folder.
    if ($ScriptPath -and ($ScriptPath -match '^\\\\wsl\.localhost\\([^\\]+)\\')) {
        $distro = $matches[1]
        $searchRoots += "\\wsl.localhost\$distro\home\cp\.vscode-server\extensions"
    }

    # Also include all WSL distro .vscode-server roots as fallback.
    if (Test-Path "\\wsl.localhost") {
        $wslDistros = Get-ChildItem "\\wsl.localhost" -Directory -ErrorAction SilentlyContinue
        foreach ($distroDir in $wslDistros) {
            $searchRoots += "\\wsl.localhost\$($distroDir.Name)\home\cp\.vscode-server\extensions"
        }
    }

    foreach ($extensionsRoot in ($searchRoots | Select-Object -Unique)) {
        if (-not (Test-Path $extensionsRoot)) {
            continue
        }

        $candidates = Get-ChildItem -Path $extensionsRoot -Directory -Filter "nanoframework.vscode-nanoframework-*" |
            Sort-Object Name -Descending

        foreach ($candidate in $candidates) {
            $path = Join-Path $candidate.FullName "dist\utils\nanoFramework\v1.0"
            if (Test-Path $path) {
                return $path
            }
        }
    }

    throw "Could not locate nanoFramework project system path automatically. Use -NanoPsPath and point to ...\\dist\\utils\\nanoFramework\\v1.0"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

if (-not $Project) {
    $Project = Join-Path $rootDir "DiSEqC_Control\DiSEqC_Control.nfproj"
}
if (-not $Solution) {
    $Solution = Join-Path $rootDir "DiSEqC_Control\DiSEqC_Control.sln"
}

$Project = Resolve-InputPath -Path $Project -BaseDir $rootDir -MustExist
$Solution = Resolve-InputPath -Path $Solution -BaseDir $rootDir -MustExist
if ($Image) {
    $Image = Resolve-InputPath -Path $Image -BaseDir $rootDir -MustExist
}
if ($NanoPsPath) {
    $NanoPsPath = Resolve-InputPath -Path $NanoPsPath -BaseDir $rootDir
}
$resolvedScriptPath = $null
if ($PSCommandPath) {
    $resolvedScriptPath = $PSCommandPath
} elseif ($MyInvocation -and $MyInvocation.MyCommand -and $MyInvocation.MyCommand.Source) {
    $resolvedScriptPath = $MyInvocation.MyCommand.Source
} elseif ($MyInvocation -and $MyInvocation.MyCommand -and $MyInvocation.MyCommand.Path) {
    $resolvedScriptPath = $MyInvocation.MyCommand.Path
}

$NanoPsPath = Resolve-NanoProjectSystemPath -OverridePath $NanoPsPath -ScriptPath $resolvedScriptPath
$msbuildCommand = Resolve-MsBuildCommand
$imageProvidedByUser = -not [string]::IsNullOrWhiteSpace($Image)

if (-not (Test-Path $Project)) {
    throw "Project not found: $Project"
}
if (-not (Test-Path $Solution)) {
    throw "Solution not found: $Solution"
}
if (-not (Test-Path $NanoPsPath)) {
    throw "NanoFrameworkProjectSystemPath not found: $NanoPsPath"
}

$projectDir = Split-Path -Parent $Project
$targetName = [System.IO.Path]::GetFileNameWithoutExtension($Project)
$imageDefaultBin = Join-Path $projectDir "bin\$Configuration\$targetName.bin"
$imageFallback = Join-Path $projectDir "bin\$Configuration\$targetName.pe"
$imageNfmrk2 = Join-Path $projectDir "bin\$Configuration\$targetName.nfmrk2.bin"
if (-not $Image) {
    if (Test-Path $imageDefaultBin) {
        $Image = $imageDefaultBin
    }
    elseif (Test-Path $imageFallback) {
        $Image = $imageFallback
    }
    else {
        $Image = $imageNfmrk2
    }
}

if (-not $DeployOnly) {
    Write-Host "[1/3] Restoring packages" -ForegroundColor Yellow
    if (Get-Command nuget -ErrorAction SilentlyContinue) {
        & nuget restore $Solution
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet restore failed."
        }
    }
    else {
        Write-Host "nuget.exe not found in PATH; falling back to MSBuild restore ($($msbuildCommand.DisplayName))" -ForegroundColor Yellow
        Invoke-MsBuild -MsBuildCommand $msbuildCommand -Arguments @(
            $Solution,
            "/t:Restore",
            "-verbosity:minimal"
        )
        if ($LASTEXITCODE -ne 0) {
            throw "Package restore failed (nuget missing, msbuild restore fallback failed)."
        }
    }
    Write-Host "[2/3] Building managed project ($Configuration)" -ForegroundColor Yellow
    Write-Host "Using MSBuild command: $($msbuildCommand.DisplayName)" -ForegroundColor DarkGray
    $buildOutput = Invoke-MsBuild -MsBuildCommand $msbuildCommand -Arguments @(
        $Project,
        "/t:Build",
        "/p:Configuration=$Configuration",
        "/p:NanoFrameworkProjectSystemPath=$NanoPsPath\",
        "-verbosity:minimal"
    ) 2>&1
    $buildExitCode = $LASTEXITCODE
    $buildOutput | ForEach-Object { Write-Host $_ }

    if ($buildExitCode -ne 0) {
        $buildFailedWithSystemDrawingCommon = ($buildOutput | Out-String) -match "System\.Drawing\.Common"
        $usingDotnetMsbuild = $msbuildCommand.PrefixArgs -contains "msbuild"

        if ($usingDotnetMsbuild -and $buildFailedWithSystemDrawingCommon) {
            Write-Host "Detected dotnet-msbuild metadata processor dependency failure (System.Drawing.Common)." -ForegroundColor Yellow
            $wslFallbackOk = Invoke-WslManagedBuildFallback `
                -ScriptPath $resolvedScriptPath `
                -ProjectPath $Project `
                -SolutionPath $Solution `
                -NanoProjectSystemPath $NanoPsPath `
                -BuildConfiguration $Configuration `
                -ImagePath $Image

            if (-not $wslFallbackOk) {
                throw "Managed build failed (dotnet msbuild metadata processor dependency issue). Install Visual Studio Build Tools (MSBuild) or run from WSL/Linux toolchain."
            }
        }
        else {
            throw "Managed build failed."
        }
    }
}
else {
    Write-Host "[1/3] Build skipped (-DeployOnly)." -ForegroundColor Yellow
}

if (-not (Test-Path $Image)) {
    if (-not $imageProvidedByUser) {
        if (Test-Path $imageDefaultBin) {
            $Image = $imageDefaultBin
        }
        elseif (Test-Path $imageFallback) {
            $Image = $imageFallback
        }
        elseif (Test-Path $imageNfmrk2) {
            $Image = $imageNfmrk2
        }
        else {
            throw "Managed image not found after build: $Image"
        }
    }
    else {
        throw "Managed image not found after build: $Image"
    }
}

Write-Host "Managed image ready: $Image" -ForegroundColor Green

if (-not $Deploy) {
    Write-Host "[3/3] Deploy skipped (use -Deploy to upload image)" -ForegroundColor Yellow
    exit 0
}

if ([string]::IsNullOrWhiteSpace($SerialPort) -or [string]::IsNullOrWhiteSpace($Address)) {
    throw "-Deploy requires both -SerialPort and -Address."
}

if (-not (Get-Command nanoff -ErrorAction SilentlyContinue)) {
    throw "nanoff not found in PATH. Install with: dotnet tool install -g nanoff"
}

Write-Host "[3/3] Deploying managed image" -ForegroundColor Yellow
$deployArgs = @(
    "--nanodevice",
    "--serialport", $SerialPort,
    "--baud", "$Baud",
    "--deploy",
    "--image", $Image,
    "--address", $Address
)
if ($Reset) {
    $deployArgs += "--reset"
}

& nanoff @deployArgs
if ($LASTEXITCODE -ne 0) {
    throw "Deploy failed."
}

Write-Host "Deploy complete." -ForegroundColor Green
