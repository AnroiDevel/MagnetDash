using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public sealed class LevelJsonGenerator : MonoBehaviour
{
    #region Serialized Fields

    [Header("Output")]
    [SerializeField] private string _outputFilePath = "Assets/Resources/levels.json";

    [Header("Levels")]
    [SerializeField, Min(1)] private int _levelsCount = 100;
    [SerializeField, Min(1)] private int _minNodesPerLevel = 3;
    [SerializeField, Min(1)] private int _maxNodesPerLevel = 20;

    [Header("Level Size (radius, for stars)")]
    [SerializeField, Min(1f)] private float _minRadius = 10f;
    [SerializeField, Min(1f)] private float _maxRadius = 80f;

    [Header("Path Steps (nodes & portal)")]
    [SerializeField, Min(0.1f)] private float _minStepDistance = 6f;   // минимальная дистанция между соседями
    [SerializeField, Min(0.1f)] private float _maxStepDistance = 14f;  // максимальная дистанция между соседями
    [SerializeField] private float _maxLateralOffset = 3f;             // боковой разброс от линии пути

    [Header("Negative Nodes % (0..1)")]
    [SerializeField, Range(0f, 1f)] private float _negativeRatioStart = 0.0f; // на первых уровнях
    [SerializeField, Range(0f, 1f)] private float _negativeRatioEnd = 0.5f;   // на последних

    [Header("Collectible Stars")]
    [SerializeField, Min(0)] private int _starsPerLevel = 3;
    [SerializeField, Min(0.1f)] private float _starMinDistanceFromPlayer = 3f;
    [SerializeField, Min(0.1f)] private float _starMinDistanceFromPortal = 3f;
    [SerializeField, Min(0.1f)] private float _starMinDistanceFromNodes = 2f;
    [SerializeField, Min(0.1f)] private float _starMinDistanceBetweenStars = 2f;

    #endregion

    #region Entry Point (Editor)

    [ContextMenu("Generate Levels Json")]
    private void Generate()
    {
        if(_levelsCount <= 0)
        {
            Debug.LogWarning("Levels count must be > 0");
            return;
        }

        var root = new LevelJsonRoot
        {
            levels = new List<LevelJsonData>(_levelsCount)
        };

        for(int i = 0; i < _levelsCount; i++)
        {
            float t = _levelsCount == 1 ? 0f : (float)i / (_levelsCount - 1);

            int nodesCount = Mathf.RoundToInt(Mathf.Lerp(_minNodesPerLevel, _maxNodesPerLevel, t));
            float radius = Mathf.Lerp(_minRadius, _maxRadius, t);
            float negativeRatio = Mathf.Lerp(_negativeRatioStart, _negativeRatioEnd, t);
            negativeRatio = Mathf.Clamp01(negativeRatio);

            LevelJsonData level = GenerateSingleLevel(i + 1, nodesCount, radius, negativeRatio);
            root.levels.Add(level);
        }

        string json = JsonUtility.ToJson(root, true);
        WriteToFile(json);

        Debug.Log($"Generated {_levelsCount} levels to {_outputFilePath}");
    }

    #endregion

    #region Level Generation

    private LevelJsonData GenerateSingleLevel(int id, int nodesCount, float radius, float negativeRatio)
    {
        var level = new LevelJsonData
        {
            id = id,
            player = new Vec2(0f, 0f),
            portal = new Vec2(),
            nodes = new List<NodeJsonData>(nodesCount),
            starsToCollect = new List<Vec2>()
        };

        Vector2 playerPos = level.player.ToVector2();

        // Случайное направление основного пути
        Vector2 dir = Random.insideUnitCircle.normalized;
        if(dir.sqrMagnitude < 0.001f)
            dir = Vector2.up;

        Vector2 perp = new(-dir.y, dir.x);

        Vector2 current = playerPos;

        // --- НОДЫ: путь от игрока к порталу шагами с ограничениями ---
        for(int i = 0; i < nodesCount; i++)
        {
            current = MakePathStep(current, dir, perp);
            level.nodes.Add(new NodeJsonData
            {
                position = new Vec2(current.x, current.y),
                isPositive = true
            });
        }

        // --- ПОРТАЛ: ещё один шаг с теми же ограничениями ---
        {
            Vector2 portalPos = MakePathStep(current, dir, perp);
            level.portal = new Vec2(portalPos.x, portalPos.y);
        }

        // Раскидываем отрицательные ноды
        ApplyNegativeNodes(level.nodes, negativeRatio);

        // Генерация звёзд для сбора
        GenerateCollectibleStars(level, radius);

        return level;
    }

    /// <summary>
    /// Один шаг вдоль основного направления с боковым разбросом и
    /// жёстким ограничением дистанции [minStep, maxStep].
    /// </summary>
    private Vector2 MakePathStep(Vector2 from, Vector2 dir, Vector2 perp)
    {
        float step = Random.Range(_minStepDistance, _maxStepDistance);
        Vector2 basePoint = from + dir * step;

        float offset = Random.Range(-_maxLateralOffset, _maxLateralOffset);
        Vector2 candidate = basePoint + perp * offset;

        float dist = Vector2.Distance(from, candidate);
        if(dist < _minStepDistance || dist > _maxStepDistance)
        {
            Vector2 dirToCandidate = (candidate - from).normalized;
            float clamped = Mathf.Clamp(dist, _minStepDistance, _maxStepDistance);
            candidate = from + dirToCandidate * clamped;
        }

        return candidate;
    }

    #endregion

    #region Stars Generation

    private void GenerateCollectibleStars(LevelJsonData level, float radius)
    {
        if(_starsPerLevel <= 0)
            return;

        var stars = level.starsToCollect;
        stars.Clear();

        Vector2 playerPos = level.player.ToVector2();
        Vector2 portalPos = level.portal.ToVector2();

        // базовая линия – от игрока к порталу
        Vector2 baseDir = portalPos - playerPos;
        float baseDist = baseDir.magnitude;
        if(baseDist < 0.001f)
            baseDir = Vector2.up;
        else
            baseDir /= baseDist;

        Vector2 perp = new(-baseDir.y, baseDir.x);

        for(int i = 0; i < _starsPerLevel; i++)
        {
            const int maxAttempts = 10000;

            for(int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // точка вдоль линии между игроком и порталом
                float t = (i + 1f) / (_starsPerLevel + 1f); // 0..1
                Vector2 basePoint = Vector2.Lerp(playerPos, portalPos, t);

                // небольшое смещение перпендикулярно
                float offset = Random.Range(-radius * 0.15f, radius * 0.15f);
                Vector2 candidate = basePoint + perp * offset;

                if(!IsValidStarPosition(candidate, level))
                    continue;

                stars.Add(new Vec2(candidate.x, candidate.y));
                break;
            }
        }
    }

    private bool IsValidStarPosition(Vector2 p, LevelJsonData level)
    {
        Vector2 playerPos = level.player.ToVector2();
        Vector2 portalPos = level.portal.ToVector2();

        if((p - playerPos).sqrMagnitude < _starMinDistanceFromPlayer * _starMinDistanceFromPlayer)
            return false;

        if((p - portalPos).sqrMagnitude < _starMinDistanceFromPortal * _starMinDistanceFromPortal)
            return false;

        // от нод
        if(level.nodes != null)
        {
            float minNodeDistSqr = _starMinDistanceFromNodes * _starMinDistanceFromNodes;
            for(int i = 0; i < level.nodes.Count; i++)
            {
                Vector2 np = level.nodes[i].position.ToVector2();
                if((p - np).sqrMagnitude < minNodeDistSqr)
                    return false;
            }
        }

        // от других звёзд для сбора
        if(level.starsToCollect != null)
        {
            float minStarDistSqr = _starMinDistanceBetweenStars * _starMinDistanceBetweenStars;
            for(int i = 0; i < level.starsToCollect.Count; i++)
            {
                Vector2 sp = level.starsToCollect[i].ToVector2();
                if((p - sp).sqrMagnitude < minStarDistSqr)
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Helpers

    private void ApplyNegativeNodes(List<NodeJsonData> nodes, float negativeRatio)
    {
        int count = nodes.Count;
        if(count == 0 || negativeRatio <= 0f)
            return;

        int negativeCount = Mathf.RoundToInt(count * negativeRatio);
        negativeCount = Mathf.Clamp(negativeCount, 0, count);

        int assigned = 0;
        while(assigned < negativeCount)
        {
            int idx = Random.Range(0, count);
            if(!nodes[idx].isPositive)
                continue;

            nodes[idx].isPositive = false;
            assigned++;
        }
    }

    private void WriteToFile(string content)
    {
        string directory = Path.GetDirectoryName(_outputFilePath);
        if(!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_outputFilePath, content);
        AssetDatabase.Refresh();
        Debug.LogError("LevelJsonGenerator: file writing works only in Editor.");
    }

    #endregion
}

#endif
