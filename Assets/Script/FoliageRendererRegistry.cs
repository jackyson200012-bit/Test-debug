using System.Collections.Generic;
using UnityEngine;

namespace JayFos.Foliage
{
    /// <summary>
    /// Static registry of foliage MeshRenderers. Foliage instances register on
    /// spawn and unregister on despawn (chunk deactivate/destroy), so controllers
    /// can iterate live foliage without per-frame hierarchy scanning.
    /// </summary>
    public static class FoliageRendererRegistry
    {
        private static readonly List<MeshRenderer> _renderers = new List<MeshRenderer>(256);

        public static int Count => _renderers.Count;

        public static void Add(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            if (!_renderers.Contains(renderer))
                _renderers.Add(renderer);
        }

        public static void Remove(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            _renderers.Remove(renderer);
        }

        public static void CopyTo(List<MeshRenderer> results)
        {
            if (results == null)
                return;

            results.Clear();

            for (int i = 0; i < _renderers.Count; i++)
            {
                MeshRenderer renderer = _renderers[i];
                if (renderer == null)
                    continue;
                results.Add(renderer);
            }
        }

        public static void Clear()
        {
            _renderers.Clear();
        }
    }
}