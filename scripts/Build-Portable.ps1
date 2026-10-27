param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $repo 'artifacts/AgentNetDoctor-0.1.0-preview.4-win-x64'
$env:DOTNET_CLI_HOME = Join-Path $repo '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $repo '.packages'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet publish (Join-Path $repo 'src/AgentNetDoctor.App') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:Version=0.1.0-preview.4 -p:RuntimeFrameworkVersion=10.0.11 -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed' }
$thirdParty = Join-Path $output 'third-party'
New-Item -ItemType Directory -Path $thirdParty -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'docs/portable-guide.txt') -Destination (Join-Path $output '使用说明.txt')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $output 'LICENSE')
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.runtime.win-x64/10.0.11/LICENSE.TXT') -Destination (Join-Path $thirdParty 'dotnet-LICENSE.TXT')
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.runtime.win-x64/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $thirdParty 'dotnet-THIRD-PARTY-NOTICES.TXT')
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.windowsdesktop.app.runtime.win-x64/10.0.11/LICENSE') -Destination (Join-Path $thirdParty 'windowsdesktop-LICENSE')
$files = @(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' })
if ($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Unexpected reparse point in package' }
if ($files | Where-Object { $_.Name -match 'RepairCrashHost|Tests\.|\.pdb$|\.log$|^\.env' }) {
    # Debug symbols are omitted from the package by the app project; never silently strip unknown content.
    throw 'Unexpected test, debug or private file in product package'
}
if (-not (Test-Path -LiteralPath (Join-Path $output 'coreclr.dll'))) { throw 'Self-contained runtime missing' }
$manifest = foreach ($file in $files) { (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash + '  ' + $file.FullName.Substring($output.Length + 1) }
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), $manifest, [Text.UTF8Encoding]::new($false))
$archive = Join-Path $repo 'artifacts/AgentNetDoctor-0.1.0-preview.4-win-x64.zip'
Compress-Archive -LiteralPath $output -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[IO.File]::WriteAllText($archive + '.sha256', $hash + '  ' + [IO.Path]::GetFileName($archive), [Text.UTF8Encoding]::new($false))
Write-Output "Portable package: $archive"
Write-Output "SHA256: $hash"
