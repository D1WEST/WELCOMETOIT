using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Stats")]
    [Range(0, 100)] public float hp = 100;
    [Range(0, 100)] public float stamina = 100;

    [Header("Settings")]
    public int segmentsCount = 10;
    public float shakeIntensity = 5f;

    private VisualElement _hpRoot, _staminaRoot;
    private Label _hpLabel, _staminaLabel;
    private List<VisualElement> _hpSegments = new List<VisualElement>();
    private List<VisualElement> _staminaSegments = new List<VisualElement>();
    [SerializeField] private Color emptySegmentColor = new Color(1, 1, 1, 0.07f);

    private int _lastHpActive = -1;
    private int _lastStaminaActive = -1;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _hpRoot = root.Q<VisualElement>("hp-segments-container");
        _staminaRoot = root.Q<VisualElement>("stamina-segments-container");
        _hpLabel = root.Q<Label>("hp-text");
        _staminaLabel = root.Q<Label>("stamina-text");

        InitBar(_hpRoot, _hpSegments);
        InitBar(_staminaRoot, _staminaSegments);
    }

    void InitBar(VisualElement container, List<VisualElement> list)
    {
        container.Clear();
        list.Clear();
        for (int i = 0; i < segmentsCount; i++)
        {
            var seg = new VisualElement();
            seg.AddToClassList("segment");
            container.Add(seg);
            list.Add(seg);
        }
    }

    void Update()
    {
        UpdateBar(hp, _hpSegments, _hpLabel, ref _lastHpActive, true);
        UpdateBar(stamina, _staminaSegments, _staminaLabel, ref _lastStaminaActive, false);
    }

    void UpdateBar(float value, List<VisualElement> segments, Label label, ref int lastActive, bool isHp)
    {
        int activeCount = Mathf.CeilToInt((value / 100f) * segmentsCount);
        label.text = Mathf.FloorToInt(value).ToString();

        // Эффект тряски при изменении (оставляем как был)
        if (lastActive != -1 && activeCount != lastActive)
        {
            // Трясем тот сегмент, который изменил состояние
            int index = (activeCount < lastActive) ? lastActive - 1 : activeCount - 1;
            if (index >= 0 && index < segments.Count)
                StartCoroutine(ShakeElement(segments[index]));
        }
        lastActive = activeCount;

        Color activeColor = isHp ? GetHpColor(value / 100f) : GetStaminaColor(value / 100f);

        for (int i = 0; i < segments.Count; i++)
        {
            // Теперь все сегменты ВСЕГДА Flex (видимы)
            segments[i].style.display = DisplayStyle.Flex;

            if (i < activeCount)
            {
                // Активные сегменты красятся в яркий цвет
                segments[i].style.backgroundColor = activeColor;
                // Можно добавить небольшое свечение или масштаб для активных
                segments[i].transform.scale = new Vector3(1f, 1f, 1f);
            }
            else
            {
                // Неактивные сегменты становятся тусклыми "слотами"
                segments[i].style.backgroundColor = emptySegmentColor;
                // Немного уменьшаем пустые ячейки для визуального отличия
                segments[i].transform.scale = new Vector3(0.95f, 0.95f, 1f);
            }
        }
    }

    // Тряска элемента через корутину
    IEnumerator ShakeElement(VisualElement el)
    {
        Vector3 originalPos = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetY = Random.Range(-shakeIntensity, shakeIntensity);
            el.transform.position = new Vector3(offsetX, offsetY, 0);
            yield return new WaitForSeconds(0.03f);
        }
        el.transform.position = originalPos;
    }

    Color GetHpColor(float pct)
    {
        if (pct > 0.6f) return new Color(0.3f, 0.8f, 0.2f); // Зеленый
        if (pct > 0.3f) return new Color(1.0f, 0.6f, 0.0f); // Оранжевый
        return new Color(0.9f, 0.2f, 0.1f); // Красный
    }

    Color GetStaminaColor(float pct)
    {
        // Розовый -> Фиолетовый -> Темно-фиолетовый
        if (pct > 0.6f) return new Color(1.0f, 0.4f, 0.7f); // Розовый (Hot Pink)
        if (pct > 0.3f) return new Color(0.6f, 0.2f, 1.0f); // Фиолетовый
        return new Color(0.3f, 0.0f, 0.5f); // Темно-фиолетовый
    }
}