param([switch]$DesktopOnly)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:windir 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$metadata = 'Windows.Foundation', 'Windows.Globalization', 'Windows.Media', 'Windows.Graphics', 'Windows.Storage' | ForEach-Object {
    '/reference:' + (Join-Path $env:windir ('System32/WinMetadata/' + $_ + '.winmd'))
}
$runtime = 'System.Runtime', 'System.Runtime.WindowsRuntime', 'System.Runtime.InteropServices.WindowsRuntime', 'System.Threading.Tasks', 'System.Collections' | ForEach-Object {
    $assembly = $_
    $file = Get-ChildItem -LiteralPath (Join-Path $env:windir "Microsoft.NET/assembly/GAC_MSIL/$assembly") -Recurse -Filter "$assembly.dll" |
        Select-Object -First 1
    if (-not $file) { throw "Missing Windows assembly $assembly." }
    '/reference:' + $file.FullName
}
Push-Location -LiteralPath $project
try {
    $files = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName
    $art = 'hamster.png', 'hamster-peek.png', 'hamster-mask.png', 'hamster-peek-mask.png', 'airi-poses.png', 'airi-interactions.png', 'airi-treats.png' | ForEach-Object {
        '/resource:' + (Join-Path $project ('dist/assets/' + $_)) + ',' + $_
    }
    $icon = Join-Path $project 'dist/assets/airs.ico'
    & $csc /nologo /utf8output /codepage:65001 /target:winexe /platform:anycpu /optimize+ /reference:System.Runtime.Serialization.dll /out:AIRS.exe "/win32icon:$icon" $metadata $runtime $art $files
    if ($LASTEXITCODE -ne 0) { throw 'AIRS could not be built.' }
    if ($DesktopOnly) { return }
    $masks = 'hamster', 'hamster-peek' | ForEach-Object {
        $mask = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $project "dist/assets/$_-mask.png")))
        'img[src="assets/' + $_ + '.png"] { mask: url("data:image/png;base64,' + $mask + '") center / 100% 100% no-repeat; }'
    }
    $masks | Set-Content -LiteralPath (Join-Path $project 'dist/hamster-masks.css') -Encoding UTF8
    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $project 'AIRS.exe')))
    $poses = $assembly.GetType('AiriPoses')
    $poseData = @{
        frames = @($poses.GetField('Frames', 'NonPublic,Static').GetValue($null) | ForEach-Object {
            ,@($_.X, $_.Y, $_.Width, $_.Height)
        })
        rests = $poses.GetField('Rests', 'NonPublic,Static').GetValue($null)
        restFrameSeconds = $poses.GetField('RestFrameSeconds').GetRawConstantValue()
    }
    'window.airiPoses = ' + ($poseData | ConvertTo-Json -Depth 5 -Compress) + ';' |
        Set-Content -LiteralPath (Join-Path $project 'dist/airi-poses.js') -Encoding UTF8
    $detector = $assembly.GetType('Detector')
    $hamster = $detector.Assembly.GetType('HamsterProgress')
    $language = @{}
    foreach ($name in 'Clauses','Quotes','Mention','QuoteCommand','PriorWork','Limited','Constructive','GoodNegation','Completion','ClauseNegation','ContextBridge','AnswerContext','Partial','ExplicitWhole','Skip','Answer','HandOff') {
        $language[$name] = $detector.GetField($name).GetRawConstantValue()
    }
    $rules = @{
        language = $language
        topics = $detector.GetField('Topics').GetValue($null)
        feedback = $detector.GetField('Feedback').GetValue($null)
        reward = $hamster.GetField('Reward').GetRawConstantValue()
        bondPerLevel = $hamster.GetField('BondPerLevel').GetRawConstantValue()
        shop = @($hamster.GetField('Shop').GetValue($null) | ForEach-Object {
            @{ id = $_.Id; name = $_.Name; kind = $_.Kind; cost = $_.Cost; bond = $_.Bond }
        })
    }
    'window.thinkBackRules = ' + ($rules | ConvertTo-Json -Depth 4 -Compress) + ';' |
        Set-Content -LiteralPath (Join-Path $project 'dist/rules.js') -Encoding UTF8
}
finally {
    Pop-Location
}
