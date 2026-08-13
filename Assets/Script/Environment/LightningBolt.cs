namespace JayFos.Environment
{
    using UnityEngine;

    public class LightningBolt : MonoBehaviour
    {
        private static readonly int[] branchDepths = { 0, 1, 2 };
        private const int MaxRenderers = 20;
        private const int MaxPoints = 64;

        private LineRenderer[] branches;
        private Vector3[] positionBuffer;
        private int usedRenderers;

        private float lifetime = 0.2f;
        private float elapsed = 0f;

        private LightningManager manager;

        public void SetManager(LightningManager mgr)
        {
            manager = mgr;
        }

        public void Initialize(LightningManager mgr)
        {
            manager = mgr;
        }

        private void Awake()
        {
            branches = new LineRenderer[MaxRenderers];
            positionBuffer = new Vector3[MaxPoints];

            for (int i = 0; i < MaxRenderers; i++)
            {
                GameObject go = new GameObject($"Branch_{i}");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;

                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.startWidth = 0.3f;
                lr.endWidth = 0.05f;
                lr.startColor = new Color(1f, 1f, 1f, 1f);
                lr.endColor = new Color(0.67f, 0.8f, 1f, 0f);
                lr.positionCount = 0;

                branches[i] = lr;
            }
        }

        private void OnEnable()
        {
            lifetime = Random.Range(0.15f, 0.25f);
            elapsed = 0f;

            if (branches != null)
                RegenerateAll();

            if (manager != null)
                manager.OnBoltSpawned(this);
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.OnBoltRetracted(this);
        }

        private void RegenerateAll()
        {
            usedRenderers = 0;

            for (int i = 0; i < branchDepths.Length; i++)
            {
                GenerateBranch(transform.position, transform.position + Vector3.down * 30f, branchDepths[i], 0f);
            }

            for (int i = usedRenderers; i < branches.Length; i++)
            {
                branches[i].positionCount = 0;
            }
        }

        private void GenerateBranch(Vector3 origin, Vector3 target, int depth, float parentLength)
        {
            if (usedRenderers >= branches.Length)
                return;

            int segments = Mathf.Clamp(3 + Mathf.RoundToInt(parentLength / 3f), 3, 10);
            float segLength = Vector3.Distance(origin, target) / segments;

            Vector3 dir = (target - origin).normalized;
            Vector3 perp = Quaternion.AngleAxis(90f, dir) * Vector3.up;

            int count = 0;
            Vector3 current = origin;
            for (int i = 0; i < segments; i++)
            {
                float offset = Random.Range(-0.3f, 0.3f) * segLength;
                current += dir * segLength + perp * offset;
                if (count < MaxPoints)
                    positionBuffer[count++] = current;
            }

            LineRenderer lr = branches[usedRenderers++];
            lr.positionCount = count;
            for (int i = 0; i < count; i++)
                lr.SetPosition(i, positionBuffer[i]);

            if (depth < 2)
            {
                int childCount = Random.Range(2, 4);
                for (int b = 0; b < childCount; b++)
                {
                    int pick = Mathf.RoundToInt(Random.value * (count - 1));
                    Vector3 branchOrigin = positionBuffer[pick];
                    float angle = Random.Range(30f, 60f);
                    Vector3 branchDir = Quaternion.AngleAxis(angle, Random.Range(-1f, 1f) * Vector3.forward) * dir;
                    float branchLength = Vector3.Distance(origin, target) * Random.Range(0.3f, 0.7f);
                    Vector3 branchTarget = branchOrigin + branchDir * branchLength;

                    GenerateBranch(branchOrigin, branchTarget, depth + 1, branchLength);
                }
            }
        }

        private void Update()
        {
            if (!isActiveAndEnabled || manager == null)
                return;

            elapsed += Time.deltaTime;

            float t = elapsed / lifetime;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < usedRenderers; i++)
            {
                LineRenderer br = branches[i];
                if (br.positionCount <= 0)
                    continue;

                int reduce = Mathf.RoundToInt(Random.Range(0.1f, 0.3f) * br.positionCount);
                int newCount = br.positionCount - reduce;
                if (newCount < 2)
                    newCount = 2;
                if (newCount > MaxPoints)
                    newCount = MaxPoints;

                for (int k = 0; k < newCount; k++)
                    positionBuffer[k] = br.GetPosition(k);

                br.positionCount = newCount;
                for (int k = 0; k < newCount; k++)
                    br.SetPosition(k, positionBuffer[k]);
            }
        }
    }
}