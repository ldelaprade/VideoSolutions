# Video Solutions Project
Purpose of that project is to integrate the steps involved in distributing a WPF VLC video application via Microsoft Store.

Video Solution contains:
- the WPF application project sources folder: *VideoCenter*
- MSIX packager project: *VideoSolutionsInstaller*

# VLC packages dependencies
- LibVLCSharp.WPF
- VideoLAN.LibVLC.Windows
## Must have VLC artifacts in final product
VideoSolutions Packager project will package VLC required artifacts thanks to manual change in VideoSolutions.wapproj:
```
  <ItemGroup>
    <ProjectReference Include="..\VideoCenter\VideoCenter.csproj" />
    <!-- All VLC DLLs and files -->
    <Content Include="..\VideoCenter\bin\Release\net8.0-windows\libvlc\win-x64\**\*.*">
      <Link>VideoCenter\libvlc\win-x64\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </Content>
  </ItemGroup>
```
## VideoCenter Manifest 
app.manifest file required for passing Windows App Cert Kit (WACK) tests
```
  <!-- Indicates that the application is DPI-aware and will not be automatically scaled by Windows at higher
       DPIs. Windows Presentation Foundation (WPF) applications are automatically DPI-aware and do not need 
       to opt in. Windows Forms applications targeting .NET Framework 4.6 that opt into this setting, should 
       also set the 'EnableWindowsFormsHighDpiAutoResizing' setting to 'true' in their app.config. 
       
       Makes the application long-path aware. See https://docs.microsoft.com/windows/win32/fileio/maximum-file-path-limitation -->
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
        <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    	<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
	    <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
	</windowsSettings>
  </application>
```
