@echo off
set CSC="C:\Program Files\dotnet\sdk\10.0.302\Roslyn\bincore\csc.dll"
set OUT="d:\Test debug\Test debug\temp_verify.dll"
set MODS="d:\Test debug\Test debug\Assets\Script\BiomeType.cs" "d:\Test debug\Test debug\Assets\Script\BiomeAttribute.cs" "d:\Test debug\Test debug\Assets\Script\BiomeDefinition.cs" "d:\Test debug\Test debug\Assets\Script\BiomeSample.cs" "d:\Test debug\Test debug\Assets\Script\BiomeMap.cs" "d:\Test debug\Test debug\Assets\Script\BiomeTerrainParams.cs" "d:\Test debug\Test debug\Assets\Script\FoliageGenerator.cs" "d:\Test debug\Test debug\Assets\Script\FoliagePool.cs" "d:\Test debug\Test debug\Assets\Script\TerrainProfiler.cs" "d:\Test debug\Test debug\Assets\Script\FoliageConfig.cs" "d:\Test debug\Test debug\Assets\Script\FoliageData.cs" "d:\Test debug\Test debug\Assets\Script\FoliageSpawnRule.cs" "d:\Test debug\Test debug\Assets\Script\FoliageSpawnRules.cs" "d:\Test debug\Test debug\Assets\Script\ChunkManager.cs" "d:\Test debug\Test debug\Assets\Script\Chunk.cs" "d:\Test debug\Test debug\Assets\Script\HeightMap.cs" "d:\Test debug\Test debug\Assets\Script\NoiseGenerator.cs" "d:\Test debug\Test debug\Assets\Script\WorldSettings.cs" "d:\Test debug\Test debug\Assets\Script\WorldManager.cs" "d:\Test debug\Test debug\Assets\Script\MeshGenerator.cs" "d:\Test debug\Test debug\Assets\Script\ChunkPool.cs" "d:\Test debug\Test debug\Assets\Script\HeightMapPool.cs" "d:\Test debug\Test debug\Assets\Script\WaterMeshGenerator.cs" "d:\Test debug\Test debug\Assets\Script\FoliageDebugOverlay.cs" "d:\Test debug\Test debug\Assets\Script\TerrainGenerator.cs" "d:\Test debug\Test debug\Assets\Script\TerrainDeterminismTest.cs"
set REFS=/reference:"D:\6000.5.3f1\Editor\Data\DotNetSdk\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1\netstandard.dll" /reference:"D:\6000.5.3f1\Editor\Data\Managed\UnityEditor.dll" /reference:"D:\6000.5.3f1\Editor\Data\Managed\UnityEngine.dll"

dotnet exec %CSC% /nologo /nostdlib /target:library /out:%OUT% %MODS% %REFS%
if %errorlevel% equ 0 (
    echo BUILD SUCCEEDED
) else (
    echo BUILD FAILED with exit code %errorlevel%
)