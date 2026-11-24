using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonolithRodColliderField))]
public class MonolithRodColliderFieldEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var field = (MonolithRodColliderField)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rod Collision Debug", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Nearby rods detected", field.NearbyRodCount.ToString());
        EditorGUILayout.LabelField("Active collision proxies", field.ActiveColliderCount.ToString());
        EditorGUILayout.LabelField("Total pooled proxies", field.PooledColliderCount.ToString());
        if (field.HasNearestRod)
        {
            MonolithRodId rod = field.NearestRod;
            EditorGUILayout.LabelField("Nearest rod identity", "Face " + rod.Face + ", Column " + rod.Column + ", Row " + rod.Row);
            EditorGUILayout.LabelField("Nearest rod distance", field.NearestRodDistance.ToString("0.00") + " m");
        }
        else
        {
            EditorGUILayout.LabelField("Nearest rod identity", "None");
            EditorGUILayout.LabelField("Nearest rod distance", "—");
        }

        EditorGUILayout.LabelField("Nearest proxy world position", field.HasNearestProxy ? field.NearestProxyWorldPosition.ToString("0.00") : "None");
        EditorGUILayout.LabelField("Nearest proxy world size", field.HasNearestProxy ? field.NearestProxyWorldSize.ToString("0.00") : "None");
        EditorGUILayout.LabelField("Nearest proxy active", field.NearestProxyActive.ToString());
        EditorGUILayout.LabelField("Nearest proxy collider enabled", field.NearestProxyColliderEnabled.ToString());
        EditorGUILayout.LabelField("Nearest proxy is trigger", field.NearestProxyIsTrigger.ToString());
        EditorGUILayout.LabelField("Player collision layer", LayerName(field.PlayerLayer));
        EditorGUILayout.LabelField("Proxy collision layer", LayerName(field.ProxyLayer));
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode and walk toward a face. Nearby rods gain colliders. Distant rods return those colliders to the pool.",
                MessageType.Info);
        }
    }

    static string LayerName(int layer)
    {
        if (layer < 0)
            return "None";

        string name = LayerMask.LayerToName(layer);
        return string.IsNullOrEmpty(name) ? layer.ToString() : name;
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}
