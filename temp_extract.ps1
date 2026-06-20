 = Get-Content 'Views/MainWindow.xaml'
 = 
for ( = 0;  -lt .Count; ++) {
    if ([] -like '*<StackPanel Grid.Column= 1*') {
         = 
        break
    }
}
 = 
if ( -ne ) {
    for ( = ;  -lt .Count; ++) {
        if ([] -like '*</StackPanel>*') {
             = 
            break
        }
    }
}
if ( -ne  -and  -ne ) {
     = [..]
    Set-Content 'backups/2026-03-19_18-06-50/monitor-row-stackpanel.xaml' -Value 
} else {
    throw 'Could not find stack panel'
}
