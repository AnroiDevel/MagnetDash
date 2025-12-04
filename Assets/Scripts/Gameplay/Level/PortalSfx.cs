using UnityEngine;

public sealed class PortalSfx : MonoBehaviour
{
    [Header("Portal SFX")]
    [SerializeField] private SfxEvent _nearLoop;         // тихий гул
    [SerializeField] private SfxEvent _absorbWhoosh;     // вт€гивание
    [SerializeField] private SfxEvent _winChime;         // победный чим

    private IAudioHandle _nearHandle;

    // K Ч степень близости к порталу (0..1), pos Ч позици€ игрока или точки контакта
    public void SetProximity(float k, Vector3 playerPos)
    {
        k = Mathf.Clamp01(k);

        if(k > 0.05f)
        {
            if(_nearHandle?.IsValid != true)
                _nearHandle = Audio.PlayLoop(_nearLoop, transform.position, spatial: 0.4f);

            _nearHandle?.SetVolume(Mathf.Lerp(0.0f, 0.20f, k), 0.08f);
        }
        else
        {
            if(_nearHandle?.IsValid == true)
            {
                _nearHandle.Stop(0.15f);
                _nearHandle = null;
            }
        }
    }

    public void OnAbsorb(Vector3 contactPos)
    {
        Audio.PlayAt(_absorbWhoosh, contactPos, spatial: 0.5f, key: "portal_absorb");
    }

    public void OnWin()
    {
        Audio.Play(_winChime, key: "win");
    }
}
