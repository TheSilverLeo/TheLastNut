using UnityEngine;

[DisallowMultipleComponent]
public class RodTestGridGenerator : MonoBehaviour
{
    public const string ContainerName = "Generated_Rods_Test";

    [Tooltip("Column count, row count, and spacing in metres.")]
    [SerializeField] RodTestGridSettings settings;

    [Tooltip("Nut count, spacing, and spin. Each rod gets its own seed from this asset.")]
    [SerializeField] RodNutTestSettings nutSettings;

    [Tooltip("Rod_Template prefab asset. Instances keep its children, materials, and scale.")]
    [SerializeField] GameObject rodPrefab;

    [Tooltip("Middle row and first column. Rods point along this transform's local +Z.")]
    [SerializeField] Transform faceAnchor;

    [Tooltip("Off: later columns step along the anchor's local +X. On: they step along local -X. The first column stays on the anchor.")]
    [SerializeField] bool reverseColumnDirection;

    [SerializeField, HideInInspector] Transform generatedContainer;

    public RodTestGridSettings Settings => settings;
    public RodNutTestSettings NutSettings => nutSettings;
    public GameObject RodPrefab => rodPrefab;
    public Transform FaceAnchor => faceAnchor;
    public bool ReverseColumnDirection => reverseColumnDirection;
    public Transform GeneratedContainer => generatedContainer;
}
