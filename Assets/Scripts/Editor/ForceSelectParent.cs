using UnityEditor;
using UnityEngine;

namespace ForceSelect
{
    [InitializeOnLoad]
    public static class ForceSelectParent
    {
        static ForceSelectParent()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            if (Selection.activeGameObject == null)
                return;

            Transform current = Selection.activeGameObject.transform;

            while (current != null)
            {
                if (current.GetComponent<ForceSelectParentTag>() != null)
                {
                    if (Selection.activeGameObject != current.gameObject)
                    {
                        GameObject target = current.gameObject;

                        // Posponer para que Unity no lo sobrescriba
                        EditorApplication.delayCall += () =>
                        {
                            if (target != null)
                                Selection.activeGameObject = target;
                        };
                    }
                    break;
                }

                current = current.parent;
            }
        }
    }
}
