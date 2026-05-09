using UnityEngine;
using System.Collections.Generic;

public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial; // Материал обводки
    private List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private Dictionary<MeshRenderer, Material[]> _originalMaterials = new Dictionary<MeshRenderer, Material[]>();

    private void Awake()
    {
        // Находим все меши в объекте и его детях
        _renderers.AddRange(GetComponentsInChildren<MeshRenderer>());

        // Запоминаем оригинальные наборы материалов
        foreach (var rend in _renderers)
        {
            _originalMaterials[rend] = rend.sharedMaterials;
        }
    }

    public void ToggleHighlight(bool state)
    {
        foreach (var rend in _renderers)
        {
            if (state)
            {
                // Добавляем материал обводки к текущим материалам
                Material[] currentMats = rend.sharedMaterials;
                Material[] newMats = new Material[currentMats.Length + 1];
                currentMats.CopyTo(newMats, 0);
                newMats[newMats.Length - 1] = outlineMaterial;
                rend.materials = newMats;
            }
            else
            {
                // Возвращаем как было
                rend.materials = _originalMaterials[rend];
            }
        }
    }
}