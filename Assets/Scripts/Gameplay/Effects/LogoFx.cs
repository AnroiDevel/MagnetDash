using System.Collections;
using UnityEngine;

public sealed class LogoFx : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField] private float _scaleMin = 1.0f;
    [SerializeField] private float _scaleMax = 1.03f;
    [SerializeField] private float _scaleSpeed = 1.2f;

    [Header("Rotation")]
    [SerializeField] private float _angle = 1.0f;
    [SerializeField] private float _rotateSpeed = 0.5f;

    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        StartCoroutine(AnimateLogo());
    }

    private IEnumerator AnimateLogo()
    {
        float t1 = 0f;
        float t2 = 0f;

        while(true)
        {
            // дыхание
            t1 += Time.unscaledDeltaTime * _scaleSpeed;
            float scale = Mathf.Lerp(_scaleMin, _scaleMax, (Mathf.Sin(t1) + 1f) * 0.5f);

            // покачивание
            t2 += Time.unscaledDeltaTime * _rotateSpeed;
            float rot = Mathf.Sin(t2) * _angle;

            _rect.localScale = Vector3.one * scale;
            _rect.localRotation = Quaternion.Euler(0, 0, rot);

            yield return null;
        }
    }
}
