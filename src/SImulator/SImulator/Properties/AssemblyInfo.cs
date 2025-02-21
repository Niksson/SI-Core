﻿using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
[assembly: AssemblyTrademark("Vladimir Khil")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

//In order to begin building localizable applications, set 
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
    //(used if a resource is not found in the page, 
    // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
    //(used if a resource is not found in the page, 
    // app, or any theme specific resource dictionaries)
)]

#if LEGACY
[assembly: AssemblyTitle("SImulator")]
[assembly: AssemblyDescription("SIGame emulator")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Khil-soft")]
[assembly: AssemblyProduct("SImulator")]
[assembly: AssemblyCopyright("Copyright © Khil-soft 2010 - 2022")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyVersion("2.8.0.0")]
[assembly: AssemblyFileVersion("2.8.0.0")]
#endif
