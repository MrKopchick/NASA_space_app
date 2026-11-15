using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AstraLink.UI
{
    /// <summary>
    /// Spawns and navigates unique animated NPCs on walkable ground tiles of the Space Center Grid.
    /// Automatically excludes all building/decoration Tilemaps and static world props so NPCs never walk on roofs.
    /// </summary>
    public class SpaceCenterNpcManager : MonoBehaviour
    {
        private const string CharacterSheetPath = "Assets/_Project/Art/Maps/2/Tilemap/tilemap.png";
        private const int SheetColumns = 27;

        [Serializable]
        public class NpcSkin
        {
            public string Name;
            public Sprite[] Left = new Sprite[3];
            public Sprite[] Down = new Sprite[3];
            public Sprite[] Up = new Sprite[3];
            public Sprite[] Right = new Sprite[3];

            public bool IsValid()
            {
                return HasAnySprite(Left) && HasAnySprite(Down) && HasAnySprite(Up) && HasAnySprite(Right);
            }

            private static bool HasAnySprite(Sprite[] arr)
            {
                if (arr == null) return false;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] != null) return true;
                }
                return false;
            }
        }

        private struct SkinSliceDef
        {
            public string Name;
            public int StartIndex;
        }

        private static readonly SkinSliceDef[] DefaultSkinSlices =
        {
            new SkinSliceDef { Name = "RedHairEngineer", StartIndex = 23 },
            new SkinSliceDef { Name = "RedHairSpecialist", StartIndex = 104 },
            new SkinSliceDef { Name = "SilverHairOfficer", StartIndex = 185 },
            new SkinSliceDef { Name = "HardhatTechnician", StartIndex = 266 },
            new SkinSliceDef { Name = "BlueSuitWorker", StartIndex = 347 },
            new SkinSliceDef { Name = "NightOpsCrew", StartIndex = 428 }
        };

        private static readonly Vector2Int[] CardinalDirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        [Header("Scene Tilemaps (auto-resolved if empty)")]
        [SerializeField] private Grid targetGrid;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private List<Tilemap> blockedTilemaps = new List<Tilemap>();

        [Header("Walkable Bounds")]
        [SerializeField] private Vector2Int fallbackMinCell = new Vector2Int(-14, -8);
        [SerializeField] private Vector2Int fallbackMaxCell = new Vector2Int(13, 7);

        [Header("NPC Spawning")]
        [SerializeField] private bool oneNpcPerUniqueSkin = true;
        [SerializeField] private int maxNpcCount = 6;
        [SerializeField] private int baseSortingOrder = 3;
        [SerializeField] private float propBlockPadding = 0.18f;
        [SerializeField] private List<NpcSkin> skins = new List<NpcSkin>();

        private readonly List<Vector2Int> _walkableCells = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> _walkableSet = new HashSet<Vector2Int>();
        private readonly List<SpaceCenterNpcWalker> _spawnedNpcs = new List<SpaceCenterNpcWalker>();

        // Reusable BFS buffers to avoid GC allocations during runtime path queries
        private readonly Queue<Vector2Int> _bfsQueue = new Queue<Vector2Int>(256);
        private readonly Dictionary<Vector2Int, Vector2Int> _bfsParent = new Dictionary<Vector2Int, Vector2Int>(256);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (skins == null || skins.Count == 0 || !skins[0].IsValid())
            {
                TryAutoPopulateSkinsInEditor();
            }
        }
#endif

        private void Awake()
        {
#if UNITY_EDITOR
            if (skins == null || skins.Count == 0 || !skins[0].IsValid())
            {
                TryAutoPopulateSkinsInEditor();
            }
#endif
        }

        private void Start()
        {
            ResolveSceneReferences();
            RebuildWalkableGrid();
            SpawnUniqueNpcs();
        }

        [ContextMenu("Auto-Fill 6 Character Skins")]
        public void AutoFillSkinsFromContextMenu()
        {
#if UNITY_EDITOR
            TryAutoPopulateSkinsInEditor();
#endif
        }

        public bool TryBuildRandomPath(Vector3 fromWorld, out Queue<Vector3> worldWaypoints)
        {
            worldWaypoints = null;
            if (targetGrid == null || _walkableCells.Count < 2) return false;

            Vector2Int startCell = WorldToNearestWalkableCell(fromWorld);
            if (!_walkableSet.Contains(startCell)) return false;

            // Pick a different walkable destination (prefer at least 3 cells away when available)
            Vector2Int goalCell = startCell;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2Int candidate = _walkableCells[UnityEngine.Random.Range(0, _walkableCells.Count)];
                if (candidate == startCell) continue;
                goalCell = candidate;
                int manhattan = Mathf.Abs(candidate.x - startCell.x) + Mathf.Abs(candidate.y - startCell.y);
                if (manhattan >= 3 || attempt >= 8) break;
            }

            if (goalCell == startCell) return false;

            _bfsQueue.Clear();
            _bfsParent.Clear();

            _bfsQueue.Enqueue(startCell);
            _bfsParent[startCell] = startCell;

            bool found = false;
            while (_bfsQueue.Count > 0)
            {
                Vector2Int cur = _bfsQueue.Dequeue();
                if (cur == goalCell)
                {
                    found = true;
                    break;
                }

                // Shuffle neighbor check order slightly for more natural path variety
                int startDir = UnityEngine.Random.Range(0, 4);
                for (int i = 0; i < 4; i++)
                {
                    Vector2Int next = cur + CardinalDirs[(startDir + i) & 3];
                    if (!_walkableSet.Contains(next) || _bfsParent.ContainsKey(next)) continue;

                    _bfsParent[next] = cur;
                    _bfsQueue.Enqueue(next);
                }
            }

            if (!found) return false;

            var reversed = new List<Vector2Int>(32);
            Vector2Int trace = goalCell;
            while (trace != startCell)
            {
                reversed.Add(trace);
                trace = _bfsParent[trace];
            }

            worldWaypoints = new Queue<Vector3>(reversed.Count);
            for (int i = reversed.Count - 1; i >= 0; i--)
            {
                Vector3 wp = targetGrid.GetCellCenterWorld(new Vector3Int(reversed[i].x, reversed[i].y, 0));
                wp.z = 0f;
                worldWaypoints.Enqueue(wp);
            }

            return worldWaypoints.Count > 0;
        }

        private void ResolveSceneReferences()
        {
            if (targetGrid == null)
            {
                targetGrid = GetComponentInParent<Grid>();
                if (targetGrid == null) targetGrid = FindObjectOfType<Grid>(true);
            }

            if (targetGrid == null) return;

            Tilemap[] childMaps = targetGrid.GetComponentsInChildren<Tilemap>(true);
            if (groundTilemap == null)
            {
                for (int i = 0; i < childMaps.Length; i++)
                {
                    if (string.Equals(childMaps[i].gameObject.name, "BG", StringComparison.OrdinalIgnoreCase))
                    {
                        groundTilemap = childMaps[i];
                        break;
                    }
                }
            }

            if (blockedTilemaps == null) blockedTilemaps = new List<Tilemap>();
            if (blockedTilemaps.Count == 0)
            {
                for (int i = 0; i < childMaps.Length; i++)
                {
                    Tilemap tm = childMaps[i];
                    if (tm == null || tm == groundTilemap) continue;
                    blockedTilemaps.Add(tm);
                }
            }
        }

        private void RebuildWalkableGrid()
        {
            _walkableCells.Clear();
            _walkableSet.Clear();
            if (targetGrid == null) return;

            GetScanBounds(out Vector2Int minCell, out Vector2Int maxCell);
            List<Bounds> staticPropBounds = CollectStaticPropBounds();

            var rawWalkable = new HashSet<Vector2Int>();
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    Vector3 worldCenter = targetGrid.GetCellCenterWorld(new Vector3Int(x, y, 0));
                    worldCenter.z = 0f;

                    // Convert world center per-Tilemap because BG has its own local offset (-7, 0, 0)
                    if (groundTilemap != null)
                    {
                        Vector3Int bgCell = groundTilemap.WorldToCell(worldCenter);
                        if (!groundTilemap.HasTile(bgCell)) continue;
                    }

                    if (IsBlockedByTilemaps(worldCenter) || IsBlockedByProps(worldCenter, staticPropBounds))
                    {
                        continue;
                    }

                    rawWalkable.Add(new Vector2Int(x, y));
                }
            }

            KeepLargestConnectedRegion(rawWalkable);
        }

        private void GetScanBounds(out Vector2Int minCell, out Vector2Int maxCell)
        {
            minCell = fallbackMinCell;
            maxCell = fallbackMaxCell;

            // Clamp against main orthographic camera view so NPCs stay comfortably on screen
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic && targetGrid != null)
            {
                float halfH = cam.orthographicSize - 0.5f;
                float aspect = cam.aspect > 0.1f ? cam.aspect : (16f / 9f);
                float halfW = halfH * aspect - 0.4f;
                Vector3 camPos = cam.transform.position;

                Vector3Int minWorld = targetGrid.WorldToCell(new Vector3(camPos.x - halfW, camPos.y - halfH, 0f));
                Vector3Int maxWorld = targetGrid.WorldToCell(new Vector3(camPos.x + halfW, camPos.y + halfH, 0f));

                minCell = new Vector2Int(Mathf.Max(minWorld.x, -16), Mathf.Max(minWorld.y, -9));
                maxCell = new Vector2Int(Mathf.Min(maxWorld.x, 15), Mathf.Min(maxWorld.y, 8));
            }
        }

        private bool IsBlockedByTilemaps(Vector3 worldCenter)
        {
            for (int i = 0; i < blockedTilemaps.Count; i++)
            {
                Tilemap tm = blockedTilemaps[i];
                if (tm == null || !tm.gameObject.activeInHierarchy) continue;

                Vector3Int localCell = tm.WorldToCell(worldCenter);
                if (tm.HasTile(localCell))
                {
                    return true;
                }
            }
            return false;
        }

        private List<Bounds> CollectStaticPropBounds()
        {
            var result = new List<Bounds>();
            if (targetGrid == null) return result;

            SpriteRenderer[] renderers = targetGrid.GetComponentsInChildren<SpriteRenderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;
                if (sr.transform == transform || sr.transform.IsChildOf(transform)) continue;
                if (sr.GetComponent<SpaceCenterNpcWalker>() != null) continue;

                Bounds b = sr.bounds;
                b.Expand(new Vector3(propBlockPadding * 2f, propBlockPadding * 2f, 10f));
                result.Add(b);
            }
            return result;
        }

        private static bool IsBlockedByProps(Vector3 worldCenter, List<Bounds> propBounds)
        {
            Vector3 probe = new Vector3(worldCenter.x, worldCenter.y, 0f);
            for (int i = 0; i < propBounds.Count; i++)
            {
                Bounds b = propBounds[i];
                probe.z = b.center.z;
                if (b.Contains(probe))
                {
                    return true;
                }
            }
            return false;
        }

        private void KeepLargestConnectedRegion(HashSet<Vector2Int> rawWalkable)
        {
            if (rawWalkable.Count == 0) return;

            var visited = new HashSet<Vector2Int>();
            List<Vector2Int> bestRegion = null;
            var queue = new Queue<Vector2Int>();

            foreach (Vector2Int seed in rawWalkable)
            {
                if (visited.Contains(seed)) continue;

                var region = new List<Vector2Int>();
                queue.Clear();
                queue.Enqueue(seed);
                visited.Add(seed);

                while (queue.Count > 0)
                {
                    Vector2Int cur = queue.Dequeue();
                    region.Add(cur);

                    for (int i = 0; i < 4; i++)
                    {
                        Vector2Int next = cur + CardinalDirs[i];
                        if (rawWalkable.Contains(next) && visited.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                if (bestRegion == null || region.Count > bestRegion.Count)
                {
                    bestRegion = region;
                }
            }

            if (bestRegion == null) return;

            for (int i = 0; i < bestRegion.Count; i++)
            {
                _walkableCells.Add(bestRegion[i]);
                _walkableSet.Add(bestRegion[i]);
            }
        }

        private void SpawnUniqueNpcs()
        {
            ClearExistingSpawnedNpcs();
            if (targetGrid == null || _walkableCells.Count == 0) return;

            // Remove editor preview SpriteRenderer from container object if user placed one as a marker
            SpriteRenderer hostPreview = GetComponent<SpriteRenderer>();
            if (hostPreview != null)
            {
                hostPreview.enabled = false;
            }

            List<NpcSkin> validSkins = CollectValidSkins();
            if (validSkins.Count == 0)
            {
                Debug.LogWarning("[SpaceCenterNpcManager] No valid NPC skins found.");
                return;
            }

            Shuffle(validSkins);
            Material sharedLitMaterial = FindSharedLitSpriteMaterial();

            int targetCount = oneNpcPerUniqueSkin
                ? validSkins.Count
                : Mathf.Max(1, maxNpcCount);

            var spawnPool = new List<Vector2Int>(_walkableCells);
            Shuffle(spawnPool);

            var usedSpawns = new List<Vector2Int>();
            int spawned = 0;

            for (int i = 0; i < targetCount && spawnPool.Count > 0; i++)
            {
                NpcSkin skin = validSkins[i % validSkins.Count];
                Vector2Int spawnCell = PickSpacedSpawnCell(spawnPool, usedSpawns);
                usedSpawns.Add(spawnCell);

                Vector3 startWorld = targetGrid.GetCellCenterWorld(new Vector3Int(spawnCell.x, spawnCell.y, 0));
                startWorld.z = 0f;

                var npcObj = new GameObject("NPC_" + (string.IsNullOrEmpty(skin.Name) ? i.ToString() : skin.Name));
                npcObj.transform.SetParent(transform, false);

                SpaceCenterNpcWalker walker = npcObj.AddComponent<SpaceCenterNpcWalker>();
                walker.Initialize(this, skin, startWorld, baseSortingOrder, sharedLitMaterial);
                _spawnedNpcs.Add(walker);
                spawned++;
            }

            if (spawned > 0)
            {
                Debug.Log(string.Format("[SpaceCenterNpcManager] Spawned {0} unique NPC(s) across {1} walkable tiles.",
                    spawned, _walkableCells.Count));
            }
        }

        private List<NpcSkin> CollectValidSkins()
        {
            var valid = new List<NpcSkin>();
            if (skins == null) return valid;

            for (int i = 0; i < skins.Count; i++)
            {
                if (skins[i] != null && skins[i].IsValid())
                {
                    valid.Add(skins[i]);
                }
            }
            return valid;
        }

        private Material FindSharedLitSpriteMaterial()
        {
            if (targetGrid != null)
            {
                TilemapRenderer[] trs = targetGrid.GetComponentsInChildren<TilemapRenderer>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] != null && trs[i].sharedMaterial != null)
                    {
                        return trs[i].sharedMaterial;
                    }
                }

                SpriteRenderer[] srs = targetGrid.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < srs.Length; i++)
                {
                    if (srs[i] != null && srs[i].sharedMaterial != null)
                    {
                        return srs[i].sharedMaterial;
                    }
                }
            }

            return null;
        }

        private static Vector2Int PickSpacedSpawnCell(List<Vector2Int> pool, List<Vector2Int> used)
        {
            int bestIdx = 0;
            int bestMinDist = -1;

            int checks = Mathf.Min(pool.Count, 24);
            for (int i = 0; i < checks; i++)
            {
                Vector2Int cand = pool[i];
                int minDist = int.MaxValue;
                for (int u = 0; u < used.Count; u++)
                {
                    int d = Mathf.Abs(cand.x - used[u].x) + Mathf.Abs(cand.y - used[u].y);
                    if (d < minDist) minDist = d;
                }

                if (used.Count == 0 || minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    bestIdx = i;
                    if (minDist >= 4) break;
                }
            }

            Vector2Int chosen = pool[bestIdx];
            pool.RemoveAt(bestIdx);
            return chosen;
        }

        private Vector2Int WorldToNearestWalkableCell(Vector3 worldPos)
        {
            Vector3Int cell = targetGrid.WorldToCell(worldPos);
            Vector2Int c2 = new Vector2Int(cell.x, cell.y);
            if (_walkableSet.Contains(c2)) return c2;

            Vector2Int nearest = _walkableCells[0];
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _walkableCells.Count; i++)
            {
                Vector2Int cand = _walkableCells[i];
                Vector3 center = targetGrid.GetCellCenterWorld(new Vector3Int(cand.x, cand.y, 0));
                float sqr = (center - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = cand;
                }
            }
            return nearest;
        }

        private void ClearExistingSpawnedNpcs()
        {
            for (int i = _spawnedNpcs.Count - 1; i >= 0; i--)
            {
                if (_spawnedNpcs[i] != null)
                {
                    Destroy(_spawnedNpcs[i].gameObject);
                }
            }
            _spawnedNpcs.Clear();
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

#if UNITY_EDITOR
        private void TryAutoPopulateSkinsInEditor()
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(CharacterSheetPath);
            if (assets == null || assets.Length == 0) return;

            var byName = new Dictionary<string, Sprite>(assets.Length, StringComparer.Ordinal);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sp)
                {
                    byName[sp.name] = sp;
                }
            }

            if (byName.Count == 0) return;

            var populated = new List<NpcSkin>(DefaultSkinSlices.Length);
            for (int i = 0; i < DefaultSkinSlices.Length; i++)
            {
                SkinSliceDef def = DefaultSkinSlices[i];
                var skin = new NpcSkin
                {
                    Name = def.Name,
                    Left = BuildColumnFrames(byName, def.StartIndex + 0),
                    Down = BuildColumnFrames(byName, def.StartIndex + 1),
                    Up = BuildColumnFrames(byName, def.StartIndex + 2),
                    Right = BuildColumnFrames(byName, def.StartIndex + 3)
                };
                if (skin.IsValid())
                {
                    populated.Add(skin);
                }
            }

            if (populated.Count > 0)
            {
                skins = populated;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private static Sprite[] BuildColumnFrames(Dictionary<string, Sprite> lookup, int topIndex)
        {
            var frames = new Sprite[3];
            for (int row = 0; row < 3; row++)
            {
                string key = "tilemap_" + (topIndex + row * SheetColumns);
                lookup.TryGetValue(key, out frames[row]);
            }
            return frames;
        }

        private void OnDrawGizmosSelected()
        {
            if (targetGrid == null || _walkableCells.Count == 0) return;

            Gizmos.color = new Color(0.25f, 0.95f, 0.45f, 0.35f);
            Vector3 size = new Vector3(targetGrid.cellSize.x * 0.85f, targetGrid.cellSize.y * 0.85f, 0.05f);
            for (int i = 0; i < _walkableCells.Count; i++)
            {
                Vector3 c = targetGrid.GetCellCenterWorld(new Vector3Int(_walkableCells[i].x, _walkableCells[i].y, 0));
                Gizmos.DrawCube(c, size);
            }
        }
#endif
    }
}
