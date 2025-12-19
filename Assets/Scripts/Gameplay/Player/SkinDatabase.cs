using UnityEngine;

[CreateAssetMenu(fileName = "SkinDatabase", menuName = "MagnetDash/Skin Database")]
public sealed class SkinDatabase : ScriptableObject
{
    public DroneSkinDefinition[] skins;
}
