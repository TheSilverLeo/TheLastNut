using UnityEngine;

[CreateAssetMenu(
    fileName = "MonolithRodCollisionSettings",
    menuName = "The Last Rivet/Rod Collision Settings")]
public class MonolithRodCollisionSettings : ScriptableObject
{
    [Tooltip("Box size in the rod's local axes. X and Y cover the nuts. Z is the rod length.")]
    [SerializeField] Vector3 size = new Vector3(0.13f, 0.13f, 2f);

    [Tooltip("Box centre in the rod's local axes. Z of 1 m covers a rod from the wall to the tip.")]
    [SerializeField] Vector3 center = new Vector3(0f, 0f, 1f);

    [Tooltip("Metres. A rod within this distance of the player receives a collider.")]
    [SerializeField, Min(0.1f)] float activateDistance = 6f;

    [Tooltip("Metres. A rod keeps its collider until the player is farther than this. Keep it larger than the activation distance.")]
    [SerializeField, Min(0.1f)] float deactivateDistance = 8f;

    public Vector3 Size => size;
    public Vector3 Center => center;
    public float ActivateDistance => activateDistance;

    public float DeactivateDistance => deactivateDistance < activateDistance ? activateDistance : deactivateDistance;
}
