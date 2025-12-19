using UnityEngine;

public sealed class PauseButtonProxy : MonoBehaviour
{
    public void Click()
    {
        if(ServiceLocator.TryGet<PauseController>(out var pc))
            pc.OnPausePressed();
    }
}
