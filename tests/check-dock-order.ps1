$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path artifacts/WinKnotch.exe).Path)
$type=$assembly.GetType('PlayerWindow')
$instanceFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags=[Reflection.BindingFlags]'Static,NonPublic'
$reorder=$type.GetMethod('ReorderVisibleApps',$staticFlags)
foreach($case in @(
    @{ All=@('A','B','C'); Visible=@('A','B','C'); From=0; To=2; Expected='B,C,A' },
    @{ All=@('A','B','C'); Visible=@('A','B','C'); From=2; To=0; Expected='C,A,B' },
    @{ All=@('A','Hidden','B','C'); Visible=@('A','B','C'); From=0; To=2; Expected='B,Hidden,C,A' },
    @{ All=@('A','B','C'); Visible=@('A','B','C'); From=1; To=1; Expected='A,B,C' },
    @{ All=@('A','B','C'); Visible=@('A','B','C'); From=-1; To=2; Expected='A,B,C' }
)) {
    $all=New-Object 'Collections.Generic.List[string]'
    $visible=New-Object 'Collections.Generic.List[string]'
    foreach($entry in $case.All){$all.Add($entry)}
    foreach($entry in $case.Visible){$visible.Add($entry)}
    $reorder.Invoke($null,@($all.PSObject.BaseObject,$visible.PSObject.BaseObject,$case.From,$case.To))
    if(($all -join ',') -ne $case.Expected){throw 'Dock reorder changed the wrong positions'}
}
$key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\BezelPlayer')
$saved=@{}
foreach($name in $key.GetValueNames()){$saved[$name]=@{Value=$key.GetValue($name);Kind=$key.GetValueKind($name)}}
$window=$null
try {
    $key.SetValue('AppsDock',1,[Microsoft.Win32.RegistryValueKind]::DWord)
    $key.SetValue('SelectedApps',[string[]]@('test:A','test:B','test:C'),[Microsoft.Win32.RegistryValueKind]::MultiString)
    $key.SetValue('AppOrder',[string[]]@('test:B','test:A','test:C'),[Microsoft.Win32.RegistryValueKind]::MultiString)
    $window=[Activator]::CreateInstance($type,$true)
    $entryType=$type.GetNestedType('AppEntry',[Reflection.BindingFlags]'NonPublic')
    $entries=$type.GetField('appEntries',$instanceFlags).GetValue($window)
    foreach($name in @('A','B','C')) {
        $entry=[Activator]::CreateInstance($entryType,$true)
        $entryType.GetField('Name').SetValue($entry,$name)
        $entryType.GetField('Target').SetValue($entry,"test:$name")
        $entries.Add($entry)
    }
    $type.GetMethod('RenderApps',$instanceFlags).Invoke($window,@())
    $rows=$type.GetField('dockRows',$instanceFlags).GetValue($window)
    if((@($rows.Children | ForEach-Object {$_.Tag.Name}) -join ',') -ne 'B,A,C'){throw 'Dock ignored saved order'}
    $order=$type.GetField('appOrder',$instanceFlags).GetValue($window)
    $visible=New-Object 'Collections.Generic.List[string]'
    foreach($target in @('test:B','test:A','test:C')){$visible.Add($target)}
    $reorder.Invoke($null,@($order.PSObject.BaseObject,$visible.PSObject.BaseObject,0,2))
    $type.GetMethod('SaveSettings',$instanceFlags).Invoke($window,@())
    $window.Close();$window=$null
    $window=[Activator]::CreateInstance($type,$true)
    $order=$type.GetField('appOrder',$instanceFlags).GetValue($window)
    if(($order -join ',') -ne 'test:A,test:C,test:B'){throw 'Dock order did not survive restart'}
    Write-Output 'Reordering in both directions, filtered positions, saved layout, and restart persistence passed'
} finally {
    if($window){$window.Close()}
    foreach($name in $key.GetValueNames()){if(-not $saved.ContainsKey($name)){$key.DeleteValue($name)}}
    foreach($name in $saved.Keys){$key.SetValue($name,$saved[$name].Value,$saved[$name].Kind)}
    $key.Close()
}
