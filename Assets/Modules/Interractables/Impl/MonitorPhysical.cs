using UnityEngine;
using Assets.Modules.Interractables;
using Unity.Collections;

[RequireComponent(typeof(Rigidbody))]
public class MonitorPhysical : MonoBehaviour, IInteractable
{
    [ReadOnly] public string targetWorkplaceId; // Помечаем как ReadOnly (если есть такой атрибут) или просто прячем
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

    public void Initialize(string id)
    {
        targetWorkplaceId = id;
        SetPhysics(false);
    }

    public void Interact(GameObject interactor)
    {
        PlayerInteraction.Instance.PickUpMonitor(this);
    }

    public void SetPhysics(bool state)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        _rb.isKinematic = !state;
        _rb.useGravity = state;

        _rb.interpolation = state ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
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