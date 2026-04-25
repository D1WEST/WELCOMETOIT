using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MonitorPhysical : MonoBehaviour, IInteractable
{
    [ReadOnly] public string targetWorkplaceId; // Помечаем как ReadOnly (если есть такой атрибут) или просто прячем
    public WorkplaceInteractable sourceDesk;

    private Rigidbody _rb;
    private bool _isCarried = false;

    // Свойства интерфейса
    public string InteractionPrompt => _isCarried ? "" : $"Поднять монитор ({targetWorkplaceId}) [E]";
    public Transform InteractionPivot => transform;
    public InteractionType InteractionType => InteractionType.Click;
    public float HoldDuration => 0;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        SetPhysics(false);
    }

    public void Initialize(string id, WorkplaceInteractable desk)
    {
        targetWorkplaceId = id;
        sourceDesk = desk;
        SetPhysics(false);
    }

    public void Interact(GameObject interactor)
    {
        // Если в руках уже что-то есть - игнорируем
        if (PlayerInteraction.Instance.IsCarryingItem) return;

        if (sourceDesk != null)
        {
            sourceDesk.OnMonitorManualPickUp();
            sourceDesk = null;
        }
        PlayerInteraction.Instance.PickUpMonitor(this);
    }

    public void SetPhysics(bool state)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = !state;
        _rb.useGravity = state;
        if (state)
        {
            if (TryGetComponent<Collider>(out var col)) col.enabled = true;
        }
    }

    public void GetKicked()
    {
        transform.SetParent(null);
        SetPhysics(true);

        Vector3 kickDir = (transform.forward + Vector3.up + Random.insideUnitSphere * 0.5f).normalized;
        float kickPower = 12f;

        _rb.AddForce(kickDir * kickPower, ForceMode.Impulse);
        _rb.AddTorque(Random.onUnitSphere * 10f, ForceMode.Impulse);
    }
}