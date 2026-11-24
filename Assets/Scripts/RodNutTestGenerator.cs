using UnityEngine;

[DisallowMultipleComponent]
public class RodNutTestGenerator : MonoBehaviour
{
    public const string ContainerName = "Generated_Nuts_Test";

    [Tooltip("Nut count, thickness, gap, end clearance, and spin.")]
    [SerializeField] RodNutTestSettings settings;

    [Tooltip("Rod_Tip on this rod. Its position along local +Z is the rod length.")]
    [SerializeField] Transform rodTip;

    [Tooltip("Nut_Anchor on this rod. Generate may move it on this instance.")]
    [SerializeField] Transform nutAnchor;

    [Tooltip("The existing Nut under Nut_Anchor. It is kept and counted as one of the 32.")]
    [SerializeField] Transform referenceNut;

    [SerializeField, HideInInspector] bool hasCapturedReferencePose;
    [SerializeField, HideInInspector] RodNutReferencePose capturedReferencePose;

    public RodNutTestSettings Settings => settings;
    public Transform RodTip => rodTip;
    public Transform NutAnchor => nutAnchor;
    public Transform ReferenceNut => referenceNut;
    public bool HasCapturedReferencePose => hasCapturedReferencePose;
    public RodNutReferencePose CapturedReferencePose => capturedReferencePose;

    public void CaptureReferencePoseIfNeeded()
    {
        if (hasCapturedReferencePose || nutAnchor == null || referenceNut == null)
            return;

        capturedReferencePose = RodNutReferencePose.Capture(nutAnchor, referenceNut);
        hasCapturedReferencePose = true;
    }

    public bool TryRestoreReferencePose()
    {
        if (!hasCapturedReferencePose || nutAnchor == null || referenceNut == null)
            return false;

        capturedReferencePose.Restore(nutAnchor, referenceNut);
        hasCapturedReferencePose = false;
        return true;
    }
}

[System.Serializable]
public struct RodNutReferencePose
{
    public Vector3 anchorLocalPosition;
    public Quaternion anchorLocalRotation;
    public Vector3 anchorLocalScale;
    public Vector3 nutLocalPosition;
    public Quaternion nutLocalRotation;
    public Vector3 nutLocalScale;
    public Quaternion nutLocalRotationOnRod;

    public static RodNutReferencePose Capture(Transform anchor, Transform nut)
    {
        return new RodNutReferencePose
        {
            anchorLocalPosition = anchor.localPosition,
            anchorLocalRotation = anchor.localRotation,
            anchorLocalScale = anchor.localScale,
            nutLocalPosition = nut.localPosition,
            nutLocalRotation = nut.localRotation,
            nutLocalScale = nut.localScale,
            nutLocalRotationOnRod = anchor.localRotation * nut.localRotation
        };
    }

    public void Restore(Transform anchor, Transform nut)
    {
        anchor.localPosition = anchorLocalPosition;
        anchor.localRotation = anchorLocalRotation;
        anchor.localScale = anchorLocalScale;
        nut.localPosition = nutLocalPosition;
        nut.localRotation = nutLocalRotation;
        nut.localScale = nutLocalScale;
    }

    public void PlaceReference(Transform anchor, Transform nut, float rodLocalZ, float spinDegrees)
    {
        ReferenceLocals(rodLocalZ, spinDegrees, out Vector3 anchorPosition, out Quaternion anchorRotation, out Vector3 anchorScale, out Vector3 nutPosition, out Quaternion nutRotation, out Vector3 nutScale);
        anchor.localPosition = anchorPosition;
        anchor.localRotation = anchorRotation;
        anchor.localScale = anchorScale;
        nut.localPosition = nutPosition;
        nut.localRotation = nutRotation;
        nut.localScale = nutScale;
    }

    public Matrix4x4 ReferenceLocalMatrix(float rodLocalZ, float spinDegrees)
    {
        ReferenceLocals(rodLocalZ, spinDegrees, out Vector3 anchorPosition, out Quaternion anchorRotation, out Vector3 anchorScale, out Vector3 nutPosition, out Quaternion nutRotation, out Vector3 nutScale);
        return Matrix4x4.TRS(anchorPosition, anchorRotation, anchorScale)
            * Matrix4x4.TRS(nutPosition, nutRotation, nutScale);
    }

    public void PlaceCopy(Transform copy, float rodLocalZ, float spinDegrees)
    {
        CopyLocals(rodLocalZ, spinDegrees, out Vector3 position, out Quaternion rotation, out Vector3 scale);
        copy.localPosition = position;
        copy.localRotation = rotation;
        copy.localScale = scale;
    }

    public Matrix4x4 CopyLocalMatrix(float rodLocalZ, float spinDegrees)
    {
        CopyLocals(rodLocalZ, spinDegrees, out Vector3 position, out Quaternion rotation, out Vector3 scale);
        return Matrix4x4.TRS(position, rotation, scale);
    }

    void ReferenceLocals(
        float rodLocalZ,
        float spinDegrees,
        out Vector3 anchorPosition,
        out Quaternion anchorRotation,
        out Vector3 anchorScale,
        out Vector3 nutPosition,
        out Quaternion nutRotation,
        out Vector3 nutScale)
    {
        Vector3 centerOnRod = Vector3.forward * rodLocalZ;
        Vector3 nutOffsetOnRod = anchorLocalRotation * Vector3.Scale(anchorLocalScale, nutLocalPosition);
        anchorPosition = centerOnRod - nutOffsetOnRod;
        anchorRotation = anchorLocalRotation;
        anchorScale = anchorLocalScale;
        nutPosition = nutLocalPosition;
        nutScale = nutLocalScale;
        nutRotation = Quaternion.Inverse(anchorLocalRotation) * SpinOnRod(spinDegrees);
    }

    void CopyLocals(float rodLocalZ, float spinDegrees, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = Vector3.forward * rodLocalZ;
        rotation = SpinOnRod(spinDegrees);
        scale = nutLocalScale;
    }

    Quaternion SpinOnRod(float spinDegrees)
    {
        return Quaternion.AngleAxis(spinDegrees, Vector3.forward) * nutLocalRotationOnRod;
    }
}
