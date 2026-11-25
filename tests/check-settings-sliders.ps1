$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path 'artifacts/WinTide.exe').Path)
$type = $assembly.GetType('PlayerWindow')
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
$key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\BezelPlayer')
$saved = @{}
foreach ($name in $key.GetValueNames()) { $saved[$name] = @{ Value=$key.GetValue($name); Kind=$key.GetValueKind($name) } }
$window = $null
try {
    $key.SetValue('SelectedApps', [string[]]@('bezel-test-choice'), [Microsoft.Win32.RegistryValueKind]::MultiString)
    $key.SetValue('AppsDock', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
    foreach ($pass in 1..2) {
        $window = [Activator]::CreateInstance($type, $true)
        $selected = $type.GetField('selectedApps', $instanceFlags).GetValue($window)
        if (-not $selected.Contains('bezel-test-choice')) { throw 'App selection was lost during startup' }
        if ($key.GetValue('SelectedApps')[0] -ne 'bezel-test-choice') { throw 'Startup overwrote the saved selection' }
        if (-not $type.GetField('appsDock', $instanceFlags).GetValue($window)) { throw 'Dock layout was not restored' }
        $type.GetMethod('OpenSettings', $instanceFlags).Invoke($window, @())
        foreach ($name in @('timeline','volumeSlider','inlineOpacity','windowOpacity')) {
            $slider = $type.GetField($name, $instanceFlags).GetValue($window)
            [void]$slider.ApplyTemplate()
            $track = $slider.Template.FindName('PART_Track', $slider)
            [void]$track.Thumb.ApplyTemplate()
            $knob = $track.Thumb.Template.FindName('Knob', $track.Thumb)
            if ($track.Thumb.Width -ne 15 -or $track.Thumb.Height -ne 15) { throw "$name has a different knob size" }
            if ($knob.Fill.Color -ne $window.Resources['IslandControlInk'].Color) { throw "$name knob does not use the current theme contrast" }
            $mediaTrack = $type.GetField('timeline', $instanceFlags).GetValue($window).Background.Color
            if ($slider.Background.Color -ne $mediaTrack) { throw "$name track differs from media" }
        }
        foreach ($name in @('inlinePlacement','windowPlacement')) {
            $view = $type.GetField($name, $instanceFlags).GetValue($window)
            if ($view.Size.Template -eq $null) { throw "$name size slider is unstyled" }
            if ($view.Size.Background.Color -ne $mediaTrack) { throw "$name size track differs from media" }
        }
        $window.Close()
        $window = $null
    }
    Write-Output 'Dock selections survive two startups without registry loss'
    Write-Output 'Media, volume, opacity, and size sliders share the flat style'
    $scroll = New-Object Windows.Controls.Primitives.ScrollBar
    $scroll.Resources['IslandControlInk'] = [Windows.Media.Brushes]::White
    $scroll.Style = $type.GetMethod('CreateGlassScrollBarStyle', $staticFlags).Invoke($null, @())
    [void]$scroll.ApplyTemplate()
    $track = $scroll.Template.FindName('PART_Track', $scroll)
    [void]$track.Thumb.ApplyTemplate()
    $knob = $track.Thumb.Template.FindName('Glass', $track.Thumb)
    if ($knob -isnot [Windows.Shapes.Ellipse] -or $knob.Fill.Color -ne [Windows.Media.Colors]::White) { throw 'App drawer scrollbar is not a white circle' }
    Write-Output 'App drawer scrollbar uses the white circular thumb'
    $scroll.Minimum = 0
    $scroll.Maximum = 100
    $scroll.ViewportSize = 80
    $scroll.Height = 300
    $scroll.Value = 0
    $scroll.Measure((New-Object Windows.Size 25,300))
    $scroll.Arrange((New-Object Windows.Rect 0,0,25,300))
    $scroll.UpdateLayout()
    $start = $track.Thumb.TranslatePoint((New-Object Windows.Point 0,0), $track).Y
    $scroll.Value = 100
    $scroll.UpdateLayout()
    $end = $track.Thumb.TranslatePoint((New-Object Windows.Point 0,0), $track).Y
    if ([Math]::Abs($track.Thumb.ActualHeight - 15) -gt .5) { throw 'Scrollbar has an oversized invisible thumb' }
    if ([Math]::Abs($start) -gt .5 -or [Math]::Abs($end + 15 - $track.ActualHeight) -gt .5) { throw 'Scrollbar thumb does not span the full track' }
    Write-Output 'Scrollbar thumb travels from the top to the bottom of its track'
} finally {
    if ($window) { $window.Close() }
    foreach ($name in $key.GetValueNames()) { if (-not $saved.ContainsKey($name)) { $key.DeleteValue($name) } }
    foreach ($name in $saved.Keys) { $key.SetValue($name, $saved[$name].Value, $saved[$name].Kind) }
    $key.Close()
}
