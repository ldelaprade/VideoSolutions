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

