[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$inputFile = [System.IO.Path]::GetFullPath($InputPath)
$outputFile = [System.IO.Path]::GetFullPath($OutputPath)
if (-not [System.IO.File]::Exists($inputFile))
{
    throw "Unity test result does not exist: $inputFile"
}

[xml]$source = Get-Content -LiteralPath $inputFile -Raw
$run = $source.'test-run'
if ($null -eq $run)
{
    throw "Unity test result has no test-run root."
}

$document = [System.Xml.XmlDocument]::new()
$suite = $document.CreateElement("testsuite")
$document.AppendChild($suite) | Out-Null
$suite.SetAttribute("name", "Unity EditMode")
$suite.SetAttribute("tests", [string]$run.total)
$suite.SetAttribute("failures", [string]$run.failed)
$suite.SetAttribute("errors", "0")
$suite.SetAttribute("skipped", [string]([int]$run.skipped + [int]$run.inconclusive))
$suite.SetAttribute("time", [string]$run.duration)

foreach ($test in $source.SelectNodes("//test-case"))
{
    $case = $document.CreateElement("testcase")
    $case.SetAttribute("classname", [string]$test.classname)
    $case.SetAttribute("name", [string]$test.name)
    $case.SetAttribute("time", [string]$test.duration)
    $suite.AppendChild($case) | Out-Null

    if ($test.result -eq "Failed")
    {
        $failure = $document.CreateElement("failure")
        $failure.SetAttribute("message", [string]$test.failure.message.InnerText)
        $failure.InnerText = [string]$test.failure.'stack-trace'.InnerText
        $case.AppendChild($failure) | Out-Null
    }
    elseif ($test.result -eq "Skipped" -or $test.result -eq "Inconclusive")
    {
        $case.AppendChild($document.CreateElement("skipped")) | Out-Null
    }

    $output = $test.SelectSingleNode("output")
    if ($null -ne $output -and -not [string]::IsNullOrEmpty($output.InnerText))
    {
        $systemOut = $document.CreateElement("system-out")
        $systemOut.InnerText = $output.InnerText
        $case.AppendChild($systemOut) | Out-Null
    }
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($outputFile)) | Out-Null
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$settings.Indent = $true
$writer = [System.Xml.XmlWriter]::Create($outputFile, $settings)
try
{
    $document.Save($writer)
}
finally
{
    $writer.Dispose()
}

Write-Host "JUnit result ready: tests=$($run.total) failed=$($run.failed)"
