# 本地构建三个发布包，产物放到 release\。
# 用法：
#   powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
#   powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Version 1.0.1
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $Version) {
        $raw = Get-Content .\GameLibrary.csproj -Raw -Encoding UTF8
        if ($raw -notmatch '<Version>([^<]+)</Version>') { throw '无法从 csproj 读取版本号' }
        $Version = $Matches[1].Trim()
    }
    $version = $Version.Trim().TrimStart('v')
    if (-not $version) { throw '无法确定版本号' }

    $out = Join-Path $root 'release'
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    New-Item -ItemType Directory -Path $out | Out-Null

    Write-Host "==> 发布无依赖版（自包含单文件），版本 $version"
    dotnet publish .\GameLibrary.csproj -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o .\dist-self-contained
    if ($LASTEXITCODE -ne 0) { throw '无依赖版发布失败' }

    Write-Host '==> 发布依赖版（框架依赖）'
    dotnet publish .\GameLibrary.csproj -c $Configuration -r win-x64 --self-contained false `
        -p:PublishSingleFile=true `
        -o .\dist-framework-dependent
    if ($LASTEXITCODE -ne 0) { throw '依赖版发布失败' }

    Write-Host '==> 打包'
    $editions = @(
        @{ Name = 'self-contained'; Dir = '.\dist-self-contained'; Readme = 'release-readme-self-contained.txt' },
        @{ Name = 'framework-dependent'; Dir = '.\dist-framework-dependent'; Readme = 'release-readme-framework-dependent.txt' }
    )
    foreach ($edition in $editions) {
        Copy-Item (Join-Path 'docs' $edition.Readme) (Join-Path $edition.Dir 'README.txt') -Force
        Compress-Archive -Path (Join-Path $edition.Dir '*') `
            -DestinationPath (Join-Path $out "GameLibrary-$version-win-x64-$($edition.Name).zip") -Force
    }

    git archive --format=zip --prefix="game-library-$version/" `
        -o (Join-Path $out "GameLibrary-$version-source.zip") HEAD
    if ($LASTEXITCODE -ne 0) { throw '源码包生成失败（需要在有提交的 git 仓库中运行）' }

    Write-Host ''
    Get-ChildItem $out | Select-Object Name, @{ Name = '大小(MB)'; Expression = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
}
finally {
    Pop-Location
}
