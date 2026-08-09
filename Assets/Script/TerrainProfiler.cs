using System;
using UnityEngine;

namespace JayFos.Terrain
{
    public static class TerrainProfiler
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
            public const bool EnableProfiling = true;
        #else
            public const bool EnableProfiling = false;
        #endif

        public static bool Enabled
        {
            get => EnableProfiling && _enabled;
            set => _enabled = value;
        }
        private static volatile bool _enabled = true;

        public static float TotalMeshTimeMs { get; private set; }
        public static float TotalFoliageTimeMs { get; private set; }
        public static float TotalChunkPoolTimeMs { get; private set; }
        public static float PeakMeshTimeMs { get; private set; }
        public static float PeakFoliageTimeMs { get; private set; }
        public static int ChunksGenerated { get; private set; }

        public static int TotalMeshAllocations { get; private set; }
        public static int TotalFoliageAllocations { get; private set; }
        public static int HighAllocationChunks { get; private set; }

        public static void RecordMeshAllocation(int approximateBytes)
        {
            if (!Enabled) return;
            TotalMeshAllocations += approximateBytes;
            if (approximateBytes > 1024)
                HighAllocationChunks++;
        }

        public static void RecordFoliageAllocation(int approximateBytes)
        {
            if (!Enabled) return;
            TotalFoliageAllocations += approximateBytes;
        }

        private const int RollingWindow = 60;
        private static readonly float[] _meshTimesBuffer = new float[RollingWindow];
        private static readonly float[] _foliageTimesBuffer = new float[RollingWindow];
        private static int _bufferIndex = 0;

        public static float AverageMeshTimeMs => RollingAverage(_meshTimesBuffer);
        public static float AverageFoliageTimeMs => RollingAverage(_foliageTimesBuffer);

        private static float RollingAverage(float[] buf)
        {
            if (_bufferIndex == 0) return 0f;
            float sum = 0f;
            int count = Mathf.Min(_bufferIndex, buf.Length);
            for (int i = 0; i < count; i++)
                sum += buf[i];
            return sum / count;
        }

        public static ProfilerScope ScopedMesh() => new ProfilerScope("Mesh");
        public static ProfilerScope ScopedFoliage() => new ProfilerScope("Foliage");
        public static ProfilerScope ScopedPool() => new ProfilerScope("Pool");
        public static ProfilerScope Scoped(string label) => new ProfilerScope(label);

        public static ProfilerStats GetStats()
        {
            return new ProfilerStats(
                TotalMeshTimeMs,
                TotalFoliageTimeMs,
                TotalChunkPoolTimeMs,
                PeakMeshTimeMs,
                PeakFoliageTimeMs,
                ChunksGenerated,
                AverageMeshTimeMs,
                AverageFoliageTimeMs,
                TotalMeshAllocations,
                TotalFoliageAllocations,
                HighAllocationChunks
            );
        }

        public static void Reset()
        {
            TotalMeshTimeMs = 0f;
            TotalFoliageTimeMs = 0f;
            TotalChunkPoolTimeMs = 0f;
            PeakMeshTimeMs = 0f;
            PeakFoliageTimeMs = 0f;
            ChunksGenerated = 0;
            TotalMeshAllocations = 0;
            TotalFoliageAllocations = 0;
            HighAllocationChunks = 0;
            _bufferIndex = 0;
            for (int i = 0; i < RollingWindow; i++)
            {
                _meshTimesBuffer[i] = 0f;
                _foliageTimesBuffer[i] = 0f;
            }
        }

        internal static void ResetPerChunk()
        {
            _bufferIndex = 0;
        }

        public struct ProfilerScope : IDisposable
        {
            private readonly string _label;
            private readonly float _startTime;
            private readonly bool _active;

            internal ProfilerScope(string label)
            {
                _label = label;
                _active = EnableProfiling && _enabled;
                _startTime = Time.realtimeSinceStartup * 1000f;

                if (_active && label == "Mesh")
                {
                    ResetPerChunk();
                }

                if (!_active)
                {
                    _label = null;
                }
            }

            public void Dispose()
            {
                if (_active)
                {
                    float elapsedMs = Time.realtimeSinceStartup * 1000f - _startTime;

                    switch (_label)
                    {
                        case "Mesh":
                            TotalMeshTimeMs += elapsedMs;
                            PeakMeshTimeMs = Mathf.Max(PeakMeshTimeMs, elapsedMs);
                            break;

                        case "Foliage":
                            TotalFoliageTimeMs += elapsedMs;
                            PeakFoliageTimeMs = Mathf.Max(PeakFoliageTimeMs, elapsedMs);
                            break;

                        case "Pool":
                            TotalChunkPoolTimeMs += elapsedMs;
                            break;
                    }

                    int idx = _bufferIndex % RollingWindow;
                    if (_label == "Mesh") _meshTimesBuffer[idx] = elapsedMs;
                    if (_label == "Foliage") _foliageTimesBuffer[idx] = elapsedMs;
                    if (_label != "Pool") _bufferIndex++;
                }

                if (_label == "Mesh")
                    ChunksGenerated++;
            }
        }

        public struct ProfilerStats
        {
            public readonly float totalMeshTimeMs;
            public readonly float totalFoliageTimeMs;
            public readonly float totalChunkPoolTimeMs;
            public readonly float peakMeshTimeMs;
            public readonly float peakFoliageTimeMs;
            public readonly int chunksGenerated;
            public readonly float averageMeshTimeMs;
            public readonly float averageFoliageTimeMs;
            public readonly int totalMeshAllocations;
            public readonly int totalFoliageAllocations;
            public readonly int highAllocationChunks;

            public ProfilerStats(
                float totalMesh, float totalFoliage, float totalPool,
                float peakMesh, float peakFoliage, int chunksGen,
                float avgMesh, float avgFoliage,
                int meshAlloc, int foliageAlloc, int highAllocChunks)
            {
                totalMeshTimeMs = totalMesh;
                totalFoliageTimeMs = totalFoliage;
                totalChunkPoolTimeMs = totalPool;
                peakMeshTimeMs = peakMesh;
                peakFoliageTimeMs = peakFoliage;
                chunksGenerated = chunksGen;
                averageMeshTimeMs = avgMesh;
                averageFoliageTimeMs = avgFoliage;
                totalMeshAllocations = meshAlloc;
                totalFoliageAllocations = foliageAlloc;
                highAllocationChunks = highAllocChunks;
            }

            public override string ToString()
            {
                return
                    $"[TerrainProfiler]\n" +
                    $"  Chunks generated : {chunksGenerated}\n" +
                    $"  Avg mesh time    : {averageMeshTimeMs:F2} ms\n" +
                    $"  Avg foliage time : {averageFoliageTimeMs:F2} ms\n" +
                    $"  Peak mesh time   : {peakMeshTimeMs:F2} ms\n" +
                    $"  Peak foliage time: {peakFoliageTimeMs:F2} ms\n" +
                    $"  Total mesh       : {totalMeshTimeMs:F1} ms\n" +
                    $"  Total foliage    : {totalFoliageTimeMs:F1} ms\n" +
                    $"  Total pool ops   : {totalChunkPoolTimeMs:F1} ms\n" +
                    $"  Mesh allocs (est): {totalMeshAllocations / 1024:F1} KB\n" +
                    $"  Foliage allocs   : {totalFoliageAllocations / 1024:F1} KB\n" +
                    $"  High-alloc chunks: {highAllocationChunks}";
            }
        }
    }

    public static class ProfilerConstants
    {
        public const bool EnableProfiling = false;
    }
}
