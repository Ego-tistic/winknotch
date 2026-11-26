$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path 'artifacts/WinKnotch.exe').Path)
$type = $assembly.GetType('PlayerWindow')
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$control = $type.GetMethod('Control', $flags)
$magnify = $type.GetMethod('MagnifyButtonGroup', $flags)
$activate = $type.GetMethod('TryActivateRunningApp', $flags)
function Pump-Frames([int]$milliseconds) {
    $frame = New-Object Windows.Threading.DispatcherFrame
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Add_Tick({ $timer.Stop(); $frame.Continue = $false }.GetNewClosure())
    $timer.Start()
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}
$window = New-Object Windows.Window
$window.Title = 'Bezel Player layout verification'
$window.SizeToContent = 'WidthAndHeight'
$window.WindowStartupLocation = 'CenterScreen'
try {
    foreach ($mode in @(
        @{ Name='Top'; Width=66; ShelfWidth=254; ShelfHeight=41; Direction='Horizontal' },
        @{ Name='Corner'; Width=51; ShelfWidth=211; ShelfHeight=41; Direction='Horizontal' },
        @{ Name='Side'; Width=70; ShelfWidth=104; ShelfHeight=125; Direction='Vertical' },
        @{ Name='Clock actions'; Width=73; ButtonHeight=32; ShelfWidth=272; ShelfHeight=44; Direction='Horizontal' }
    )) {
        $panel = New-Object Windows.Controls.StackPanel
        $panel.Orientation = $mode.Direction
        $panel.HorizontalAlignment = 'Center'
        $panel.VerticalAlignment = 'Center'
        $shelf = New-Object Windows.Controls.Border
        $shelf.Width = $mode.ShelfWidth
        $shelf.Height = $mode.ShelfHeight
        $shelf.BorderThickness = New-Object Windows.Thickness 1
        $shelf.Child = $panel
        $buttons = @()
        foreach ($name in @('Media','Clock','Apps')) {
            $button = $control.Invoke($null, @($name, $name, [Action]{}))
            $button.Width = $mode.Width
            $button.Height = if ($mode.ContainsKey('ButtonHeight')) { $mode.ButtonHeight } else { 29 }
            $button.Margin = New-Object Windows.Thickness 8,5,8,5
            [void]$panel.Children.Add($button)
            $buttons += $button
        }
        $window.Content = $shelf
        $window.Show()
        $window.UpdateLayout()
        foreach ($hover in $buttons) {
            $magnify.Invoke($null, @($hover, $true))
            Pump-Frames 240
            $bounds = @($buttons | ForEach-Object {
                $face = $_.Template.FindName('ButtonFace', $_)
                $face.TransformToAncestor($shelf).TransformBounds((New-Object Windows.Rect 0,0,$face.ActualWidth,$face.ActualHeight))
            })
            for ($i=0; $i -lt 2; $i++) {
                if ($bounds[$i].IntersectsWith($bounds[$i+1])) { throw "$($mode.Name) tabs overlap" }
            }
            foreach ($rect in $bounds) {
                if ($rect.Left -lt 0 -or $rect.Top -lt 0 -or $rect.Right -gt $shelf.Width -or $rect.Bottom -gt $shelf.Height) {
                    throw "$($mode.Name) magnification overflows the tab shelf"
                }
            }
        }
        Write-Output "$($mode.Name) hover geometry passed"
    }
    $entryType = $type.GetNestedType('AppEntry', [Reflection.BindingFlags]'NonPublic')
    $entry = [Activator]::CreateInstance($entryType, $true)
    $entryType.GetField('Executable').SetValue($entry, [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName)
    $window.WindowState = 'Minimized'
    Pump-Frames 150
    if (-not $activate.Invoke($null, @($entry))) { throw 'Running window was not matched' }
    Pump-Frames 350
    if ($window.WindowState -eq 'Minimized') { throw 'Running window was not restored' }
    $handle = (New-Object Windows.Interop.WindowInteropHelper $window).Handle
    $foreground = $type.GetMethod('GetForegroundWindow', $flags).Invoke($null, @())
    if ($foreground -ne $handle) { throw 'Restored window was not brought to the foreground' }
    Write-Output 'Existing minimized app restored and foregrounded'
    $entryType.GetField('Executable').SetValue($entry, 'C:\BezelTest\NotRunning.exe')
    if ($activate.Invoke($null, @($entry))) { throw 'Unrelated window incorrectly matched' }
    Write-Output 'Unrelated windows excluded'
} finally { $window.Close() }
