$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path artifacts/WinTide.exe).Path)
$type=$assembly.GetType('PlayerWindow')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\BezelPlayer')
$saved=@{}
foreach($name in $key.GetValueNames()){$saved[$name]=@{Value=$key.GetValue($name);Kind=$key.GetValueKind($name)}}
$window=$null
function Capture-Settings($window, $name) {
    $page=$type.GetField('settingsPage',$flags).GetValue($window)
    $page.Visibility='Visible'; $page.Opacity=1; $page.Margin=New-Object Windows.Thickness 0
    if($page.Parent){[void]$page.Parent.Children.Remove($page)}
    $hostPanel=New-Object Windows.Controls.Grid
    $hostPanel.Resources['IslandControlInk']=$window.Resources['IslandControlInk']
    $hostPanel.Resources[[Windows.Controls.CheckBox]]=$window.Resources[[Windows.Controls.CheckBox]]
    $hostPanel.Resources[[Windows.Controls.RadioButton]]=$window.Resources[[Windows.Controls.RadioButton]]
    $isLight=$type.GetField('lightTheme',$flags).GetValue($window)
    $converter=New-Object Windows.Media.BrushConverter
    $hostPanel.Background=$converter.ConvertFromString($(if($isLight){'#F2F3F6'}else{'#17181B'}))
    [void]$hostPanel.Children.Add($page)
    $hostPanel.Measure((New-Object Windows.Size 314,441))
    $hostPanel.Arrange((New-Object Windows.Rect 0,0,314,441))
    $hostPanel.UpdateLayout()
    $slider=$type.GetField('inlineOpacity',$flags).GetValue($window)
    [void]$slider.ApplyTemplate()
    $track=$slider.Template.FindName('PART_Track',$slider)
    [void]$track.Thumb.ApplyTemplate()
    $knob=$track.Thumb.Template.FindName('Knob',$track.Thumb)
    if($knob.Fill.Color -ne $window.Resources['IslandControlInk'].Color){throw 'Slider knob did not adapt to theme contrast'}
    $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap 471,662,144,144,([Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($hostPanel)
    $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file=[IO.File]::Create((Join-Path (Resolve-Path tests/output).Path "$name.png"))
    try{$encoder.Save($file)}finally{$file.Dispose()}
    $hostPanel.Children.Clear()
}
try {
    $window=[Activator]::CreateInstance($type,$true)
    $select=$type.GetMethod('SelectTheme',$flags)
    $select.Invoke($window,@(1))
    if($key.GetValue('Theme') -ne 'Light'){throw 'Light theme did not save'}
    Capture-Settings $window 'theme-light'
    $type.GetField('customThemeColor',$flags).SetValue($window,[Windows.Media.Color]::FromRgb(103,72,151))
    $select.Invoke($window,@(2))
    if($key.GetValue('Theme') -ne 'Custom'){throw 'Custom theme did not save'}
    Capture-Settings $window 'theme-custom'
    $controls=$type.GetField('inlineColorControls',$flags).GetValue($window)
    $controls.Brightness.Value=45
    $color=$type.GetField('customThemeColor',$flags).GetValue($window)
    if([Math]::Abs([Math]::Max($color.R,[Math]::Max($color.G,$color.B))-115) -gt 1){throw 'Brightness did not update the custom color'}
    $opacity=$type.GetField('inlineOpacity',$flags).GetValue($window)
    $opacity.Value=100
    $controls.Hex.Text='#D4EDFC'
    $focusArgs=New-Object Windows.Input.KeyboardFocusChangedEventArgs ([Windows.Input.Keyboard]::PrimaryDevice),0,$controls.Hex,$null
    $focusArgs.RoutedEvent=[Windows.Input.Keyboard]::LostKeyboardFocusEvent
    $controls.Hex.RaiseEvent($focusArgs)
    if(-not $type.GetField('lightTheme',$flags).GetValue($window)){throw 'Light custom color did not select dark text'}
    if($key.GetValue('CustomColor') -ne '#D4EDFC'){throw 'Hex color did not save'}
    $opacity.Value=53
    if($type.GetField('lightTheme',$flags).GetValue($window)){throw 'Transparent custom color incorrectly uses dark text'}
    $opacity.Value=100
    if(-not $type.GetField('lightTheme',$flags).GetValue($window)){throw 'Opaque pale custom color did not restore dark text'}
    $window.Close();$window=$null
    $window=[Activator]::CreateInstance($type,$true)
    if(-not $type.GetField('customTheme',$flags).GetValue($window)){throw 'Custom theme did not survive restart'}
    $color=$type.GetField('customThemeColor',$flags).GetValue($window)
    if($color.ToString() -ne '#FFD4EDFC'){throw 'Custom color did not survive restart'}
    Write-Output 'Light/custom selection, brightness, hex entry, contrast, and restart persistence passed'
} finally {
    if($window){$window.Close()}
    foreach($name in $key.GetValueNames()){if(-not $saved.ContainsKey($name)){$key.DeleteValue($name)}}
    foreach($name in $saved.Keys){$key.SetValue($name,$saved[$name].Value,$saved[$name].Kind)}
    $key.Close()
}
