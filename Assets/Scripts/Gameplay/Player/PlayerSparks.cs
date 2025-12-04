using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerSparks : MonoBehaviour
{
    [SerializeField] private ParticleSystem _sparkEffect;
    [SerializeField] private float _minVelocity = 1.5f;
    [SerializeField] private float _cooldown = 0.2f;

    private Rigidbody2D _rb;
    private float _nextSparkTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        
        _sparkEffect.transform.SetParent(null, false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(!collision.collider.CompareTag("Wall"))
            return;
        if(Time.time < _nextSparkTime)
            return;
        if(_rb.linearVelocity.magnitude < _minVelocity)
            return;

        Vector2 contactPoint = collision.GetContact(0).point;

        // перемещаем существующий эффект в точку контакта и запускаем
        _sparkEffect.transform.SetPositionAndRotation(contactPoint, Quaternion.LookRotation(Vector3.forward, -_rb.linearVelocity.normalized));
        _sparkEffect.Play();

        _nextSparkTime = Time.time + _cooldown;
    }
}
