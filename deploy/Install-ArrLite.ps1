<#
.SYNOPSIS
    Installs the ArrLite managed module into an IIS Express instance.

.DESCRIPTION
    1. Copies arrLite_schema.xml into <IIS Express>\config\schema (spec §2-3).
    2. Installs ArrLite.dll into the GAC (required for server-wide registration, spec §5.4).
    3. Patches applicationHost.config (a timestamped backup is written first):
       - declares system.webServer/arrLite/rules and globalRules (spec §4);
       - registers the module under <modules>, without preCondition (spec §5.4).

    Run once per IIS Express instance (x64 and x86 have separate schema folders).
    Must run in Windows PowerShell 5.1 as administrator (GAC install uses System.EnterpriseServices).

.EXAMPLE
    .\Install-ArrLite.ps1 -ConfigPath "C:\work\applicationhost.config"
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $IisExpressPath = (Join-Path $env:ProgramFiles 'IIS Express'),
    [string] $ConfigPath = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'IISExpress\config\applicationhost.config'),
    [string] $AssemblyPath = (Join-Path $PSScriptRoot '..\src\ArrLite\bin\Release\net481\ArrLite.dll'),
    [string] $SchemaPath = (Join-Path $PSScriptRoot '..\src\ArrLite\Schema\arrLite_schema.xml')
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this script in Windows PowerShell 5.1 (powershell.exe): the GAC install needs .NET Framework.'
}

$schemaFolder = Join-Path $IisExpressPath 'config\schema'
foreach ($p in @($schemaFolder, $ConfigPath, $AssemblyPath, $SchemaPath)) {
    if (-not (Test-Path $p)) { throw "Not found: $p" }
}
$AssemblyPath = (Resolve-Path $AssemblyPath).Path
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($AssemblyPath)
$moduleType = "ArrLite.ArrLiteModule, $($assemblyName.FullName)"

# 1. Schema
if ($PSCmdlet.ShouldProcess($schemaFolder, 'Copy arrLite_schema.xml')) {
    Copy-Item $SchemaPath (Join-Path $schemaFolder 'arrLite_schema.xml') -Force
}

# 2. GAC
if ($PSCmdlet.ShouldProcess($assemblyName.FullName, 'Install into GAC')) {
    Add-Type -AssemblyName System.EnterpriseServices
    (New-Object System.EnterpriseServices.Internal.Publish).GacInstall($AssemblyPath)
}

# 3. applicationHost.config
if ($PSCmdlet.ShouldProcess($ConfigPath, 'Declare arrLite sections and register module')) {
    Copy-Item $ConfigPath "$ConfigPath.$(Get-Date -Format yyyyMMddHHmmss).bak"

    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($ConfigPath)

    function Get-OrAddChild([System.Xml.XmlElement] $parent, [string] $name, [string] $nameAttr) {
        $xpath = if ($nameAttr) { "$name[@name='$nameAttr']" } else { $name }
        $node = $parent.SelectSingleNode($xpath)
        if (-not $node) {
            $node = $parent.OwnerDocument.CreateElement($name)
            if ($nameAttr) { $node.SetAttribute('name', $nameAttr) }
            [void] $parent.AppendChild($node)
        }
        return $node
    }

    $webServerGroup = $xml.SelectSingleNode("/configuration/configSections/sectionGroup[@name='system.webServer']")
    if (-not $webServerGroup) { throw 'system.webServer sectionGroup not found in configSections.' }

    $arrLiteGroup = Get-OrAddChild $webServerGroup 'sectionGroup' 'arrLite'
    $rules = Get-OrAddChild $arrLiteGroup 'section' 'rules'
    $rules.SetAttribute('overrideModeDefault', 'Allow')
    $globalRules = Get-OrAddChild $arrLiteGroup 'section' 'globalRules'
    $globalRules.SetAttribute('overrideModeDefault', 'Deny')
    $globalRules.SetAttribute('allowDefinition', 'AppHostOnly')

    # IIS Express keeps <modules> inside <location path="" overrideMode="Allow">; full IIS at the root.
    $modules = $xml.SelectSingleNode("/configuration/location[@path='']/system.webServer/modules")
    if (-not $modules) { $modules = $xml.SelectSingleNode('/configuration/system.webServer/modules') }
    if (-not $modules) { throw '<system.webServer><modules> not found.' }

    $module = $modules.SelectSingleNode("add[@name='ArrLite']")
    if (-not $module) {
        $module = $xml.CreateElement('add')
        $module.SetAttribute('name', 'ArrLite')
        [void] $modules.AppendChild($module)
    }
    $module.SetAttribute('type', $moduleType)
    $module.RemoveAttribute('preCondition')

    $xml.Save($ConfigPath)
}

Write-Host "ArrLite installed. Module type: $moduleType"
