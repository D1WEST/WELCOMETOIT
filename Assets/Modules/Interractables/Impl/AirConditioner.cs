using UnityEngine;
using Assets.Modules.Interractables;
using Assets.Modules.Audio;
using Cysharp.Threading.Tasks;

public class AirConditioner : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private string deviceName = "Кондиционер";
    [SerializeField] private bool isOn = false;
    [SerializeField] private Transform interactionPivot;

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem coldAirParticles; // Ссылка на систему частиц

    // Реализация интерфейса
    public string InteractionPrompt => isOn ? $"Выключить {deviceName} [E]" : $"Включить {deviceName} [E]";
    public Transform InteractionPivot => interactionPivot != null ? interactionPivot : transform;
    public InteractionType InteractionType => InteractionType.Click;
    public float HoldDuration => 0;

    private void Start()
    {
        ApplyState(true);
    }

    public void Interact(GameObject interactor)
    {
        isOn = !isOn;

        // 1. Звук кнопки (Индекс 0)
        AudioManager.Instance.PlayAudio(
            AudioQuery.ByKey("AirCon")
            .ByIndex(0)
            .RandomSound()
            .At(this.transform)
        ).Forget();

        // 2. Применяем состояние (звук гула и частицы)
        ApplyState(false);
    }

    private void ApplyState(bool isInitial)
    {
        if (isOn)
        {
            // Запускаем звук (если это не первая загрузка сцены, иначе звук может наслоиться)
            StartLoopSound();

            // Запускаем частицы
            if (coldAirParticles != null)
            {
                coldAirParticles.Play();
            }
        }
        else
        {
            // Останавливаем звук
            StopLoopSound();

            // Останавливаем частицы
            if (coldAirParticles != null)
            {
                coldAirParticles.Stop();
            }
        }
    }

    private void StartLoopSound()
    {
        AudioManager.Instance.PlayAudio(
            AudioQuery.ByKey("AirCon")
            .ByIndex(1)
            .AsInstance()
            .Cycle()
            .At(this.transform)
            .WithVolume(0.25f)
        ).Forget();
    }

    private void StopLoopSound()
    {
        AudioManager.Instance.StopAudio(
            AudioQuery.ByKey("AirCon").At(this.transform)
        );
    }
}