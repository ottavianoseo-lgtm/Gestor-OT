> [build 11/13] COPY . .                                                                                         2.3s
 => [build 12/13] WORKDIR /src/src/GestorOT.Api                                                                    1.0s
 => ERROR [build 13/13] RUN dotnet build "GestorOT.Api.csproj" -c Release -o /app/build                           30.0s
------
 > [build 13/13] RUN dotnet build "GestorOT.Api.csproj" -c Release -o /app/build:
1.914   Determining projects to restore...
5.372   Restored /src/src/GestorOT.Client/GestorOT.Client.csproj (in 1.74 sec).
5.461   Restored /src/src/GestorOT.Api/GestorOT.Api.csproj (in 1.68 sec).
5.467   4 of 6 projects are up-to-date for restore.
13.00   GestorOT.Domain -> /app/build/GestorOT.Domain.dll
16.22   GestorOT.Shared -> /app/build/GestorOT.Shared.dll
16.76   GestorOT.Application -> /app/build/GestorOT.Application.dll
23.58   GestorOT.Infrastructure -> /app/build/GestorOT.Infrastructure.dll
27.24 /src/src/GestorOT.Client/Pages/Admin/Users.razor(150,8): warning CS0105: The using directive for 'GestorOT.Domain.Enums' appeared previously in this namespace [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Components/LaborAttachments.razor(139,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Components/LaborAttachments.razor(173,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Pages/WorkPlanner.razor(534,28): warning CS8619: Nullability of reference types in value of type 'Task<List<CampaignLotDto>?>' doesn't match target type 'Task<List<CampaignLotDto>>'. [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Admin/WorkOrderStatuses_razor.g.cs(856,70): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Admin/WorkOrderStatuses_razor.g.cs(882,70): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Pages/Admin/LaborCatalog.razor(86,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Campos_razor.g.cs(248,56): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Components/LaborEditorForm.razor(242,24): warning CS8619: Nullability of reference types in value of type 'Task<List<CampaignLotDto>?>' doesn't match target type 'Task<List<CampaignLotDto>>'. [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Pages/LaboresSueltas.razor(423,28): warning CS8619: Nullability of reference types in value of type 'Task<List<CampaignLotDto>?>' doesn't match target type 'Task<List<CampaignLotDto>>'. [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Lotes_razor.g.cs(254,56): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Pages/Lotes.razor(344,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/OrdenesTrabajos_razor.g.cs(1976,78): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/OrdenesTrabajos_razor.g.cs(2002,78): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Pages/Lotes.razor(425,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
27.25 /src/src/GestorOT.Client/Pages/Lotes.razor(289,33): warning CS0169: The field 'Lotes._form' is never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
28.94   Compiling native assets with /usr/share/dotnet/packs/Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk.linux-x64/10.0.6/Sdk/../tools/emscripten/emcc with -Oz. This may take a while ...
29.01 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : Failed to compile /usr/share/dotnet/packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/10.0.6/runtimes/browser-wasm/native/src/driver.c -> /src/src/GestorOT.Client/obj/Release/net10.0/wasm/for-build/driver.o [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.01 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : unable to find python in $PATH [took 0.06s] [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.01 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : Failed to compile /usr/share/dotnet/packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/10.0.6/runtimes/browser-wasm/native/src/pinvoke.c -> /src/src/GestorOT.Client/obj/Release/net10.0/wasm/for-build/pinvoke.o [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.01 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : unable to find python in $PATH [took 0.06s] [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04
29.04 Build FAILED.
29.04
29.04 /src/src/GestorOT.Client/Pages/Admin/Users.razor(150,8): warning CS0105: The using directive for 'GestorOT.Domain.Enums' appeared previously in this namespace [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Components/LaborAttachments.razor(139,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Components/LaborAttachments.razor(173,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Pages/WorkPlanner.razor(534,28): warning CS8619: Nullability of reference types in value of type 'Task<List<CampaignLotDto>?>' doesn't match target type 'Task<List<CampaignLotDto>>'. [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Admin/WorkOrderStatuses_razor.g.cs(856,70): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Admin/WorkOrderStatuses_razor.g.cs(882,70): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Pages/Admin/LaborCatalog.razor(86,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Campos_razor.g.cs(248,56): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Components/LaborEditorForm.razor(242,24): warning CS8619: Nullability of reference types in value of type 'Task<List<CampaignLotDto>?>' doesn't match target type 'Task<List<CampaignLotDto>>'. [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Pages/LaboresSueltas.razor(423,28): warning CS8619: Nullability of reference types in value of type 'Task<List<CampaignLotDto>?>' doesn't match target type 'Task<List<CampaignLotDto>>'. [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/Lotes_razor.g.cs(254,56): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Pages/Lotes.razor(344,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/OrdenesTrabajos_razor.g.cs(1976,78): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/obj/Release/net10.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Pages/OrdenesTrabajos_razor.g.cs(2002,78): warning CS0618: 'Checkbox.CheckedExpression' is obsolete: 'Currently not implemented' [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Pages/Lotes.razor(425,26): warning CS0168: The variable 'ex' is declared but never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /src/src/GestorOT.Client/Pages/Lotes.razor(289,33): warning CS0169: The field 'Lotes._form' is never used [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : Failed to compile /usr/share/dotnet/packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/10.0.6/runtimes/browser-wasm/native/src/driver.c -> /src/src/GestorOT.Client/obj/Release/net10.0/wasm/for-build/driver.o [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : unable to find python in $PATH [took 0.06s] [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : Failed to compile /usr/share/dotnet/packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/10.0.6/runtimes/browser-wasm/native/src/pinvoke.c -> /src/src/GestorOT.Client/obj/Release/net10.0/wasm/for-build/pinvoke.o [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04 /usr/share/dotnet/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.6/Sdk/WasmApp.Common.targets(853,5): error : unable to find python in $PATH [took 0.06s] [/src/src/GestorOT.Client/GestorOT.Client.csproj]
29.04     16 Warning(s)
29.04     2 Error(s)
29.04
29.04 Time Elapsed 00:00:27.66
------
[+] build 0/1
 ⠙ Image gestor-ot-gestor-ot-app Building                                                                         116.3s
Dockerfile:23

--------------------

  21 |

  22 |     WORKDIR "/src/src/GestorOT.Api"

  23 | >>> RUN dotnet build "GestorOT.Api.csproj" -c Release -o /app/build

  24 |

  25 |     FROM build AS publish

--------------------

failed to solve: process "/bin/sh -c dotnet build \"GestorOT.Api.csproj\" -c Release -o /app/build" did not complete successfully: exit code: 1