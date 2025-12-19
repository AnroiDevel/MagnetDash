using UnityEngine;

[CreateAssetMenu(menuName = "MagnetDash/Drone Skin")]
public sealed class DroneSkinDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public int price;

    public Sprite icon;                // для магазина
    public PlayerSkinView prefab;      // полноценный визуальный префаб
}
