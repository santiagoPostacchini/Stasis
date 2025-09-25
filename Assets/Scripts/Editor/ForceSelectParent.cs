using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class ForceSelectParent
{
    static ForceSelectParent()
    {
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged()
    {
        if (Selection.activeTransform == null) return;

        Transform selected = Selection.activeTransform;

        // Si no hay padre, salir
        if (selected.parent == null) return;

        // Revisar si el padre o el root tienen la marca
        ForceSelectParentTag marker = selected.parent.GetComponent<ForceSelectParentTag>();
        ForceSelectParentTag rootMarker = selected.root.GetComponent<ForceSelectParentTag>();

        if (marker != null || rootMarker != null)
        {
            // Forzar selección al padre más alto con la marca
            if (rootMarker != null)
                Selection.activeTransform = selected.root;
            else
                Selection.activeTransform = selected.parent;
        }
    }
}
